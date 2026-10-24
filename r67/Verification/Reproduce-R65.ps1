param([Parameter(Mandatory=$true)][string]$BaseRoot)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$source = Join-Path $BaseRoot 'Verification/RewardsRegression.cs'
$original = Get-Content -Raw -LiteralPath $source
if (!$original.Contains('GroupedDrop=new GroupDropInfo { Random=true,') -or
    !$original.Contains('monster=new TestMonster(map);monster.StruckAttributed(600,a,DefenceType.MAC);')) {
    throw 'Use an untouched R6.5/R6.6 extraction for reproduction; never point this at an installed server.'
}
$logs = Join-Path $BaseRoot 'build-logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$output = @(& dotnet build (Join-Path $BaseRoot 'Verification/RewardsRegression.csproj') -c Release 2>&1)
$code = $LASTEXITCODE
$text = $output -join [Environment]::NewLine
$text | Set-Content -LiteralPath (Join-Path $logs 'baseline-compile.log')
if ($code -eq 0 -or $text -notmatch 'CS0747') { throw 'Expected original mixed-initializer compiler error was not reproduced.' }
Write-Host 'BASELINE CONFIRMED: CS0747 in RewardsRegression.cs.'

$broken = @'
        grouped=new DropInfo { Chance=1,QuestRequired=true,GroupedDrop=new GroupDropInfo { Random=true,
            new DropInfo { Chance=1,Item=material }
        }};
'@
$fixed = @'
        grouped=new DropInfo { Chance=1,QuestRequired=true,GroupedDrop=new GroupDropInfo { Random=true }};
        grouped.GroupedDrop.Add(new DropInfo { Chance=1,Item=material });
'@
$probe = @'
        monster=new TestMonster(map);
        int observed=monster.StruckAttributed(600,a,DefenceType.MAC);
        Console.WriteLine($"BASELINE AREA: rate={monster.DamageRate}, dealt={observed}, HP={monster.HP}");
'@
$s = $original.Replace("`r`n","`n").Replace($broken.Replace("`r`n","`n"),$fixed.Replace("`r`n","`n"))
$s = $s.Replace('        monster=new TestMonster(map);monster.StruckAttributed(600,a,DefenceType.MAC);',$probe)
$s = $s.Replace('        Check(monster.EXPOwner==a,"Owned area spell damage or poison lost attribution");',
    '        Console.WriteLine($"BASELINE POISON: HP={monster.HP}, dead={monster.Dead}, owner={monster.EXPOwner?.Name ?? "none"}");' + "`n" +
    '        Check(monster.EXPOwner==a,"Owned area spell damage or poison lost attribution");')
try {
    [IO.File]::WriteAllText($source,$s)
    $output = @(& dotnet run --project (Join-Path $BaseRoot 'Verification/RewardsRegression.csproj') -c Release 2>&1)
    $code = $LASTEXITCODE
    $text = $output -join [Environment]::NewLine
    $text | Set-Content -LiteralPath (Join-Path $logs 'baseline-attribution.log')
    $output | Where-Object { "$_" -match 'BASELINE|Owned area' } | ForEach-Object { Write-Host "$_" }
    if ($code -eq 0 -or !$text.Contains('Owned area spell damage or poison lost attribution') -or
        !$text.Contains('BASELINE AREA: rate=0, dealt=0, HP=1000') -or
        !$text.Contains('BASELINE POISON: HP=600, dead=False, owner=none')) {
        throw 'Original attribution failure did not match the zero-initialized multiplier diagnosis.'
    }
} finally { [IO.File]::WriteAllText($source,$original) }
Write-Host 'BASELINE CONFIRMED: A dealt zero, B dealt 400; monster remained alive with no settled owner.'
