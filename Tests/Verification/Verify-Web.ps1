<#
.SYNOPSIS
    Browser-level acceptance checks against a running instance of TutorConnect.

.DESCRIPTION
    The MSTest project proves the rules. It cannot prove the pages, because a page can
    break in ways a service call never sees - a ViewState key that changes per request, or
    a Focus() call made after PreRender, both of which turned working rules into HTTP 500s
    while every service-level test still passed. These checks exercise the application over
    HTTPS exactly as a browser does.

    Cookies are issued with requireSSL, so the application cannot be exercised over plain
    HTTP: the browser will not send the cookie back and every postback fails. That is
    correct behaviour, not a defect, and it is why $BaseUrl must be the https binding.

.PARAMETER BaseUrl
    The site's HTTPS root, e.g. https://localhost:44306

.PARAMETER Password
    The password the seeded development accounts were created with (Seed:DevPassword).

.EXAMPLE
    .\Verify-Web.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!'

.NOTES
    Run these against a seeded development database only. Nothing here writes user data,
    but the sign-in attempts do increment the failed-login counters they exercise.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $BaseUrl,
    [Parameter(Mandatory = $true)] [string] $Password
)

$ErrorActionPreference = 'Continue'

# IIS Express uses a self-signed certificate. Trusting it here keeps the checks to one
# command; it applies to this PowerShell process only.
Add-Type @'
using System.Net;using System.Security.Cryptography.X509Certificates;
public class TutorConnectDevCertificatePolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; }
}
'@ -ErrorAction SilentlyContinue
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TutorConnectDevCertificatePolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$base = $BaseUrl.TrimEnd('/')
$script:pass = 0
$script:fail = 0

function Check($name, $condition, $detail = '') {
    if ($condition) { $script:pass++; "  PASS  $name" }
    else { $script:fail++; "  FAIL  $name   $detail" }
}

function Field($html, $name) {
    $m = [regex]::Match($html, "id=`"$name`"[^>]*value=`"([^`"]*)`"")
    if ($m.Success) { $m.Groups[1].Value }
}

# The status of a request that may legitimately redirect or be refused.
function StatusOf($url, $session) {
    try {
        $r = Invoke-WebRequest $url -WebSession $session -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 60
        return [int]$r.StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

# Signs in and returns the session. The GET and the POST share one session, which matters:
# ViewState is signed against the session's key, so minting it in one session and posting
# it from another is correctly rejected.
function SignIn($userName, $password) {
    $session = $null
    $page = Invoke-WebRequest "$base/Account/Login.aspx" -SessionVariable session -UseBasicParsing -TimeoutSec 60

    $body = @{
        '__VIEWSTATE'          = (Field $page.Content '__VIEWSTATE')
        '__VIEWSTATEGENERATOR' = (Field $page.Content '__VIEWSTATEGENERATOR')
        '__EVENTVALIDATION'    = (Field $page.Content '__EVENTVALIDATION')
        'ctl00$MainContent$txtUserName' = $userName
        'ctl00$MainContent$txtPassword' = $password
        'ctl00$MainContent$btnLogin'    = 'Login'
    }

    try {
        Invoke-WebRequest "$base/Account/Login.aspx" -Method Post -Body $body -WebSession $session `
            -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 60 | Out-Null
    } catch { }

    return $session
}

function SessionCookie($session) {
    ($session.Cookies.GetCookies("$base/") |
        Where-Object { $_.Name -eq 'ASP.NET_SessionId' } | Select-Object -First 1).Value
}

''
'=== SEC-12  Security headers ==='
try {
    $r = Invoke-WebRequest "$base/Account/Login.aspx" -UseBasicParsing -TimeoutSec 60
    Check 'HSTS is sent'                 ($r.Headers['Strict-Transport-Security'] -match 'max-age=\d+')
    Check 'X-Frame-Options: DENY'        ($r.Headers['X-Frame-Options'] -eq 'DENY')
    Check 'X-Content-Type-Options'       ($r.Headers['X-Content-Type-Options'] -eq 'nosniff')
    Check 'Referrer-Policy'              ($r.Headers['Referrer-Policy'] -eq 'same-origin')
    Check 'X-Powered-By is removed'      (-not $r.Headers['X-Powered-By'])
} catch { Check 'security headers' $false $_.Exception.Message }

''
'=== SEC-07  The session identifier is rotated on sign-in ==='
try {
    $s = $null
    # Touch a page first, so a session exists before signing in. That is the identifier an
    # attacker would have fixed in the victim's browser.
    $g = Invoke-WebRequest "$base/Account/Login.aspx" -SessionVariable s -UseBasicParsing -TimeoutSec 60
    $before = SessionCookie $s
    Check 'a session exists before sign-in' (-not [string]::IsNullOrEmpty($before))

    $body = @{
        '__VIEWSTATE'          = (Field $g.Content '__VIEWSTATE')
        '__VIEWSTATEGENERATOR' = (Field $g.Content '__VIEWSTATEGENERATOR')
        '__EVENTVALIDATION'    = (Field $g.Content '__EVENTVALIDATION')
        'ctl00$MainContent$txtUserName' = 'admin'
        'ctl00$MainContent$txtPassword' = $Password
        'ctl00$MainContent$btnLogin'    = 'Login'
    }
    try {
        Invoke-WebRequest "$base/Account/Login.aspx" -Method Post -Body $body -WebSession $s `
            -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 60 | Out-Null
    } catch { }

    $after = SessionCookie $s
    Check 'the identifier changed at sign-in' ($before -ne $after) "unchanged: $after"
} catch { Check 'session rotation' $false $_.Exception.Message }

''
'=== AT-05  A wrong password shows a message, not an error page ==='
try {
    $s = $null
    $g = Invoke-WebRequest "$base/Account/Login.aspx" -SessionVariable s -UseBasicParsing -TimeoutSec 60
    $body = @{
        '__VIEWSTATE'          = (Field $g.Content '__VIEWSTATE')
        '__VIEWSTATEGENERATOR' = (Field $g.Content '__VIEWSTATEGENERATOR')
        '__EVENTVALIDATION'    = (Field $g.Content '__EVENTVALIDATION')
        'ctl00$MainContent$txtUserName' = 'admin'
        'ctl00$MainContent$txtPassword' = 'definitely-not-the-password'
        'ctl00$MainContent$btnLogin'    = 'Login'
    }
    $p = Invoke-WebRequest "$base/Account/Login.aspx" -Method Post -Body $body -WebSession $s `
        -UseBasicParsing -TimeoutSec 60

    Check 'the page still returns 200'   ($p.StatusCode -eq 200)
    Check 'no server error page'         ($p.Content -notmatch 'Server Error in')
    Check 'the failure message is shown' ($p.Content -match 'alert--error')
} catch {
    Check 'a wrong password is handled' $false "HTTP $($_.Exception.Response.StatusCode) - the page faulted"
}

''
'=== SEC-04  Role authorisation at the URL ==='
$admin   = SignIn 'admin' $Password
$student = SignIn 'zanele_k' $Password
$tutor   = SignIn 'bongani_z' $Password

foreach ($url in '/Admin/Dashboard.aspx', '/Admin/Users/ManageUsers.aspx') {
    $code = StatusOf "$base$url" $student
    Check "a student is refused $url" ($code -ne 200) "HTTP $code"
}
Check 'an administrator is refused /Student/Dashboard.aspx' `
    ((StatusOf "$base/Student/Dashboard.aspx" $admin) -ne 200)
Check 'a student is refused /Tutor/Dashboard.aspx' `
    ((StatusOf "$base/Tutor/Dashboard.aspx" $student) -ne 200)
Check 'anonymous is refused /Notifications/Index.aspx' `
    ((StatusOf "$base/Notifications/Index.aspx" $null) -ne 200)

# The audit log is the one page where being refused matters most: it is the record of what
# everyone else did.
Check 'a student is refused /Admin/Audit/AuditLog.aspx' `
    ((StatusOf "$base/Admin/Audit/AuditLog.aspx" $student) -ne 200)
Check 'a tutor is refused /Admin/Audit/AuditLog.aspx' `
    ((StatusOf "$base/Admin/Audit/AuditLog.aspx" $tutor) -ne 200)
Check 'anonymous is refused /Admin/Audit/AuditLog.aspx' `
    ((StatusOf "$base/Admin/Audit/AuditLog.aspx" $null) -ne 200)

''
'=== Every page renders for the role that owns it ==='
$byRole = @{
    'admin'   = @('/Admin/Dashboard.aspx', '/Admin/Users/ManageUsers.aspx',
                  '/Admin/Subjects/ManageSubjects.aspx', '/Admin/Bookings/ManageBookings.aspx',
                  '/Admin/Reviews/ManageReviews.aspx', '/Admin/Audit/AuditLog.aspx',
                  '/Notifications/Index.aspx')
    'student' = @('/Student/Dashboard.aspx', '/Student/Tutors/FindTutor.aspx',
                  '/Student/Requests/MyRequests.aspx', '/Student/Requests/RequestSession.aspx',
                  '/Student/Bookings/MyBookings.aspx', '/Student/Reviews/MyReviews.aspx',
                  '/Student/Subjects/MySubjects.aspx', '/Student/Profile/MyProfile.aspx')
    'tutor'   = @('/Tutor/Dashboard.aspx', '/Tutor/Requests/StudentRequests.aspx',
                  '/Tutor/Availability/MyAvailability.aspx', '/Tutor/Sessions/MySessions.aspx',
                  '/Tutor/Subjects/MySubjects.aspx', '/Tutor/Profile/MyProfile.aspx')
}
$sessions = @{ 'admin' = $admin; 'student' = $student; 'tutor' = $tutor }

foreach ($role in $byRole.Keys) {
    foreach ($url in $byRole[$role]) {
        try {
            $r = Invoke-WebRequest "$base$url" -WebSession $sessions[$role] -UseBasicParsing -TimeoutSec 60
            Check "$role $url" ($r.StatusCode -eq 200 -and $r.Content -notmatch 'Server Error in')
        } catch {
            Check "$role $url" $false $_.Exception.Message
        }
    }
}

''
"PASSED $script:pass   FAILED $script:fail"
if ($script:fail -gt 0) { exit 1 }
