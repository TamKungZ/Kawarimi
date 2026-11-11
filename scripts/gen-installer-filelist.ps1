param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Target,
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$OutDir
)
$ErrorActionPreference = 'Stop'
$src = (Resolve-Path $Source).Path.TrimEnd('\')
New-Item -ItemType Directory -Force $OutDir | Out-Null
$files = Get-ChildItem $src -Recurse -File | Sort-Object FullName
$install = [System.Collections.Generic.List[string]]::new()
$remove = [System.Collections.Generic.List[string]]::new()
$dirs = [System.Collections.Generic.HashSet[string]]::new()
foreach ($f in $files) {
    $rel = $f.FullName.Substring($src.Length + 1)
    $relDir = Split-Path $rel -Parent
    $destDir = if ($relDir) { "$Target\$relDir" } else { $Target }
    $install.Add("  SetOutPath `"$destDir`"")
    $install.Add("  File `"$($f.FullName)`"")
    $remove.Add("  Delete `"$Target\$rel`"")
    $d = $relDir
    while ($d) {
        [void]$dirs.Add($d)
        $d = Split-Path $d -Parent
    }
}
foreach ($d in ($dirs | Sort-Object { $_.Length } -Descending)) { $remove.Add("  RMDir `"$Target\$d`"") }
Set-Content "$OutDir\$Name.nsh" $install -Encoding UTF8
Set-Content "$OutDir\$Name-un.nsh" $remove -Encoding UTF8
Write-Host "  $Name : $($files.Count) file(s)"
