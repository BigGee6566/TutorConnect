<#
.SYNOPSIS
    AT-26 (responsive), AT-27 (keyboard-only) and AT-28 (contrast) against a running site.

.DESCRIPTION
    Verify-Web.ps1 proves the pages respond and the rules hold. It cannot prove that a table
    stacks, that a control is reachable by Tab, or that text clears 4.5:1 - those need a real
    layout engine and the computed styles that come with it.

    Everything here is driven through the Chrome DevTools Protocol. Sizes come from
    Emulation.setDeviceMetricsOverride, never from --window-size: headless Chrome lays out
    wider than the window and then clips, which produced a phantom "off-centre" defect in
    this project once already.

.PARAMETER BaseUrl
    The site's HTTPS root, e.g. https://localhost:44306

.PARAMETER Password
    The password the seeded development accounts were created with (Seed:DevPassword).

.PARAMETER ChromePath
    Overrides the Chrome executable if it is not in the usual place.

.EXAMPLE
    .\Verify-Acceptance.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $BaseUrl,
    [Parameter(Mandatory = $true)] [string] $Password,
    [string] $ChromePath = "C:\Program Files\Google\Chrome\Application\chrome.exe",
    [int] $Port = 9226
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')

$script:pass = 0
$script:fail = 0
$script:notes = @()

function Check($name, $condition, $detail = '') {
    if ($condition) { $script:pass++; "  PASS  $name" }
    else { $script:fail++; "  FAIL  $name   $detail" }
}

function Note($text) { $script:notes += $text; "  NOTE  $text" }

if (-not (Test-Path $ChromePath)) { throw "Chrome not found at $ChromePath. Pass -ChromePath." }

# $profileDir, not $profile: $profile is an automatic variable, and assigning to it has
# caused trouble in this project before.
$profileDir = Join-Path $env:TEMP "tc-acceptance"
if (Test-Path $profileDir) { Remove-Item $profileDir -Recurse -Force -ErrorAction SilentlyContinue }

$proc = Start-Process -FilePath $ChromePath -PassThru -WindowStyle Hidden -ArgumentList @(
    '--headless=new', '--disable-gpu', '--no-sandbox', '--ignore-certificate-errors',
    '--hide-scrollbars', "--remote-debugging-port=$Port", "--user-data-dir=$profileDir",
    '--remote-allow-origins=*', 'about:blank'
)

try {
    #region CDP plumbing

    $wsUrl = $null
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        try {
            $targets = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json/list" -TimeoutSec 5
            $page = $targets | Where-Object { $_.type -eq 'page' } | Select-Object -First 1
            if ($page) { $wsUrl = $page.webSocketDebuggerUrl; break }
        } catch { }
    }
    if (-not $wsUrl) { throw "DevTools endpoint did not come up on port $Port" }

    $client = New-Object System.Net.WebSockets.ClientWebSocket
    $client.Options.SetRequestHeader('Origin', "http://127.0.0.1:$Port")
    $client.ConnectAsync([Uri]$wsUrl, [Threading.CancellationToken]::None).Wait(10000) | Out-Null

    $script:msgId = 0
    function Send-Cdp([string]$method, $params) {
        $script:msgId++
        $payload = @{ id = $script:msgId; method = $method }
        if ($params) { $payload.params = $params }
        $json = $payload | ConvertTo-Json -Depth 12 -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $seg = New-Object System.ArraySegment[byte] (,$bytes)
        $client.SendAsync($seg, 'Text', $true, [Threading.CancellationToken]::None).Wait(10000) | Out-Null

        # Read until the reply carrying our id arrives; events in between are discarded.
        $deadline = (Get-Date).AddSeconds(45)
        while ((Get-Date) -lt $deadline) {
            $buffer = New-Object byte[] 262144
            $sb = New-Object Text.StringBuilder
            do {
                $seg2 = New-Object System.ArraySegment[byte] (,$buffer)
                $t = $client.ReceiveAsync($seg2, [Threading.CancellationToken]::None)
                $t.Wait(45000) | Out-Null
                $r = $t.Result
                [void]$sb.Append([Text.Encoding]::UTF8.GetString($buffer, 0, $r.Count))
            } while (-not $r.EndOfMessage)

            $msg = $sb.ToString() | ConvertFrom-Json
            if ($msg.id -eq $script:msgId) { return $msg }
        }
        throw "Timed out waiting for $method"
    }

    function Eval([string]$expr) {
        $res = Send-Cdp 'Runtime.evaluate' @{ expression = $expr; returnByValue = $true; awaitPromise = $false }
        return $res.result.result.value
    }

    function Go([string]$url, [string]$expect) {
        Send-Cdp 'Page.navigate' @{ url = $url } | Out-Null
        $deadline = (Get-Date).AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 350
            $state = Eval 'document.readyState'
            $href = Eval 'location.href'
        } while ((Get-Date) -lt $deadline -and -not ($state -eq 'complete' -and $href -like "*$expect*"))
        return $href
    }

    function Resize([int]$width) {
        Send-Cdp 'Emulation.setDeviceMetricsOverride' @{
            width = $width; height = 900; deviceScaleFactor = 1; mobile = ($width -lt 768)
        } | Out-Null
        Start-Sleep -Milliseconds 350
    }

    # A real key event, not a synthetic DOM event: rawKeyDown then keyUp, as a browser sends.
    function Key([string]$key, [int]$vk, [string]$code) {
        Send-Cdp 'Input.dispatchKeyEvent' @{
            type = 'rawKeyDown'; key = $key; code = $code
            windowsVirtualKeyCode = $vk; nativeVirtualKeyCode = $vk
        } | Out-Null
        Send-Cdp 'Input.dispatchKeyEvent' @{
            type = 'keyUp'; key = $key; code = $code
            windowsVirtualKeyCode = $vk; nativeVirtualKeyCode = $vk
        } | Out-Null
    }

    function Tab { Key 'Tab' 9 'Tab' }
    function Esc { Key 'Escape' 27 'Escape' }

    # Enter needs the full keyDown with text, not rawKeyDown. rawKeyDown moves focus and
    # fires the event, but it does not activate a submit control, so the form never posts.
    function Enter {
        Send-Cdp 'Input.dispatchKeyEvent' @{
            type = 'keyDown'; key = 'Enter'; code = 'Enter'; text = "`r"; unmodifiedText = "`r"
            windowsVirtualKeyCode = 13; nativeVirtualKeyCode = 13
        } | Out-Null
        Send-Cdp 'Input.dispatchKeyEvent' @{
            type = 'keyUp'; key = 'Enter'; code = 'Enter'
            windowsVirtualKeyCode = 13; nativeVirtualKeyCode = 13
        } | Out-Null
    }

    function TypeText([string]$text) { Send-Cdp 'Input.insertText' @{ text = $text } | Out-Null }

    # A stable description of whatever currently has focus.
    function ActiveDescriptor {
        return Eval @'
(function(){var a=document.activeElement;if(!a||a===document.body)return 'BODY';
var id=a.id?('#'+a.id):'';var cl=a.className&&typeof a.className==='string'?('.'+a.className.trim().split(/\s+/).join('.')):'';
return a.tagName+id+cl;})()
'@
    }

    Send-Cdp 'Page.enable' $null | Out-Null
    Send-Cdp 'Runtime.enable' $null | Out-Null
    Send-Cdp 'DOM.enable' $null | Out-Null

    #endregion

    #region Sign-in

    # The browser performs the sign-in itself, so one session owns both the ViewState and
    # the cookie. Minting ViewState elsewhere and posting it here is correctly rejected.
    function SignIn([string]$user) {
        Resize 1440
        Go "$base/Account/Login.aspx" 'Login.aspx' | Out-Null
        # The click is deferred by a tick on purpose. Clicking inline starts the navigation
        # before Runtime.evaluate has replied, the execution context is torn down with the
        # reply still in it, and the call then blocks until its timeout. Returning first and
        # submitting afterwards keeps the protocol conversation intact.
        $r = Eval @"
(function(){
  var u=document.querySelector('input[name\$="txtUserName"]');
  var p=document.querySelector('input[name\$="txtPassword"]');
  var b=document.querySelector('input[name\$="btnLogin"]');
  if(!u||!p||!b){return 'controls-missing';}
  u.value='$user'; p.value='$Password';
  setTimeout(function(){ b.click(); }, 50);
  return 'submitted';
})()
"@
        if ($r -ne 'submitted') { throw "Could not drive the login form for ${user}: $r" }

        $deadline = (Get-Date).AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 350
            $href = Eval 'location.href'
        } while ((Get-Date) -lt $deadline -and $href -like '*Login.aspx*')
        return $href
    }

    #endregion

    $widths = @(1440, 1024, 768, 375)

    $rolePages = @{
        'admin'       = @('/Admin/Dashboard.aspx', '/Admin/Users/ManageUsers.aspx',
                          '/Admin/Subjects/ManageSubjects.aspx', '/Admin/Bookings/ManageBookings.aspx',
                          '/Admin/Reviews/ManageReviews.aspx', '/Admin/Audit/AuditLog.aspx',
                          '/Notifications/Index.aspx')
        'sinesipho_t' = @('/Student/Dashboard.aspx', '/Student/Tutors/FindTutor.aspx',
                          '/Student/Requests/MyRequests.aspx', '/Student/Requests/RequestSession.aspx',
                          '/Student/Bookings/MyBookings.aspx', '/Student/Reviews/MyReviews.aspx',
                          '/Student/Subjects/MySubjects.aspx', '/Student/Profile/MyProfile.aspx')
        'nhlawulo_m'  = @('/Tutor/Dashboard.aspx', '/Tutor/Requests/StudentRequests.aspx',
                          '/Tutor/Availability/MyAvailability.aspx', '/Tutor/Sessions/MySessions.aspx',
                          '/Tutor/Subjects/MySubjects.aspx', '/Tutor/Profile/MyProfile.aspx')
    }

    $authPages = @('/Account/Login.aspx', '/Account/Register.aspx',
                   '/Account/ForgotPassword.aspx', '/Account/Setup.aspx')

    ''
    '================ AT-26  Responsive layout ================'

    # The auth pages first, while signed out. They matter on their own: Auth.Master loads
    # colors, typography and auth.css only - NOT responsive.css - so none of the app-shell
    # rules apply to them and their breakpoints are entirely separate.
    ''
    '--- signed-out pages (no horizontal scroll) ---'
    foreach ($url in $authPages) {
        foreach ($w in $widths) {
            Resize $w
            Go "$base$url" ([IO.Path]::GetFileName($url)) | Out-Null
            $overflow = Eval 'document.documentElement.scrollWidth - document.documentElement.clientWidth'
            Check ("{0,-32} @ {1,4}px" -f $url, $w) ([int]$overflow -le 1) "overflows by ${overflow}px"
        }
    }

    foreach ($user in @('admin', 'sinesipho_t', 'nhlawulo_m')) {
        ''
        "--- $user ---"
        SignIn $user | Out-Null

        foreach ($url in $rolePages[$user]) {
            foreach ($w in $widths) {
                Resize $w
                $landed = Go "$base$url" ([IO.Path]::GetFileName($url))
                if ($landed -like '*Login.aspx*') {
                    Check ("{0,-40} @ {1,4}px" -f $url, $w) $false 'session lost'
                    continue
                }

                $overflow = Eval 'document.documentElement.scrollWidth - document.documentElement.clientWidth'
                Check ("{0,-40} @ {1,4}px  no horizontal scroll" -f $url, $w) ([int]$overflow -le 1) "overflows by ${overflow}px"

                if ($w -le 767) {
                    # Rendered DOM, not the source: these cells come out of Repeater
                    # templates, so a template that was missed is invisible to a grep.
                    $tables = Eval @'
(function(){
  var cells=document.querySelectorAll('.data-table td');
  if(!cells.length) return 'none';
  var missing=0, notStacked=0;
  for(var i=0;i<cells.length;i++){
    var c=cells[i];
    var label=c.getAttribute('data-label');
    if(!label||!label.trim()) missing++;
    var d=getComputedStyle(c).display;
    if(d==='table-cell') notStacked++;
  }
  return cells.length+'|'+missing+'|'+notStacked;
})()
'@
                    if ($tables -ne 'none') {
                        $t = $tables -split '\|'
                        Check ("{0,-40} @ {1,4}px  every cell labelled ({2} cells)" -f $url, $w, $t[0]) ([int]$t[1] -eq 0) "$($t[1]) without data-label"
                        Check ("{0,-40} @ {1,4}px  table stacks" -f $url, $w) ([int]$t[2] -eq 0) "$($t[2]) still table-cell"
                    }
                }
            }
        }
    }

    ''
    '--- sidebar drawer ---'
    SignIn 'sinesipho_t' | Out-Null

    # 1024 is NOT a drawer width. responsive.css hides the hamburger at min-width: 1024 and
    # shows the drawer at max-width: 1023, so at exactly 1024 the sidebar is fixed and on
    # screen - the same as 1440. Testing 1024 as a drawer width fails against correct code.
    foreach ($w in @(1440, 1024)) {
        Resize $w
        Go "$base/Student/Dashboard.aspx" 'Dashboard.aspx' | Out-Null

        $hamburger = Eval "(function(){var h=document.querySelector('[data-sidebar-open]');return h?getComputedStyle(h).display:'absent';})()"
        Check ("the hamburger is hidden at {0}px" -f $w) ($hamburger -eq 'none') "display: $hamburger"

        $fixed = Eval @'
(function(){var s=document.querySelector('.sidebar');if(!s)return 'absent';
var cs=getComputedStyle(s);
return (cs.display!=='none'&&s.getBoundingClientRect().left>=0)?'on-screen':'off-screen';})()
'@
        Check ("the sidebar is fixed and on screen at {0}px" -f $w) ($fixed -eq 'on-screen') $fixed
    }

    foreach ($w in @(1023, 768, 375)) {
        Resize $w
        Go "$base/Student/Dashboard.aspx" 'Dashboard.aspx' | Out-Null

        $visible = Eval "(function(){var h=document.querySelector('[data-sidebar-open]');return h?getComputedStyle(h).display:'absent';})()"
        Check ("the hamburger is shown at {0}px" -f $w) ($visible -ne 'none' -and $visible -ne 'absent') "display: $visible"

        # Open by click, then check the drawer took focus (sidebar.js moves it there so a
        # keyboard user is not left behind the overlay).
        Eval "(function(){document.querySelector('[data-sidebar-open]').click();return 1;})()" | Out-Null
        Start-Sleep -Milliseconds 300
        $open = Eval "document.body.classList.contains('sidebar-open')"
        Check ("the drawer opens at {0}px" -f $w) ($open -eq $true)

        $focused = Eval "(function(){var a=document.activeElement;return a&&a.classList&&a.classList.contains('sidebar__close')?'close':'elsewhere';})()"
        Check ("focus moves into the drawer at {0}px" -f $w) ($focused -eq 'close') "focus went $focused"

        $expanded = Eval "document.querySelector('[data-sidebar-open]').getAttribute('aria-expanded')"
        Check ("aria-expanded is true at {0}px" -f $w) ($expanded -eq 'true') "got $expanded"

        # Escape closes it, as it would any overlay, and focus returns to the trigger.
        Esc
        Start-Sleep -Milliseconds 300
        $closed = Eval "document.body.classList.contains('sidebar-open')"
        Check ("Escape closes the drawer at {0}px" -f $w) ($closed -eq $false)

        $returned = Eval "(function(){var a=document.activeElement;return a&&a.hasAttribute&&a.hasAttribute('data-sidebar-open')?'trigger':'elsewhere';})()"
        Check ("focus returns to the trigger at {0}px" -f $w) ($returned -eq 'trigger') "focus went $returned"

        # The backdrop is the second closer and needs exercising too.
        Eval "(function(){document.querySelector('[data-sidebar-open]').click();return 1;})()" | Out-Null
        Start-Sleep -Milliseconds 250
        Eval "(function(){var b=document.querySelector('.sidebar-backdrop');if(b)b.click();return 1;})()" | Out-Null
        Start-Sleep -Milliseconds 250
        $backdropClosed = Eval "document.body.classList.contains('sidebar-open')"
        Check ("the backdrop closes the drawer at {0}px" -f $w) ($backdropClosed -eq $false)
    }

    # Leaving the drawer breakpoint must not strand the page in the open state.
    Resize 375
    Go "$base/Student/Dashboard.aspx" 'Dashboard.aspx' | Out-Null
    Eval "(function(){document.querySelector('[data-sidebar-open]').click();return 1;})()" | Out-Null
    Start-Sleep -Milliseconds 250
    Resize 1440
    Start-Sleep -Milliseconds 500
    $stranded = Eval "document.body.classList.contains('sidebar-open')"
    Check 'growing past 1024 clears the open drawer' ($stranded -eq $false)

    ''
    '================ AT-27  Keyboard-only ================'

    # Reachability, focus visibility and absence of a trap, measured page by page. A control
    # counts as reachable when tabbing from the top of the document lands on it.
    $tabAudit = @'
(function(){
  var focusable = Array.prototype.slice.call(document.querySelectorAll(
    'a[href], button:not([disabled]), input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
  )).filter(function(el){
    var cs=getComputedStyle(el);
    if(cs.display==='none'||cs.visibility==='hidden') return false;
    // Content inside a collapsed <details> is not rendered and not tabbable. That is
    // correct behaviour, not an unreachable control: the <summary> is in the tab order,
    // and opening it reveals what is inside. Counting the hidden inputs as "should be
    // reachable" would fail the page for doing the right thing.
    var d=el.closest('details');
    if(d&&!d.open&&el.tagName!=='SUMMARY') return false;
    var r=el.getBoundingClientRect();
    // A skip link is off-screen until focused; it is still reachable and still counts.
    if(r.width===0&&r.height===0&&!el.classList.contains('skip-link')) return false;
    return true;
  });
  return focusable.length;
})()
'@

    $flow = @(
        @{ Url = '/Student/Dashboard.aspx';        Name = 'dashboard' },
        @{ Url = '/Student/Tutors/FindTutor.aspx'; Name = 'search' },
        @{ Url = '/Student/Requests/RequestSession.aspx'; Name = 'request' },
        @{ Url = '/Student/Reviews/MyReviews.aspx'; Name = 'review' }
    )

    ''
    '--- login, by keyboard alone ---'
    Resize 1440
    Go "$base/Account/Login.aspx" 'Login.aspx' | Out-Null

    # Tab from the top until the username field has focus, so the path to it is proved
    # rather than assumed.
    $reached = $false
    $steps = 0
    Eval "(function(){document.body.focus();return 1;})()" | Out-Null
    for ($i = 0; $i -lt 15; $i++) {
        Tab
        $steps++
        $d = ActiveDescriptor
        if ($d -like '*txtUserName*') { $reached = $true; break }
    }
    Check 'the username field is reachable by Tab' $reached "gave up after $steps tabs"

    if ($reached) {
        TypeText 'sinesipho_t'
        Tab
        $d = ActiveDescriptor
        Check 'Tab moves to the password field' ($d -like '*txtPassword*') "landed on $d"
        TypeText $Password

        # Tab on to the submit button rather than pressing Enter in the field, so the
        # button's own reachability is what gets proved.
        $onButton = $false
        for ($i = 0; $i -lt 6; $i++) {
            Tab
            $d = ActiveDescriptor
            if ($d -like '*btnLogin*') { $onButton = $true; break }
        }
        Check 'the Login button is reachable by Tab' $onButton "landed on $d"

        if ($onButton) {
            Enter
            $deadline = (Get-Date).AddSeconds(25)
            do {
                Start-Sleep -Milliseconds 350
                $href = Eval 'location.href'
            } while ((Get-Date) -lt $deadline -and $href -like '*Login.aspx*')
            Check 'Enter on the button signs in' ($href -like '*Student/Dashboard*') "landed at $href"
        }
    }

    ''
    '--- every control reachable, focus always visible, no trap ---'
    foreach ($step in $flow) {
        Resize 1440
        $landed = Go "$base$($step.Url)" ([IO.Path]::GetFileName($step.Url))
        if ($landed -like '*Login.aspx*') { Check "$($step.Name): session held" $false 'redirected to login'; continue }

        $expected = [int](Eval $tabAudit)

        # Walk the whole page with Tab, recording what focus lands on and whether the ring
        # is actually drawn. The cap is generous; exceeding it is itself the trap signal.
        $cap = $expected * 3 + 20
        Eval "(function(){document.body.focus();return 1;})()" | Out-Null

        $seen = @{}
        $invisible = 0
        $lastDescriptor = ''
        $stuck = 0
        $maxStuck = 0

        for ($i = 0; $i -lt $cap; $i++) {
            Tab

            # JSON, not a delimited string. An earlier version joined the fields with '||'
            # and split on it - but any element with an empty name attribute (most links)
            # emitted a literal '||' inside its own descriptor, so the split landed on the
            # wrong field. That one line produced all three AT-27 symptoms at once: every
            # stop counted as having no focus ring, distinct controls collided into one
            # descriptor, and the collisions then looked like a keyboard trap.
            $info = Eval @'
(function(){
  var a=document.activeElement;
  if(!a||a===document.body) return JSON.stringify({key:'',visible:true,body:true});
  var cs=getComputedStyle(a);
  var ow=parseFloat(cs.outlineWidth)||0;
  var seq=a.getAttribute('data-tab-seq');
  if(!seq){ seq=String(Math.random()).slice(2,10); a.setAttribute('data-tab-seq',seq); }
  return JSON.stringify({
    key: seq,
    label: a.tagName+(a.id?('#'+a.id):'')+' '+(a.textContent||'').trim().slice(0,24),
    visible: ((cs.outlineStyle!=='none'&&ow>0)||cs.boxShadow!=='none'),
    body: false
  });
})()
'@
            $state = $info | ConvertFrom-Json
            if ($state.body) { continue }

            # Identity is a per-element stamp, so ten "View" links are ten controls rather
            # than one repeated ten times.
            $descriptor = $state.key
            $visible = if ($state.visible) { '1' } else { '0' }

            if (-not $seen.ContainsKey($descriptor)) { $seen[$descriptor] = $true }
            if ($visible -ne '1') { $invisible++ }

            if ($descriptor -eq $lastDescriptor) {
                $stuck++
                if ($stuck -gt $maxStuck) { $maxStuck = $stuck }
            } else { $stuck = 0 }
            $lastDescriptor = $descriptor

            if ($seen.Count -ge $expected -and $i -gt $expected) { break }
        }

        # Name whatever Tab never reached. Every element that took focus was stamped with
        # data-tab-seq, so the focusable set minus the stamped set is exactly the shortfall -
        # far more useful than "reached 18 of 19".
        $unreached = Eval @'
(function(){
  var list = Array.prototype.slice.call(document.querySelectorAll(
    'a[href], button:not([disabled]), input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
  )).filter(function(el){
    var cs=getComputedStyle(el);
    if(cs.display==='none'||cs.visibility==='hidden') return false;
    // Content inside a collapsed <details> is not rendered and not tabbable. That is
    // correct behaviour, not an unreachable control: the <summary> is in the tab order,
    // and opening it reveals what is inside. Counting the hidden inputs as "should be
    // reachable" would fail the page for doing the right thing.
    var d=el.closest('details');
    if(d&&!d.open&&el.tagName!=='SUMMARY') return false;
    var r=el.getBoundingClientRect();
    if(r.width===0&&r.height===0&&!el.classList.contains('skip-link')) return false;
    return !el.hasAttribute('data-tab-seq');
  }).map(function(el){
    return el.tagName+(el.id?('#'+el.id):'')+
           (el.className&&typeof el.className==='string'?('.'+el.className.trim().split(/\s+/)[0]):'')+
           ' tabindex='+(el.getAttribute('tabindex')||'default')+
           ' "'+(el.textContent||el.value||'').trim().slice(0,24)+'"';
  });
  return list.join(' ;; ');
})()
'@

        Check "$($step.Name): all $expected controls reachable by Tab" ($seen.Count -ge $expected) "reached $($seen.Count) - never focused: $unreached"
        Check "$($step.Name): focus ring always drawn" ($invisible -eq 0) "$invisible stop(s) with no visible focus"
        Check "$($step.Name): no keyboard trap" ($maxStuck -lt 3) "focus stuck on one element $maxStuck times"
    }

    ''
    '--- progressive disclosure is keyboard-operable ---'

    # Find Tutor keeps "Qualification" and "Minimum experience" inside a collapsed <details>.
    # Those controls are therefore not tabbable until it is opened - which is only acceptable
    # if the disclosure itself can be opened from the keyboard. Proving that is the whole
    # point: otherwise two filters would be permanently out of reach.
    Resize 1440
    Go "$base/Student/Tutors/FindTutor.aspx" 'FindTutor.aspx' | Out-Null

    $hidden = Eval "(function(){var d=document.querySelector('details.filter-more');return d?(d.open?'open':'closed'):'absent';})()"
    Check 'the extra filters start collapsed' ($hidden -eq 'closed') $hidden

    Eval "(function(){document.body.focus();return 1;})()" | Out-Null
    $onSummary = $false
    for ($i = 0; $i -lt 25; $i++) {
        Tab
        $tag = Eval "(function(){var a=document.activeElement;return a?a.tagName:'';})()"
        if ($tag -eq 'SUMMARY') { $onSummary = $true; break }
    }
    Check 'the disclosure summary is reachable by Tab' $onSummary

    if ($onSummary) {
        Enter
        Start-Sleep -Milliseconds 300
        $opened = Eval "(function(){var d=document.querySelector('details.filter-more');return d&&d.open;})()"
        Check 'Enter opens the extra filters' ($opened -eq $true)

        if ($opened -eq $true) {
            $reachable = $false
            for ($i = 0; $i -lt 6; $i++) {
                Tab
                $id = Eval "(function(){var a=document.activeElement;return a&&a.id?a.id:'';})()"
                if ($id -like '*txtQualification*') { $reachable = $true; break }
            }
            Check 'the revealed filters then take focus' $reachable 'txtQualification never focused'
        }
    }

    ''
    '================ AT-28  Contrast ================'

    # Auditing the rendered page rather than the tokens in the abstract: what matters is the
    # pair a reader actually sees, after inheritance and after any alpha is composited.
    $contrastAudit = @'
(function(){
  function parse(s){
    var m=s.match(/rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)/);
    if(!m) return null;
    return {r:+m[1],g:+m[2],b:+m[3],a:m[4]===undefined?1:+m[4]};
  }
  function over(fg,bg){
    if(fg.a>=1) return fg;
    return {r:fg.r*fg.a+bg.r*(1-fg.a), g:fg.g*fg.a+bg.g*(1-fg.a), b:fg.b*fg.a+bg.b*(1-fg.a), a:1};
  }
  function backdrop(el){
    var node=el;
    while(node&&node.nodeType===1){
      var c=parse(getComputedStyle(node).backgroundColor);
      if(c&&c.a>=0.999) return c;
      node=node.parentElement;
    }
    return {r:255,g:255,b:255,a:1};
  }
  function lum(c){
    var f=[c.r,c.g,c.b].map(function(v){v/=255;return v<=0.03928?v/12.92:Math.pow((v+0.055)/1.055,2.4);});
    return 0.2126*f[0]+0.7152*f[1]+0.0722*f[2];
  }
  function ratio(a,b){var l1=lum(a),l2=lum(b);return (Math.max(l1,l2)+0.05)/(Math.min(l1,l2)+0.05);}

  var fails=[], counted=0;
  var all=document.querySelectorAll('*');
  for(var i=0;i<all.length;i++){
    var el=all[i];
    // Only elements holding their own text; otherwise a wrapper is judged on a child's text.
    var own='';
    for(var j=0;j<el.childNodes.length;j++){ if(el.childNodes[j].nodeType===3) own+=el.childNodes[j].textContent; }
    own=own.trim();
    if(!own) continue;

    var cs=getComputedStyle(el);
    if(cs.display==='none'||cs.visibility==='hidden'||parseFloat(cs.opacity)===0) continue;
    var rect=el.getBoundingClientRect();
    if(rect.width===0||rect.height===0) continue;

    // Skip anything inside an aria-hidden subtree. Those are decorative glyphs - the star
    // characters - whose meaning is carried by real text beside them (.stars__value) and by
    // the sr-only phrase after them. This is an exemption, and it is recorded in
    // TestingPlan.md rather than left implicit: gold on white cannot reach 4.5:1 while
    // still being gold, so the rating is conveyed as text instead.
    if(el.closest('[aria-hidden="true"]')) continue;

    var bg=backdrop(el);
    var fgRaw=parse(cs.color);
    if(!fgRaw) continue;
    var fg=over(fgRaw,bg);

    var size=parseFloat(cs.fontSize);
    var weight=parseInt(cs.fontWeight,10)||400;
    var large=(size>=24)||(size>=18.66&&weight>=700);
    var need=large?3:4.5;
    var r=ratio(fg,bg);
    counted++;
    if(r<need-0.005){
      fails.push([el.tagName+(el.className&&typeof el.className==='string'?('.'+el.className.trim().split(/\s+/)[0]):''),
                  own.slice(0,28), Math.round(r*100)/100, need, Math.round(size)+'px/'+weight].join(' ~ '));
    }
  }
  return counted+' ~~ '+fails.join(' ;; ');
})()
'@

    $contrastPages = @(
        @{ User = ''; Url = '/Account/Login.aspx' },
        @{ User = ''; Url = '/Account/Register.aspx' },
        @{ User = 'sinesipho_t'; Url = '/Student/Dashboard.aspx' },
        @{ User = 'sinesipho_t'; Url = '/Student/Tutors/FindTutor.aspx' },
        @{ User = 'sinesipho_t'; Url = '/Student/Requests/MyRequests.aspx' },
        @{ User = 'sinesipho_t'; Url = '/Student/Bookings/MyBookings.aspx' },
        @{ User = 'nhlawulo_m'; Url = '/Tutor/Dashboard.aspx' },
        @{ User = 'nhlawulo_m'; Url = '/Tutor/Requests/StudentRequests.aspx' },
        @{ User = 'admin'; Url = '/Admin/Dashboard.aspx' },
        @{ User = 'admin'; Url = '/Admin/Users/ManageUsers.aspx' },
        @{ User = 'admin'; Url = '/Admin/Reviews/ManageReviews.aspx' },
        @{ User = 'admin'; Url = '/Admin/Audit/AuditLog.aspx' }
    )

    $currentUser = ''
    $totalChecked = 0
    foreach ($p in $contrastPages) {
        if ($p.User -ne $currentUser) {
            if ($p.User -ne '') { SignIn $p.User | Out-Null }
            $currentUser = $p.User
        }
        Resize 1440
        $landed = Go "$base$($p.Url)" ([IO.Path]::GetFileName($p.Url))
        if ($p.User -ne '' -and $landed -like '*Login.aspx*') {
            Check "contrast $($p.Url)" $false 'session lost'
            continue
        }

        $result = Eval $contrastAudit
        $split = $result -split ' ~~ '
        $counted = [int]$split[0]
        $totalChecked += $counted
        $failures = if ($split.Count -gt 1 -and $split[1].Trim()) { $split[1] -split ' ;; ' } else { @() }

        Check ("{0,-42} {1,3} text pairs" -f $p.Url, $counted) ($failures.Count -eq 0) "$($failures.Count) below threshold"
        foreach ($f in $failures) { "          $f" }
    }

    Note "AT-28 examined $totalChecked rendered text/background pairs across $($contrastPages.Count) pages."

    $client.Dispose()
}
finally {
    if ($proc -and -not $proc.HasExited) { $proc.Kill() }
}

''
"PASSED $script:pass   FAILED $script:fail"
if ($script:fail -gt 0) { exit 1 }
