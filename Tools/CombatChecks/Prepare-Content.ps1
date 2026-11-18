$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
function GuidOf([string]$file) { ((Get-Content -LiteralPath ($file + '.meta') | Where-Object { $_ -like 'guid:*' }) -split ': ')[1] }
function EnsureMeta([string]$file, [bool]$folder = $false) {
 if (-not (Test-Path -LiteralPath ($file + '.meta'))) {
  $lines = @('fileFormatVersion: 2', ('guid: ' + [guid]::NewGuid().ToString('N')))
  if ($folder) { $lines += @('folderAsset: yes', 'DefaultImporter:', '  externalObjects: {}', '  userData:', '  assetBundleName:', '  assetBundleVariant:') }
  $lines | Set-Content -LiteralPath ($file + '.meta')
 }
}
function Scalar($value, [string]$key) {
 if ($null -eq $value) { if ($key -in @('icon','worldPrefab','heldPrefab','attackSound','reloadSound','impactSound','muzzleEffect','impactEffect','prefab')) { return '{fileID: 0}' }; return '""' }
 if ($value -is [bool]) { return $(if ($value) { '1' } else { '0' }) }
 if ($value -is [string]) { return (ConvertTo-Json -InputObject $value -Compress) }
 return [System.Convert]::ToString($value, [System.Globalization.CultureInfo]::InvariantCulture)
}
function YamlMap($map, [int]$indent) {
 $pad = ' ' * $indent
 foreach ($key in $map.Keys) {
  if ($key -eq 'name' -or $key -eq 'allowedRolls') { continue }
  $value = $map[$key]
  if ($value -is [System.Collections.IDictionary]) { "$pad${key}:"; YamlMap $value ($indent + 2) }
  elseif ($value -is [array]) {
   if ($value.Count -eq 0) { "$pad${key}: []" }
   else {
    "$pad${key}:"
    foreach ($entry in $value) {
     if ($entry -is [System.Collections.IDictionary]) {
      $nested = @(YamlMap $entry ($indent + 2))
      "$pad- " + $nested[0].TrimStart()
      if ($nested.Count -gt 1) { $nested[1..($nested.Count-1)] }
     } else { "$pad- " + (Scalar $entry '') }
    }
   }
  } else { "$pad${key}: " + (Scalar $value $key) }
 }
}
function MonoHeader([long]$id, [string]$script, [string]$name) {
 @("--- !u!114 &$id", 'MonoBehaviour:', '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0', "  m_Script: {fileID: 11500000, guid: $script, type: 3}", "  m_Name: $name", '  m_EditorClassIdentifier:')
}
$resourceDir = Join-Path $root 'Assets/Resources/Combat'
$catalogPath = Join-Path $resourceDir 'DefaultCatalog.asset'
if (-not (Test-Path $catalogPath)) {
 $items = Get-Content (Join-Path $root 'Logs/CombatChecks/catalog-data.json') -Raw | ConvertFrom-Json -AsHashtable
 $lines = @('%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:')
 $lines += MonoHeader 11400000 (GuidOf (Join-Path $root 'Assets/Scripts/Combat/CombatCatalog.cs')) 'DefaultCatalog'
 $lines += '  items:'
 for ($i=0; $i -lt $items.Count; $i++) { $lines += '  - {fileID: ' + (11400001 + $i) + '}' }
 for ($i=0; $i -lt $items.Count; $i++) {
  $type = if ($items[$i].Contains('attackKind')) { 'WeaponDefinition' } else { 'ItemDefinition' }
  $lines += MonoHeader (11400001 + $i) (GuidOf (Join-Path $root ('Assets/Scripts/Combat/' + $type + '.cs'))) $items[$i].id
  $lines += @(YamlMap $items[$i] 2)
 }
 $lines | Set-Content $catalogPath
 EnsureMeta $catalogPath
}
$inputPath = Join-Path $resourceDir 'CombatActions.inputactions'
if (-not (Test-Path $inputPath)) {
 $actions = @(); $bindings = @()
 $keys = [ordered]@{ AimPosition=@('PassThrough','Vector2','<Pointer>/position'); AimStick=@('Value','Vector2','<Gamepad>/rightStick'); Attack=@('Button','Button','<Mouse>/leftButton','<Gamepad>/rightTrigger'); Reload=@('Button','Button','<Keyboard>/r','<Gamepad>/buttonWest'); Slot1=@('Button','Button','<Keyboard>/1'); Slot2=@('Button','Button','<Keyboard>/2'); Next=@('Button','Button','<Gamepad>/rightShoulder'); Previous=@('Button','Button','<Gamepad>/leftShoulder'); Throw=@('Button','Button','<Keyboard>/g','<Gamepad>/leftTrigger'); Interact=@('Button','Button','<Keyboard>/e','<Gamepad>/buttonSouth'); Inventory=@('Button','Button','<Keyboard>/tab','<Gamepad>/start'); Save=@('Button','Button','<Keyboard>/f5'); Load=@('Button','Button','<Keyboard>/f9') }
 foreach ($name in $keys.Keys) {
  $data = $keys[$name]; $actions += @{ name=$name; type=$data[0]; id=[guid]::NewGuid().ToString(); expectedControlType=$data[1]; processors=''; interactions=''; initialStateCheck=($data[0] -ne 'Button') }
  foreach ($binding in $data[2..($data.Count-1)]) { $bindings += @{ name=''; id=[guid]::NewGuid().ToString(); path=$binding; interactions=''; processors=''; groups=''; action=$name; isComposite=$false; isPartOfComposite=$false } }
 }
 @{ name='CombatActions'; maps=@(@{ name='Combat'; id=[guid]::NewGuid().ToString(); actions=$actions; bindings=$bindings }); controlSchemes=@() } | ConvertTo-Json -Depth 8 | Set-Content $inputPath
 EnsureMeta $inputPath
}
$scenePath = Join-Path $root 'Assets/Scenes/CombatArena.unity'
if (-not (Test-Path $scenePath)) {
 $source = [System.IO.File]::ReadAllText((Join-Path $root 'Assets/Scenes/MAIN SCENE.unity'))
 $prefix = $source.Substring(0, $source.IndexOf('--- !u!1 '))
 $scene = $prefix + "--- !u!1660057539 &9223372036854775807`nSceneRoots:`n  m_ObjectHideFlags: 0`n  m_Roots: []`n"
 [System.IO.File]::WriteAllText($scenePath, $scene); EnsureMeta $scenePath
}
$buildPath = Join-Path $root 'ProjectSettings/EditorBuildSettings.asset'
$build = [System.IO.File]::ReadAllText($buildPath)
if (-not $build.Contains('Assets/Scenes/CombatArena.unity')) {
 $entry = "  - enabled: 1`n    path: Assets/Scenes/CombatArena.unity`n    guid: " + (GuidOf $scenePath) + "`n"
 [System.IO.File]::WriteAllText($buildPath, $build.Replace('  m_configObjects:', $entry + '  m_configObjects:'))
}
foreach ($dir in @('Assets/Scripts/Combat','Assets/Editor/Combat','Assets/Resources','Assets/Resources/Combat')) { EnsureMeta (Join-Path $root $dir) $true }
Write-Output 'Combat catalog, input asset and test scene prepared.'
