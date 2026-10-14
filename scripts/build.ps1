$ErrorActionPreference = 'Stop'
$src = Split-Path $PSScriptRoot
$out = Join-Path ([IO.Path]::GetTempPath()) "emberhud-build-$([guid]::NewGuid().ToString('N'))"

try {
    $projects = @("$src/EmberHUD/EmberHUD.csproj", "$src/EmberHUD.Launcher/EmberHUD.Launcher.csproj", "$src/EmberHUD.Setup/EmberHUD.Setup.csproj", "$src/Plugins/Plugins.csproj")
    foreach ($project in $projects) {
        dotnet build $project -o (Join-Path $out ([IO.Path]::GetFileNameWithoutExtension($project)))
        if ($LASTEXITCODE) { exit $LASTEXITCODE }
    }

    foreach ($test in Get-ChildItem "$src/tests" -Filter *.csproj -Recurse) {
        $dir = Join-Path $out $test.BaseName
        dotnet build $test.FullName -o $dir
        if ($LASTEXITCODE) { exit $LASTEXITCODE }
        & (Join-Path $dir "$($test.BaseName).exe")
        if ($LASTEXITCODE) { exit $LASTEXITCODE }
    }
}
finally {
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
}
