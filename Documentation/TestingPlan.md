# TutorConnect — Testing Plan

Three layers, because none is sufficient alone.

| Layer | What it proves | Where |
|---|---|---|
| **MSTest integration tests** | The business rules — 79 tests, 201 assertions | `Tests/TutorConnect.Tests/` |
| **Browser checks over HTTPS** | The pages — 35 checks | `Tests/Verification/Verify-Web.ps1` |
| **Acceptance checks** | Responsive, keyboard and contrast — 179 checks (AT-26, AT-27, AT-28) | `Tests/Verification/Verify-Acceptance.ps1` |
| **Fresh install** | AT-30, from an empty SQL Server — 23 checks | `Tests/Verification/Verify-FreshInstall.ps1` |
| **Part 12 walkthroughs** | Cancellation and postponement end to end — 29 checks | `Tests/Verification/Verify-Phase12.ps1` |

The split is not tidiness. It comes from two defects found in this project that the other
layer could not have caught:

- **`ViewStateUserKey = Session.SessionID`.** The build was clean and every service-level
  assertion passed while *every postback in the application* failed with "Validation of
  viewstate MAC failed". Reading `SessionID` does not persist a session, so the key changed
  per request.
- **`Focus()` inside a registered async task.** A registered async task runs *after*
  PreRender, and `Page.SetFocus` is only legal before and during it. Every failed sign-in
  therefore returned HTTP 500 instead of "Incorrect username or password" — while the
  AT-05 rule tests passed, because the rule was never the problem.

Neither is visible from a service call. Both are visible in one HTTP request.

---

## Layer 1 — the MSTest project

### What it is

`Tests/TutorConnect.Tests/TutorConnect.Tests.vbproj`, an SDK-style VB.NET project
targeting `net481` with `Option Strict On`. SDK-style so that `PackageReference` restores
MSTest without a `nuget.exe` on the machine; the application stays a classic Web
Application Project and the two coexist in one solution.

The application is referenced as a **built assembly** (`..\..\bin\TutorConnect.dll`), not
by `ProjectReference`: a Web Application Project is not designed to be consumed as a
library. Build the solution first; the tests then run against exactly what was built.

### These are integration tests, deliberately

They run against a real SQL Server. That is a choice, not a shortcut. The rules under test
are enforced *partly by the database* — the role-integrity triggers (BR-04), the one
review per booking constraint (BR-17), and the `SERIALIZABLE` + `UPDLOCK, HOLDLOCK`
transaction that makes concurrent acceptance safe (BR-14). Mocking `SqlConnection` would
test the mock.

`TestDatabase` rebuilds the schema from `01_CreateSchema.sql` and seeds it once per run,
in `<AssemblyInitialize>`. **The connection string must point at a disposable database.**

### Independence

The original console harnesses were one long script: the request created for AT-09 became
the booking completed by AT-18 and reviewed by AT-19. That reads well and fails badly — a
single early failure cascades, and no test can be run on its own.

Each test here builds its own fixture through `TestScenario`. That reintroduces one
problem: several tests book the same student with the same tutor, and BR-12 forbids a
student holding two overlapping bookings. Rather than order the tests carefully — which is
the coupling again, wearing a different hat — every slot comes from
`TestScenario.NextFreeDate()`, which hands out a distinct future date per call. Two tests
cannot collide whatever order they run in.

Tests that switch something off (approval, a user's active flag) restore it in
`<TestCleanup>` or a `Finally`, so one real failure stays one failure.

> VB forbids `Await` inside `Finally`, so those restores use `.GetAwaiter().GetResult()`.
> They are single indexed updates with no synchronisation context to deadlock against.

### Coverage

| Class | Covers |
|---|---|
| `CancellationTests` | Part 12.1, 12.3, 12.6 — cancelling with a reason by all three roles, who is notified, the audit record, and the inactive-subject guards |
| `PostponementTests` | Part 12.4, 12.5, 12.7 — proposing, approving, declining, withdrawing, administrator rescheduling, conflict refusal, and two concurrent approvals |
| `AuthenticationTests` | AT-01 … AT-06, SEC-01, SEC-08, BR-04, BR-26 |
| `StudentWorkflowTests` | AT-08 … AT-13, AT-17 … AT-22, BR-05, BR-11, BR-12, BR-13, BR-16, BR-17, BR-24, BR-27 |
| `TutorWorkflowTests` | AT-14 … AT-16, AT-23, BR-06, BR-07, BR-15, BR-20, BR-21, BR-22, BR-24 |
| `AdministratorTests` | AT-23 … AT-25, BR-25, review moderation, the administrator's read-only views, and Part 12.7 — every administrator mutation writes an audit record naming who did it |
| `FirstRunSetupTests` | First-run administrator creation, and that it closes behind itself |

Three of these deserve a note.

**AT-15, the race.** Two students hold pending requests for one slot; the tutor accepts
both at the same instant. The two accepts are genuinely started before either is awaited —
awaiting them in turn would pass without proving anything. Exactly one must win, and the
loser must be given a reason. A read-then-write with no transaction passes every
sequential test and still double-books here.

**Review moderation.** Hiding a review must remove it from *every* public surface — the
tutor's profile, the average rating, and the rating shown in Find Tutor — while its author
still sees it, marked hidden, with the reason. Concealing it from the person who wrote it
would be dishonest.

**First-run setup.** The success path needs an installation with no administrator, which
the seeded database is not. `TestDatabase.UsingEmptyInstallationAsync` rebuilds from the
scripts alone, runs the test, then restores the seeded state in `Finally`. The alternative
— a settable connection string on `DatabaseHelper` — would mean putting a mutable static
into shipping code so a test could reach it.

### Running it

```
msbuild TutorConnect.sln /p:Configuration=Debug

"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" ^
    "Tests\TutorConnect.Tests\bin\Debug\net481\TutorConnect.Tests.dll" ^
    /Framework:.NETFramework,Version=v4.8.1
```

Or Test Explorer in Visual Studio.

**Last run: 110 passed, 0 failed.** Run twice in succession to confirm the rebuild-and-seed
really is idempotent — it was.

### Part 12 — the walkthroughs

```
.\Tests\Verification\Verify-Phase12.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!'
```

**Last run: 29 passed, 0 failed.** Drives the real screens as three people in turn — student,
tutor, administrator — and asserts every outcome after a **new sign-in**, because Part 12 is
explicit that a feature is not complete merely because the interface showed it.

Three harness bugs were found and fixed while writing it, and they are worth recording
because each one produced a convincing-looking failure against correct code:

- The site was not warmed before the first assertion. The first request after a rebuild
  recompiles the page, and posting an empty `__VIEWSTATE` to whatever came back produced
  *"The state information is invalid"* — which reads as a ViewState defect and is not one.
- `<%# %>` does not HTML-encode, so an `href` carries a bare `&`, not `&amp;`.
- A `LinkButton` renders as an anchor calling `__doPostBack`, with the apostrophes around
  its target **encoded as `&#39;`** because they sit inside an `href`. There is no `name=`
  attribute to find.

A fourth was worse than a failure: two assertions searched the whole page for the new date,
which also matched the *proposed* range shown in the postponement panel — so they passed
while the booking had not moved at all. They are now anchored to the booking's own `<dt>Date</dt>`
and `<dt>Time</dt>` rows.

### One defect the tests caught that the walkthrough had missed

Administrator cancellation was written, and reported as working, but **never worked**:
`GetForViewerAsync` admitted only the owning student and the assigned tutor, so an
administrator was a stranger to every booking. Nothing caught it because no test exercised
it and the walkthrough only had the administrator *view* a cancellation the student had made.

It surfaced later, indirectly: three administrator-rescheduling tests failed, all saying the
change had done nothing, while the three matching **negative** tests passed. A feature that
always fails passes every test that asserts refusal — that asymmetry is the signature, and
it is worth looking for whenever the negative cases are the only green ones.

The lesson for the suite: **a permission is not tested until the role that needs it is the
one being tested.** Two tests now cover it directly.

### Two more found by reading the code against its own UI

Neither would have failed a test, because no test existed for either.

- **`AdminService` wrote no audit records at all**, while the Audit Log page offered filters
  for tutor approval, account activation and review moderation. A filter that can never
  match is the same defect as a button that does nothing. Every administrator mutation is
  now audited, and four tests assert the records exist with a named actor.
- **A tutor had no way to cancel a session.** The service supported it and the tests proved
  the service worked — but no screen called it for a tutor, so the feature was unreachable
  for that role. A service test passing does not mean a user can reach the behaviour.

### An error report that destroyed itself

A seeding failure once surfaced as a bare `SqlTransaction.ZombieCheck` stack with nothing in
it about the actual cause. `UserRepository.CreateAccountAsync` was calling
`transaction.Rollback()` unguarded inside its `Catch`. When SQL Server has already doomed a
transaction, `Rollback` throws *"This SqlTransaction has completed"* — and thrown from inside
the `Catch`, it **replaces** the exception being handled. The real failure never reaches the
log.

The rollback is now guarded, and every other catch-block rollback in the codebase was
audited at the same time (only three files use transactions; the rest were already guarded).

Worth remembering as a shape rather than a one-off: **cleanup code that can throw, placed in
a `Catch`, will hide the fault it was meant to report.** This is the third variant of it in
this project — the first crashed every second accept, the second was this.

---

## Layer 2 — browser checks

`Tests/Verification/Verify-Web.ps1`, against a running instance:

```
.\Tests\Verification\Verify-Web.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!'
```

It must be the **https** binding. Cookies are `requireSSL`, so over plain HTTP the browser
never returns them and every postback fails — correct behaviour, not a defect.

| Group | Checks |
|---|---|
| SEC-12 | HSTS, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `X-Powered-By` removed |
| SEC-07 | The `ASP.NET_SessionId` value is captured before and after a real sign-in and asserted to have **changed** — session fixation is not closed by `Session.Clear()`, which keeps the identifier |
| AT-05 | A wrong password returns 200 with the message on the form, not an error page |
| SEC-04 | A student is refused `/Admin/**` and `/Tutor/**`; an administrator is refused `/Student/**`; anonymous is refused `/Notifications/**` |
| Rendering | All 20 role pages return 200 with no `Server Error in` |

The GET and the POST share one `WebSession`. That matters: ViewState is signed against the
session's key, so minting it in one session and posting from another is correctly
rejected — which was itself proved once, by a broken test harness that did exactly that.

**Last run: 35 passed, 0 failed.**

---

## Layer 3 — acceptance checks: AT-26, AT-27, AT-28

`Tests/Verification/Verify-Acceptance.ps1`, driven through the Chrome DevTools Protocol
against a running site:

```
.\Tests\Verification\Verify-Acceptance.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!'
```

**Last run: 179 passed, 0 failed.**

### AT-26 — responsive

24 pages (the 20 role pages plus the four on `Auth.Master`) at 1440 / 1024 / 768 / 375.

- **No horizontal scroll** anywhere: `scrollWidth - clientWidth <= 1` at every size.
  The auth pages are checked separately on purpose — `Auth.Master` loads `colors.css`,
  `typography.css` and `auth.css` but **not** `responsive.css`, so none of the app-shell
  breakpoints apply to them.
- **Tables stack below 768**: 1,394 rendered cells across the 15 pages that have a table,
  every one carrying a non-empty `data-label` and none still `display: table-cell`. The
  check reads the rendered DOM rather than the source, because those cells come from
  `Repeater` templates — a template that had been missed would be invisible to a search of
  the markup.
- **Drawer**: opens, moves focus to the close button, sets `aria-expanded`, closes on
  Escape, returns focus to the trigger, closes on the backdrop, and clears itself when the
  viewport grows past the breakpoint.

> The drawer is exercised at **1023 / 768 / 375**, not 1024. `responsive.css` hides the
> hamburger at `min-width: 1024px`, so at exactly 1024 the sidebar is fixed and on screen —
> at 1024 and 1440 the script asserts that instead. An earlier version tested 1024 as a
> drawer width and reported three failures against entirely correct code.

### AT-27 — keyboard-only

Real key events via `Input.dispatchKeyEvent`, through login → search → request → review.

- Sign-in is performed **by keyboard alone**: Tab to the username, Tab to the password, Tab
  to the button, Enter to submit.
- On each page in the flow, every focusable control is reached by tabbing from the top of
  the document, every stop draws a visible focus ring, and no element holds focus across
  repeated presses.
- **Progressive disclosure**: Find Tutor keeps "Qualification" and "Minimum experience"
  inside a collapsed `<details>`. Their being untabbable while collapsed is correct browser
  behaviour, not an unreachable control — so the script proves the disclosure itself is
  keyboard-operable: Tab to the `<summary>`, Enter to open, and the two inputs then take
  focus.

> `Enter` needs `type: 'keyDown'` with `text: "\r"`. `rawKeyDown` alone moves focus but
> never activates a submit control, so the form silently fails to post.

### AT-28 — contrast

Every element holding its own text is measured against its nearest opaque ancestor
background, with any alpha composited first. Threshold 4.5:1, or 3:1 for text ≥ 24 px or
bold ≥ 18.66 px. **821 text/background pairs across 11 pages.**

The first run found five real defects. All are fixed; measured before and after:

| Element | Pair | Before | After |
|---|---|---|---|
| `.btn--success` ("Accept") | white on `--green-600` | 3.28 | **5.01** — fill darkened `#22A34A` → `#1C8038` |
| `.btn--danger` ("Decline", "Cancel Request", "Deactivate", "Hide") | white on `--red-600` | 4.05 | **4.78** — fill darkened `#E4452A` → `#D33A1E` |
| `.bell__count` | white on `--red-600` | 4.05 | **4.78** — follows the token |
| `.sidebar__count`, `.dash-card__count` | on `--orange-500` | 2.99 | **5.34** — text changed to `--navy-900`; the sampled orange is unchanged |
| `.stars__on` / `.stars__off` | gold / grey on white | 1.58 / 1.23 | exempt — see below |

**The one exemption, stated rather than buried.** Gold on white cannot reach 4.5:1 and
still be gold. The star glyphs are therefore treated as decoration: every renderer wraps
them in `aria-hidden="true"`, and the rating is carried by real text — a visible
`.stars__value` ("4/5") beside them and an `sr-only` "4 out of 5" after them, both of which
pass comfortably. The audit skips `aria-hidden` subtrees for that reason. An auditor who
insists the glyphs are text will still count them; the rating is available as text either
way, which is the point of the requirement.

---

## Layer 4 — fresh install: AT-30

`Tests/Verification/Verify-FreshInstall.ps1`. **Destructive** — it drops the database, so it
is a separate script and refuses to run without `-Confirm`.

```
.\Tests\Verification\Verify-FreshInstall.ps1 -BaseUrl https://localhost:44306 -Password 'TutorDev2026!' -Confirm
```

**Last run: 23 passed, 0 failed.**

1. `DROP DATABASE` — after `SET SINGLE_USER WITH ROLLBACK IMMEDIATE`, which evicts anything
   still connected and would otherwise block the drop indefinitely.
2. `01_CreateSchema.sql` then `02_SeedLookups.sql`, asserting the script **created** the
   database rather than reusing one. That branch had never actually run before: every
   previous execution in this project reported *"Database TutorConnect already exists"*.
3. Schema shape: 13 tables, exactly 2 cascading foreign keys, subjects present, and
   `dbo.Person` **empty** — the scripts create no people.
4. The seeder, driven through the test project's `<AssemblyInitialize>` rather than by adding
   a runner that would exist only for this script.
5. All four seed accounts sign in over HTTPS and land on the dashboard for their role.

> **A real operational fact this uncovered:** dropping the database underneath a running
> application leaves its connection pool holding handles to a database that no longer
> exists, and the first requests afterwards fail. ADO.NET discards a broken connection when
> it fails, so the site recovers after a few attempts — the script waits for that rather
> than reporting it as a defect, and the same cold-start effect briefly showed up as three
> spurious `Verify-Web.ps1` failures immediately after a rebuild.

---

## Manually verified, once

Some things are checked by hand because automating them would cost more than it returns.

| Check | Result |
|---|---|
| First-run setup end to end over HTTPS — Login redirects to Setup, the form creates the administrator, Setup closes, the new credentials sign in and reach the dashboard | 13 checks, all passed |
| Layout at 1440 px, 1024 px, 768 px and 375 px | **Now automated** — see AT-26 in Layer 3. Measured through the Chrome DevTools Protocol against the live DOM, **not** by scanning `--window-size` screenshots: headless Chrome lays out wider than the window and then clips, which produced a "defect" that did not exist |
| `ErrorLogger` | A fault was forced; the reference shown on the 500 page appeared in `App_Data/Logs`, and the log contained no `Password`, no `__VIEWSTATE` and not the seed password |
| Schema re-runnability | `01_CreateSchema.sql` run twice; 13 tables and exactly two cascading foreign keys both times |

---

## Known gaps in the testing

Stated rather than hidden.

- **No unit tests in the strict sense.** Everything touches the database. The validators
  and `PasswordHelper` could be tested in isolation and only `PasswordHelper` currently is.
- **No load or performance testing.** The pagination and the indexes are reasoned about,
  not measured.
- **Accessibility testing covers what AT-26 to AT-28 ask for, not everything.** Tab order,
  focus visibility, keyboard traps and text contrast are now measured. Screen-reader
  announcement, heading structure, landmark roles and form-error association are still
  judged by reading the markup rather than by a tool or a real assistive technology.
- **The race test is probabilistic in principle.** It passes consistently and would fail
  reliably if the locking were removed, but two tasks on one machine are not a proof of
  correctness under real concurrency.
- **Browser checks are single-browser.** Chrome and IIS Express only.
