$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\release'))
$manifest = Join-Path $root 'manifest.json'
if (-not (Test-Path $manifest)) { throw "no manifest.json in $root - run release.ps1 -Public first" }
$version = (Get-Content $manifest -Raw | ConvertFrom-Json).Version
$zip = Get-ChildItem $root -Filter "EmberHUD-*-v$version.zip" | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $zip) { throw "no EmberHUD-*-v$version.zip in $root - run release.ps1 -Public first" }
Push-Location $root
try {
    if (git status --porcelain --untracked-files=no) { throw 'release repo has uncommitted changes to tracked files - commit and push first' }
    git fetch --quiet
    if (git rev-list '@{u}..HEAD') { throw 'release repo has unpushed commits - push first' }
    gh release create $version $zip.FullName $manifest --title $version --notes "EmberHUD $version" --latest
}
finally { Pop-Location }
