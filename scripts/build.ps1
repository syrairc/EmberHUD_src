$ErrorActionPreference = 'Stop'
$src = Split-Path $PSScriptRoot
$out = Join-Path ([IO.Path]::GetTempPath()) "aember-build-$([guid]::NewGuid().ToString('N'))"

$projects = @("$src/Aember/Aember.csproj", "$src/Aember.Launcher/Aember.Launcher.csproj", "$src/Aember.Setup/Aember.Setup.csproj")
if (Test-Path "$src/Plugins/Plugins.csproj") { $projects += "$src/Plugins/Plugins.csproj" }
foreach ($project in $projects) {
    dotnet build $project -o (Join-Path $out ([IO.Path]::GetFileNameWithoutExtension($project)))
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

foreach ($test in Get-ChildItem "$src/tests" -Filter *.csproj -Recurse -ErrorAction SilentlyContinue) {
    $dir = Join-Path $out $test.BaseName
    dotnet build $test.FullName -o $dir
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    & (Join-Path $dir "$($test.BaseName).exe")
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}
