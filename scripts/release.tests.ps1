$ErrorActionPreference = 'Stop'
$box = Join-Path ([IO.Path]::GetTempPath()) "emberhud-releasetests-$([guid]::NewGuid().ToString('N'))"
$release = "$box/repo/src/scripts/release.ps1"
$links = [Collections.Generic.List[string]]::new()
$failed = 0
$stubDotnet = @'
$a = $args
$stage = $env:STUB_FAIL
if ($a[0] -eq 'publish') {
    if ($stage -eq 'publish') { exit 1 }
    $dir = $a[[array]::IndexOf($a, '-o') + 1]
    New-Item -ItemType Directory -Force $dir | Out-Null
    $project = $a[1]
    if ($project -like '*EmberHUD.Launcher*') { foreach ($name in 'EmberLauncher.exe', 'EmberLauncher.dll', 'EmberLauncher.runtimeconfig.json', 'EmberLauncher.deps.json') { Set-Content (Join-Path $dir $name) plain } }
    elseif ($project -like '*EmberHUD.Updater*') { foreach ($name in 'EmberUpdater.exe', 'EmberUpdater.dll', 'EmberUpdater.runtimeconfig.json', 'EmberUpdater.deps.json') { Set-Content (Join-Path $dir $name) plain } }
    elseif ($project -like '*EmberHUD.Setup*') { foreach ($name in 'EmberSetup.exe', 'EmberSetup.dll', 'EmberSetup.runtimeconfig.json', 'EmberSetup.deps.json', 'EmberLauncher.exe', 'EmberLauncher.dll', 'EmberLauncher.runtimeconfig.json', 'EmberLauncher.deps.json', 'ooz.dll', 'BCnEncoder.dll', 'Microsoft.Toolkit.HighPerformance.dll', 'ZstdSharp.dll', 'EmberHUD.Api.dll', 'Hexa.NET.ImGui.dll', 'SkiaSharp.dll', 'libSkiaSharp.dll') { Set-Content (Join-Path $dir $name) plain } }
    else {
        foreach ($name in 'Ember.exe', 'Ember.dll', 'Ember.runtimeconfig.json', 'Ember.deps.json') { Copy-Item (Join-Path $env:STUB_APP $name) $dir }
        foreach ($name in 'EmberHUD.Api.dll', 'Hexa.NET.ImGui.dll', 'HexaGen.Runtime.dll', 'cimgui.dll', 'ImGuiImpl.dll', 'Vortice.Direct3D11.dll', 'SharpGen.Runtime.dll', 'Microsoft.CodeAnalysis.dll', 'Svg.Skia.dll', 'ShimSkiaSharp.dll', 'ExCSS.dll', 'SkiaSharp.dll', 'HarfBuzzSharp.dll', 'libSkiaSharp.dll', 'libHarfBuzzSharp.dll', 'libSkiaSharp.pdb') { Set-Content (Join-Path $dir $name) plain }
    }
    if ($project -notlike '*EmberHUD.Launcher*' -and $project -notlike '*EmberHUD.Setup*' -and $project -notlike '*EmberHUD.Updater*' -and $stage -eq 'unlisted') { Set-Content (Join-Path $dir 'Extra.dll') plain }
    if ($project -notlike '*EmberHUD.Launcher*' -and $project -notlike '*EmberHUD.Setup*' -and $project -notlike '*EmberHUD.Updater*' -and $stage -eq 'swap') {
        Remove-Item -LiteralPath $env:STUB_SWAP_DIST -Recurse -Force
        New-Item -ItemType Junction -Path $env:STUB_SWAP_DIST -Target $env:STUB_SWAP_TARGET | Out-Null
    }
}
elseif ($a[0] -eq 'tool' -and $a[1] -eq 'restore') { exit 0 }
elseif ($a[0] -eq 'tool' -and $a[1] -eq 'run' -and $a[2] -eq 'obfuscar.console') {
    if ($stage -eq 'obfuscate') { exit 1 }
    $raw = Get-Content -LiteralPath $a[3] -Raw
    if ($raw -match '%[A-Za-z_]+%') { Write-Error "obfuscar config has unsubstituted token $($Matches[0])"; exit 1 }
    [xml]$config = $raw
    $modules = @($config.Obfuscator.Module)
    if (-not $modules) { Write-Error 'obfuscar config has no Module elements'; exit 1 }
    $inPath = @($config.Obfuscator.Var | Where-Object name -eq 'InPath')[0].value
    $outPath = @($config.Obfuscator.Var | Where-Object name -eq 'OutPath')[0].value
    New-Item -ItemType Directory -Force $outPath | Out-Null
    foreach ($module in $modules) {
        $modulePath = $module.file.Replace('$(InPath)', $inPath)
        if (-not (Test-Path -LiteralPath $modulePath)) { Write-Error "obfuscar config module file not found: $modulePath"; exit 1 }
        $target = Join-Path $outPath (Split-Path $modulePath -Leaf)
        Copy-Item -LiteralPath $modulePath $target
        if ($stage -ne 'obfuscate-samehash') { [IO.File]::AppendAllText($target, 'x') }
    }
    Set-Content (Join-Path $outPath 'Mapping.txt') x
}
exit 0
'@
$stubBuild = 'if ($env:STUB_FAIL -eq ''build'') { exit 99 }; exit 0'
try {
    New-Item -ItemType Directory -Force "$box/bin", "$box/stubapp", "$box/bin/packs/Microsoft.NETCore.App.Ref/10.0.0/ref/net10.0" | Out-Null
    Set-Content "$box/stubapp/EmberHUD.csproj" '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>Ember</AssemblyName><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>'
    Set-Content "$box/stubapp/Program.cs" 'if (Environment.GetEnvironmentVariable("STUB_FAIL") == "check") return 1; Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Logs")); File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Logs", "check.txt"), "PASS stub"); return 0;'
    dotnet build "$box/stubapp/EmberHUD.csproj" -o "$box/stubapp-out" | Out-Null
    if ($LASTEXITCODE) { throw 'stub app build failed' }
    Set-Content "$box/bin/stubdotnet.ps1" $stubDotnet
    Set-Content "$box/bin/dotnet.cmd" "@pwsh -NoProfile -File `"%~dp0stubdotnet.ps1`" %*`r`n@exit /b %ERRORLEVEL%"

    foreach ($nested in 'ancestor/dist/src', 'same/dist') {
        New-Item -ItemType Directory -Force "$box/$nested/scripts", "$box/$($nested -replace '/src$')/.git" | Out-Null
        Copy-Item "$PSScriptRoot/release.ps1" "$box/$nested/scripts/release.ps1"
        Set-Content "$box/$nested/scripts/build.ps1" $stubBuild
    }
    New-Item -ItemType Directory -Force "$box/repo/.git", "$box/repo/src/scripts", "$box/repo/dist/.git", "$box/repo/dist/old", "$box/plain/dist", "$box/notdist/.git", "$box/repo/other/.git", "$box/repo/dist2" | Out-Null
    Copy-Item "$PSScriptRoot/release.ps1" $release
    Copy-Item "$PSScriptRoot/obfuscar.xml" "$box/repo/src/scripts/obfuscar.xml"
    Set-Content "$box/repo/src/scripts/build.ps1" $stubBuild
    New-Item -ItemType Directory -Force "$box/repo/src/Fonts", "$box/repo/src/Themes", "$box/repo/src/licenses", "$box/repo/src/Plugins/Sample/Icons/deep", "$box/repo/src/Plugins/Sample/bin", "$box/repo/src/Plugins/Sample/Tests", "$box/repo/src/Plugins/Minimap/Tests", "$box/repo/src/Plugins/Inspector", "$box/repo/src/Plugins/Mcp", "$box/repo/src/scripts/steam", "$box/repo/assets/icons" | Out-Null
    foreach ($file in 'Fonts/a.ttf', 'Fonts/OFL.txt', 'Themes/a.json', 'licenses/THIRD-PARTY.txt', 'licenses/ooz-GPL-3.0.txt', 'Plugins/Sample/Main.cs', 'Plugins/Sample/defaults.json', 'Plugins/Sample/notes.txt', 'Plugins/Sample/Icons/dot.svg', 'Plugins/Sample/Icons/deep/skip.svg', 'Plugins/Sample/bin/Skip.cs', 'Plugins/Sample/Tests/T.cs', 'Plugins/Minimap/Main.cs', 'Plugins/Minimap/Tests/T.cs', 'Plugins/Inspector/Main.cs', 'Plugins/Mcp/Main.cs', 'Plugins/Plugins.csproj', 'scripts/README.release.md', 'scripts/steam/a.reg') { Set-Content "$box/repo/src/$file" x }
    Set-Content "$box/repo/src/version.txt" '1.2.3' -NoNewline
    foreach ($file in 'index.json', 'a.png', 'notes.txt') { Set-Content "$box/repo/assets/icons/$file" x }
    foreach ($dir in 'repo', 'repo/src', 'repo/dist', 'plain', 'notdist', 'repo/other', 'plain/dist') { Set-Content "$box/$dir/keep.txt" keep }
    Set-Content "$box/repo/dist/old/old.bin" old
    Set-Content "$box/repo/dist/README.md" readme

    foreach ($real in 'victim', 'plainvictim', 'victimparent/inner', 'real/dist', 'srclike/dist', 'slnx/dist', 'csproj/dist', 'rel/dist', 'q1/dist') { New-Item -ItemType Directory -Force "$box/$real/.git" | Out-Null; Set-Content "$box/$real/keep.txt" keep }
    New-Item -ItemType Directory -Force "$box/victimparent/.git", "$box/victim/scripts", "$box/srclike/dist/scripts", "$box/rel/dist/scripts" | Out-Null
    Set-Content "$box/victimparent/keep.txt" keep
    Set-Content "$box/victim/EmberHUD.slnx" x
    Set-Content "$box/victim/scripts/release.ps1" x
    Set-Content "$box/slnx/dist/EmberHUD.slnx" x
    Set-Content "$box/csproj/dist/A.csproj" x
    Set-Content "$box/rel/dist/scripts/release.ps1" x
    New-Item -ItemType Directory -Force "$box/j1", "$box/j2", "$box/j3", "$box/j4" | Out-Null
    foreach ($pair in @(@('j1/dist', 'victim'), @('j2/dist', 'plainvictim'), @('j3/dist', 'victimparent'), @('jp', 'real'))) {
        New-Item -ItemType Junction -Path "$box/$($pair[0])" -Target "$box/$($pair[1])" | Out-Null
        $links.Add("$box/$($pair[0])")
    }
    $symlinks = $true
    try { New-Item -ItemType SymbolicLink -Path "$box/j4/dist" -Target "$box/plainvictim" | Out-Null; $links.Add("$box/j4/dist") }
    catch { $symlinks = $false }

    function Snapshot {
        $items = Get-ChildItem $box -Recurse -Force
        (($items | Where-Object PSIsContainer | ForEach-Object { "dir|$($_.FullName)" }) + ($items | Where-Object { -not $_.PSIsContainer } | ForEach-Object { "file|$($_.FullName)|$((Get-FileHash -LiteralPath $_.FullName).Hash)" }) | Sort-Object) -join "`n"
    }
    function WorkDirs { (Get-ChildItem ([IO.Path]::GetTempPath()) -Directory -Filter 'emberhud-release-*' | ForEach-Object Name) -join ',' }
    function Run([string]$out, [string]$cwd, [string]$stage = 'build', [string]$script = $release, [string[]]$extra = @()) {
        Push-Location $cwd
        $oldPath = $env:PATH
        $env:PATH = "$box\bin;$env:PATH"
        $env:STUB_FAIL = $stage
        $env:STUB_APP = "$box/stubapp-out"
        $env:STUB_SWAP_DIST = "$box/repo/dist"
        $env:STUB_SWAP_TARGET = "$box/plainvictim"
        try { $script:output = pwsh -NoProfile -File $script -Out $out @extra *>&1 | Out-String; $script:code = $LASTEXITCODE }
        finally { $env:PATH = $oldPath; $env:STUB_FAIL = $null; Pop-Location }
    }
    function Check([bool]$condition, [string]$label) {
        if ($condition) { Write-Host "ok   $label" }
        else { Write-Host "FAIL $label - exit $script:code"; Write-Host $script:output; $script:failed++ }
    }
    function Refuses([string]$out, [string]$cwd, [string]$label, [string]$script = $release) {
        $before = Snapshot
        Run $out $cwd 'build' $script
        Check ($code -ne 0 -and $output -match 'refusing to wipe' -and $before -eq (Snapshot)) $label
    }
    function ResetDist {
        if (Test-Path -LiteralPath "$box/repo/dist") {
            if ((Get-Item -LiteralPath "$box/repo/dist" -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { [IO.Directory]::Delete("$box/repo/dist") }
            else { Remove-Item -LiteralPath "$box/repo/dist" -Recurse -Force }
        }
        New-Item -ItemType Directory -Force "$box/repo/dist/.git", "$box/repo/dist/old", "$box/repo/dist/licenses" | Out-Null
        Set-Content "$box/repo/dist/LICENSE.md" license
        Set-Content "$box/repo/dist/LICENSE" license
        Set-Content "$box/repo/dist/LICENSE.txt" license
        Set-Content "$box/repo/dist/LICENSE-old.md" stale
        Set-Content "$box/repo/dist/LICENSE_notes" stale
        Set-Content "$box/repo/dist/.gitignore" ignore
        Set-Content "$box/repo/dist/licenses/stale.txt" stale
        Set-Content "$box/repo/dist/keep.txt" keep
        Set-Content "$box/repo/dist/old/old.bin" old
        Set-Content "$box/repo/dist/README.md" readme
        New-Item -ItemType Directory -Force "$box/repo/dist/Config/Plugins", "$box/repo/dist/Logs", "$box/repo/dist/Cache" | Out-Null
        Set-Content "$box/repo/dist/Config/Plugins/Minimap.json" mine
        Set-Content "$box/repo/dist/Logs/ember.log" log
        Set-Content "$box/repo/dist/Cache/plugin.bin" cache
        Set-Content "$box/repo/dist/imgui.ini" layout
        New-Item -ItemType Directory -Force "$box/repo/dist/GameIcons" | Out-Null; Set-Content "$box/repo/dist/GameIcons/mine.png" mine
    }
    function FailsAt([string]$stage, [string]$message) {
        ResetDist
        $before = Snapshot
        $workBefore = WorkDirs
        Run "$box/repo/dist" $box $stage
        Check ($code -ne 0 -and $output -match $message -and $output -notmatch 'refusing' -and $before -eq (Snapshot) -and $workBefore -eq (WorkDirs)) "failure at $stage ($message) leaves -Out untouched and the work dir removed"
    }

    Refuses "\\?\$box\repo\dist" $box '-Out as a \\?\ device path'
    Refuses "\\localhost\$($box -replace ':', '$')\repo\dist" $box '-Out as a UNC admin share'
    Refuses "$box/repo/src" $box '-Out src'
    Refuses "$box/repo/src/" $box '-Out src with a trailing slash'
    Refuses "$box/repo/src/../src" $box '-Out src through ..'
    Refuses "$box/repo" $box '-Out the repo root, an ancestor of src'
    Refuses '..' "$box/repo/src" '-Out .. from src'
    Refuses '..\..' "$box/repo/src/scripts" '-Out ..\.. from scripts'
    Refuses $box $box '-Out an ancestor of the repo'
    Refuses "$box/plain" $box '-Out a dir with no .git'
    Refuses "$box/notdist" $box '-Out a git repo not named dist'
    Refuses "$box/repo/other" $box '-Out a git repo named other'
    Refuses "$box/repo/dist2" $box '-Out a dir named dist2 with no .git'
    Refuses "$box/plain/dist" $box '-Out a non-empty dir named dist with no .git'
    Refuses "$box/missing/dist" $box '-Out a missing dir named dist'
    Refuses "$box/ancestor/dist" $box '-Out a dist repo that is an ancestor of src' "$box/ancestor/dist/src/scripts/release.ps1"
    Refuses "$box/same/dist" $box '-Out a dist repo that is src' "$box/same/dist/scripts/release.ps1"
    Refuses "$box/j1/dist" $box '-Out a junction to a src-like dir'
    Refuses "$box/j2/dist" $box '-Out a junction to a plain git dir'
    Refuses "$box/j3/dist" $box '-Out a junction to the parent of a repo'
    Refuses "$box/jp/dist" $box '-Out below a junctioned parent segment'
    if ($symlinks) { Refuses "$box/j4/dist" $box '-Out a symlink to a plain git dir' } else { Write-Host 'skip -Out a symlink (not allowed here)' }
    Refuses "$box/slnx/dist" $box '-Out holding a .slnx'
    Refuses "$box/csproj/dist" $box '-Out holding a .csproj'
    Refuses "$box/rel/dist" $box '-Out holding scripts\release.ps1'
    Refuses "$box/q[1]/dist" $box '-Out with brackets is not globbed onto q1'

    ResetDist
    Run "$box/repo/dist" $box
    Check ($output -match 'build failed \(exit 99\)' -and $output -notmatch 'refusing') '-Out a git repo named dist passes the guard'
    ResetDist
    Run '..\dist' "$box/repo/src"
    Check ($output -match 'build failed \(exit 99\)' -and $output -notmatch 'refusing') '-Out ..\dist from src passes the guard'

    FailsAt 'build' 'build failed \(exit 99\)'
    FailsAt 'publish' 'publish failed'
    FailsAt 'check' 'without writing'
    FailsAt 'unlisted' 'allow-list doesn''t ship'
    FailsAt 'obfuscate' 'obfuscar failed \(exit 1\)'
    FailsAt 'obfuscate-samehash' 'obfuscar output for Ember\.dll is byte-identical to its input'

    Set-Content "$box/repo/src/Plugins/Sample/Offsets.g.cs" x
    FailsAt 'none' 'private files staged'
    Remove-Item "$box/repo/src/Plugins/Sample/Offsets.g.cs"

    ResetDist
    $workBefore = WorkDirs
    Run "$box/repo/dist" $box 'none'
    $dist = "$box/repo/dist"
    Check ($code -eq 0 -and $output -match 'release ready' -and $workBefore -eq (WorkDirs) -and
        (Get-Content "$dist/LICENSE.md") -eq 'license' -and (Test-Path "$dist/LICENSE") -and (Test-Path "$dist/LICENSE.txt") -and
        -not (Test-Path "$dist/LICENSE-old.md") -and -not (Test-Path "$dist/LICENSE_notes") -and
        (Test-Path "$dist/README.md") -and (Test-Path "$dist/.gitignore") -and (Test-Path "$dist/.git") -and
        -not (Test-Path "$dist/keep.txt") -and -not (Test-Path "$dist/old") -and -not (Test-Path "$dist/licenses/stale.txt") -and
        (Test-Path "$dist/licenses/THIRD-PARTY.txt") -and (Test-Path "$dist/Fonts/a.ttf") -and (Test-Path "$dist/Fonts/OFL.txt") -and (Test-Path "$dist/Themes/a.json") -and
        (Test-Path "$dist/Plugins/Sample/Main.cs") -and (Test-Path "$dist/Plugins/Sample/defaults.json") -and (Test-Path "$dist/Plugins/Sample/Icons/dot.svg") -and
        -not (Test-Path "$dist/Plugins/Sample/Icons/deep") -and -not (Test-Path "$dist/Plugins/Sample/bin") -and -not (Test-Path "$dist/Plugins/Sample/notes.txt") -and -not (Test-Path "$dist/Plugins/Plugins.csproj") -and
        (Get-Content "$dist/GameIcons/mine.png" -ErrorAction SilentlyContinue) -eq 'mine' -and -not (Test-Path "$dist/GameIcons/a.png") -and -not (Test-Path "$dist/GameIcons/index.json") -and
        (Test-Path "$dist/EmberLauncher.exe") -and (Test-Path "$dist/EmberSetup.exe") -and (Test-Path "$dist/ooz.dll") -and (Test-Path "$dist/BCnEncoder.dll") -and (Test-Path "$dist/licenses/ooz-GPL-3.0.txt") -and
        (Test-Path "$dist/Ember.exe") -and (Test-Path "$dist/libSkiaSharp.dll") -and -not (Test-Path "$dist/libSkiaSharp.pdb") -and
        (Get-Content "$dist/Config/Plugins/Minimap.json" -ErrorAction SilentlyContinue) -eq 'mine' -and (Get-Content "$dist/Logs/ember.log" -ErrorAction SilentlyContinue) -eq 'log' -and -not (Test-Path "$dist/Logs/check.txt") -and
        (Get-Content "$dist/Cache/plugin.bin" -ErrorAction SilentlyContinue) -eq 'cache' -and (Get-Content "$dist/imgui.ini" -ErrorAction SilentlyContinue) -eq 'layout' -and
        -not (Test-Path "$dist/Mapping.txt")) 'a full release keeps .git, README.md, .gitignore, LICENSE* and the user''s Config / Cache / Logs / imgui.ini, wipes the rest, ships plugins with top-level Icons svgs, fonts, themes, licences, the launcher, Setup and ooz.dll, keeps the user''s GameIcons and ships no icon art, and leaves no pdb, Logs or work dir'

    New-Item -ItemType Directory -Force "$box/repo/release/.git", "$box/repo/release/Config", "$box/repo/release/Logs", "$box/repo/release/GameIcons", "$box/repo/release/Plugins/Inspector" | Out-Null
    foreach ($file in '.gitignore', 'LICENSE', 'imgui.ini', 'README.md', 'Config/mine.json', 'Logs/ember.log', 'GameIcons/mine.png', 'Plugins/Inspector/Main.cs') { Set-Content "$box/repo/release/$file" stale }
    Run "$box/repo/release" $box 'none' $release @('-Public')
    $public = "$box/repo/release"
    Check ($code -eq 0 -and $output -match 'release ready' -and
        (Test-Path "$public/.git") -and (Test-Path "$public/.gitignore") -and
        -not (Test-Path "$public/Config") -and -not (Test-Path "$public/Logs") -and -not (Test-Path "$public/GameIcons") -and -not (Test-Path "$public/LICENSE") -and -not (Test-Path "$public/imgui.ini") -and
        (Get-Content "$public/README.md") -eq 'x' -and (Test-Path "$public/Ember.exe") -and (Test-Path "$public/Fonts/a.ttf") -and
        (Test-Path "$public/Plugins/Minimap/Main.cs") -and -not (Test-Path "$public/Plugins/Minimap/Tests") -and
        -not (Test-Path "$public/Plugins/Sample") -and -not (Test-Path "$public/Plugins/Inspector") -and -not (Test-Path "$public/Plugins/Mcp") -and
        (Test-Path "$dist/Plugins/Sample/Tests/T.cs")) 'a -Public release wipes everything but .git and .gitignore, ships the README and no plugin tests, Inspector, Mcp or Sample, while a normal release still ships plugin tests'
    $manifestPath = "$public/manifest.json"
    $manifestText = if (Test-Path $manifestPath) { Get-Content $manifestPath -Raw } else { '{}' }
    $manifest = $manifestText | ConvertFrom-Json
    $bytes = if (Test-Path $manifestPath) { [IO.File]::ReadAllBytes($manifestPath) } else { [byte[]]@() }
    $keys = [string[]]@($manifest.Files.PSObject.Properties.Name | Where-Object { $_ })
    $sorted = [string[]]$keys.Clone()
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    $hashesMatch = $keys.Count -gt 0
    foreach ($key in $keys) { if (-not (Test-Path -LiteralPath "$public/$key") -or (Get-FileHash -LiteralPath "$public/$key" -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.Files.$key) { $hashesMatch = $false } }
    $onDisk = @(Get-ChildItem -LiteralPath $public -Recurse -File -Force | Where-Object { $_.FullName -notmatch '[\/]\.git([\/]|$)' -and $_.Name -notin '.gitignore', 'manifest.json' -and $_.Extension -ne '.zip' }).Count
    Check ((Test-Path $manifestPath) -and $manifest.Version -eq '1.2.3' -and
        -not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF) -and $manifestText -match '(?m)^  "Files": \{' -and
        $keys -contains 'EmberUpdater.exe' -and $keys -contains 'Plugins/Minimap/Main.cs' -and $keys -contains 'Fonts/a.ttf' -and
        -not ($keys | Where-Object { $_.Contains('\') -or $_ -match '^(\.git|Config|Logs|Cache|GameIcons|Updates)(/|$)' -or $_ -in 'manifest.json', '.gitignore', 'imgui.ini' -or $_ -like '*.zip' }) -and
        $keys.Count -eq $onDisk -and $hashesMatch -and ($keys -join '|') -ceq ($sorted -join '|')) 'a -Public release writes manifest.json with the version, / keys sorted ordinal, EmberUpdater.exe, sha256 of every shipped file and none of .git, Config, manifest.json or zips'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipFile = @(Get-ChildItem -LiteralPath $public -Filter 'EmberHUD-*-v1.2.3.zip')
    $zipNames = if ($zipFile.Count -eq 1) { $a = [IO.Compression.ZipFile]::OpenRead($zipFile[0].FullName); try { @($a.Entries.FullName) } finally { $a.Dispose() } } else { @() }
    Check ($zipFile.Count -eq 1 -and $zipNames.Count -eq $keys.Count + 1 -and $zipNames -contains 'manifest.json' -and -not ($keys | Where-Object { $_ -notin $zipNames })) 'a -Public release zips EmberHUD-date-vVersion.zip with every manifest file plus manifest.json'
    Check (-not (Test-Path "$dist/manifest.json")) 'a normal release writes no manifest.json'
    Run "$box/repo/dist" $box 'none' $release @('-Public')
    Check ($code -ne 0 -and $output -match 'refusing to wipe') '-Public refuses a folder not named release'

    ResetDist
    Copy-Item "$env:SystemRoot\System32\PING.EXE" "$box/repo/dist/Ember.exe"
    $running = Start-Process "$box/repo/dist/Ember.exe" -ArgumentList '-n', '60', '127.0.0.1' -WindowStyle Hidden -PassThru
    try { Refuses "$box/repo/dist" $box '-Out with a program running from it' }
    finally { Stop-Process -Id $running.Id -Force -ErrorAction SilentlyContinue; $running.WaitForExit() }

    ResetDist
    Remove-Item "$box/repo/dist/GameIcons" -Recurse -Force
    Run "$box/repo/dist" $box 'none'
    Check ($code -eq 0 -and $output -match 'release ready' -and -not (Test-Path "$box/repo/dist/GameIcons") -and $output -notmatch 'game icon art') 'a dist without GameIcons releases without creating one - the icons come from Setup'

    ResetDist
    $victimBefore = (Get-ChildItem "$box/plainvictim" -Recurse -Force | ForEach-Object { "$($_.FullName)|$($_.Length)" }) -join ','
    Run "$box/repo/dist" $box 'swap'
    $victimAfter = (Get-ChildItem "$box/plainvictim" -Recurse -Force | ForEach-Object { "$($_.FullName)|$($_.Length)" }) -join ','
    Check ($code -ne 0 -and $output -match 'junction or symlink' -and $victimBefore -eq $victimAfter) 'a dist swapped for a junction after the publish is refused before the wipe'
    ResetDist

    Remove-Item "$box/repo/src/Fonts/OFL.txt"
    ResetDist
    $before = Snapshot
    Run "$box/repo/dist" $box
    Check ($code -ne 0 -and $output -match 'nothing matching OFL\*\.txt' -and $before -eq (Snapshot)) 'one missing asset pattern fails before the wipe'
}
finally {
    foreach ($link in $links) { if (Test-Path -LiteralPath $link) { [IO.Directory]::Delete($link) } }
    Remove-Item -LiteralPath $box -Recurse -Force -ErrorAction SilentlyContinue
}
if ($failed) { Write-Host "$failed release test(s) failed"; exit 1 }
exit 0
