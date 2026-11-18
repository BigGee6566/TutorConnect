# TutorConnect

A tutoring management system for students, tutors and administrators. Students find tutors and request sessions, tutors publish availability and manage their bookings, and administrators oversee users, subjects and reviews.

Built for **Information Systems 3** at Walter Sisulu University.

**Stack:** ASP.NET Web Forms (VB.NET) · .NET Framework 4.8.1 · SQL Server 2022 Express · no ORM, `Option Strict On` throughout

---

## Features

| Role | What they can do |
|---|---|
| **Student** | Find tutors with filters and paging, view tutor profiles, request sessions, track and cancel requests, view bookings, propose postponements, write reviews, manage subjects of interest |
| **Tutor** | Choose subjects taught, publish availability, accept or decline requests with a reason, manage sessions, approve or decline postponements, mark sessions complete |
| **Administrator** | Approve and activate users, manage subjects and study levels, oversee bookings and requests, moderate reviews (hide with a reason), reschedule sessions as an audited override, read the audit log |
| **Everyone** | Notifications, role dashboards, cancelling a scheduled session with a reason, friendly error pages, print stylesheet |

**Security:** PBKDF2-SHA256 password hashing, account lockout, rate limiting, role-based page access, parameterised SQL everywhere, HTTPS-only cookies, and Windows authentication to SQL Server, so no database password is stored anywhere.

---

## Project structure

```
Account/         Login, registration, first-run setup, password reset
Student/         Student pages (tutors, requests, bookings, reviews, profile)
Tutor/           Tutor pages (availability, requests, sessions, subjects, profile)
Admin/           Administrator pages (users, subjects, bookings, reviews, audit log)
Notifications/   Shared notification list
App_Code/
  Services/      Business rules
  Data/          Repositories with parameterised SQL
  Validation/    Input validators
  Security/      Password hashing, role checks, rate limiting
  Models/        Domain models
App_Data/
  Scripts/       Database schema and lookup seed scripts
  Seed/          Demonstration data seeder
Controls/        Reusable user controls (sidebar, header, alerts, badges)
MasterPages/     Layouts per role
Content/, Scripts/  CSS and JavaScript
Tests/           MSTest project and PowerShell verification scripts
Documentation/   Installation guide, user manual, database design, testing plan, security checklist
```

The layering is strict: code-behind only binds data, `Services/` holds the rules, `Data/` holds the SQL, and SQL Server enforces the constraints.

---

## Getting started

### Prerequisites

- Visual Studio 2022 (17.8+) with the **ASP.NET and web development** workload
- .NET Framework **4.8.1** Developer Pack
- SQL Server 2022 Express (instance `.\SQLEXPRESS`)
- SQL Server Management Studio or `sqlcmd`

### 1. Create the database

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\01_CreateSchema.sql"
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\02_SeedLookups.sql"
```

> ⚠️ `01_CreateSchema.sql` drops and recreates every table. Never run it against a database you want to keep.

### 2. Configure

The connection string in `Web.config` already points to `.\SQLEXPRESS` using Windows authentication. Change `Data Source` only if your instance has a different name.

To use the demonstration data, copy the settings template and set a password of your own:

```powershell
copy App_Data\local.settings.config.sample App_Data\local.settings.config
```

`local.settings.config` is git-ignored. Never commit or deploy it.

### 3. Build and run

```powershell
msbuild TutorConnect.sln /p:Configuration=Debug
```

Or open `TutorConnect.sln` in Visual Studio and press **F5**. The site runs at **https://localhost:44306**.

> Use the **HTTPS** address. Cookies are HTTPS-only, so over plain HTTP every postback fails.

### 4. Create the first administrator

On first run, Login redirects to `Account/Setup.aspx`. Create the administrator account there. The setup page closes permanently once that account exists.

---

## Tests

The `Tests/TutorConnect.Tests` project rebuilds the database from the schema scripts and seeds demonstration accounts (they share the `Seed:DevPassword` value). It covers authentication, the student and tutor workflows, cancellation, postponement, the subject catalogue and administration. Run it from Visual Studio's Test Explorer.

PowerShell acceptance and fresh-install checks live in `Tests/Verification/`.

---

## Documentation

| Document | Contents |
|---|---|
| [Installation Guide](Documentation/InstallationGuide.md) | Full setup, with checks for each step |
| [User Manual](Documentation/UserManual.md) | Walkthrough for each role |
| [Database Design](Documentation/DatabaseDesign.md) | Tables, keys and constraints |
| [Testing Plan](Documentation/TestingPlan.md) | Test approach and coverage |
| [Security Checklist](Documentation/SecurityChecklist.md) | Security requirements and the evidence for each |
| [Final Report](Documentation/FinalReport.md) | What was built, measured against the specification |

---

## Authors

Tshangela Sinesipho · Maluleke Nhlawulo, Walter Sisulu University
