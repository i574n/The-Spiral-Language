$WarningPreference = 'SilentlyContinue'; $ErrorActionPreference = "Stop"; Set-StrictMode -Version Latest

param (
    [bool]$Prerelease = $false,
    [string]$Rank = "patch" # Can also be "minor" and "major"
)

# TODO: Need to test whether this works.
$prerelease_arg = if ($Prerelease) { "--pre-release" } else { "" }

try {
    $keys = Get-Content -Raw -Path .\keys.json | ConvertFrom-Json
    function Publish-Vsce {
        Write-Host "Publishing on VSCE."
        npx "@vscode/vsce" publish $Rank $prerelease_arg # https://marketplace.visualstudio.com/items?itemName=mrakgr.spiral-lang-vscode
    }
    
    function Publish-Ovsx {
        Write-Host "Publishing on OVSX."
        npx ovsx publish -p $keys.ovsx $prerelease_arg # https://open-vsx.org/extension/mrakgr/spiral-lang-vscode
    }

    $files = @(
        "readme.md"
        "spiral_logo.png"
        )
    foreach ($file in $files) { # Copies the readme and the image files into the current folder so they get packed with the extension.
        Copy-Item ../$file .
    }
    
    Publish-Vsce
    Publish-Ovsx
}
finally {
    foreach ($file in $files) { # Copies the readme and the image files in the current folder
        Remove-Item ./$file
    }
}
