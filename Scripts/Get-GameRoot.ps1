<#
.NOTES
    Author: SaintCirno9
#>
function Get-LongYinSetting
{
    param([string]$Name, [string]$Value)
    if (-not [string]::IsNullOrWhiteSpace($Value)) { return $Value }
    $envPath = Join-Path (Split-Path -Parent $PSScriptRoot) '.env'
    if (-not (Test-Path -LiteralPath $envPath -PathType Leaf))
    {
        throw "找不到 .env。请复制 .env.example 并填写 $Name。"
    }
    $settings = @{}
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $envPath -Encoding utf8)
    {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
        if ($line -notmatch '^\s*([A-Z_][A-Z0-9_]*)\s*=\s*(.*?)\s*$')
        {
            throw ".env 第 $lineNumber 行格式错误，应使用 KEY=VALUE。"
        }
        $key = $Matches[1]
        $settingValue = $Matches[2]
        if ($settings.ContainsKey($key)) { throw ".env 第 $lineNumber 行重复配置：$key。" }
        if ($settingValue.StartsWith('"') -or $settingValue.StartsWith("'"))
        {
            if ($settingValue.Length -lt 2 -or $settingValue[-1] -ne $settingValue[0])
            {
                throw ".env 第 $lineNumber 行引号不匹配：$key。"
            }
            $settingValue = $settingValue.Substring(1, $settingValue.Length - 2)
        }
        $settings[$key] = $settingValue
    }
    if ([string]::IsNullOrWhiteSpace($settings[$Name]))
    {
        throw ".env 中未配置 $Name。"
    }
    return $settings[$Name]
}

function Get-LongYinGameRoot
{
    param([string]$GameRootDir)
    $value = Get-LongYinSetting -Name 'LONGYIN_GAME_ROOT' -Value $GameRootDir
    return [IO.Path]::GetFullPath($value)
}
