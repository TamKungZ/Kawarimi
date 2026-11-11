param(
    [string]$Version = '0.3.0',
    [switch]$SkipInstaller,
    [switch]$SkipTests,
    [switch]$Deploy,
    [switch]$Format
)
$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
$Out = Join-Path $Root 'out'
$Stage = Join-Path $Root 'stage'
. (Join-Path $PSScriptRoot 'env.ps1')

function Invoke-Step([string]$Name, [scriptblock]$Block) {
    Write-Host "== $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "$Name failed ($LASTEXITCODE)" }
}

function Invoke-Format {
    Invoke-Step 'format C#' { dotnet format (Join-Path $Root 'Kawarimi.sln') }
    $clang = Get-EnvValue 'CLANG_FORMAT' 'clang-format'
    $files = Get-ChildItem (Join-Path $Root 'src\native'), (Join-Path $Root 'tests\native') -Include *.cpp, *.h -Recurse
    Invoke-Step 'format C++' { & $clang -i --style=file $files.FullName }
}

function Build-Managed {
    Invoke-Step 'managed: host' { dotnet build -c Release (Join-Path $Root 'src\Kawarimi.Host') -o "$Out\kawarimi" -v q --nologo -p:Version=$Version }
    Invoke-Step 'managed: build task' { dotnet build -c Release (Join-Path $Root 'src\Kawarimi.Build') -o "$Out\build" -v q --nologo -p:Version=$Version }
    Invoke-Step 'managed: tools' { dotnet publish -c Release (Join-Path $Root 'src\Kawarimi.Tools') -o "$Out\tools" -v q --nologo -p:Version=$Version }
    Invoke-Step 'managed: sample mod' { dotnet build -c Release (Join-Path $Root 'samples\ExampleSkinMod') -o "$Out\example" -v q --nologo -p:Version=$Version }
}

function Build-Native {
    Use-Msvc
    $dir = New-Item -ItemType Directory -Force "$Out\native"
    $src = (Get-ChildItem (Join-Path $Root 'src\native') -Filter *.cpp).FullName
    $tests = Join-Path $Root 'tests\native'
    Push-Location $dir
    try {
        Invoke-Step 'native: winmm.dll' { cl /nologo /std:c++17 /O2 /EHsc /W3 /LD /Fe:winmm.dll $src /link ws2_32.lib user32.lib | Out-Null }
        Invoke-Step 'native: tests' {
            cl /nologo /std:c++17 /EHsc /Fe:hook_test.exe "$tests\hook_test.cpp" (Join-Path $Root 'src\native\hook.cpp') | Out-Null
            cl /nologo /std:c++17 /EHsc /Fe:e2e_test.exe "$tests\e2e_test.cpp" | Out-Null
            cl /nologo /EHsc /Fe:capture_standin.exe "$tests\capture_standin.cpp" | Out-Null
        }
    }
    finally { Pop-Location }
}

function Invoke-Tests {
    Invoke-Step 'tests: managed' { dotnet run -c Release --project (Join-Path $Root 'tests\Kawarimi.Tests') }
    Invoke-Step 'tests: hook engine' { & "$Out\native\hook_test.exe" }
    $e2e = Join-Path $Out 'e2e'
    Remove-Item $e2e -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$e2e\kawarimi", "$e2e\mods\SampleSkin\files", "$e2e\mods\ExampleSkinMod\files", "$e2e\mods\Broken" | Out-Null
    Copy-Item "$Out\native\winmm.dll" $e2e
    Copy-Item "$Out\kawarimi\*" "$e2e\kawarimi"
    Copy-Item "$Out\example\ExampleSkinMod.dll" "$e2e\mods\ExampleSkinMod"
    foreach ($f in '9n01bod1.xfbin', '2hntbod1.xfbin') { Set-Content "$e2e\mods\ExampleSkinMod\files\$f" 'fake' }
    Set-Content "$e2e\mods\SampleSkin\files\9t01bod1.xfbin" 'fake'
    Set-Content "$e2e\mods\SampleSkin\skin.json" '{"skins":[{"character":"1tst","skinCode":"9t01"},{"character":"1tst","skinCode":"9t02"}]}'
    Set-Content "$e2e\mods\Broken\skin.json" '{ not json'
    Invoke-Step 'tests: proxy end to end' { & "$Out\native\e2e_test.exe" $e2e }
}

function New-Payload {
    Remove-Item $Stage -Recurse -Force -ErrorAction SilentlyContinue
    $k = New-Item -ItemType Directory -Force "$Stage\kawarimi"
    New-Item -ItemType Directory -Force "$k\docs", "$k\samples\ExampleSkinMod" | Out-Null
    Copy-Item "$Out\native\winmm.dll" "$Stage\winmm.dll"
    Copy-Item (Join-Path $Root 'installer\install.md') "$Stage\install.md"
    foreach ($f in 'Kawarimi.Api.dll', 'Kawarimi.Api.deps.json', 'Kawarimi.Host.dll', 'Kawarimi.Host.deps.json', 'Kawarimi.Host.runtimeconfig.json') {
        Copy-Item "$Out\kawarimi\$f" "$k\$f"
    }
    foreach ($f in 'Kawarimi.Build.dll', 'Kawarimi.Formats.dll', 'Kawarimi.Mod.targets') {
        Copy-Item "$Out\build\$f" "$k\$f"
    }
    Copy-Item "$Out\tools\Kawarimi.exe" "$k\Kawarimi.exe"
    Copy-Item (Join-Path $Root 'README.md') "$k\README.md"
    Copy-Item (Join-Path $Root 'LICENSE') "$k\LICENSE.txt"
    Copy-Item (Join-Path $Root 'docs\*.md') "$k\docs"
    Copy-Item (Join-Path $Root 'samples\ExampleSkinMod\ExampleMod.cs') "$k\samples\ExampleSkinMod"
    $csproj = Get-Content (Join-Path $Root 'samples\ExampleSkinMod\ExampleSkinMod.csproj') -Raw
    $csproj -replace '(?s)<ProjectReference.*?</ProjectReference>', '<Reference Include="Kawarimi.Api"><HintPath>..\..\Kawarimi.Api.dll</HintPath><Private>false</Private></Reference>' |
        Set-Content "$k\samples\ExampleSkinMod\ExampleSkinMod.csproj"
}

function Build-Installer {
    $gen = Join-Path $Root 'installer\gen'
    & (Join-Path $PSScriptRoot 'gen-installer-filelist.ps1') -Source "$Stage\kawarimi" -Target '$INSTDIR\kawarimi' -Name payload -OutDir $gen
    $nsis = Get-EnvValue 'MAKENSIS' 'makensis'
    New-Item -ItemType Directory -Force (Join-Path $Root 'dist') | Out-Null
    Invoke-Step 'installer' { & $nsis /V2 "/DVERSION=$Version" (Join-Path $Root 'installer\Kawarimi.nsi') }
    Compress-Archive -Path "$Stage\*" -DestinationPath (Join-Path $Root "dist\Kawarimi-$Version-portable.zip") -Force
    Write-Host "Installer: $(Join-Path $Root "dist\KawarimiSetup-$Version.exe")" -ForegroundColor Green
}

function Invoke-Deploy {
    $game = Get-EnvValue 'KAWARIMI_GAME_DIR'
    if (-not $game -or -not (Test-Path "$game\NSUNS4.exe")) { throw 'KAWARIMI_GAME_DIR in .env does not point at the game folder.' }
    if (Get-Process NSUNS4 -ErrorAction SilentlyContinue) { throw 'Close the game before deploying.' }
    Copy-Item "$Stage\winmm.dll" "$game\winmm.dll" -Force
    Copy-Item "$Stage\kawarimi\*" "$game\kawarimi" -Recurse -Force
    New-Item -ItemType Directory -Force "$game\mods" | Out-Null
    Write-Host "Deployed to $game" -ForegroundColor Green
}

Push-Location $Root
try {
    if ($Format) { Invoke-Format }
    Build-Managed
    Build-Native
    if (-not $SkipTests) { Invoke-Tests }
    New-Payload
    if (-not $SkipInstaller) { Build-Installer }
    if ($Deploy) { Invoke-Deploy }
}
finally { Pop-Location }
