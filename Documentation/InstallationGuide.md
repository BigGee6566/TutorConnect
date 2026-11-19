# TutorConnect — Installation Guide

Everything needed to take this repository to a running system, in order. Each step says
how to tell that it worked, because a step that silently half-succeeded is worse than one
that fails loudly.

---

## 1. Prerequisites

| Requirement | Version | Why this one |
|---|---|---|
| Visual Studio 2022 | 17.8 or later, **ASP.NET and web development** workload | The project is a classic Web Application Project; the workload brings MSBuild, IIS Express and the test runner |
| .NET Framework | **4.8.1** Developer Pack | The target framework. 4.8 will not build it |
| SQL Server Express | 2022 (`.\SQLEXPRESS`) | The schema uses `DATETIME2(0)`, `THROW`, and set-based triggers |
| SQL Server Management Studio *or* `sqlcmd` | any current version | To run the two build scripts |

Windows authentication is used throughout, so no SQL login is created and no password is
stored in any configuration file (SEC-02).

---

## 2. Build the database

Two scripts, in this order, from `App_Data/Scripts/`:

```
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\01_CreateSchema.sql"
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\02_SeedLookups.sql"
```

- `01_CreateSchema.sql` creates the `TutorConnect` database if it is absent, then **drops
  and recreates all 13 tables**. It is re-runnable by design, which is what lets the test
  project rebuild from it on every run.
- `02_SeedLookups.sql` loads the subject and study-level lists.

> **The first script destroys data.** It is a build script, not a migration. Never run it
> against a database that holds anything you want to keep.

**Check it worked.** The last line of the first script reads *"TutorConnect schema created
successfully."* Then:

```sql
USE TutorConnect;
SELECT COUNT(*) FROM sys.tables;                                  -- 15
SELECT COUNT(*) FROM sys.foreign_keys WHERE delete_referential_action = 1;  -- 2
SELECT COUNT(*) FROM sys.indexes WHERE has_filter = 1;            -- 1
SELECT COUNT(*) FROM dbo.[Subject];                               -- > 0
SELECT COUNT(*) FROM dbo.Person;                                  -- 0
```

The filtered index is `UQ_Postponement_OnePending`. **It only exists if the script ran with
`QUOTED_IDENTIFIER ON`** — `sqlcmd` connects with it off, and SQL Server refuses to create a
filtered index under that setting (`Msg 1934`). The script sets it explicitly at the top, so
a count of 0 here means the script was run some other way and the one-open-proposal-per-
booking rule is not being enforced.

Exactly **two** cascading foreign keys is correct and deliberate: `Student → Person` and
`Tutor → Person`. Everything else is `NO ACTION`, so a delete that would strand a booking
fails rather than quietly taking the booking with it — and so an audit record outlives the
account of whoever caused it.

`dbo.Person` being empty is also correct. The scripts create no people; step 5 does.

---

## 3. Configure the application

The connection string already sits in `Web.config` and uses `Integrated Security`, so it
carries no password:

```xml
<add name="TutorConnectDb"
     connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=TutorConnect;Integrated Security=True" />
```

Change `Data Source` only if your instance is named differently.

### SQL timeouts

Both SQL time limits are removed by default, so neither a slow instance nor a long query
produces a spurious *"Execution Timeout Expired"*:

| Limit | Where it is set | Default |
|---|---|---|
| **Connect** — how long to wait for the instance | `Connect Timeout=0` in the connection string above | no limit |
| **Command** — how long a query may run | `Sql:CommandTimeoutSeconds` in `App_Data\local.settings.config`, applied by `DatabaseHelper.CreateCommand` | `0` — no limit |

`Command Timeout` **cannot** go in the connection string. This project uses
`System.Data.SqlClient`, which rejects that keyword with *"Keyword not supported"*; it
arrived in `Microsoft.Data.SqlClient` 2.0. Every `SqlCommand` in the application is therefore
built by `DatabaseHelper.CreateCommand`, which applies the setting centrally.

Unlimited is convenient but not free: a query that never returns holds an ASP.NET request
thread indefinitely. If that becomes a problem, set a positive number and no rebuild is
needed:

```xml
<add key="Sql:CommandTimeoutSeconds" value="300" />
```

A missing, blank, unparseable or negative value falls back to `0`.

> Note: this governs the **application**. Visual Studio's *Add Connection* dialog builds its
> own connection string and ignores `Web.config` entirely — raise its limit under
> **Advanced… → Connect Timeout** in that dialog.

**Optional, for demonstration data.** The seeder that creates the demonstration accounts
reads its password from a git-ignored file. Copy the template and choose a value:

```
copy App_Data\local.settings.config.sample App_Data\local.settings.config
```

```xml
<appSettings>
  <add key="Seed:DevPassword" value="YourLocalPassword1!" />
</appSettings>
```

`Web.config` references it with `<appSettings file="App_Data\local.settings.config" />`,
which is optional — the application starts perfectly well without the file. Only the
seeder and the test project need it, and the seeder refuses to run rather than invent a
default password.

**This file must never be deployed.** It is git-ignored, but a file-copy deployment would
carry it to the server; exclude it explicitly.

---

## 4. Build and run

```
msbuild TutorConnect.sln /p:Configuration=Debug
```

Zero errors and zero warnings is the expected result, and it is a requirement (AT-29), not
an aspiration.

Press **F5** in Visual Studio, or start IIS Express directly:

```
"C:\Program Files\IIS Express\iisexpress.exe" /site:TutorConnect ^
    /config:".vs\TutorConnect\config\applicationhost.config"
```

The site is bound to **https://localhost:44306**.

> **Use the HTTPS address.** Authentication and session cookies are issued with
> `requireSSL="true"`, so over plain HTTP the browser will not send them back and every
> postback fails. That is the intended behaviour, not a fault.

---

## 5. Create the first administrator

A freshly built database has subjects and study levels but no people, and registration
refuses to create an administrator by design — so the first one is created once, on the
first run.

1. Browse to **https://localhost:44306**. Because no administrator exists, Login redirects
   itself to `Account/Setup.aspx`.
2. Fill in the form and submit. The account is created with the password you choose,
   hashed the same way as every other account (PBKDF2-SHA256, 100 000 iterations).
3. You are returned to Login with a confirmation. Sign in.

**Setup closes permanently the moment that account exists.** Returning to `Setup.aspx`
afterwards shows an explanation and a link to Login, and a form posted to it directly is
refused by the service as well as hidden by the page — hiding a form is presentation, and
a hidden form can still be submitted.

There is no application route to a second administrator: after this, accounts are created
by registration (students and tutors only), and administrators are made by an existing
administrator.

---

## 6. Optional — load the demonstration data

The demonstration set fills the screens out: three tutors, five students, subjects taught,
and published availability. It is what the tests run against and what the walkthrough in
the User Manual assumes.

It is loaded by running the test project once (see `Documentation/TestingPlan.md`), which
rebuilds the schema and seeds it. **Do not do this on an installation you have already
started using** — it drops every table first.

The seeded accounts all share the `Seed:DevPassword` value from step 3:

| Username | Role | Notes |
|---|---|---|
| `admin` | Administrator | |
| `zanele_k` | Student | STU2026001, also signs in as `zanele@example.com` |
| `john_doe` | Student | STU2026002 |
| `akhona_n`, `luvo_m`, `thabo_j` | Students | STU2026003–005 |
| `bongani_z` | Tutor | Approved, teaches Information Systems (Third Year) |
| `lerato_m`, `andile_d` | Tutors | Approved |

These are development accounts with one shared password. They have no place on a
deployed system.

---

## 7. Deploying

`Web.Release.config` transforms the configuration at publish time:

- `debug` is removed from `<compilation>`
- `customErrors` becomes `mode="On"` — `RemoteOnly` still shows the yellow screen to
  anyone browsing *from* the server, which on a deployed machine is not the developer
- the connection string is replaced with a `Data Source=SET-ME` placeholder you must fill in

After deploying, encrypt the connection string in place:

```
aspnet_regiis.exe -pe "connectionStrings" -app "/TutorConnect"
```

Two further things the application cannot do for you, both recorded in
`Documentation/SecurityChecklist.md` under residual risks:

- **`<machineKey>`.** `AutoGenerate,IsolateApps` is stable on one server but invalidates
  every authentication ticket and ViewState when the app-pool identity changes, and breaks
  outright on a web farm. A static key is a secret: it belongs in the encrypted section,
  never in source control.
- **Session state.** `InProc` loses sessions — and the CSRF key with them — on recycle.
  Acceptable for a single instance; a farm needs a shared provider.

The application pool identity needs write access to `App_Data/Logs` (fault logs, pruned
after 30 days) and read access to `App_Data`. `App_Data` is never served by IIS, which is
why the logs live there.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| *"Validation of viewstate MAC failed"* on every postback | The session is not being persisted, so the ViewState key changes per request. `BasePage` writes a GUID into the session in `OnInit` precisely to prevent this — confirm the `ASP.NET_SessionId` cookie is being issued |
| Every postback appears to do nothing | You are on `http://`, not `https://`. Cookies are `requireSSL` |
| `Msg 8117` or `Msg 547` while running the schema script | The script is being run against an older database that still has the previous tables. It drops them first; run it complete, not statement by statement |
| The seeder throws `ConfigurationErrorsException` | `Seed:DevPassword` is not set. See step 3 — it will not fall back to a default |
| Login shows "Incorrect username or password" for a password you are sure of | Five failures in fifteen minutes locks the account, and the lockout deliberately does not announce itself. Wait, or clear `LockoutEndUtc` |
| *"Execution Timeout Expired… or the server is not responding"* | **Check free disk space first.** A full system drive stops SQL Server writing to its data file, its log and the Windows event log; it presents as a timeout, not as a disk error. It also produces doomed transactions (`SqlTransaction.ZombieCheck` in the stack) and build output that silently disappears. The application's own timeouts are already unlimited (see step 3), so a timeout here is the *server*, not the setting. Free space, restart the `SQLEXPRESS` service, then retry |
| The same message inside Visual Studio's **Add Connection** dialog | That dialog does not read `Web.config`. Set **Advanced… → Connect Timeout** there, or fix the underlying cause above |
