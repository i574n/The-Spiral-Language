param([string[]] $Post = @())
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Find-Repo([string] $name) {
    $local = Join-Path $root "deps/$name"
    if (Test-Path $local) { return $local }
    for ($dir = Split-Path $root; $dir; $dir = Split-Path $dir) {
        if ((Split-Path $dir -Leaf) -eq $name -and (Test-Path (Join-Path $dir '.git'))) { return $dir }
        $candidate = Join-Path $dir $name
        if (Test-Path (Join-Path $candidate '.git')) { return $candidate }
    }
    $null
}
$spiral = Find-Repo 'spiral'
$blog = Find-Repo 'blog'
if (-not $spiral -or -not $blog) { throw 'post.ps1 / spiral or blog not found beside or above this checkout: run scripts/init.ps1' }
$posts = if ($Post.Count) { $Post } else { @(Get-ChildItem (Join-Path $root 'posts') -Directory | Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).spi") } | ForEach-Object Name) }
if ($IsWindows) { $env:PATH = "$HOME\scoop\apps\erlang\current\bin;$HOME\scoop\apps\elixir\current\bin;$env:PATH" }
foreach ($name in $posts) {
    $source = Join-Path $root "posts/$name"
    $overlay = Join-Path $root "target/post/$name/blog"
    if (Test-Path $overlay) { Remove-Item -LiteralPath $overlay -Recurse -Force }
    New-Item -ItemType Directory -Force $overlay | Out-Null
    foreach ($part in 'engine', 'posts', 'app') { $from = Join-Path $blog $part; if (Test-Path $from) { Copy-Item $from (Join-Path $overlay $part) -Recurse } }
    Copy-Item (Join-Path $source "$name.spi") $overlay
    $project = [IO.File]::ReadAllText((Join-Path $blog 'package.spiproj')).Replace("`r`n", "`n")
    $libAnchor = 'packageDir: ../spiral/lib'
    $postsAnchor = "`n    posts/`n"
    if (-not $project.Contains($libAnchor) -or ([regex]::Matches($project, [regex]::Escape($postsAnchor))).Count -ne 1) { throw 'post.ps1 / the blog package.spiproj changed: update the anchors' }
    $project = $project.Replace($libAnchor, "packageDir: $((Join-Path $spiral 'lib').Replace('\', '/'))").Replace($postsAnchor, "`n    $name$postsAnchor")
    [IO.File]::WriteAllText((Join-Path $overlay 'package.spiproj'), $project)
    $env:MIX_BUILD_PATH = Join-Path ([IO.Path]::GetTempPath()) "fork-post-$name"
    $env:SPIRAL_CELLS_MEMO = '0'
    $env:SPIRAL_CELLS_WARM = '0'
    $env:SPIRAL_LIB = Join-Path $spiral 'lib/spiral'
    Push-Location (Join-Path $spiral 'apps/cells')
    try {
        $out = (mix spiral.notebook --no-memo --path (Join-Path $overlay "$name.spi") --output-path (Join-Path $source "$name.spi.ipynb") 2>&1 | Out-String)
        $out
        if ($LASTEXITCODE -ne 0) { throw "post.ps1 / $name gate exit $LASTEXITCODE" }
        if ($out -notmatch '"failed":0,' -or $out -match '"executed":0,') { throw "post.ps1 / $name ran no tests or had failures" }
    } finally {
        Pop-Location
        Remove-Item Env:MIX_BUILD_PATH, Env:SPIRAL_CELLS_MEMO, Env:SPIRAL_CELLS_WARM, Env:SPIRAL_LIB -ErrorAction Ignore
    }
    "post $name compiles against blog's engine and its tests pass; page: posts/$name/$name.spi.html"
}