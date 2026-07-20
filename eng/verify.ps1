param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
$projectVersion = Join-Path $repository 'ProjectSettings/ProjectVersion.txt'
$manifestPath = Join-Path $repository 'Packages/manifest.json'

if ((Get-Content -LiteralPath $projectVersion -Raw) -notmatch
    '(?m)^m_EditorVersion: 6000\.3\.13f1$') {
    throw 'The Unity project is not pinned to 6000.3.13f1.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($null -eq $manifest.dependencies.'com.unity.modules.assetbundle') {
    throw 'The Unity AssetBundle module is missing.'
}
foreach ($relative in @(
    'Assets/OFS.ModAuthoring/Editor/OFSBundleBuilder.cs',
    'Assets/OFS.ModAuthoring/Editor/OFSBundleFixtureGenerator.cs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $repository $relative) -PathType Leaf)) {
        throw "Required authoring source is missing: $relative"
    }
}
Write-Host 'OFS Asset Authoring structure verification passed.'
