<#
.SYNOPSIS
    Downloads App_Data\Logs from the MonsterASP.NET site into deploy\server-logs.
.EXAMPLE
    .\deploy\Get-ServerLogs.ps1 -Server site109082.siteasp.net -SiteName site109082 -UserName site109082
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Server,
    [Parameter(Mandatory)] [string] $SiteName,
    [Parameter(Mandatory)] [string] $UserName
)

$ErrorActionPreference = 'Stop'
$msdeploy = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
# msdeploy rejects quoted paths, so download to a path without spaces first.
$stage = 'C:\tutorconnect-logs'
$final = Join-Path $PSScriptRoot 'server-logs'

$secure = Read-Host "Web Deploy password for $UserName" -AsSecureString
$plain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
              [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
if ([string]::IsNullOrWhiteSpace($plain) -or $plain -match '[\x00-\x1F]') {
    throw "The password was not entered correctly. Right-click to paste, or type it."
}

try {
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    $src = "contentPath=$SiteName/App_Data/Logs,computerName=https://${Server}:8172/msdeploy.axd?site=$SiteName," +
           "userName=$UserName,password=$plain,authType=Basic"
    & $msdeploy -verb:sync "-source:$src" "-dest:contentPath=$stage" -allowUntrusted
    if ($LASTEXITCODE -ne 0) { throw "Download failed." }
    New-Item -ItemType Directory -Force $final | Out-Null
    Copy-Item (Join-Path $stage '*') $final -Recurse -Force
}
finally {
    $plain = $null
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
}

Write-Host "Logs saved to $final" -ForegroundColor Green
