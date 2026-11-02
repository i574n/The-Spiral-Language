$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$spiral = $null; foreach ($c in @((Join-Path $root 'deps/spiral')) + @(for ($d = Split-Path $root; $d; $d = Split-Path $d) { $d; Join-Path $d 'spiral' })) { if ((Split-Path $c -Leaf) -eq 'spiral' -and (Test-Path (Join-Path $c 'lib/spiral/lib.ps1'))) { $spiral = $c; break } }
if (-not $spiral) { throw 'publish.ps1 / spiral not found' }
& (Join-Path $spiral 'scripts/publish-tree.ps1') -Root $root -Include '*.spi.html', '*.spi.ipynb' -Exclude 'target', 'deps', '_build', 'node_modules'