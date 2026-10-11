$ErrorActionPreference = 'Stop'
$src = Split-Path $PSScriptRoot
$temp = [IO.Path]::GetTempPath()
$previous = Get-ChildItem $temp -Directory -Filter 'tli-live*' | Where-Object { Test-Path (Join-Path $_.FullName 'Config') } | Sort-Object { (Get-ChildItem (Join-Path $_.FullName 'Config') -Recurse -File | Measure-Object LastWriteTime -Maximum).Maximum } -Descending | Select-Object -First 1
$next = 1 + ((Get-ChildItem $temp -Directory -Filter 'tli-live*' | ForEach-Object { [int]($_.Name -replace '\D', '0') } | Measure-Object -Maximum).Maximum)
$out = Join-Path $temp "tli-live$next"

dotnet build "$src/TorchlightSDK/TorchlightSDK.csproj" -c Debug -o $out --nologo -v q
if ($LASTEXITCODE) { exit $LASTEXITCODE }

if ($previous) {
    foreach ($folder in 'Config', 'Themes', 'Fonts', 'Icons') {
        $from = Join-Path $previous.FullName $folder
        if (-not (Test-Path $from)) { continue }
        Get-ChildItem $from -Recurse -File | ForEach-Object {
            $to = Join-Path $out (Join-Path $folder $_.FullName.Substring($from.Length + 1))
            if (-not (Test-Path $to)) { New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null; Copy-Item $_.FullName $to }
        }
    }
    Write-Host "settings copied from $($previous.Name)"
}

$icons = Join-Path (Split-Path $src) 'assets\icons'
$sdkPath = Join-Path $out 'Config\sdk.json'
if (Test-Path $icons) {
    $sdk = if (Test-Path $sdkPath) { Get-Content $sdkPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    if ($null -eq $sdk) { $sdk = [pscustomobject]@{} }
    if (-not $sdk.GameIconsDirectory) {
        $sdk | Add-Member -Force NoteProperty GameIconsDirectory (Resolve-Path $icons).Path
        New-Item -ItemType Directory -Force (Split-Path $sdkPath) | Out-Null
        $sdk | ConvertTo-Json -Depth 10 | Set-Content $sdkPath -Encoding utf8
        Write-Host "game icons: $((Resolve-Path $icons).Path)"
    }
}

& (Join-Path $out 'TorchlightSDK.exe') --check --plugins "$src/Plugins" | Select-String -NotMatch '^PASS'
Write-Host "run: & '$out\TorchlightSDK.exe' --live --plugins '$src\Plugins'"
