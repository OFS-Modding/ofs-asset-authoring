param(
    [string]$UnityPath,
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\artifacts\assetbundles'),
    [switch]$GenerateFixture
)

$ErrorActionPreference = 'Stop'
$requiredVersion = '6000.3.13f1'
$project = [IO.Path]::GetFullPath($ProjectPath)
$output = [IO.Path]::GetFullPath($OutputPath)

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$requiredVersion\Editor\Unity.exe"
}
$unity = [IO.Path]::GetFullPath($UnityPath)
if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) {
    throw "Unity $requiredVersion was not found at '$unity'. Install it through Unity Hub or pass -UnityPath."
}
if (-not (Test-Path -LiteralPath $project -PathType Container)) {
    throw "Asset authoring project was not found at '$project'."
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $env:LOCALAPPDATA 'Unity\Caches') -Force | Out-Null
$executeMethod = if ($GenerateFixture) {
    'OFS.ModAuthoring.Editor.OFSBundleFixtureGenerator.GenerateAndBuildFromCommandLine'
}
else {
    'OFS.ModAuthoring.Editor.OFSBundleBuilder.BuildFromCommandLine'
}
Push-Location $project
try {
    $unityProcess = Start-Process `
        -FilePath $unity `
        -ArgumentList @(
            '-batchmode',
            '-nographics',
            '-quit',
            '-accept-apiupdate',
            '-buildTarget', 'StandaloneWindows64',
            '-projectPath', '.',
            '-executeMethod', $executeMethod,
            '-ofsOutput', ('"' + $output + '"'),
            '-logFile', '-') `
        -NoNewWindow `
        -PassThru `
        -Wait
    $unityExitCode = $unityProcess.ExitCode
}
finally {
    Pop-Location
}
if ($unityExitCode -eq 198) {
    throw "Unity $requiredVersion has no active license. Sign in and activate Unity Personal in Unity Hub, then retry."
}
if ($unityExitCode -ne 0) {
    throw "Unity AssetBundle build failed with exit code $unityExitCode."
}

$index = Join-Path $output 'ofs-bundles.json'
if (-not (Test-Path -LiteralPath $index -PathType Leaf)) {
    throw "Unity completed without writing '$index'."
}
Get-Content -LiteralPath $index
