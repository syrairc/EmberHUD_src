$ErrorActionPreference = 'Stop'
$src = Split-Path $PSScriptRoot
dotnet run --project "$src/tools/OffsetGen/OffsetGen.csproj" -- "$src/offsets/layout.want" "$src/EmberHUD/Offsets"
exit $LASTEXITCODE
