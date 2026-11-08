$ErrorActionPreference = 'Stop'
pwsh (Join-Path $PSScriptRoot 'init.ps1')
if ($LASTEXITCODE -ne 0) { throw "workflow.ps1 / init failed (exit code $LASTEXITCODE)" }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$spiral = $null; foreach ($c in @((Join-Path $root 'deps/spiral')) + @(for ($d = Split-Path $root; $d; $d = Split-Path $d) { $d; Join-Path $d 'spiral' })) { if ((Split-Path $c -Leaf) -eq 'spiral' -and (Test-Path (Join-Path $c 'lib/spiral/lib.ps1'))) { $spiral = $c; break } }
if (-not $spiral) { throw 'workflow.ps1 / spiral not found' }
. (Join-Path $spiral 'scripts/core.ps1')
. (Join-Path $spiral 'lib/spiral/lib.ps1')
Ensure-SpiralCompiler
if (-not $env:SPIRAL_COMPILER_PACKAGE_DIR) { $env:SPIRAL_COMPILER_PACKAGE_DIR = Join-Path $root 'VS Code Plugin' }
Push-Location (Join-Path $spiral 'apps/cells'); try { { mix deps.get } | Invoke-Block } finally { Pop-Location }
pwsh (Join-Path $PSScriptRoot 'post.ps1')
if ($LASTEXITCODE -ne 0) { throw "workflow.ps1 / post failed (exit code $LASTEXITCODE)" }