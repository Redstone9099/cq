param([string]$ClientRoot = 'D:\Crystal-Preview\Client', [switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
. (Join-Path $here 'Repair-Common.ps1')
foreach ($relative in @('Source\Client\Client.csproj','Source\Shared\Shared.csproj')) {
    if (!(Test-Path -LiteralPath (Join-Path $here $relative) -PathType Leaf)) {
        throw 'Extract the complete R6 package before running Build-R6.cmd.'
    }
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Run this build and install tool on Windows.' }
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The x64 .NET 8 SDK is required on this build computer.' }
if (!((& dotnet --list-sdks) -match '^8\.0\.')) { throw 'The x64 .NET 8 SDK is required; a runtime alone cannot compile the client.' }
$target = $null
if (!$BuildOnly) {
    $target=(Resolve-Path -LiteralPath $ClientRoot).Path.TrimEnd([char[]]'\/')
    $sourceRoot=[IO.Path]::GetFullPath((Join-Path $here 'Source')).TrimEnd([char[]]'\/')
    if ($target.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Use a game installation outside the source/build directory.' }
    if ($target -eq $here.TrimEnd([char[]]'\/')) { throw 'Select the installed game directory, not this source package.' }
    if (!(Test-Path -LiteralPath (Join-Path $target 'Client.exe') -PathType Leaf) -or
        !(Test-Path -LiteralPath (Join-Path $target 'Data') -PathType Container)) { throw "Installed game not found: $target" }
}
function Assert-StartupClosed {
    if ($BuildOnly) { return }
    foreach ($process in @(Get-Process -Name @('Client','AutoPatcher') -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($process.Path) -or
            [IO.Path]::GetDirectoryName($process.Path).Equals($target, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Close the game and update window before rebuilding/installing.'
        }
    }
}
Assert-StartupClosed
$logDir = Join-Path $here 'build-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir ('r6-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.log')
Start-Transcript -LiteralPath $log | Out-Null
try {
    & (Join-Path $here 'Verify-Package.ps1')
    & (Join-Path $here 'Verify-SkillPackage.ps1')
    $stage = Join-Path $here ('r6-publish-' + [Guid]::NewGuid().ToString('N'))
    foreach ($project in @('RewardsRegression','LootEffectsRegression','SkillArtworkRegression','PresentationRegression','ServiceFlowRegression','CursorLogicRegression','UiRegression')) {
        $projectPath=Join-Path $here ('Verification\'+$project+'.csproj')
        # ZIP timestamps can precede old bin/obj files. Rebuild before executing.
        & dotnet build $projectPath -c Release --no-incremental
        if ($LASTEXITCODE -ne 0) { throw "Regression compilation failed: $project. Installed game was not changed." }
        if ($project -eq 'CursorLogicRegression') {
            & dotnet run --no-build --project $projectPath -c Release -- (Join-Path $here 'client\Data\UI\CursorsR5')
        } else { & dotnet run --no-build --project $projectPath -c Release }
        if ($LASTEXITCODE -ne 0) { throw "Regression failed: $project. Installed game was not changed." }
    }
    Push-Location (Join-Path $here 'Source')
    try {
        & dotnet build (Join-Path $here 'Source\Client\Client.csproj') -c Release --runtime win-x64 --self-contained true -p:UseAppHost=true -p:EnableWindowsTargeting=true -p:PlatformTarget=x64 -p:PublishSingleFile=false -p:PublishTrimmed=false --no-incremental
        if ($LASTEXITCODE -ne 0) { throw 'Client compilation failed; installed game was not changed.' }
        & dotnet publish (Join-Path $here 'Source\Client\Client.csproj') -c Release --runtime win-x64 --self-contained true -p:PublishSelfContained=true -p:UseAppHost=true -p:EnableWindowsTargeting=true -p:PlatformTarget=x64 -p:PublishSingleFile=false -p:PublishTrimmed=false -p:NoBuild=false -o $stage
        if ($LASTEXITCODE -ne 0) { throw 'Client publish failed; installed game was not changed.' }
        & dotnet build (Join-Path $here 'Verification\DirectLaunchRegression.csproj') -c Release --no-incremental
        if ($LASTEXITCODE -ne 0) { throw 'Startup verification compilation failed; installed game was not changed.' }
        & dotnet run --no-build --project (Join-Path $here 'Verification\DirectLaunchRegression.csproj') -c Release -- (Join-Path $stage 'Client.dll')
        if ($LASTEXITCODE -ne 0) { throw 'Compiled startup verification failed; installed game was not changed.' }
    } finally { Pop-Location }
    Assert-ClientSelfContained $stage
    $files = @()
    foreach ($relative in @(Get-ClientPublishFiles $stage)) {
        # A publish must not introduce or replace game configuration or game assets.
        if ($relative -match '(?i)(^|[\\/])Mir2(Config|Test)\.ini$|^Data[\\/]') { throw "Unexpected game settings/assets in publish output: $relative" }
        $files += [pscustomobject]@{path=$relative; sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Get-CheckedChildPath $stage $relative)).Hash}
    }
    # Install the supplied UI and cursor assets alongside this same build.
    # No maps, accounts, INI settings or unrelated model libraries are copied.
    $assetRoot=Join-Path $here 'client'
    $uiRoot=Join-Path $assetRoot 'Data\UI'
    $assets=@(Get-ChildItem -LiteralPath $uiRoot -Recurse -File)
    $assets+=Get-Item -LiteralPath (Join-Path $assetRoot 'Data\UI_32bit.Lib')
    foreach ($asset in $assets) {
        $relative=$asset.FullName.Substring($assetRoot.Length+1)
        $dest=Get-CheckedChildPath $stage $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
        Copy-Item -LiteralPath $asset.FullName -Destination $dest -Force
        $files+=[pscustomobject]@{path=$relative;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash}
    }
    if ($BuildOnly) {
        Write-Host "R6 compiled and prepared: $stage"
        Write-Host 'Windows gameplay checks are still required; see README-R6.md.'
        return
    }
    [pscustomobject]@{revision='loot-feedback-r6-9'; deployment='self-contained'; runtime_identifier='win-x64'; compiled_startup_verified=$true; files=$files} |
        ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $stage 'direct-start-publish.json')
    Assert-StartupClosed
    $backup = Join-Path $target ('_r6-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $backup | Out-Null
    $records = @()
    foreach ($record in $files) {
        $destination = Get-CheckedChildPath $target $record.path
        $saved = Get-CheckedChildPath (Join-Path $backup 'original') $record.path
        $existed = Test-Path -LiteralPath $destination -PathType Leaf
        $oldHash = $null
        if ($existed) {
            $oldHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $saved) | Out-Null
            Copy-Item -LiteralPath $destination -Destination $saved
            if ((Get-FileHash -Algorithm SHA256 -LiteralPath $saved).Hash -ne $oldHash) { throw "Backup verification failed: $($record.path)" }
        }
        $records += [pscustomobject]@{path=$record.path; existed=$existed; before_sha256=$oldHash; after_sha256=$record.sha256}
    }
    [pscustomobject]@{revision='loot-feedback-r6-9'; target=$target; files=$records} |
        ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $backup 'receipt.json')
    try {
        foreach ($record in $records) {
            $source = Get-CheckedChildPath $stage $record.path
            $destination = Get-CheckedChildPath $target $record.path
            if ((Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash -ne $record.after_sha256) { throw "Publish file changed: $($record.path)" }
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Force
            if ((Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash -ne $record.after_sha256) { throw "Installed checksum mismatch: $($record.path)" }
        }
        Assert-ClientSelfContained $target
    } catch {
        $failure = $_
        & (Join-Path $here 'Restore-ClientBackup.ps1') -BackupRoot $backup
        throw $failure
    }
    Write-Host "R6 client published and installed. Backup: $backup"
    Write-Host 'R6 installed. Start Client.exe and follow the R6 gameplay checklist.'
} catch {
    Write-Host "Build/install stopped. Log: $log"
    throw
} finally { Stop-Transcript | Out-Null }
