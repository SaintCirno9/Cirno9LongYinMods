<#
.SYNOPSIS
    构建并部署指定龙胤模组，默认仅在退出游戏后部署。
.EXAMPLE
    pwsh -NoProfile -File Scripts/Deploy-Mod.ps1 ForcePermissionTweaks
.NOTES
    Author: SaintCirno9
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, Position = 0)]
    [string[]]$ModName,
    [string]$Configuration = 'Release',
    [string]$GameRootDir,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Get-GameRoot.ps1')
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$gameRoot = Get-LongYinGameRoot $GameRootDir
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'LongYinLiZhiZhuan.exe') -PathType Leaf))
{
    throw "找不到游戏可执行文件：$gameRoot"
}
$targetMods = Join-Path $gameRoot 'Mods'
foreach ($name in $ModName)
{
    if ($name -notmatch '^[A-Za-z0-9_]+$') { throw "非法模组名称：$name" }
    $project = Join-Path $repositoryRoot "Mods\$name\$name.csproj"
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "找不到模组工程：$project" }
    if (-not $NoBuild)
    {
        & dotnet build $project -c $Configuration '-p:DeployMods=false' "-p:GameRootDir=$gameRoot"
        if ($LASTEXITCODE -ne 0) { throw "模组构建失败：$name（退出码 $LASTEXITCODE）" }
    }
    $sourceDll = Join-Path $repositoryRoot "Artifacts\bin\$name\$Configuration\$name.dll"
    if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) { throw "找不到构建产物：$sourceDll" }
    $running = @(Get-Process -Name 'LongYinLiZhiZhuan', '啷个哩个啷' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { throw "游戏仍在运行（PID：$($running.Id -join ', ')），请退出后部署。" }
    $destination = Join-Path $targetMods "$name.dll"
    if ($PSCmdlet.ShouldProcess($destination, '部署模组 DLL'))
    {
        New-Item -ItemType Directory -Path $targetMods -Force | Out-Null
        Copy-Item -LiteralPath $sourceDll -Destination $destination -Force
        if ((Get-FileHash -LiteralPath $sourceDll).Hash -ne (Get-FileHash -LiteralPath $destination).Hash)
        {
            throw "部署 DLL 哈希不一致：$destination"
        }
        Write-Host "DLL 已部署并通过 SHA256 校验：$destination"
    }
    $userDataRoot = Join-Path $repositoryRoot "Mods\$name\PackageRoot\UserData"
    if (Test-Path -LiteralPath $userDataRoot -PathType Container)
    {
        foreach ($file in Get-ChildItem -LiteralPath $userDataRoot -Recurse -File)
        {
            $relativePath = [IO.Path]::GetRelativePath($userDataRoot, $file.FullName)
            $targetFile = Join-Path $gameRoot "UserData\$relativePath"
            if (Test-Path -LiteralPath $targetFile)
            {
                Write-Host "保留现有用户资源：$targetFile"
                continue
            }
            if ($PSCmdlet.ShouldProcess($targetFile, '补充缺失的模组资源'))
            {
                New-Item -ItemType Directory -Path (Split-Path -Parent $targetFile) -Force | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $targetFile
                if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $targetFile).Hash)
                {
                    throw "资源部署哈希不一致：$targetFile"
                }
                Write-Host "资源已部署：$targetFile"
            }
        }
    }
}
