# TutorConnect — Security Checklist (SEC-01 … SEC-12)

Phase 9 gate: each requirement evidenced line by line. "Evidence" means a file and a
behaviour that was observed, not an intention.

---

## SEC-01 — Password hashing

**Met.** `App_Code/Security/PasswordHelper.vb`.

PBKDF2 via `Rfc2898DeriveBytes` with `HashAlgorithmName.SHA256`, **100 000 iterations**, a
**16-byte** cryptographically random salt per user, and a **32-byte** subkey.
`HashIterations` is stored per row so the work factor can be raised later without
invalidating existing accounts.

*Evidence:* exercised at runtime, not merely compiled — hash 32 bytes, salt 16 bytes,
iterations 100 000, correct password verifies, wrong password rejected, and the same
password hashed twice produces different output because the salts differ.

**Two corrections to the supplied specification, both established by compiling:**

| Claim in the supplied documents | Reality on .NET Framework 4.8.1 |
|---|---|
| The SHA-256 overload's availability is "unresolved between sources" | **It compiles.** No PBKDF2-SHA1 fallback needed. |
| `CryptographicOperations.FixedTimeEquals` is available on ".NET 4.8+" | **It does not exist** on .NET Framework — .NET Core 2.1+ only |

The constant-time comparison is therefore written out in `PasswordHelper.FixedTimeEquals`:
it accumulates the XOR of every byte pair and tests once at the end, never returning early.
An early return would make the running time reveal how many leading bytes an attacker had
guessed.

---

## SEC-02 — No secrets in source

**Met.** Verified by pattern search across `.vb`, `.aspx`, `.ascx`, `.config`, `.js`, `.sql`:
no hard-coded password, connection secret or API key.

- Connection string in `Web.config` `<connectionStrings>`, using `Integrated Security` so it
  carries no password at all.
- `Web.Release.config` replaces it at publish time and documents
  `aspnet_regiis.exe -pe "connectionStrings" -app "/TutorConnect"`.
- The development seed password lives in `App_Data/local.settings.config`, referenced by
  `<appSettings file="…"/>` and **git-ignored**; the committed `.sample` holds a placeholder.
- The original schema's `HASHED_PW_1/2/3` placeholders were discarded, never migrated.

---

## SEC-03 — Parameterised SQL

**Met.** Every command uses `DatabaseHelper.AddParameter` with an explicit `SqlDbType` and,
for variable-length types, an explicit size. Sizes are not left to inference: letting
ADO.NET size a parameter from the current value produces a different plan per input length
and thrashes the plan cache.

No string concatenation of user input into SQL exists anywhere in the codebase. Filters are
expressed as `(@Param IS NULL OR column = @Param)` rather than assembled conditionally.

*Evidence:* every statement was executed or compiled against the live schema before use —
which is how the `Msg 8117 datetime2` defect was found in `MarkCompletedAsync` while the VB
still compiled perfectly.

---

## SEC-04 — Role authorisation at the URL

**Met.** `Web.config` `<location>` blocks for `Student`, `Tutor`, `Admin` and
`Notifications`, each `<allow roles="…"/>` followed by `<deny users="*"/>`. **The trailing
deny is required** — without it any authenticated user passes.

These only work because `Global.asax` `Application_PostAuthenticateRequest` rebuilds a
`GenericPrincipal` carrying the role from the ticket on every request. Without that step the
rules match nothing and **every role check fails open**, which is why it was verified rather
than assumed.

*Evidence:* a signed-in student requesting `/Tutor/Dashboard.aspx` receives
`302 → /Account/Login.aspx?ReturnUrl=…`; a student is refused `/Admin/**`; an administrator
is refused `/Student/**`; anonymous requests to all three areas redirect to Login.

---

## SEC-05 — Authorisation at the record

**Met.** Every service method takes the acting identity from the authentication ticket,
never from a URL or a form field (BR-24). `StudentPageBase` and `TutorPageBase` resolve
`StudentId`/`TutorId` from `CurrentPersonId`, so no page can do it differently.

Ownership is re-checked on POST, not only on GET, and writes are scoped in the SQL itself —
`DELETE … WHERE StudentSubjectId = @Id AND StudentId = @StudentId` — so a tampered
identifier changes nothing rather than being caught by a check that could be raced.

A record belonging to someone else returns **the same message as one that does not exist**,
so an id cannot be probed for existence.

*Evidence:* another student cannot open a booking; another tutor cannot answer a request,
delete a slot, or publish availability against a foreign subject; a student cannot review
another student's completed booking.

---

## SEC-06 — CSRF

**Met.** `Security/BasePage.vb` sets `ViewStateUserKey` in `OnInit`, combined with
`<pages enableEventValidation="true" viewStateEncryptionMode="Always" enableViewStateMac="true"/>`.

The key is a GUID stored **in** the session, not `Session.SessionID`. ASP.NET does not
persist a session until something is written to it, and reading `SessionID` does not count —
so with an empty session no cookie is issued, every request gets a new identifier, and the
ViewState is signed with one value and validated against another. That produced
*"Validation of viewstate MAC failed"* on **every postback in the application**. Writing the
GUID is what materialises the session and makes the value stable.

*Evidence:* the cookie is now issued (`ASP.NET_SessionId=…; secure; HttpOnly; SameSite=Lax`)
and postbacks succeed. Independently, a ViewState minted in one session and posted from
another was **correctly rejected** — the protection demonstrably works.

---

## SEC-07 — Cookies and session

**Met.** `<forms requireSSL="true" protection="All" timeout="30" slidingExpiration="true">`
and `<httpCookies httpOnlyCookies="true" requireSSL="true"/>`.

Session identifier rotation on sign-in is implemented in `RoleHelper.RegenerateSessionId`
using `SessionIDManager`. `Session.Clear()` alone empties a session but **keeps its id**, so
an identifier planted in the victim's browser before sign-in would still be valid
afterwards — that is session fixation, and clearing does not close it.

*Evidence, measured rather than assumed:* the session cookie before and after a real sign-in
changed from `oasj40kmz5ndljktdm3whl24` to `dsdhdocoezodfxrnvov155er`.

*Consequence worth knowing:* because cookies are `requireSSL`, this application **cannot be
exercised over plain HTTP** — the browser will not return the cookie, and every postback
fails. That is correct behaviour, not a defect.

---

## SEC-08 — Brute-force resistance

**Met.** Lockout after **5 failures in 15 minutes** via `Person.FailedLoginCount` and
`LockoutEndUtc`, applied in a single `UPDATE` so concurrent attempts cannot both read a
stale counter.

A locked account returns **the generic failure message**, identical to a wrong password and
to an unknown username. Revealing the lockout would confirm the username exists.

*Extended in Phase 9:* sign-in lockout is per account, but registration has no account to
lock. `Security/RateLimiter.vb` limits registration to 5 attempts per client address per 15
minutes. Its limits are stated honestly in its own remarks: in-process, resets on recycle,
does not span a farm, and the key can be varied — it raises the cost of casual abuse, it is
not a defence against a distributed one.

Also closed: registration previously said *"An account already exists for that email
address"*, which let anyone test an address and learn whether that person was a member.
Username and email clashes now return one shared message.

*Evidence:* after five failures `LockoutEndUtc` is set, the **correct** password is then
refused, and the message is byte-identical to the wrong-password one.

---

## SEC-05a — First-run setup

**Met.** `Account/Setup.aspx` creates the administrator account on an installation that has
none, and is the only route by which an administrator can come into existence without one
already existing. It is therefore worth stating exactly what stops it being a back door.

The check is `UserRepository.AdministratorExistsAsync`, and it is applied **in the service
on POST**, not only on the page's GET. Hiding a form is presentation; a form that has been
hidden can still be submitted. `AuthenticationService.CreateFirstAdministratorAsync`
refuses before it validates anything, so a submission to a closed setup page cannot even
learn what the form would have accepted.

Two decisions worth recording:

- **A deactivated administrator still counts.** Treating the system as unconfigured because
  the only administrator was switched off would let anyone create one for themselves.
- **No session is issued.** On success the user is redirected to Login and signs in with
  the credentials they just chose, rather than being handed a session they never
  authenticated for.

The password is hashed by the same `PasswordHelper` as every other account — the setup path
does not have a weaker one.

*Evidence, over HTTPS against IIS Express on a database built from the scripts alone:*
Login redirected to Setup; the form created the account; the redirect landed on
`Login.aspx?setup=1`; the new credentials signed in and reached `/Admin/Dashboard.aspx`;
returning to Setup showed the closed message with no form; and a second call to the service
was refused. 13 checks, all passed. Alongside them, `FirstRunSetupTests` covers the same
ground in MSTest, including the direct-POST refusal.

---

## SEC-09 — Output encoding

**Met.** Every dynamic value is rendered with `<%: %>` or an `<asp:Literal Mode="Encode">`.
`<%= %>` is not used for user data anywhere.

The highest-risk fields are handled deliberately: a tutor's **bio**, a student's **review
comment**, a **decline reason**, and a **moderation reason** are all encoded. Error pages
never echo the requested URL — reflecting a path onto an error page is a classic injection
vector.

The one place raw markup is emitted is authored strings the application itself owns: inline
SVG icons, star glyphs, and status badges built from integers. No user input reaches them.

---

## SEC-10 — Uploads

**Not applicable.** No upload exists. Avatars are rendered as initials in a coloured circle,
which also avoids a broken-image frame when someone has no photograph. Should uploads be
added later, the requirement stands in full: whitelist `.jpg`/`.png`, ≤ 2 MB, verify magic
bytes, re-encode, rename to a GUID, and store where execution is disabled.

---

## SEC-11 — Error handling

**Met.** `<customErrors mode="RemoteOnly">` locally, transformed to `mode="On"` for a
published build by `Web.Release.config` — `RemoteOnly` still shows the yellow screen to
anyone browsing *from the server*, which on a deployed machine is not the developer.

`App_Code/Utilities/ErrorLogger.vb` writes a timestamped entry to `App_Data/Logs`, a folder
IIS never serves. **The reference shown to the user is the one written to the log**;
previously `Error500.aspx` minted its own GUID that appeared in no log, which is worse than
showing none. Logs are pruned after 30 days.

Never logged: form values, cookies, ViewState, or any header that could carry a credential.
A log that captures a login POST has become a password file.

404 is handled in `Global.asax` rather than by `customErrors`, so a missing page stays
friendly even locally while genuine 500s keep their stack trace for the developer.

*Evidence:* a real fault was forced (a `Notification` row with an unmapped type, which
`EnumHelper` refuses by design). It produced reference `ED4218C06261` in
`errors-2026-08-28.log` with the request path and acting user, and the log was checked to
contain no `Password`, no `__VIEWSTATE`, and not the seed password. The underlying cause —
a missing `CHECK` constraint on `Notification.NotificationType` — has since been fixed, so
that value can no longer be written at all.

---

## SEC-12 — Transport

**Met.** HTTPS throughout, with `Strict-Transport-Security: max-age=31536000; includeSubDomains`
now sent from `<system.webServer><httpProtocol>`. Cookies already refused to travel over
HTTP; HSTS stops the browser attempting HTTP in the first place.

Added alongside it, each for a specific reason:

| Header | Reason |
|---|---|
| `X-Frame-Options: DENY` | The application never frames itself; clickjacking |
| `X-Content-Type-Options: nosniff` | Stops a browser second-guessing a declared type |
| `Referrer-Policy: same-origin` | A booking URL should not leak to a third party |
| `Permissions-Policy` | Nothing here uses a camera, microphone or location |
| `X-Powered-By` removed | Announcing the version helps only an attacker |

---

## Residual risks, stated rather than hidden

| Risk | Position |
|---|---|
| **No `<machineKey>`** | `AutoGenerate,IsolateApps` is stable for one server but invalidates every ticket and ViewState on an app-pool identity change, and breaks on a web farm. A static key is a secret and must not be committed — it belongs in the encrypted section at deployment. |
| **`InProc` session state** | Sessions and the CSRF key are lost on recycle, signing everyone out. Acceptable for a single instance; a farm needs a shared provider. |
| **Rate limiting is in-process** | See SEC-08. Adequate here, not a distributed defence. |
| **`App_Data/local.settings.config` exists on disk** | Correctly git-ignored, but a file-copy deployment would carry a development password to the server. Exclude it explicitly. |
| **No email delivery** | Password reset is manual by design (Part N puts email out of scope). Documented on `ForgotPassword.aspx` rather than presented as a form that silently does nothing. |
