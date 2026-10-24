param()
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Run this server build on Windows.' }
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the x64 .NET 8 SDK first.' }
if (!((& dotnet --list-sdks) -match '^8\.0\.')) { throw 'The .NET 8 SDK is required, not only a runtime.' }
$logDir = Join-Path $here 'build-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir ('server-r67-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.log')
Start-Transcript -LiteralPath $log | Out-Null
try {
    & (Join-Path $here 'Verify-Package.ps1')
    & dotnet build (Join-Path $here 'Verification\RewardsRegression.csproj') -c Release --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Rewards regression compilation failed. Existing server was not changed.' }
    & dotnet run --no-build --project (Join-Path $here 'Verification\RewardsRegression.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Rewards regression failed. Existing server was not changed.' }
    $stage = Join-Path $here ('server-r67-publish-' + [Guid]::NewGuid().ToString('N'))
    & dotnet build (Join-Path $here 'Source\Server.MirForms\Server.csproj') -c Release --runtime win-x64 --self-contained true -p:UseAppHost=true -p:EnableWindowsTargeting=true -p:PlatformTarget=x64 -p:PublishSingleFile=false -p:PublishTrimmed=false --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Server compilation failed. Existing server was not changed.' }
    & dotnet publish (Join-Path $here 'Source\Server.MirForms\Server.csproj') -c Release --runtime win-x64 --self-contained true -p:PublishSelfContained=true -p:UseAppHost=true -p:EnableWindowsTargeting=true -p:PlatformTarget=x64 -p:PublishSingleFile=false -p:PublishTrimmed=false -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Server publish failed. Existing server was not changed.' }
    foreach ($name in @('Server.exe','Server.dll','Server.Library.dll','Shared.dll','coreclr.dll','Server.runtimeconfig.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $stage $name) -PathType Leaf)) { throw "Published server is incomplete: $name" }
    }
    $files = @(Get-ChildItem -LiteralPath $stage -Recurse -File)
    $records = @()
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($stage.Length + 1)
        if ($relative -match '(?i)\.(MirDB|MirADB|ini|map)$') { throw "Unexpected world/account/settings data in build: $relative" }
        $records += [pscustomobject]@{path=$relative;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash}
    }
    [pscustomobject]@{revision='rewards-hotbar-r6-7';runtime_identifier='win-x64';rewards_regression_passed=$true;live_gameplay_verified=$false;files=$records} |
        ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $stage 'server-r67-publish.json')
    Write-Host "Server build prepared: $stage"
    Write-Host 'Existing server was not modified or started. Follow 00-README-R6.7.md for backup, installation and gameplay checks.'
} finally { Stop-Transcript | Out-Null }
