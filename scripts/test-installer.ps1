param([string]$Setup = (Get-ChildItem "$PSScriptRoot\..\dist\KawarimiSetup-*.exe" | Sort-Object LastWriteTime | Select-Object -Last 1).FullName)
$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
$fails = 0
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Kawarimi'

function Check($name, $ok) {
    if ($ok) { Write-Host "PASS $name" }
    else {
        Write-Host "FAIL $name"
        $script:fails++
    }
}

function Get-GameHashes($dir) {
    Get-ChildItem $dir -Recurse -File | Where-Object { $_.FullName -notmatch '\\kawarimi\\|\\mods\\|\\winmm\.dll' } |
        ForEach-Object { "$($_.FullName.Substring($dir.Length))=$((Get-FileHash $_.FullName).Hash)" } | Sort-Object
}

function Install($dir, [string[]]$extra = @()) {
    (Start-Process $Setup -ArgumentList (@('/S') + $extra + "/D=$dir") -Wait -PassThru).ExitCode
}

function Uninstall($dir) {
    Start-Process "$dir\kawarimi\uninstall.exe" -ArgumentList '/S'
    $deadline = (Get-Date).AddSeconds(30)
    while ((Test-Path "$dir\kawarimi") -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
    0
}

if (Test-Path $key) {
    Write-Host "SKIP: Kawarimi is installed on this machine ($((Get-ItemProperty $key).GameDir)). Uninstall it first so this test cannot disturb it."
    exit 0
}

$e2e = "$Root\out\native\e2e_test.exe"
$tmp = Join-Path $env:TEMP ("kawarimi-test-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$game = "$tmp\NSUNS4"
New-Item -ItemType Directory "$game\data\spc" -Force | Out-Null
Set-Content "$game\NSUNS4.exe" 'fake exe'
Set-Content "$game\config.ini" 'width=1920'
Set-Content "$game\data\spc\x.xfbin" 'data'
$before = Get-GameHashes $game
try {
    New-Item -ItemType Directory "$tmp\notgame" | Out-Null
    Check 'invalid folder rejected (exit 2)' ((Install "$tmp\notgame") -eq 2)
    Check 'invalid folder untouched' (-not (Test-Path "$tmp\notgame\winmm.dll"))

    Check 'install exit 0' ((Install $game) -eq 0)
    foreach ($f in 'winmm.dll', 'kawarimi\Kawarimi.Host.dll', 'kawarimi\Kawarimi.Api.dll', 'kawarimi\Kawarimi.exe', 'kawarimi\docs\MODDING.md',
        'kawarimi\samples\ExampleSkinMod\ExampleMod.cs', 'kawarimi\uninstall.exe') {
        Check "installed $f" (Test-Path "$game\$f")
    }
    Check 'mods folder created' (Test-Path "$game\mods")
    Check 'registry entry written' ((Get-ItemProperty $key).GameDir -eq $game)
    Check 'game files unchanged by install' (-not (Compare-Object $before (Get-GameHashes $game)))
    Check 'installed tool runs' ((& "$game\kawarimi\Kawarimi.exe" help) -match 'characters')

    foreach ($m in 'SampleSkin', 'Broken', 'ExampleSkinMod') { New-Item -ItemType Directory "$game\mods\$m\files" -Force | Out-Null }
    Set-Content "$game\mods\SampleSkin\files\9t01bod1.xfbin" 'fake'
    Set-Content "$game\mods\SampleSkin\skin.json" '{"skins":[{"character":"1tst","skinCode":"9t01"},{"character":"1tst","skinCode":"9t02"}]}'
    Set-Content "$game\mods\Broken\skin.json" '{ not json'
    foreach ($f in '9n01bod1.xfbin', '2hntbod1.xfbin') { Set-Content "$game\mods\ExampleSkinMod\files\$f" 'fake' }
    Copy-Item "$Root\out\example\ExampleSkinMod.dll" "$game\mods\ExampleSkinMod"
    if (Test-Path $e2e) { Check 'installed runtime passes the end-to-end test' (((& $e2e $game 2>&1) -join "`n") -match 'ALL PASSED') }
    else { Write-Host 'SKIP installed-runtime end-to-end (run scripts\build.ps1 first)' }
    Remove-Item "$game\kawarimi\kawarimi.log", "$game\kawarimi\mods-report.txt" -ErrorAction SilentlyContinue

    New-Item -ItemType Directory "$game\mods\MySkin" | Out-Null
    Set-Content "$game\mods\MySkin\skin.json" '{}'
    Check 'update exit 0' ((Install $game) -eq 0)
    Check 'update keeps user mods' (Test-Path "$game\mods\MySkin\skin.json")

    New-Item -ItemType Directory "$tmp\run" | Out-Null
    Copy-Item "$env:SystemRoot\System32\ping.exe" "$tmp\run\NSUNS4.exe"
    $proc = Start-Process "$tmp\run\NSUNS4.exe" -ArgumentList '-n', '30', '127.0.0.1' -PassThru -WindowStyle Hidden
    Start-Sleep 1
    Check 'install blocked while game runs (exit 3)' ((Install $game) -eq 3)
    Check 'uninstall blocked while game runs (exit 3)' ((Start-Process "$game\kawarimi\uninstall.exe" -ArgumentList '/S', "_?=$game\kawarimi" -Wait -PassThru).ExitCode -eq 3)
    Stop-Process $proc -Force
    Start-Sleep 1

    Check 'uninstall exit 0' ((Uninstall $game) -eq 0)
    Check 'winmm.dll removed' (-not (Test-Path "$game\winmm.dll"))
    Check 'kawarimi folder removed' (-not (Test-Path "$game\kawarimi"))
    Check 'registry entry removed' (-not (Test-Path $key))
    Check 'user mods kept' (Test-Path "$game\mods\MySkin\skin.json")
    Check 'game files unchanged after uninstall' (-not (Compare-Object $before (Get-GameHashes $game)))
    Remove-Item "$game\mods" -Recurse -Force

    Set-Content "$game\winmm.dll" 'someone elses proxy'
    Check 'foreign winmm refused by default (exit 4)' ((Install $game) -eq 4)
    Check 'foreign winmm untouched after refusal' ((Get-Content "$game\winmm.dll") -eq 'someone elses proxy')
    Check 'install with /BACKUP exit 0' ((Install $game @('/BACKUP')) -eq 0)
    Check 'foreign winmm backed up' ((Get-Content "$game\winmm.dll.pre-kawarimi") -eq 'someone elses proxy')
    Check 'uninstall exit 0 (backup case)' ((Uninstall $game) -eq 0)
    Check 'foreign winmm restored' ((Get-Content "$game\winmm.dll") -eq 'someone elses proxy')
    Check 'backup file gone' (-not (Test-Path "$game\winmm.dll.pre-kawarimi"))
}
finally {
    Get-Process NSUNS4 -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$tmp*" } | Stop-Process -Force
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $key -ErrorAction SilentlyContinue
}
if ($fails) {
    Write-Host "$fails FAILED"
    exit 1
}
Write-Host 'ALL PASSED'
