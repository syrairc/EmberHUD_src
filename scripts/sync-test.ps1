param([string]$Dist = 'E:\EmberHUD\dist', [string]$Test = 'E:\EmberHUD\test')

$running = Get-Process Ember -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$Test\*" }
if ($running) { throw "Ember.exe is running from $Test - close it first, a partial copy leaves old host dlls next to new plugins" }

$keep = 'Config', 'Cache', 'Logs', 'GameIcons'
robocopy "$Dist\Plugins" "$Test\Plugins" /MIR /R:0 /W:0 /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "plugin sync failed ($LASTEXITCODE)" }
robocopy $Dist $Test /E /R:0 /W:0 /NFL /NDL /NJH /NJS /XD @keep Plugins /XF imgui.ini | Out-Null
if ($LASTEXITCODE -ge 8) { throw "host sync failed ($LASTEXITCODE)" }
Write-Host "test synced from $Dist (Config, Cache, Logs, GameIcons, imgui.ini untouched)"
