# TutorConnect — Final Report

**Information Systems 3 · Walter Sisulu University**
Tshangela Sinesipho (231504160) · Maluleke Nhlawulo (231042353)

ASP.NET Web Forms (VB.NET) on .NET Framework 4.8.1, SQL Server 2022 Express, no ORM,
`Option Strict On` throughout.

---

## 1. What was built

A tutoring management system for three roles. The application opens at Login; there is no
public landing page.

| Area | Delivered |
|---|---|
| **Authentication** | Registration for students and tutors, sign-in by username *or* email, PBKDF2-SHA256 password hashing, account lockout, deactivation that ends live sessions, first-run administrator setup |
| **Student** | Find Tutor with filters and paging, tutor profiles, session requests, My Requests with cancellation, My Bookings, reviews, subjects of interest, profile |
| **Tutor** | Subjects taught, published availability, accept/decline requests with a reason, My Sessions, session detail, completion, profile |
| **Administrator** | Manage users with approval and activation, manage subjects and study levels, read-only oversight of bookings and requests, review moderation by hiding with a reason |
| **Shared** | Notification bell and list, dashboards, error pages with a real fault reference, print stylesheet, double-submit protection |
| **Session changes (Part 12)** | Cancelling a scheduled session with a reason by any of the three roles; student postponement proposals, withdrawable while pending; tutor approve/decline; administrator rescheduling as an audited override; an immutable audit log with its own administrator page; server-side guards against using a withdrawn subject |

### Against Part G of the specification

| Requirement | Status |
|---|---|
| G.1 Authentication and authorisation | **Complete.** One exception recorded: the specification prescribes `Request.IsUrlLocalToHost`, which does not exist on `HttpRequest` — it is an extension method in `System.Web.WebPages`, an assembly a Web Forms project does not reference. The check is written out in `Login.IsLocalUrl` and matches the same rules, including the protocol-relative and backslash forms a browser treats as absolute |
| G.2 Student workflow | **Complete** |
| G.3 Tutor workflow | **Complete** |
| G.4 Administrator | **Complete.** `ManageReviews` hides with a reason instead of deleting, which required four new columns on `dbo.Review` |
| G.5 Business rules BR-01 … BR-27 | **Complete**, and covered by tests |
| G.6 Security SEC-01 … SEC-12 | **Complete**, except SEC-10 (uploads), which is **not applicable** — the system has no upload. Evidenced line by line in `SecurityChecklist.md` |
| G.7 Acceptance tests AT-01 … AT-30 | **Complete**, with one stated exemption under AT-28 (the decorative star glyphs — the rating is carried by text instead) |
| AT-29 zero errors, zero warnings | **Met** |

### Two additions beyond the specification, both deliberate

**`AdminService`.** Appendix B lists no administration service, and none of the services it
does list owns "manage users". Pages must not call repositories directly, so one was added
rather than breaking the layering.

**`Account/Setup.aspx`.** The schema scripts create no people, and registration refuses to
create an administrator. Without this page a correctly installed system has no way in at
all — the installation instructions would have had to end by telling the reader to run the
test project, which is not a deployment step. It closes permanently once an administrator
exists, and the refusal lives in the service as well as the page.

---

## 2. Files, by layer

Layering is strict: `.aspx.vb` binds only, `Services/` holds the rules, `Data/` holds
parameterised SQL, SQL Server holds the constraints. No page opens a connection; no
repository decides a rule.

| Layer | Location | Count |
|---|---|---|
| Pages | `Account/`, `Student/`, `Tutor/`, `Admin/`, `Notifications/`, `ErrorPages/` | 31 `.aspx` + code-behind |
| Master pages | `MasterPages/` | 4 (Auth, Student, Tutor, Admin) + `AppMasterBase.vb` |
| User controls | `Controls/` | 5 |
| Services | `App_Code/Services/` | 10, plus `Interfaces.vb` and `ServiceResult.vb` |
| Repositories | `App_Code/Data/` | 8 + `DatabaseHelper.vb` |
| Security | `App_Code/Security/` | 5 (`PasswordHelper`, `BasePage`, `RoleHelper`, `RateLimiter`, identity) |
| Models | `App_Code/Models/` | 4 |
| Validation | `App_Code/Validation/` | 4 |
| Utilities | `App_Code/Utilities/` | 3 (including `ErrorLogger`) |
| Seeding | `App_Data/Seed/SeedRunner.vb` | 1 |
| SQL | `App_Data/Scripts/` | 2 |
| Styles / scripts | `Content/css/`, `Scripts/` | 11 CSS, 5 JS |
| Tests | `Tests/TutorConnect.Tests/`, `Tests/Verification/` | 7 `.vb`, 1 `.ps1` |

81 `.vb` files in the application, plus 7 in the test project.

---

## 3. Database

13 tables. `Person` carries the account and the role; `Student` and `Tutor` hold the
role-specific profile. The chain that matters runs
`TutorSubject → TutorAvailability → SessionRequest → Booking → TutoringSession → Review`.

Three decisions worth stating.

**Exactly two cascading foreign keys.** `Student → Person` and `Tutor → Person`. Everything
else is `NO ACTION`, so deleting a person who has bookings fails loudly instead of silently
taking the bookings with them.

**Role integrity is enforced by triggers, not by application code.** A `Person` cannot hold
both a `Student` and a `Tutor` row. Set-based `AFTER INSERT, UPDATE` triggers raise
`THROW 50001`–`50004`, so the rule holds for a write that never goes near the services.

**Overlap guards are `SERIALIZABLE` transactions with `UPDLOCK, HOLDLOCK`** (BR-12, BR-14,
BR-15), and the overlap test is half-open — a slot beginning exactly when another ends is
not a clash.

Phase 7 added review moderation columns (`IsHidden`, `HiddenReason`, `HiddenAt`,
`HiddenBy`); Phase 9 added `CK_Notification_Type`, which closed a demonstrated HTTP 500.

Full reasoning in `Documentation/DatabaseDesign.md`.

---

## 4. Setting it up

Summarised; the full procedure with verification steps is in
`Documentation/InstallationGuide.md`.

1. Run `App_Data/Scripts/01_CreateSchema.sql` then `02_SeedLookups.sql` against
   `.\SQLEXPRESS`. Expect 13 tables, exactly 2 cascading foreign keys, and **zero rows in
   `dbo.Person`**.
2. Point the `TutorConnectDb` connection string at the instance. It uses
   `Integrated Security`, so it holds no password.
3. Build, and browse to **https://localhost:44306** — the https binding, because cookies
   are `requireSSL` and postbacks fail over plain HTTP by design.
4. Login redirects itself to first-run setup. Create the administrator; setup then closes
   permanently.

Demonstration data is optional and comes from running the test project once, which rebuilds
and seeds the database.

### Test accounts

All share the `Seed:DevPassword` value from the git-ignored `App_Data/local.settings.config`.
They are development accounts and have no place on a deployed system.

| Username | Role |
|---|---|
| `admin` | Administrator |
| `sinesipho_t` (or `sines@gmail.com`) | Student, STU2026001 |
| `john_doe` | Student, STU2026002 |
| `akhona_n`, `luvo_m`, `thabo_j` | Students |
| `nhlawulo_m` | Tutor, approved, Information Systems (Third Year) |
| `lerato_m`, `andile_d` | Tutors, approved |

---

## 5. Test results

| Suite | Covers | Result |
|---|---|---|
| MSTest integration tests — 110 tests | AT-01 … AT-25, Part 12 | **110 passed, 0 failed** |
| `Verify-Phase12.ps1` | **Part 12.9 walkthroughs** | **29 passed, 0 failed** |
| `Verify-Web.ps1` | SEC-04, SEC-07, SEC-12, AT-05, page rendering | **35 passed, 0 failed** |
| `Verify-Acceptance.ps1` | **AT-26, AT-27, AT-28** | **179 passed, 0 failed** |
| `Verify-FreshInstall.ps1` | **AT-30** | **23 passed, 0 failed** |
| First-run setup, end to end on an empty installation | Setup.aspx | **13 passed, 0 failed** |
| Release build | AT-29 | **0 errors, 0 warnings** |

**All thirty acceptance tests pass**, with one exemption recorded in full under AT-28: the
star glyphs are gold on white at 1.58:1, which no gold can survive, so they are marked
`aria-hidden` and the rating is carried by visible text ("4/5") and an `sr-only` phrase
beside them. See `TestingPlan.md`.

The three accessibility tests found five genuine contrast defects, all fixed and re-measured:
the success and danger button fills were darkened (3.28 → 5.01 and 4.05 → 4.78 against
white), and the two count badges moved to navy text so the sampled orange could stay exactly
as it was (2.99 → 5.34).

**One defect the tests caught after I had already reported the feature as working.**
Administrator cancellation was written and described as delivered, but `GetForViewerAsync`
admitted only the owning student and the assigned tutor — so an administrator was a stranger
to every booking, and both cancellation and rescheduling failed for them. No test exercised
it, and the walkthrough only had the administrator *view* a cancellation someone else had
made. It surfaced when three administrator-rescheduling tests failed while the three matching
*negative* tests passed: a feature that always fails passes every test that asserts refusal.
Fixed, and covered directly now.

Worth recording about the process rather than the product: of the 24 failures in the first
acceptance run, **19 were defects in the test harness, not the application** — a `||`
delimiter that also occurred inside the data it delimited, a drawer tested at a width where
the drawer correctly does not exist, `Enter` dispatched in a form that does not activate a
submit control, and `$HOME` used as a variable name when it is read-only in PowerShell. Each
was diagnosed before anything was "fixed". Had they been taken at face value, five of them
would have produced changes to correct code.

Two layers, because neither is sufficient alone — a point this project proved twice at its
own expense:

- **`ViewStateUserKey = Session.SessionID`.** Every service-level assertion passed and the
  build was clean while *every postback in the application* failed with "Validation of
  viewstate MAC failed". Reading `SessionID` does not persist a session, so the key changed
  per request.
- **`Focus()` inside a registered async task.** A registered async task runs *after*
  PreRender, and `Page.SetFocus` is legal only before and during it. Every failed sign-in
  returned HTTP 500 instead of "Incorrect username or password" — while the AT-05 rule
  tests passed throughout, because the rule was never at fault. Found by submitting a wrong
  password to a real page; now covered by a permanent check.

`Documentation/TestingPlan.md` has the coverage map and the reasoning behind the test
design.

---

## 6. Known limitations

Stated rather than hidden.

| Limitation | Position |
|---|---|
| **No `<machineKey>`** | `AutoGenerate,IsolateApps` is stable on one server but invalidates every ticket and ViewState when the app-pool identity changes, and breaks on a web farm. A static key is a secret and belongs in the encrypted configuration section, not in source control |
| **`InProc` session state** | Sessions — and the CSRF key with them — are lost on recycle, signing everyone out. Fine for one instance; a farm needs a shared provider |
| **Rate limiting is in-process** | `RateLimiter` resets on recycle, does not span a farm, and is keyed on the client address. It raises the cost of casual abuse; it is not a defence against a distributed one |
| **No email** | Password reset is manual by design. The page says so instead of presenting a form that does nothing |
| **BR-25 costs one lookup per page view** | An indexed primary-key read on every authenticated request. That is the literal requirement — "ended on the next request" — and caching it would trade correctness for a saving this system does not need |
| **All tests touch the database** | Deliberate, because the rules are partly enforced by SQL Server. It does mean there are no fast unit tests and the suite takes minutes |
| **No load or cross-browser testing** | Reasoned about, not measured. Chrome and IIS Express only |
| **Accessibility is measured only where AT-26…AT-28 ask** | Tab order, focus visibility, keyboard traps and text contrast are now automated. Screen-reader announcement, heading structure and landmark roles are still judged by reading the markup, not by a real assistive technology |
| **Administrator rescheduling takes free text, not a reason category** | Cancellation uses the fixed category list. The categories describe a student's circumstances ("medical", "exam preparation") and read oddly for a venue change, so rescheduling takes a mandatory free-text reason instead. Still required, still audited |
| **`local.settings.config` exists on disk** | Correctly git-ignored, but a file-copy deployment would carry a development password to the server. Exclude it explicitly |

---

## 7. What I would do next, in order

1. **Put the two suites in CI.** They are reproducible now — an SDK-style test project and
   a parameterised PowerShell script — but nothing runs them automatically. Everything below
   is safer once this exists.
2. **Move `<machineKey>` and session state out of process.** These are the two things
   standing between this system and a second server, and both are configuration rather than
   code.
3. **Add email.** It closes self-service password reset and turns notifications from
   something you must be signed in to see into something that reaches people. It is the
   largest gap in usefulness rather than correctness.
4. **Extract the validators into genuine unit tests.** The suite is entirely integration
   tests today; the validation rules do not need a database and would run in milliseconds.
5. **Index review from a query plan, not from reasoning.** The pagination and the search
   filters are argued for but never measured. With demonstration-sized data, that argument
   is untested.
6. **Reconsider the audit trail.** Review moderation records who, when and why. Nothing else
   does — deactivating a user, approving a tutor and deleting a subject leave no trace
   beyond the notification. On a system with real users, that asymmetry would not hold up.
