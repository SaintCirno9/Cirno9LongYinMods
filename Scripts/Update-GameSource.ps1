<#
.NOTES
    Author: SaintCirno9
#>
param(
    [string]$GameRootDir,
    [string]$DumperPath
)
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "Get-GameRoot.ps1")
$gameRoot = Get-LongYinGameRoot $GameRootDir
$DumperPath = Get-LongYinSetting -Name 'IL2CPP_DUMPER_PATH' -Value $DumperPath
$gameAssemblyPath = Join-Path $gameRoot "GameAssembly.dll"
$metadataPath = Join-Path $gameRoot "LongYinLiZhiZhuan_Data\il2cpp_data\Metadata\global-metadata.dat"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repositoryRoot "GameSource"

$requiredInputs = @($dumperPath, $gameAssemblyPath, $metadataPath)
foreach ($path in $requiredInputs) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "找不到必需文件：$path"
    }
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$backupPath = Join-Path $repositoryRoot (".cache\GameSource-backups\" + (Get-Date -Format "yyyyMMdd-HHmmss-fffffff"))
New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
foreach ($name in @("dump.cs", "il2cpp.h", "script.json", "stringliteral.json", "DummyDll"))
{
    $path = Join-Path $outputPath $name
    if (Test-Path -LiteralPath $path)
    {
        $resolved = (Resolve-Path -LiteralPath $path).Path
        if (-not $resolved.StartsWith($outputPath + "\", [StringComparison]::OrdinalIgnoreCase)) { throw "非法导出路径：$resolved" }
        Move-Item -LiteralPath $resolved -Destination (Join-Path $backupPath $name)
    }
}
Write-Host "旧导出已备份：$backupPath"

Write-Host "正在刷新 GameSource..."
& $dumperPath $gameAssemblyPath $metadataPath $outputPath
$dumperExitCode = $LASTEXITCODE

$requiredOutputs = @(
    (Join-Path $outputPath "dump.cs"),
    (Join-Path $outputPath "il2cpp.h"),
    (Join-Path $outputPath "script.json"),
    (Join-Path $outputPath "stringliteral.json")
)
$missingOutputs = @($requiredOutputs | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
$dummyDllPath = Join-Path $outputPath "DummyDll"
$dummyDll = Get-ChildItem -LiteralPath $dummyDllPath -Filter "*.dll" -File -ErrorAction SilentlyContinue | Select-Object -First 1

if ($missingOutputs.Count -gt 0 -or $null -eq $dummyDll) {
    throw "Il2CppDumper 未生成完整产物，退出码：$dumperExitCode"
}

if ($dumperExitCode -ne 0) {
    Write-Warning "Il2CppDumper 返回退出码 $dumperExitCode，但核心产物已完整生成。"
}

Write-Host "GameSource 刷新完成：$outputPath"
