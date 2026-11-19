<#
.SYNOPSIS
    AT-30 - a fresh install on an empty SQL Server instance yields a working application.

.DESCRIPTION
    DESTRUCTIVE. This drops the TutorConnect database and rebuilds it from the scripts, so
    that 01_CreateSchema.sql exercises its create-from-nothing branch. Every other run in
    this project reported "Database TutorConnect already exists - reusing it", which means
    the path a real installer takes had never actually been tested.

    It is a separate script from Verify-Web.ps1 and Verify-Acceptance.ps1 precisely because
    it destroys data, and it refuses to start without -Confirm.

    The site must be running and pointed at the same database.

.PARAMETER Confirm
    Required. Without it the script explains what it would do and exits.

.EXAMPLE
    .\Verify-FreshInstall.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!' -Confirm
#>
param(
    [Parameter(Mandatory = $true)] [string] $BaseUrl,
    [Parameter(Mandatory = $true)] [string] $Password,
    [string] $SqlInstance = '.\SQLEXPRESS',
    [string] $Database = 'TutorConnect',
    [switch] $Confirm
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')

if (-not $Confirm) {
    Write-Host ""
    Write-Host "This script DROPS the database '$Database' on '$SqlInstance' and rebuilds it." -ForegroundColor Yellow
    Write-Host "Every row in it is lost. Re-run with -Confirm if that is what you want."
    Write-Host ""
    exit 2
}

$script:pass = 0
$script:fail = 0
function Check($name, $condition, $detail = '') {
    if ($condition) { $script:pass++; "  PASS  $name" }
    else { $script:fail++; "  FAIL  $name   $detail" }
}

# The repository root, found by walking up from this script rather than assumed, so the
# script keeps working if it is run from somewhere else.
$root = Split-Path -Parent $PSScriptRoot
while ($root -and -not (Test-Path (Join-Path $root 'App_Data\Scripts\01_CreateSchema.sql'))) {
    $root = Split-Path -Parent $root
}
if (-not $root) { throw "Could not locate App_Data\Scripts above $PSScriptRoot" }

$scripts = Join-Path $root 'App_Data\Scripts'

function Sql($query, $db = 'master') {
    $out = & sqlcmd -S $SqlInstance -E -b -d $db -h -1 -W -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $out" }
    return ($out | Where-Object { $_ -and $_ -notmatch '^\(\d+ rows affected\)$' }) -join "`n"
}

''
'================ AT-30  Fresh install ================'
''
'--- 1. remove the database entirely ---'

# SINGLE_USER evicts anything still connected - IIS Express, a query window - which would
# otherwise block the drop and hang the run.
Sql "IF DB_ID('$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" | Out-Null

$exists = Sql "SELECT CASE WHEN DB_ID('$Database') IS NULL THEN 'absent' ELSE 'present' END"
Check 'the database is gone' ($exists.Trim() -eq 'absent') $exists

''
'--- 2. run the creation and lookup scripts ---'
$create = & sqlcmd -S $SqlInstance -E -b -i (Join-Path $scripts '01_CreateSchema.sql') 2>&1
Check '01_CreateSchema.sql runs clean' ($LASTEXITCODE -eq 0) ($create -join ' ')
Check 'it created the database rather than reusing one' (($create -join ' ') -notmatch 'already exists')

$lookups = & sqlcmd -S $SqlInstance -E -b -i (Join-Path $scripts '02_SeedLookups.sql') 2>&1
Check '02_SeedLookups.sql runs clean' ($LASTEXITCODE -eq 0) ($lookups -join ' ')

''
'--- 3. the schema is what the design says ---'
# 15 since Part 12 added PostponementRequest and AuditLog to the original 13.
$tables = [int](Sql "SELECT COUNT(*) FROM sys.tables" $Database).Trim()
Check 'fifteen tables' ($tables -eq 15) "got $tables"

# Still two, and that is the point: everything added is NO ACTION, so an audit record
# outlives the account of whoever caused it.
$cascades = [int](Sql "SELECT COUNT(*) FROM sys.foreign_keys WHERE delete_referential_action = 1" $Database).Trim()
Check 'exactly two cascading foreign keys' ($cascades -eq 2) "got $cascades"

# The one-open-proposal-per-booking rule lives in a filtered index, which SQL Server refuses
# to create unless QUOTED_IDENTIFIER is ON. sqlcmd connects with it OFF, so a script that
# forgot to set it would install a database missing this rule and say nothing.
$filtered = [int](Sql "SELECT COUNT(*) FROM sys.indexes WHERE has_filter = 1" $Database).Trim()
Check 'the filtered unique index exists' ($filtered -eq 1) "got $filtered"

$subjects = [int](Sql "SELECT COUNT(*) FROM dbo.[Subject]" $Database).Trim()
Check 'subjects seeded' ($subjects -gt 0) "got $subjects"

$people = [int](Sql "SELECT COUNT(*) FROM dbo.Person" $Database).Trim()
Check 'no people yet - the scripts create none' ($people -eq 0) "got $people"

''
'--- 4. run the seeder ---'
# The seeder has no command-line entry point. The test project's <AssemblyInitialize>
# rebuilds and seeds, so one cheap test is used to drive it rather than adding a runner
# that would exist only for this script.
$testDll = Join-Path $root 'Tests\TutorConnect.Tests\bin\Debug\net481\TutorConnect.Tests.dll'
if (-not (Test-Path $testDll)) { throw "Build the solution first - $testDll not found" }

$vstest = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
$seedRun = & $vstest $testDll /Framework:.NETFramework,Version=v4.8.1 /Tests:AT04_SigningInByEmailAddressAlsoWorks 2>&1
$seedOk = ($LASTEXITCODE -eq 0)
Check 'the seeder ran' $seedOk (($seedRun | Select-Object -Last 3) -join ' ')

$seeded = [int](Sql "SELECT COUNT(*) FROM dbo.Person" $Database).Trim()
Check 'accounts now exist' ($seeded -ge 4) "got $seeded"

foreach ($u in 'admin', 'zanele_k', 'bongani_z', 'john_doe') {
    $found = (Sql "SELECT COUNT(*) FROM dbo.Person WHERE UserName = '$u'" $Database).Trim()
    Check "seed account '$u' exists" ([int]$found -eq 1)
}

''
'--- 5. the four seed accounts sign in over HTTPS ---'
Add-Type @'
using System.Net;using System.Security.Cryptography.X509Certificates;
public class TrustFreshInstall : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; }
}
'@ -ErrorAction SilentlyContinue
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustFreshInstall
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

function Field($html, $name) {
    $m = [regex]::Match($html, "id=`"$name`"[^>]*value=`"([^`"]*)`"")
    if ($m.Success) { $m.Groups[1].Value }
}

$expectedHome = @{
    'admin'       = 'Admin/Dashboard.aspx'
    'zanele_k' = 'Student/Dashboard.aspx'
    'bongani_z'  = 'Tutor/Dashboard.aspx'
    'john_doe'    = 'Student/Dashboard.aspx'
}

# The database was dropped underneath a running application, so its connection pool still
# holds handles to a database that no longer exists. ADO.NET discards a broken connection
# when it fails, so the site recovers after a few attempts - but the first requests after a
# rebuild will fail, and that is worth waiting out rather than reporting as a defect.
$ready = $false
for ($attempt = 1; $attempt -le 12; $attempt++) {
    try {
        $probe = Invoke-WebRequest "$base/Account/Login.aspx" -UseBasicParsing -TimeoutSec 30
        if ($probe.StatusCode -eq 200 -and $probe.Content -match '__VIEWSTATE') { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 3
}
Check 'the site recovers after the database is rebuilt beneath it' $ready `
    'still failing after 12 attempts - restart the site and re-run'

foreach ($u in $expectedHome.Keys) {
    try {
        $session = $null
        $page = Invoke-WebRequest "$base/Account/Login.aspx" -SessionVariable session -UseBasicParsing -TimeoutSec 60

        $body = @{
            '__VIEWSTATE'          = (Field $page.Content '__VIEWSTATE')
            '__VIEWSTATEGENERATOR' = (Field $page.Content '__VIEWSTATEGENERATOR')
            '__EVENTVALIDATION'    = (Field $page.Content '__EVENTVALIDATION')
            'ctl00$MainContent$txtUserName' = $u
            'ctl00$MainContent$txtPassword' = $Password
            'ctl00$MainContent$btnLogin'    = 'Login'
        }

        # The redirect is followed rather than intercepted, and the assertion is on where the
        # browser actually ended up. Reading the Location header is unreliable here - PS 5.1
        # follows the 302 regardless of -MaximumRedirection 0, leaving no header to read -
        # and the destination is the thing worth proving anyway.
        $r = Invoke-WebRequest "$base/Account/Login.aspx" -Method Post -Body $body -WebSession $session `
            -UseBasicParsing -TimeoutSec 60
        $dest = $r.BaseResponse.ResponseUri.AbsolutePath

        Check "$u signs in and lands on its dashboard" ($dest -match [regex]::Escape($expectedHome[$u])) "went to '$dest'"

        # $landing, not $home: $HOME is a read-only automatic variable and assigning to it
        # throws. Same family as $profile, which bit this project once already.
        $landing = Invoke-WebRequest "$base/$($expectedHome[$u])" -WebSession $session -UseBasicParsing -TimeoutSec 60
        Check "$u's dashboard renders" ($landing.StatusCode -eq 200 -and $landing.Content -notmatch 'Server Error in')
    } catch {
        Check "$u signs in" $false $_.Exception.Message
    }
}

''
"PASSED $script:pass   FAILED $script:fail"
if ($script:fail -gt 0) { exit 1 }
