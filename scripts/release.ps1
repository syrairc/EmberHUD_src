param([string]$Out, [switch]$Public, [switch]$SkipTests, [switch]$SkipObfuscar)
$ErrorActionPreference = 'Stop'
$src = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot)).TrimEnd('\', '/')
$outName = $Public ? 'release' : 'dist'
$versionFile = Join-Path $src 'version.txt'
if (-not $Out -and -not $Public) {
    $v = [version](Get-Content $versionFile -Raw).Trim()
    Set-Content $versionFile "$($v.Major).$($v.Minor).$($v.Build + 1)" -NoNewline
}
if (-not $Out) { $Out = Join-Path $PSScriptRoot "..\..\$outName" }
$Out = [IO.Path]::GetFullPath($Out, (Get-Location).ProviderPath).TrimEnd('\', '/')
$skipPlugins = 'bin', 'obj'
if ($Public) { $skipPlugins += 'Inspector', 'Sample', 'Mcp', 'Tartarus' }
$work = Join-Path ([IO.Path]::GetTempPath()) "emberhud-release-$([guid]::NewGuid().ToString('N'))"
$publish = Join-Path $work 'publish'
$obfuscated = Join-Path $work 'obfuscated'
$ship = @(
    'Ember.exe'
    'Ember.dll'
    'EmberHUD.Api.dll'
    'Ember.runtimeconfig.json'
    'Ember.deps.json'
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
    'EmberLauncher.exe'
    'EmberLauncher.dll'
    'EmberLauncher.runtimeconfig.json'
    'EmberLauncher.deps.json'
    'EmberUpdater.exe'
    'EmberUpdater.dll'
    'EmberUpdater.runtimeconfig.json'
    'EmberUpdater.deps.json'
    'EmberSetup.exe'
    'EmberSetup.dll'
    'EmberSetup.runtimeconfig.json'
    'EmberSetup.deps.json'
    'ooz.dll'
    'BCnEncoder.dll'
    'Microsoft.Toolkit.HighPerformance.dll'
    'ZstdSharp.dll'
)
$assets = @(
    @{ Dir = 'Fonts'; Include = '*.ttf', 'OFL*.txt' }
    @{ Dir = 'Themes'; Include = '*.json' }
    @{ Dir = 'licenses'; Include = 'THIRD-PARTY.txt', 'ooz-GPL-3.0.txt' }
)

if ($Out.StartsWith('\\')) { throw "$Out is a UNC or device path - refusing to wipe it" }
if ($Out -ieq $src -or "$src\".StartsWith("$Out\", [StringComparison]::OrdinalIgnoreCase)) { throw "$Out is the src dir or an ancestor of it - refusing to wipe it" }
if ((Split-Path $Out -Leaf) -ine $outName) { throw "$Out is not named $outName - refusing to wipe it" }
if ($Public) { [IO.Directory]::CreateDirectory($Out) | Out-Null }
elseif (-not (Test-Path -LiteralPath (Join-Path $Out '.git'))) { throw "$Out is not a git repo root - refusing to wipe it" }
if ($Public -and -not (Test-Path -LiteralPath "$src/scripts/README.release.md")) { throw "$src/scripts/README.release.md is missing" }

foreach ($asset in $assets) {
    foreach ($pattern in $asset.Include) {
        if (-not (Get-ChildItem (Join-Path "$src/$($asset.Dir)" $pattern) -File -ErrorAction SilentlyContinue)) { throw "nothing matching $pattern in $src/$($asset.Dir)" }
    }
}
if (-not (Get-ChildItem "$src/Plugins" -Directory -ErrorAction SilentlyContinue | Where-Object Name -notin $skipPlugins)) { throw "no plugin sources under $src/Plugins" }

$userData = 'Config', 'Cache', 'Logs', 'imgui.ini', 'GameIcons'
$assertWipeable = {
    $running = @(Get-CimInstance Win32_Process -Property ExecutablePath | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith("$Out\", [StringComparison]::OrdinalIgnoreCase) })
    if ($running) { throw "$($running[0].ExecutablePath) is running - close it first, refusing to wipe $Out" }
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
    if (-not $SkipTests) {
        & "$PSScriptRoot/build.ps1"
        if ($LASTEXITCODE) { throw "build failed (exit $LASTEXITCODE)" }
    }

    foreach ($project in 'EmberHUD/EmberHUD.csproj', 'EmberHUD.Launcher/EmberHUD.Launcher.csproj', 'EmberHUD.Setup/EmberHUD.Setup.csproj', 'EmberHUD.Updater/EmberHUD.Updater.csproj') {
        dotnet publish "$src/$project" -c Release -o $publish
        if ($LASTEXITCODE) { throw "publish failed for $project (exit $LASTEXITCODE)" }
    }

    $plugins = Join-Path $publish 'Plugins'
    Remove-Item $plugins -Recurse -Force -ErrorAction SilentlyContinue
    foreach ($plugin in Get-ChildItem "$src/Plugins" -Directory | Where-Object Name -notin $skipPlugins) {
        foreach ($file in Get-ChildItem $plugin.FullName -Recurse -File) {
            $relative = $file.FullName.Substring($plugin.FullName.Length)
            if ($relative -match '[\\/](bin|obj)[\\/]') { continue }
            if ($Public -and $relative -match '^[\\/]Tests[\\/]') { continue }
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

    $dlls = 'Ember.dll', 'EmberSetup.dll', 'EmberLauncher.dll', 'EmberUpdater.dll'
    $plainHashes = @{}
    $obfuscatedHashes = @{}
    foreach ($name in $dlls) { $plainHashes[$name] = (Get-FileHash (Join-Path $publish $name)).Hash }

    $dotnet = Split-Path (Get-Command dotnet).Source
    $framework = Get-ChildItem "$dotnet/packs/Microsoft.NETCore.App.Ref" -Directory | Where-Object Name -match '^10\.0\.\d+$' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
    if (-not $framework) { throw "no .NET 10 reference pack under $dotnet/packs" }
    $frameworkRef = Join-Path $framework.FullName 'ref\net10.0'
    if (-not (Test-Path $frameworkRef)) { throw "missing $frameworkRef" }
    if (-not $SkipObfuscar) {
        $obfuscarXml = Join-Path $work 'obfuscar.generated.xml'
        (Get-Content "$src/scripts/obfuscar.xml" -Raw).
            Replace('%EMBERHUD_PUBLISH%', $publish).
            Replace('%EMBERHUD_OBFUSCATED%', $obfuscated).
            Replace('%EMBERHUD_FRAMEWORK%', $frameworkRef) |
            Set-Content -LiteralPath $obfuscarXml
        Push-Location $src
        try {
            dotnet tool restore
            if ($LASTEXITCODE) { throw "dotnet tool restore failed (exit $LASTEXITCODE)" }
            dotnet tool run obfuscar.console $obfuscarXml
            if ($LASTEXITCODE) { throw "obfuscar failed (exit $LASTEXITCODE)" }
        }
        finally { Pop-Location }

        foreach ($name in $dlls) {
            $obfuscatedDll = Join-Path $obfuscated $name
            if (-not (Test-Path $obfuscatedDll)) { throw "obfuscar wrote no $name to $obfuscated" }
            $obfuscatedHash = (Get-FileHash $obfuscatedDll).Hash
            if ($obfuscatedHash -eq $plainHashes[$name]) { throw "obfuscar output for $name is byte-identical to its input" }
            $obfuscatedHashes[$name] = $obfuscatedHash
            Copy-Item $obfuscatedDll (Join-Path $publish $name) -Force
        }
    }

    $folders = @('Plugins') + $assets.Dir

    $check = Start-Process (Join-Path $publish 'Ember.exe') -ArgumentList '--check' -WorkingDirectory $publish -NoNewWindow -PassThru
    $null = $check.Handle
    if (-not $check.WaitForExit(300000)) { $check.Kill(); $check.WaitForExit(); throw 'Ember.exe --check timed out after 5 minutes' }
    $report = Join-Path $publish 'Logs/check.txt'
    if (-not (Test-Path $report)) { throw "Ember.exe --check exited $($check.ExitCode) without writing $report" }
    Get-Content $report | Write-Host
    if ($check.ExitCode) { throw "Ember.exe --check failed (exit $($check.ExitCode))" }

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
    $keep = if ($Public) { '.git', '.gitignore' } else { @('.git', '.gitignore', 'README.md') + $userData }
    Get-ChildItem -LiteralPath $Out -Force | Where-Object { $_.Name -notin $keep -and -not (-not $Public -and -not $_.PSIsContainer -and $_.Name -in 'LICENSE', 'LICENSE.md', 'LICENSE.txt') } | Remove-Item -Recurse -Force
    $files | Where-Object { $name = $_.Name; $ship | Where-Object { $name -like $_ } } | Copy-Item -Destination $Out
    foreach ($dir in $folders) { Copy-Item (Join-Path $publish $dir) $Out -Recurse }
    Copy-Item "$src/scripts/steam/*.reg" $Out
    if ($Public) { Copy-Item "$src/scripts/README.release.md" (Join-Path $Out 'README.md') }

    $root = (Resolve-Path -LiteralPath $Out).Path
    $private = & $findPrivate (Get-ChildItem -LiteralPath $root -Recurse -Force | Where-Object { $_.FullName -notmatch '[\\/]\.git([\\/]|$)' -and ($_.FullName.Substring($root.Length + 1) -split '[\\/]')[0] -notin $userData }) $root
    if ($private) { throw "private files in $root - $($private.FullName -join ', ')" }

    if (-not $SkipObfuscar) {
        foreach ($name in $dlls) {
            if ((Get-FileHash -LiteralPath (Join-Path $root $name)).Hash -ne $obfuscatedHashes[$name]) { throw "$root/$name is not the obfuscated build" }
        }
    }

    if ($Public) {
        $hashes = [Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::Ordinal)
        foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Force) {
            $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
            $parts = $relative -split '/'
            if ($parts[0] -in $userData -or $parts[0] -eq 'Updates' -or ($parts | Where-Object { $_ -in '.git', '.gitignore' }) -or $relative -eq 'manifest.json' -or $file.Extension -eq '.zip') { continue }
            $hashes[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        $manifest = [ordered]@{ Version = (Get-Content $versionFile -Raw).Trim(); Files = $hashes }
        [IO.File]::WriteAllText((Join-Path $root 'manifest.json'), ($manifest | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))
        $zip = Join-Path $root "EmberHUD-$(Get-Date -Format yyyyMMdd)-v$($manifest.Version).zip"
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
        try {
            foreach ($relative in @($hashes.Keys) + 'manifest.json') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $root $relative), $relative) }
        }
        finally { $archive.Dispose() }
    }

    Write-Host "release ready in $root - the obfuscation map was at $obfuscated\Mapping.txt, already deleted with the temp work dir"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
