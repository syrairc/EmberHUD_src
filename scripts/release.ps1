param([string]$Out = (Join-Path $PSScriptRoot '..\..\dist'))
$ErrorActionPreference = 'Stop'
$src = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot)).TrimEnd('\', '/')
$Out = [IO.Path]::GetFullPath($Out, (Get-Location).ProviderPath).TrimEnd('\', '/')
$work = Join-Path ([IO.Path]::GetTempPath()) "torchlightsdk-release-$([guid]::NewGuid().ToString('N'))"
$publish = Join-Path $work 'publish'
$gameIcons = Join-Path (Split-Path $src) 'assets\icons'
$ship = @(
    'TorchlightSDK.exe'
    'TorchlightSDK.dll'
    'TorchlightSDK.Api.dll'
    'TorchlightSDK.runtimeconfig.json'
    'TorchlightSDK.deps.json'
    'Hexa*.dll'
    'cimgui.dll'
    'ImGuiImpl.dll'
    'Vortice*.dll'
    'SharpGen*.dll'
    'Microsoft.CodeAnalysis*.dll'
    'Svg*.dll'
    'ShimSkiaSharp.dll'
    'ExCSS.dll'
    'SkiaSharp.dll'
    'HarfBuzzSharp.dll'
    'libSkiaSharp.dll'
    'libHarfBuzzSharp.dll'
)
$assets = @(
    @{ Dir = 'Fonts'; Include = '*.ttf', 'OFL*.txt' }
    @{ Dir = 'Themes'; Include = '*.json' }
    @{ Dir = 'licenses'; Include = 'THIRD-PARTY.txt' }
)

if ($Out.StartsWith('\\')) { throw "$Out is a UNC or device path - refusing to wipe it" }
if ($Out -ieq $src -or "$src\".StartsWith("$Out\", [StringComparison]::OrdinalIgnoreCase)) { throw "$Out is the src dir or an ancestor of it - refusing to wipe it" }
if ((Split-Path $Out -Leaf) -ine 'dist') { throw "$Out is not named dist - refusing to wipe it" }
if (-not (Test-Path -LiteralPath (Join-Path $Out '.git'))) { throw "$Out is not a git repo root - refusing to wipe it" }

foreach ($asset in $assets) {
    foreach ($pattern in $asset.Include) {
        if (-not (Get-ChildItem (Join-Path "$src/$($asset.Dir)" $pattern) -File -ErrorAction SilentlyContinue)) { throw "nothing matching $pattern in $src/$($asset.Dir)" }
    }
}
if (-not (Get-ChildItem "$src/Plugins" -Directory -ErrorAction SilentlyContinue | Where-Object Name -notin 'bin', 'obj')) { throw "no plugin sources under $src/Plugins" }

$assertWipeable = {
    for ($segment = $Out; $segment; $segment = Split-Path $segment -Parent) {
        $item = Get-Item -LiteralPath $segment -Force -ErrorAction SilentlyContinue
        if ($item -and $item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw "$segment is a junction or symlink - refusing to wipe through it" }
    }
    if ((Get-ChildItem -LiteralPath $Out -File -Force | Where-Object { $_.Name -like '*.slnx' -or $_.Name -like '*.csproj' }) -or (Test-Path -LiteralPath (Join-Path $Out 'scripts\release.ps1'))) { throw "$Out looks like a source tree - refusing to wipe it" }
}
& $assertWipeable
$findPrivate = {
    param($items, $root)
    @($items | Where-Object {
        ($_.Extension -in '.pdb', '.want', '.csproj') -or
        ($_.Extension -eq '.md' -and -not (($_.Name -eq 'README.md' -or $_.Name -eq 'LICENSE.md') -and $_.DirectoryName -eq $root)) -or
        ($_.Name -like '*.g.cs') -or
        ($_.Name -like 'offsets*') -or
        ($_.Name -in 'Mapping.txt', 'obfuscar.xml', 'imgui.ini') -or
        ($_.PSIsContainer -and $_.Name -in 'docs', 'Config', 'Cache', 'Logs', 'bin', 'obj')
    })
}
try {
    & "$PSScriptRoot/build.ps1"
    if ($LASTEXITCODE) { throw "build failed (exit $LASTEXITCODE)" }

    dotnet publish "$src/TorchlightSDK/TorchlightSDK.csproj" -c Release -r win-x64 --self-contained false -p:SatelliteResourceLanguages=en -o $publish
    if ($LASTEXITCODE) { throw "publish failed (exit $LASTEXITCODE)" }

    $plugins = Join-Path $publish 'Plugins'
    Remove-Item $plugins -Recurse -Force -ErrorAction SilentlyContinue
    foreach ($plugin in Get-ChildItem "$src/Plugins" -Directory | Where-Object Name -notin 'bin', 'obj') {
        foreach ($file in Get-ChildItem $plugin.FullName -Recurse -File) {
            $relative = $file.FullName.Substring($plugin.FullName.Length)
            if ($relative -match '[\\/](bin|obj)[\\/]') { continue }
            if ($file.Extension -ne '.cs' -and $file.Name -ne 'defaults.json' -and $relative -notmatch '^[\\/]Icons[\\/][^\\/]+\.svg$') { continue }
            $target = Join-Path $plugins "$($plugin.Name)$relative"
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            Copy-Item $file.FullName $target
        }
    }
    if (-not (Test-Path $plugins)) { throw "no plugin sources staged from $src/Plugins" }

    foreach ($asset in $assets) {
        $target = Join-Path $publish $asset.Dir
        Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force $target | Out-Null
        foreach ($pattern in $asset.Include) {
            $found = @(Get-ChildItem (Join-Path "$src/$($asset.Dir)" $pattern) -File)
            if (-not $found) { throw "nothing matching $pattern in $src/$($asset.Dir)" }
            $found | Copy-Item -Destination $target
        }
    }

    $folders = @('Plugins') + $assets.Dir
    if (Test-Path -LiteralPath (Join-Path $gameIcons 'index.json')) {
        $target = Join-Path $publish 'GameIcons'
        New-Item -ItemType Directory -Force $target | Out-Null
        Get-ChildItem -LiteralPath $gameIcons -File | Where-Object { $_.Name -eq 'index.json' -or $_.Extension -eq '.png' } | Copy-Item -Destination $target
        $folders += 'GameIcons'
    }
    else { Write-Host "no game icon art at $gameIcons - the release draws shapes" }

    $check = Start-Process (Join-Path $publish 'TorchlightSDK.exe') -ArgumentList '--check' -WorkingDirectory $publish -NoNewWindow -PassThru
    $null = $check.Handle
    if (-not $check.WaitForExit(300000)) { $check.Kill(); $check.WaitForExit(); throw 'TorchlightSDK.exe --check timed out after 5 minutes' }
    $report = Join-Path $publish 'Logs/check.txt'
    if (-not (Test-Path $report)) { throw "TorchlightSDK.exe --check exited $($check.ExitCode) without writing $report" }
    Get-Content $report | Write-Host
    if ($check.ExitCode) { throw "TorchlightSDK.exe --check failed (exit $($check.ExitCode))" }

    $staged = @(Get-ChildItem -LiteralPath $publish -File | Where-Object { $name = $_.Name; $ship | Where-Object { $name -like $_ } })
    foreach ($dir in $folders) { $staged += Get-ChildItem -LiteralPath (Join-Path $publish $dir) -Recurse -Force }
    $stagedPrivate = & $findPrivate $staged $publish
    if ($stagedPrivate) { throw "private files staged for release - $($stagedPrivate.FullName -join ', ')" }

    $files = @(Get-ChildItem $publish -File)
    foreach ($pattern in $ship) {
        if (-not ($files | Where-Object Name -like $pattern)) { throw "allow-list entry $pattern matches nothing in $publish" }
    }
    $unlisted = @(Get-ChildItem $publish -Recurse -File -Filter *.dll | Where-Object { $name = $_.Name; $_.DirectoryName -ne $publish -or -not ($ship | Where-Object { $name -like $_ }) })
    if ($unlisted) { throw "publish has dlls the allow-list doesn't ship - $($unlisted.FullName -join ', ')" }

    & $assertWipeable
    [IO.Directory]::CreateDirectory($Out) | Out-Null
    Get-ChildItem -LiteralPath $Out -Force | Where-Object { $_.Name -notin '.git', '.gitignore', 'README.md' -and -not (-not $_.PSIsContainer -and $_.Name -in 'LICENSE', 'LICENSE.md', 'LICENSE.txt') } | Remove-Item -Recurse -Force
    $files | Where-Object { $name = $_.Name; $ship | Where-Object { $name -like $_ } } | Copy-Item -Destination $Out
    foreach ($dir in $folders) { Copy-Item (Join-Path $publish $dir) $Out -Recurse }

    $root = (Resolve-Path -LiteralPath $Out).Path
    $private = & $findPrivate (Get-ChildItem -LiteralPath $root -Recurse -Force | Where-Object { $_.FullName -notmatch '[\\/]\.git([\\/]|$)' }) $root
    if ($private) { throw "private files in $root - $($private.FullName -join ', ')" }

    Write-Host "release ready in $root"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
