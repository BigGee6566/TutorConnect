<#
.SYNOPSIS
    The two Part 12.9 walkthroughs: cancellation and postponement, end to end.

.DESCRIPTION
    Drives the real screens over HTTPS, as three different people in turn - the student, the
    tutor, then the administrator - and checks that each of them sees the same outcome.

    This is the check the specification asks for and the one the MSTest suite cannot make.
    The service tests prove the rules; these prove the pages that reach them. Both have been
    needed in this project: a ViewState key that changed per request and a Focus() call made
    after PreRender each turned a working rule into an HTTP 500 while every service-level
    test stayed green.

    Persistence is asserted by re-reading each page in a fresh session rather than by
    trusting the response to the post - Part 12 is explicit that a feature is not complete
    merely because the interface showed it.

.PARAMETER BaseUrl
    The site's HTTPS root, e.g. https://localhost:44306

.PARAMETER Password
    The password the seeded development accounts were created with.

.EXAMPLE
    .\Verify-Phase12.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $BaseUrl,
    [Parameter(Mandatory = $true)] [string] $Password
)

$ErrorActionPreference = 'Continue'
$base = $BaseUrl.TrimEnd('/')

Add-Type @'
using System.Net;using System.Security.Cryptography.X509Certificates;
public class TrustPhase12 : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; }
}
'@ -ErrorAction SilentlyContinue
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustPhase12
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

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

# The three hidden fields every Web Forms post needs. ViewState is signed against the
# session that issued it, so the GET and the POST must share one WebSession.
function PostBack($url, $session, $page, $extra) {
    $body = @{
        '__VIEWSTATE'          = (Field $page.Content '__VIEWSTATE')
        '__VIEWSTATEGENERATOR' = (Field $page.Content '__VIEWSTATEGENERATOR')
        '__EVENTVALIDATION'    = (Field $page.Content '__EVENTVALIDATION')
    }
    foreach ($k in $extra.Keys) { $body[$k] = $extra[$k] }

    return Invoke-WebRequest $url -Method Post -Body $body -WebSession $session `
        -UseBasicParsing -TimeoutSec 60
}

function SignIn($user) {
    $session = $null
    $page = Invoke-WebRequest "$base/Account/Login.aspx" -SessionVariable session `
        -UseBasicParsing -TimeoutSec 60

    # A cold or recompiling app can return something that is not the login form at all.
    # Posting an empty __VIEWSTATE to it produces "The state information is invalid",
    # which then looks like a defect in the page rather than a warm-up problem.
    if ($page.Content -notmatch 'id="__VIEWSTATE"') {
        throw "Login.aspx did not return the sign-in form for $user - is the site warm?"
    }

    PostBack "$base/Account/Login.aspx" $session $page @{
        'ctl00$MainContent$txtUserName' = $user
        'ctl00$MainContent$txtPassword' = $Password
        'ctl00$MainContent$btnLogin'    = 'Login'
    } | Out-Null

    return $session
}

# Warm the application before asserting anything. The first request after a rebuild
# compiles the changed pages, and a redirect to Login satisfies "200 and no error page" -
# so an unwarmed run reports failures that say nothing about the code.
$ready = $false
for ($attempt = 1; $attempt -le 15; $attempt++) {
    try {
        $probe = Invoke-WebRequest "$base/Account/Login.aspx" -UseBasicParsing -TimeoutSec 60
        if ($probe.StatusCode -eq 200 -and $probe.Content -match 'id="__VIEWSTATE"') { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 3
}
if (-not $ready) { throw "The site never came up at $base" }

''
'================ Part 12.9 walkthroughs ================'

# --------------------------------------------------------------------------------------
''
'--- the student can reach a scheduled session ---'

$student = SignIn 'zanele_k'
$list = Invoke-WebRequest "$base/Student/Bookings/MyBookings.aspx" -WebSession $student `
    -UseBasicParsing -TimeoutSec 60

# Not just "200 and no error page": a failed sign-in redirects here to Login, which is also
# a 200 with no error on it. The page must actually be My Bookings.
Check 'the student is signed in and on My Bookings' `
    ($list.StatusCode -eq 200 -and $list.Content -notmatch 'Server Error in' -and
     $list.Content -notmatch 'btnLogin') 'landed on the login page - sign-in failed'

# A booking id from a row that offers the Cancel action - i.e. one that is still scheduled.
# The ampersand is matched either way: <%# %> writes the href without HTML-encoding it, so
# the page carries a bare "&", not "&amp;".
$bookingId = $null
$m = [regex]::Match($list.Content, 'BookingDetails\.aspx\?bookingId=(\d+)(?:&amp;|&)action=cancel')
if ($m.Success) { $bookingId = $m.Groups[1].Value }

Check 'a scheduled session offers Cancel and Postpone' ($null -ne $bookingId) `
    'no cancellable booking on the page - seed one and re-run'

if (-not $bookingId) {
    ''
    "PASSED $script:pass   FAILED $script:fail"
    exit 1
}
"        using booking $bookingId"

# --------------------------------------------------------------------------------------
''
'--- postponement: student proposes ---'

$detail = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId&action=postpone" `
    -WebSession $student -UseBasicParsing -TimeoutSec 60

Check 'the postpone form opens' ($detail.Content -match 'btnConfirmPostpone')

$newDate = (Get-Date).AddDays(75).ToString('yyyy-MM-dd')
$posted = PostBack "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" $student $detail @{
    'ctl00$MainContent$txtNewDate'        = $newDate
    'ctl00$MainContent$txtNewStart'       = '14:00'
    'ctl00$MainContent$txtNewEnd'         = '16:00'
    'ctl00$MainContent$ddlPostponeReason' = 'TimetableClash'
    'ctl00$MainContent$txtPostponeNote'   = 'Lecture moved to that slot.'
    'ctl00$MainContent$btnConfirmPostpone' = 'Send the request'
}

Check 'the proposal posts without an error page' `
    ($posted.StatusCode -eq 200 -and $posted.Content -notmatch 'Server Error in')

# Fresh session: proves it was stored, not merely rendered back.
$studentAgain = SignIn 'zanele_k'
$reread = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" `
    -WebSession $studentAgain -UseBasicParsing -TimeoutSec 60

Check 'the proposal survives a new sign-in' ($reread.Content -match 'Waiting for the tutor')
Check 'the session has NOT moved yet' ($reread.Content -notmatch 'btnConfirmPostpone')

# --------------------------------------------------------------------------------------
''
'--- the student can withdraw their own proposal ---'

$withdrawPage = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" `
    -WebSession $studentAgain -UseBasicParsing -TimeoutSec 60

Check 'a withdraw action is offered while it is pending' ($withdrawPage.Content -match 'btnWithdraw')

$withdrawn = PostBack "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" $studentAgain $withdrawPage @{
    'ctl00$MainContent$btnWithdraw' = 'Withdraw this request'
}
Check 'withdrawing posts without an error page' `
    ($withdrawn.StatusCode -eq 200 -and $withdrawn.Content -notmatch 'Server Error in')

$afterWithdraw = SignIn 'zanele_k'
$reReadAfterWithdraw = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" `
    -WebSession $afterWithdraw -UseBasicParsing -TimeoutSec 60
Check 'the proposal is gone after a new sign-in' ($reReadAfterWithdraw.Content -notmatch 'Waiting for the tutor')
Check 'the session is offered for postponement again' ($reReadAfterWithdraw.Content -match 'btnShowPostpone')

# Propose again, so the tutor has something to answer for the rest of the walkthrough.
$detail2 = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId&action=postpone" `
    -WebSession $afterWithdraw -UseBasicParsing -TimeoutSec 60

PostBack "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" $afterWithdraw $detail2 @{
    'ctl00$MainContent$txtNewDate'        = $newDate
    'ctl00$MainContent$txtNewStart'       = '14:00'
    'ctl00$MainContent$txtNewEnd'         = '16:00'
    'ctl00$MainContent$ddlPostponeReason' = 'TimetableClash'
    'ctl00$MainContent$txtPostponeNote'   = 'Lecture moved to that slot.'
    'ctl00$MainContent$btnConfirmPostpone' = 'Send the request'
} | Out-Null
Check 'a fresh proposal can be made after withdrawing' `
    ((Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" `
        -WebSession $afterWithdraw -UseBasicParsing -TimeoutSec 60).Content -match 'Waiting for the tutor')

# --------------------------------------------------------------------------------------
''
'--- postponement: the tutor sees and answers it ---'

$tutor = SignIn 'bongani_z'
$sessions = Invoke-WebRequest "$base/Tutor/Sessions/MySessions.aspx" -WebSession $tutor `
    -UseBasicParsing -TimeoutSec 60

Check 'My Sessions renders' ($sessions.StatusCode -eq 200 -and $sessions.Content -notmatch 'Server Error in')
Check 'the tutor sees the request' ($sessions.Content -match 'Postponement requests')
Check 'it is marked as waiting for them' ($sessions.Content -match 'Waiting for you')

# A LinkButton renders as an anchor calling __doPostBack, not as a named input - there is no
# name= attribute to find. The postback target is the argument to that call, and the
# apostrophes around it arrive HTML-encoded as &#39; because they sit inside an href.
$answerButton = [regex]::Match($sessions.Content,
    '__doPostBack\((?:&#39;|'')([^&'']*btnAnswer)(?:&#39;|'')')
Check 'an Answer action is offered' $answerButton.Success

if ($answerButton.Success) {
    $opened = PostBack "$base/Tutor/Sessions/MySessions.aspx" $tutor $sessions @{
        '__EVENTTARGET'   = $answerButton.Groups[1].Value
        '__EVENTARGUMENT' = ''
    }
    Check 'the answer form opens' ($opened.Content -match 'btnApprove')

    $approved = PostBack "$base/Tutor/Sessions/MySessions.aspx" $tutor $opened @{
        'ctl00$MainContent$txtResponse' = 'That time works for me.'
        'ctl00$MainContent$btnApprove'  = 'Approve and move the session'
    }
    Check 'approving posts without an error page' `
        ($approved.StatusCode -eq 200 -and $approved.Content -notmatch 'Server Error in')
    Check 'the tutor is told it moved' ($approved.Content -match 'Session moved')
}

# --------------------------------------------------------------------------------------
''
'--- the student sees the new date, in a new session ---'

$studentThird = SignIn 'zanele_k'
$moved = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" `
    -WebSession $studentThird -UseBasicParsing -TimeoutSec 60

# Anchored to the Date and Time rows of the booking itself. Searching the whole page for the
# date would also match the postponement panel, which shows the PROPOSED range - so the
# assertion would pass while the booking had not moved at all.
$expected = (Get-Date).AddDays(75).ToString('dd/MM/yyyy')
Check "the booking's own Date row now reads $expected" `
    ($moved.Content -match "<dt>Date</dt>\s*<dd>$([regex]::Escape($expected))</dd>") `
    'the approved date did not reach the booking'
Check "the booking's own Time row reads 14:00 - 16:00" `
    ($moved.Content -match '<dt>Time</dt>\s*<dd>14:00 - 16:00</dd>')
Check 'the proposal is no longer shown as waiting' ($moved.Content -notmatch 'Waiting for the tutor')

# --------------------------------------------------------------------------------------
''
'--- cancellation: the student cancels the moved session ---'

$cancelForm = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId&action=cancel" `
    -WebSession $studentThird -UseBasicParsing -TimeoutSec 60

Check 'the cancel form opens' ($cancelForm.Content -match 'btnConfirmCancel')

$cancelled = PostBack "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" $studentThird $cancelForm @{
    'ctl00$MainContent$ddlCancelReason'  = 'Medical'
    'ctl00$MainContent$txtCancelNote'    = 'Booked a clinic appointment.'
    'ctl00$MainContent$btnConfirmCancel' = 'Confirm cancellation'
}

Check 'cancelling posts without an error page' `
    ($cancelled.StatusCode -eq 200 -and $cancelled.Content -notmatch 'Server Error in')

$afterCancel = SignIn 'zanele_k'
$cancelledView = Invoke-WebRequest "$base/Student/Bookings/BookingDetails.aspx?bookingId=$bookingId" `
    -WebSession $afterCancel -UseBasicParsing -TimeoutSec 60

Check 'the cancellation survives a new sign-in' ($cancelledView.Content -match 'Cancelled')
Check 'the reason is shown back' ($cancelledView.Content -match 'Medical issue')
Check 'who cancelled it is recorded' ($cancelledView.Content -match 'Cancelled by')

# --------------------------------------------------------------------------------------
''
'--- the tutor sees the cancellation ---'

$tutorAgain = SignIn 'bongani_z'
$notifications = Invoke-WebRequest "$base/Notifications/Index.aspx" -WebSession $tutorAgain `
    -UseBasicParsing -TimeoutSec 60

Check 'the tutor was notified' ($notifications.Content -match 'Session cancelled') `
    'no cancellation notice reached the tutor'

# --------------------------------------------------------------------------------------
''
'--- the administrator sees the booking and the audit trail ---'

$admin = SignIn 'admin'

$adminBookings = Invoke-WebRequest "$base/Admin/Bookings/ManageBookings.aspx" -WebSession $admin `
    -UseBasicParsing -TimeoutSec 60
Check 'Manage Bookings renders' `
    ($adminBookings.StatusCode -eq 200 -and $adminBookings.Content -notmatch 'Server Error in')
Check 'the cancellation column is populated' ($adminBookings.Content -match 'Medical issue')

$auditPage = Invoke-WebRequest "$base/Admin/Audit/AuditLog.aspx" -WebSession $admin `
    -UseBasicParsing -TimeoutSec 60
Check 'the audit log renders' `
    ($auditPage.StatusCode -eq 200 -and $auditPage.Content -notmatch 'Server Error in')
Check 'the cancellation is recorded' ($auditPage.Content -match 'BookingCancelled')
Check 'the postponement is recorded' ($auditPage.Content -match 'PostponementApproved')
Check 'the withdrawal is recorded' ($auditPage.Content -match 'PostponementWithdrawn')

# --------------------------------------------------------------------------------------
''
'--- the administrator moves a different session ---'

# A second scheduled booking, so the reschedule is not fighting the one just cancelled.
$adminList = Invoke-WebRequest "$base/Admin/Bookings/ManageBookings.aspx" -WebSession $admin `
    -UseBasicParsing -TimeoutSec 60

$moveButton = [regex]::Match($adminList.Content,
    '__doPostBack\((?:&#39;|'')([^&'']*btnMove)(?:&#39;|'')')
Check 'a Reschedule action is offered on a scheduled booking' $moveButton.Success

if ($moveButton.Success) {
    $moveForm = PostBack "$base/Admin/Bookings/ManageBookings.aspx" $admin $adminList @{
        '__EVENTTARGET'   = $moveButton.Groups[1].Value
        '__EVENTARGUMENT' = ''
    }
    Check 'the reschedule form opens' ($moveForm.Content -match 'btnConfirmMove')

    # Refuse first: a move with no stated reason is not an audit trail.
    $noReason = PostBack "$base/Admin/Bookings/ManageBookings.aspx" $admin $moveForm @{
        'ctl00$MainContent$txtMoveDate'   = (Get-Date).AddDays(96).ToString('yyyy-MM-dd')
        'ctl00$MainContent$txtMoveStart'  = '08:00'
        'ctl00$MainContent$txtMoveEnd'    = '10:00'
        'ctl00$MainContent$txtMoveReason' = ''
        'ctl00$MainContent$btnConfirmMove' = 'Move the session'
    }
    Check 'a move with no reason is refused' ($noReason.Content -match 'Give a reason')

    $moved = PostBack "$base/Admin/Bookings/ManageBookings.aspx" $admin $noReason @{
        'ctl00$MainContent$txtMoveDate'   = (Get-Date).AddDays(96).ToString('yyyy-MM-dd')
        'ctl00$MainContent$txtMoveStart'  = '08:00'
        'ctl00$MainContent$txtMoveEnd'    = '10:00'
        'ctl00$MainContent$txtMoveReason' = 'Venue closed for maintenance.'
        'ctl00$MainContent$btnConfirmMove' = 'Move the session'
    }
    Check 'the move succeeds' ($moved.Content -match 'Session moved and both people notified')

    $auditAfter = Invoke-WebRequest "$base/Admin/Audit/AuditLog.aspx" -WebSession $admin `
        -UseBasicParsing -TimeoutSec 60
    Check 'the reschedule is recorded' ($auditAfter.Content -match 'BookingRescheduled')
}

# --------------------------------------------------------------------------------------
''
'--- the audit log is administrators only ---'

foreach ($who in 'zanele_k', 'bongani_z') {
    $session = SignIn $who
    $code = 0
    try {
        $r = Invoke-WebRequest "$base/Admin/Audit/AuditLog.aspx" -WebSession $session `
            -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 60
        $code = [int]$r.StatusCode
    } catch {
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
    }
    Check "$who is refused the audit log" ($code -ne 200) "HTTP $code"
}

''
"PASSED $script:pass   FAILED $script:fail"
if ($script:fail -gt 0) { exit 1 }
