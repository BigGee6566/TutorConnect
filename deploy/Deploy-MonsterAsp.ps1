<#
.SYNOPSIS
    Publishes TutorConnect and uploads it to a MonsterASP.NET site with Web Deploy.

.DESCRIPTION
    1. Builds a Release publish of TutorConnect.vbproj into deploy\out.
    2. Copies App_Data\local.settings.config (git-ignored; holds the Supabase
       connection string) into the publish output, because the site cannot reach the
       database without it.
    3. Syncs deploy\out to the site with msdeploy. Your Web Deploy password is asked
       for here, in your own terminal, and is never written to disk.

    Find the Web Deploy details in the MonsterASP.NET control panel:
    Websites -> your site -> Deploy -> WebDeploy (Server, Site name, Login).

.EXAMPLE
    .\deploy\Deploy-MonsterAsp.ps1 -Server site12345.siteasp.net -SiteName site12345 -UserName site12345
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Server,
    [Parameter(Mandatory)] [string] $SiteName,
    [Parameter(Mandatory)] [string] $UserName,
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$root     = Split-Path -Parent $PSScriptRoot
$out      = Join-Path $PSScriptRoot 'out'
$msbuild  = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$msdeploy = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
$settings = Join-Path $root 'App_Data\local.settings.config'

if (-not (Test-Path $settings)) {
    throw "App_Data\local.settings.config is missing. Copy local.settings.config.sample and put the Supabase connection string in Db:ConnectionString."
}
if (-not (Select-String -Path $settings -Pattern 'key="Db:ConnectionString"' -Quiet)) {
    throw "App_Data\local.settings.config has no Db:ConnectionString entry."
}
if (Select-String -Path $settings -Pattern 'YOUR-DATABASE-PASSWORD' -Quiet) {
    throw "Replace YOUR-DATABASE-PASSWORD in App_Data\local.settings.config with your Supabase database password first."
}

if (-not $SkipBuild) {
    Write-Host "Publishing to $out ..." -ForegroundColor Cyan
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    & $msbuild (Join-Path $root 'TutorConnect.vbproj') /nologo /v:m /t:WebPublish `
        /p:Configuration=Release /p:VisualStudioVersion=17.0 /p:WebPublishMethod=FileSystem `
        /p:PublishUrl="$out" /p:DeleteExistingFiles=true
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

New-Item -ItemType Directory -Force (Join-Path $out 'App_Data') | Out-Null
Copy-Item $settings (Join-Path $out 'App_Data\local.settings.config') -Force
# The SQL Server scripts and the seed sources are not needed on the server.
Remove-Item -Recurse -Force (Join-Path $out 'App_Data\Scripts') -ErrorAction SilentlyContinue

$secure = Read-Host "Web Deploy password for $UserName" -AsSecureString
$plain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
              [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
try {
    $dest = "contentPath=$SiteName,computerName=https://${Server}:8172/msdeploy.axd?site=$SiteName," +
            "userName=$UserName,password=$plain,authType=Basic"
    Write-Host "Uploading to $Server ..." -ForegroundColor Cyan
    & $msdeploy -verb:sync "-source:contentPath=$out" "-dest:$dest" `
        -allowUntrusted -enableRule:AppOffline -enableRule:DoNotDeleteRule `
        "-skip:objectName=dirPath,absolutePath=App_Data\\Logs$"
    if ($LASTEXITCODE -ne 0) { throw "Web Deploy failed." }
}
finally {
    $plain = $null
}

Write-Host "Done. Open your site's https:// address to sign in." -ForegroundColor Green
