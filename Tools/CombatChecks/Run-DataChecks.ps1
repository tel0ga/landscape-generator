param([string]$UnityData = 'D:/Unity/6000.4.8f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$out = Join-Path $project 'Logs/CombatChecks'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$runtime = Get-ChildItem (Join-Path $UnityData 'NetCoreRuntime/shared/Microsoft.NETCore.App') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$arguments = @('-nostdlib+', '-target:exe', ('-out:"' + (Join-Path $out 'DataChecks.dll') + '"'))
Get-ChildItem $runtime.FullName -Filter '*.dll' | Where-Object { try { $null = [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName); $true } catch { $false } } | ForEach-Object { $arguments += '-r:"' + $_.FullName + '"' }
foreach ($source in @('CombatTypes','ItemDefinition','WeaponDefinition','CombatCatalog','CombatDefaults','Inventory')) { $arguments += '"' + (Join-Path $project ('Assets/Scripts/Combat/' + $source + '.cs')) + '"' }
$arguments += '"' + (Join-Path $PSScriptRoot 'UnityDataStubs.cs') + '"'
$arguments += '"' + (Join-Path $PSScriptRoot 'DataChecks.cs') + '"'
$arguments | Set-Content (Join-Path $out 'data-checks.rsp')
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + (Join-Path $out 'data-checks.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Data checks compilation failed' }
@{ runtimeOptions = @{ tfm = 'net8.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name } } } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $out 'DataChecks.runtimeconfig.json')
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $out 'DataChecks.dll') (Join-Path $out 'catalog-data.json')
if ($LASTEXITCODE -ne 0) { throw 'Data checks failed' }

