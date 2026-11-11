$script:DotEnv = @{}
$envFile = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') { $script:DotEnv[$Matches[1]] = $Matches[2].Trim().Trim('"') }
    }
}

function Get-EnvValue([string]$Key, [string]$Default = '') {
    $fromProcess = [Environment]::GetEnvironmentVariable($Key)
    if ($fromProcess) { return $fromProcess }
    if ($script:DotEnv.ContainsKey($Key) -and $script:DotEnv[$Key]) { return $script:DotEnv[$Key] }
    return $Default
}

function Use-Msvc {
    if (Get-Command cl -ErrorAction SilentlyContinue) { return }
    $vcvars = Get-EnvValue 'VCVARS'
    if (-not $vcvars -or -not (Test-Path $vcvars)) { throw 'cl.exe not found. Set VCVARS in .env to your vcvars64.bat.' }
    foreach ($line in cmd /c "`"$vcvars`" >nul && set") {
        if ($line -match '^([^=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2]) }
    }
}
