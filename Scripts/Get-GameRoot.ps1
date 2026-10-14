<#
.NOTES
    Author: SaintCirno9
#>
function Get-LongYinGameRoot
{
    param([string]$GameRootDir)
    if ([string]::IsNullOrWhiteSpace($GameRootDir))
    {
        $repositoryRoot = Split-Path -Parent $PSScriptRoot
        foreach ($name in @('GamePaths.props', 'GamePaths.local.props'))
        {
            $path = Join-Path $repositoryRoot $name
            if (Test-Path -LiteralPath $path)
            {
                [xml]$settings = Get-Content -LiteralPath $path -Raw
                $value = $settings.Project.PropertyGroup.GameRootDir | Select-Object -Last 1
                if (-not [string]::IsNullOrWhiteSpace([string]$value))
                {
                    $GameRootDir = [string]$value
                }
            }
        }
    }
    if ([string]::IsNullOrWhiteSpace($GameRootDir)) { throw '未配置游戏路径。' }
    return [IO.Path]::GetFullPath($GameRootDir)
}
