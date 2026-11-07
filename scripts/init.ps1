$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
foreach ($name in 'blog', 'spiral') {
    $found = $null
    for ($dir = Split-Path $root; $dir -and -not $found; $dir = Split-Path $dir) {
        if ((Split-Path $dir -Leaf) -eq $name -and (Test-Path (Join-Path $dir '.git'))) { $found = $dir }
        elseif (Test-Path (Join-Path (Join-Path $dir $name) '.git')) { $found = Join-Path $dir $name }
    }
    if ($found) { "$name found at $found"; continue }
    $target = Join-Path $root "deps/$name"
    if (Test-Path $target) { continue }
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    git clone --recurse-submodules "https://github.com/i574n/$name.git" $target
    if ($LASTEXITCODE -ne 0) { throw "init.ps1 / git clone $name failed (exit code $LASTEXITCODE)" }
}
