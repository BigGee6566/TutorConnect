# TutorConnect — Current Architecture Audit

**Date:** 28 August 2026
**Scope:** read-only inspection of the TutorConnect repository. No source file was modified;
this document is the only file created.

---

## 0. Two problems with the audit request, stated up front

### 0.1 The described stack is not this application

The request describes "React 18, TypeScript, Tailwind CSS, and centralized Context state"
with "17 student, tutor, and administrator pages". **No part of that exists in this
repository.** Verified by enumeration:

| Looked for | Found |
|---|---|
| `package.json`, `tsconfig.json`, `vite.config.ts`, `tailwind.config.*` | none |
| `.ts`, `.tsx`, `.jsx` files | none |
| `node_modules/` | absent |
| React, Context, any npm dependency | none |

What is actually here:

| Artefact | Count |
|---|---|
| `TutorConnect.vbproj` (ASP.NET Web Forms, VB.NET, .NET Framework 4.8.1) | 1 |
| `.aspx` pages | 30 |
| `.ascx` user controls | 5 |
| `.vb` source files | 80 |
| Client-side JavaScript files | 4 |

This is a **server-rendered VB.NET Web Forms application backed by SQL Server**. It is not
a single-page application, and it has no client-side framework of any kind.

Several of the audit questions assume the React architecture and therefore have no
subject — "business rules currently implemented only in React", "client-side
authentication logic", "client-side role guards". Those are answered below as *what the
application does instead*, which is the useful answer, rather than left blank.

### 0.2 The referenced audit report was not provided

The request says to "compare the current codebase against the supplied Google AI Studio
audit report." **No such report was supplied**, in this message or earlier. Nothing in the
repository or the working directory contains one.

This document is therefore a **standalone audit of the codebase as it stands**, not a
comparison. If the report exists, supplying it would allow a genuine point-by-point
comparison — and, given the stack mismatch above, it is worth checking whether that report
describes a different project altogether.

---

## 1. Frontend routes and pages

There is no client-side router. Every route is a physical `.aspx` file, and URL-level
access is controlled by `<location>` blocks in `Web.config`.

### Anonymous (`/Account`, `/ErrorPages`, static asset folders)

| Route | Purpose |
|---|---|
| `Account/Login.aspx` | Application entry point. No public landing page exists, by design. |
| `Account/Register.aspx` | Student or tutor self-registration |
| `Account/ForgotPassword.aspx` | Explains the manual reset route (no email in scope) |
| `Account/Logout.aspx` | Signs out, abandons session, redirects |
| `ErrorPages/Error404.aspx`, `Error500.aspx` | Friendly error pages |

### Student (`/Student`, role `Student`)

`Dashboard.aspx` · `Profile/MyProfile.aspx` · `Subjects/MySubjects.aspx` ·
`Tutors/FindTutor.aspx` · `Tutors/TutorDetails.aspx` · `Requests/RequestSession.aspx` ·
`Requests/MyRequests.aspx` · `Bookings/MyBookings.aspx` · `Bookings/BookingDetails.aspx` ·
`Reviews/WriteReview.aspx` · `Reviews/MyReviews.aspx`

### Tutor (`/Tutor`, role `Tutor`)

`Dashboard.aspx` · `Profile/MyProfile.aspx` · `Subjects/MySubjects.aspx` ·
`Availability/MyAvailability.aspx` · `Requests/StudentRequests.aspx` ·
`Sessions/MySessions.aspx` · `Sessions/SessionDetails.aspx`

### Administrator (`/Admin`, role `Administrator`)

`Dashboard.aspx` · `Users/ManageUsers.aspx` · `Subjects/ManageSubjects.aspx` ·
`Bookings/ManageBookings.aspx` · `Reviews/ManageReviews.aspx`

### Shared, authenticated (`/Notifications`, all three roles)

`Notifications/Index.aspx` — one page that selects its master page in `Page_PreInit` from
the signed-in role, rather than three near-identical copies.

**Layout:** four master pages (`Auth`, `Student`, `Tutor`, `Admin`, the last three sharing
`AppMasterBase.vb`) and five user controls (`NavigationSidebar`, `TopHeader`,
`DashboardCard`, `StatusBadge`, `AlertMessage`).

---

## 2. Mock data

There is **no mock data in the application code**. No page, service or repository returns
hard-coded records; every screen reads from SQL Server.

Seed data exists in exactly two places, both explicit:

| File | Contents |
|---|---|
| `App_Data/Scripts/02_SeedLookups.sql` | Subjects (Mathematics, Information Systems, English) and study levels (First/Second/Third Year). Re-runnable; inserts only what is missing. |
| `App_Data/Seed/SeedRunner.vb` | Four required development accounts (`admin`, `sinesipho_t`, `nhlawulo_m`, `john_doe`) plus an optional demo set: three extra tutors/students, subjects taught, and future availability slots. |

`SeedRunner` refuses to run from a web request unless the application is compiled with
`debug="true"`, and refuses entirely if the development password key is absent rather than
falling back to a default.

---

## 3. Client-side authentication logic

**None, and this is correct.** Authentication is entirely server-side:

- `App_Code/Services/AuthenticationService.vb` verifies credentials.
- `App_Code/Security/PasswordHelper.vb` performs PBKDF2-SHA256, 100 000 iterations,
  16-byte per-user salt, 32-byte subkey, with a hand-written constant-time comparison
  (`CryptographicOperations` does not exist on .NET Framework).
- `App_Code/Security/RoleHelper.vb` issues the Forms Authentication ticket, carrying
  `PersonId|Role` in encrypted `UserData`.

The four JavaScript files contain **no authentication logic whatsoever**:

| File | Purpose |
|---|---|
| `Scripts/password-toggle.js` | Show/hide password eye control |
| `Scripts/auth.js` | Password-strength meter and match hint on Register |
| `Scripts/sidebar.js` | Mobile navigation drawer |
| `Scripts/reviews.js` | Star hover preview and character counter |

All four are progressive enhancement. Every rule they hint at is enforced again on the
server; disabling JavaScript weakens nothing.

---

## 4. Client-side role guards

**None, and again this is correct.** Authorisation is enforced at three independent layers:

1. **URL level** — `<location path="Student|Tutor|Admin|Notifications">` in `Web.config`,
   each with `<allow roles="…"/>` followed by `<deny users="*"/>`. Without the trailing
   deny, any authenticated user would pass.
2. **Principal level** — `Global.asax` `Application_PostAuthenticateRequest` rebuilds a
   `GenericPrincipal` carrying the role from the ticket on every request. Without this the
   `<location>` rules would match nothing and **every role check would fail open**.
3. **Record level** — every service method takes the acting identity from the ticket, never
   from the URL or a form field, and scopes its query by it (BR-24).

`BasePage.vb` additionally re-checks on every authenticated page view that the account is
still active (BR-25), so deactivating a user ends a session already in progress rather than
letting the ticket run its 30 minutes.

---

## 5. Business rules and where they live

No business rule lives in client code. The distribution is:

| Layer | Rules |
|---|---|
| **Database constraints** | `CHECK` on gender, statuses, rating 1–5, `EndTime > StartTime`; `UNIQUE` on username, email, `Review.BookingId` (BR-17), student/tutor subject combinations (BR-05); role-integrity triggers (BR-04) |
| **Serialisable transactions** | BR-12, BR-14, BR-15 — overlap and slot-consumption guards inside `BookingRepository.AcceptRequestAsync` with `UPDLOCK, HOLDLOCK` |
| **Services** | BR-08…BR-13, BR-16…BR-27 in `RequestService`, `BookingService`, `ReviewService`, `AvailabilityService`, `TutorService`, `StudentService`, `AdminService` |
| **Validators** | `UserValidator`, `BookingValidator`, `RequestValidator`, `ReviewValidator` — pure, no database or HTTP, directly unit-testable |
| **Pages** | None. Code-behind reads controls, calls one service method, binds the result. |

---

## 6. Does a backend exist?

**Yes.** The application *is* the backend — there is no separate API tier because the UI is
server-rendered. The layering is one-directional and enforced by convention:

```
.aspx.vb   →   Services/   →   Data/   →   SQL Server
(bind only)    (rules,          (parameterised
                transactions)     SQL only)
```

Nine services and eight repositories. All data access is `SqlCommand` with explicitly typed
and sized parameters; no string concatenation into SQL exists anywhere.

---

## 7. Does a database exist?

**Yes.** Microsoft SQL Server 2022 Express, database `TutorConnect`, **13 tables**.

- `App_Data/Scripts/01_CreateSchema.sql` — creates everything from empty, re-runnable
  (drop/create guarded). Verified: 13 tables, exactly two cascading foreign keys
  (`Student→Person`, `Tutor→Person`), all others `NO ACTION`.
- `App_Data/Scripts/02_SeedLookups.sql` — lookups only, no accounts, no passwords.
- `Documentation/DatabaseDesign.md` — full mapping from the Phase 1 MySQL model, with the
  reasoning for each change.

---

## 8. Are passwords stored in source code?

**No.** Verified by pattern search across `.vb`, `.aspx`, `.ascx`, `.config`, `.js` and
`.sql`: no hard-coded password, connection secret or API key.

- `Person` stores `PasswordHash VARBINARY(64)`, `PasswordSalt VARBINARY(32)` and
  `HashIterations INT` — never plaintext, never a reversible form.
- The original supplied schema's `HASHED_PW_1/2/3` placeholders were discarded, not
  migrated.
- The development seed password lives in `App_Data/local.settings.config`, referenced by
  `<appSettings file="…"/>` and **git-ignored**. The committed `.sample` file contains only
  a placeholder.
- The connection string uses `Integrated Security=True`, so it carries no secret.

⚠️ `App_Data/local.settings.config` **exists on disk** with a real development password. It
is correctly ignored by git, but it must never be included in a deployment package.

---

## 9. Find Tutor filtering logic

Located in `App_Code/Data/TutorRepository.vb` (`SearchAsync` / `SearchCountAsync`), driven
by `TutorSearchService` and the `Student/Tutors/FindTutor.aspx` page.

**All filtering and paging happen in SQL**, not in memory:

- Optional filters: `SubjectId`, `StudyLevelId`, `Qualification` (contains),
  `MinExperience`. Each is applied as `(@Param IS NULL OR column = @Param)`, so a blank
  filter means "no filter" rather than "match empty".
- Paging: `OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY`, default 10 per page, with a
  separate count query using identical predicates.
- One row per **TutorSubject** match, not per tutor — a tutor teaching two subjects appears
  twice, which is what the design shows.
- Ratings come from an `OUTER APPLY` that excludes hidden reviews (`rv.IsHidden = 0`).

**BR-26 is enforced in the `WHERE` clause, not by the caller:**
`WHERE p.IsActive = 1 AND t.IsApproved = 1`. An unapproved or deactivated tutor cannot be
returned however the search is called — a rule that keeps people out of results should not
depend on every caller remembering a flag.

---

## 10. Existing tests and test commands

⚠️ **No test project is committed to the repository.** `Tests/` does not exist, and no file
matching `*Test*` is present in source control. Appendix B of the specification calls for
`Tests/TutorConnect.Tests` and `Tests/TutorConnect.IntegrationTests`; neither exists.

Four console harnesses were written and run during development, totalling **157 assertions,
all passing**, covering AT-01…AT-25 and BR-01…BR-27:

| Harness | Assertions | Covers |
|---|---|---|
| `AuthTests` | 36 | AT-01…AT-06, SEC-08 lockout, BR-04 |
| `StudentTests` | 46 | AT-08…AT-13, AT-17…AT-22, BR-05, BR-27 |
| `TutorTests` | 35 | AT-14…AT-16, AT-23, BR-06/07/15/20/21/22/24 |
| `AdminTests` | 40 | AT-23, AT-24, AT-25, review moderation |

**These live in a temporary session scratchpad, not in the repository.** They are therefore
not reproducible by anyone else, cannot run in CI, and will be lost. This is the single
largest gap in the codebase.

There is no `msbuild` test target, no test runner reference in `packages.config`, and no CI
configuration. The only repeatable commands today are:

```
msbuild TutorConnect.sln /p:Configuration=Release
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\01_CreateSchema.sql"
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\02_SeedLookups.sql"
```

---

## 11. Environment variables

**None.** The application reads no environment variables. Configuration is entirely
`Web.config`:

| Setting | Location | Notes |
|---|---|---|
| `TutorConnectDb` connection string | `<connectionStrings>` | `Integrated Security`, no secret |
| `Seed:DevPassword` | `App_Data/local.settings.config` via `<appSettings file="…"/>` | git-ignored; absent file is tolerated |
| Forms auth, cookies, session, custom errors | `<system.web>` | — |

`Web.Debug.config` and `Web.Release.config` exist but contain only the default commented
examples — **no release transform is defined**, which matters for the `debug="true"` finding
below.

---

## 12. Security risks

Ordered by what I would fix first.

| # | Risk | Detail |
|---|---|---|
| **1** | **`debug="true"` ships in `Web.config`** | Disables timeouts, bloats memory, and pairs badly with any misconfiguration of `customErrors`. `Web.Release.config` defines no transform to turn it off. **Must be `false` before any deployment.** |
| **2** | **No test project in source control** | 157 assertions exist but are unreproducible by anyone else. Any future change can silently break a business rule with nothing to catch it. |
| **3** | **`Notification.NotificationType` has no `CHECK` constraint** | It is `VARCHAR(30)` with no constraint, unlike every other status column. Invalid data reaching `EnumHelper.Parse` throws and produces an HTTP 500 — **demonstrated empirically**, not theorised. Either add a `CHECK` or make that one parse tolerant. |
| **4** | No `<machineKey>` | `AutoGenerate,IsolateApps` is fine for a single server but invalidates every ticket and ViewState on an app-pool identity change, and breaks outright on a web farm. |
| **5** | Connection string not encrypted | SEC-02 calls for `aspnet_regiis -pe "connectionStrings"` at deployment. Low impact while Integrated Security carries no password, but it is a documented step not yet performed. |
| **6** | No HSTS header | SEC-12 is only half met: cookies are `requireSSL` and `httpOnly`, but nothing forces HTTPS at the browser level. |
| **7** | No anti-automation on registration | SEC-08 lockout protects sign-in only. Registration can be scripted to create accounts or enumerate which emails are taken (the message distinguishes a taken email from a taken username). |
| **8** | `App_Data/local.settings.config` present on disk | Correctly git-ignored, but a file-copy deployment would carry a real development password to the server. |
| **9** | `ErrorLogger` output is unbounded | One file per day under `App_Data/Logs`, never pruned. Not exploitable — `App_Data` is not served — but it grows without limit. |
| **10** | `InProc` session state | Sessions and the CSRF `ViewStateUserKey` are lost on an app-pool recycle, logging everyone out. Acceptable for one instance, not for a farm. |

**Not risks, verified:** no SQL injection surface (every query parameterised with explicit
types), no plaintext passwords, no secrets in source, no client-side authorisation, output
encoded via `<%: %>` and `Literal` in Encode mode, CSRF covered by a session-bound
`ViewStateUserKey` with `viewStateEncryptionMode="Always"`.

---

## 13. Specification items still outstanding

| Item | Status |
|---|---|
| Phases P0–P7 | Complete |
| P8 — print-friendly booking detail, further responsive polish | Not started |
| P10 — xUnit/MSTest projects, `InstallationGuide.md`, `TestingPlan.md`, `UserManual.md`, final report | Not started |
| `TutorCard.ascx` (Appendix B) | Deliberately deferred — Find Tutor is a table, not cards, so the control has no consumer |
| `AdminService` / `IAdminService` | Deliberate addition beyond Appendix B — pages must not call repositories and no listed service owned user management |

---

## 14. Documentation naming note

This file was created at `docs/current-architecture-audit.md` exactly as requested. The
repository's existing convention is `Documentation/` (capitalised) — `DatabaseDesign.md`
lives there, and Appendix B names that folder. Consolidating the two would be tidier;
flagged rather than done, since no file was to be modified.
