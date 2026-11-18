# TutorConnect — Database Design

**Target platform:** Microsoft SQL Server 2016+ (developed and verified against **SQL Server 2022 Express**, 16.0.1000.6)
**Scripts:** `/App_Data/Scripts/01_CreateSchema.sql`, `/App_Data/Scripts/02_SeedLookups.sql`
**Database name:** `TutorConnect` (the Phase 1 script used `TutorConnect1`; the trailing digit was dropped)

---

## 1. Why the Phase 1 model changed

The Phase 1 / Phase 2 model was designed in MySQL. Three things forced revision:

1. **The target platform is SQL Server.** `AUTO_INCREMENT`, `ENUM(...)`, `DELIMITER $$` and `SIGNAL SQLSTATE` have no SQL Server equivalent. Two of the required changes alter *behaviour*, not just syntax (see §3).
2. **Several required business rules were unimplementable** against the original tables — most importantly, `SESSION_REQUEST` recorded no date or time, so "is this slot inside the tutor's availability?" had nothing to compare.
3. **The original model had no roles, no active flag, and no administrator**, so role-based authorisation had nothing to authorise against.

For an academic submission, a *justified* change scores better than an original that could not support its own requirements. Every change below is recorded with its reason.

---

## 2. Entity mapping — Phase 1/2 model → shipped schema

| Phase 1/2 (MySQL) | Shipped (SQL Server) | Change |
|---|---|---|
| `PERSON` | `Person` | `Password` removed; `PasswordHash`, `PasswordSalt`, `HashIterations` added; `Role`, `IsActive`, `FailedLoginCount`, `LockoutEndUtc`, `CreatedAt`, `UpdatedAt` added; `Cell_Number` → `CellNumber` |
| `STUDENT` | `Student` | + `CreatedAt` |
| `TUTOR` | `Tutor` | `Qual` → `Qualification`; `ExpYears` → `ExperienceYears`; `StuPerSession` → `RatePerSession`; + `Bio`, `IsApproved`, `ApprovedAt`, `CreatedAt` |
| `SUBJECT` | `Subject` | `SubName` → `SubjectName`; + `IsActive` |
| `STUDY_LEVEL` | `StudyLevel` | `StudyLevel` (column) → `StudyLevelName`; + `SortOrder` |
| `STUDENT_SUBJECT` | `StudentSubject` | + `CreatedAt` |
| `TUTOR_SUBJECT` | `TutorSubject` | + `CreatedAt`. The duplicate `CREATE` / `DROP` / `CREATE` block in the original was leftover editing — both definitions were identical; one is kept |
| `AVAILABILITY` | `TutorAvailability` | `AvaDate` → `AvailabilityDate`; + `IsBooked`, `CreatedAt` |
| `SESSION_REQUEST` | `SessionRequest` | + `AvailabilityId`, `RequestedDate`, `StartTime`, `EndTime`, `StudentMessage`, `RespondedAt`, `DeclineReason` |
| `BOOKING` | `Booking` | + `CompletedAt`, `CancelledAt`, `CancelReason`, `CreatedAt` |
| `SESSION` | `TutoringSession` | Renamed (`SESSION` is a reserved-word hazard); `Session_Date` → `SessionDate`; + `TutorNotes`; now written **only on completion** |
| `REVIEW` | `Review` | `Session_ID` → `BookingId`; **`Student_ID` removed** (see §6) |
| — | `Notification` | New — required by BR-27 |

**13 tables total.**

### Naming rule: `Location`, never `Address`

The Phase 2 ERD labels this attribute **`Address`**; the Phase 1 SQL script calls it **`Location`**. The two disagreed. **`Location` is canonical** — in the ERD, the column, the model property, the ADO.NET parameter (`@Location`), the form label, and every validation message.

Reason: the field holds a town or campus area (the original seed values are *East London*, *Komani*, *Mthatha*), not a postal address. Naming it `Address` invites free-text street data, which cannot be filtered — and tutor search is expected to filter on this later. `EmailAddress` is a separate attribute and keeps its name; the two are not "harmonised".

**Action for the ERD:** rename `PERSON.Address` to `PERSON.Location` before resubmission.

### Other ERD corrections needed

The Phase 2 ERD diagram contains four transcription errors that the SQL script gets right:

| ERD shows | Should be |
|---|---|
| `SUDENT_SUBJECT` | `STUDENT_SUBJECT` |
| `REVIEW.Stusent_ID` | (column removed entirely — see §6) |
| `BOOKING.Availabilitty_ID` | `Availability_ID` |
| `PERSON.Address` | `PERSON.Location` |

### `PersonType` → `Role`

The Phase 2 ERD already models a disjoint specialisation of `PERSON` into `STUDENT` / `TUTOR` using a `PersonType` discriminator (`PersonType_S` / `PersonType_T`). This is the same idea as the required `Role` column — so roles are **not** a foreign concept bolted on, they are the ERD's own discriminator, renamed and extended.

Two changes: the attribute is called **`Role`**, and it gains a third value **`Administrator`**, which participates in neither specialisation.

---

## 3. MySQL → T-SQL translation

| Original | Shipped | Behaviour change? |
|---|---|---|
| `INT AUTO_INCREMENT PRIMARY KEY` | `INT IDENTITY(1,1) PRIMARY KEY` | No |
| `ENUM('Male','Female','Other')` | `VARCHAR(10) + CHECK (... IN ...)` | No |
| `VARCHAR(50)` for names, locations, comments | `NVARCHAR(...)` | **Yes** — `VARCHAR` mangles non-ASCII. isiXhosa names and place names need Unicode. `VARCHAR` is kept only for `UserName`, `EmailAddress` and status codes, which are ASCII by definition |
| `DATETIME DEFAULT CURRENT_TIMESTAMP` | `DATETIME2(0) DEFAULT SYSUTCDATETIME()` | **Yes** — timestamps are stored in **UTC** and converted for display. SAST is UTC+2 with no daylight saving, so this is safe and future-proof |
| `TIME` | `TIME(0)` | No — fractional seconds dropped, which is correct for 10:00–12:00 slots |
| `DELIMITER $$ … END$$` | Deleted; `GO` separates batches | No |
| `SIGNAL SQLSTATE '45000'` | `THROW 50001, '…', 1` | No — `THROW` requires an error number ≥ 50000 and a semicolon terminating the preceding statement |
| `BEFORE INSERT … FOR EACH ROW` | `AFTER INSERT, UPDATE`, set-based | **Yes** — a rewrite, not a substitution. See §5 |
| `ON DELETE CASCADE` on nearly every FK | Two cascades only | **Yes** — see §4 |

> **Note on the original `CHECK` constraints.** MySQL parsed but *ignored* `CHECK` before version 8.0.16. Any row that "worked" in the original database — a `Rating` of 7, a negative `ExpYears` — would be rejected here. This has no practical effect for TutorConnect because the database is built from empty, but it is worth knowing if the original data is ever imported.

---

## 4. Delete rules and the cascade-path problem

SQL Server rejects multiple cascade paths to the same table (error 1785). In this model, `Review` and `Booking` are each reachable from `Person` by two routes — `Person → Student → …` directly, and `Person → Student → SessionRequest → Booking → Review`.

**Resolution — exactly two cascading deletes:**

```
Student.PersonId  -> Person.PersonId   ON DELETE CASCADE
Tutor.PersonId    -> Person.PersonId   ON DELETE CASCADE
everything else                        ON DELETE NO ACTION, ON UPDATE NO ACTION
```

Both cascades are single-path, and the child row is meaningless without its parent.

Deletion is otherwise handled in the service layer. **The policy is deactivate, never delete** (`Person.IsActive = 0`): an academic record of who tutored whom should not evaporate because an administrator removed an account.

No foreign key was dropped to make error 1785 go away, and none should ever be.

---

## 5. Role integrity — the trap in the original triggers

The original triggers were `BEFORE INSERT … FOR EACH ROW`. **SQL Server has neither construct.** Translating them by pattern-matching produces something that either fails to compile or — far worse — compiles and validates only *one* row of a multi-row insert.

The shipped triggers therefore:

1. Use `AFTER INSERT, UPDATE` (also stricter than the original, which never checked updates).
2. Work against the `inserted` pseudo-table **as a set** — never `SELECT @x = … FROM inserted`.
3. Set `XACT_ABORT ON` so `THROW` rolls the statement back reliably.

`INSTEAD OF` triggers are not an option here: SQL Server rejects them on a table carrying a cascading foreign key, which `Student` and `Tutor` both do.

| Error | Meaning |
|---|---|
| 50001 | Person is already a Tutor and cannot also be a Student |
| 50002 | `Person.Role` must be `Student` for a row in `dbo.Student` |
| 50003 | Person is already a Student and cannot also be a Tutor |
| 50004 | `Person.Role` must be `Tutor` for a row in `dbo.Tutor` |

An **Administrator** is a `Person` with `Role = 'Administrator'` and no row in either table. The triggers never fire for them, and both `UNIQUE (PersonId)` constraints stay intact.

---

## 6. `Review` points at `Booking`, and no longer stores the student

Two changes, one structural and one corrective.

**Structural.** The original chain `Request → Booking → Session` was strictly 1:1:1, with `Review` hanging off `Session`, while the rule is that reviews belong to *completed bookings*. Three tables described one real event, so "completed" had no single home. Meanings are now fixed:

- **Request** — what the student *asked for*. Can be declined.
- **Booking** — what was *agreed*. Created only on acceptance. Carries the schedule and the status.
- **Session** (`TutoringSession`) — evidence the booking was *delivered*. One row, written when the tutor marks the booking Completed.
- **Review** — one per completed **Booking**.

"Reviewable" is now a single condition: `Booking.Status = 'Completed' AND NOT EXISTS (review for this booking)`.

**Corrective.** `REVIEW` originally stored **both** `Student_ID` and `Session_ID`, with nothing forcing `Student_ID` to be the student who actually owned that booking. A tampered request could file a review under another student's name, and the two columns could silently drift apart.

`Review.StudentId` is therefore **removed**. The reviewing student is derived:

```sql
Review -> Booking -> SessionRequest -> Student
```

This makes the disagreement structurally impossible rather than merely policed. It is a deliberate deviation from the Phase 2 ERD and should be reflected in the resubmitted diagram.

---

## 6a. Review moderation (Phase 7)

`Review` gained four columns so an administrator can **hide a review rather than delete
one**, which the brief requires in order to preserve the audit trail:

```sql
IsHidden     BIT           NOT NULL DEFAULT (0),
HiddenReason NVARCHAR(200) NULL,
HiddenAt     DATETIME2(0)  NULL,
HiddenBy     INT           NULL   -- FK to Person, ON DELETE NO ACTION
```

`HiddenBy` uses `NO ACTION` like every foreign key outside the two `Person` cascades: an
administrator's account being removed must not take the moderation record with it.

**Hiding has to be applied in three places, not one.** A hidden review is excluded from:

| Query | Why |
|---|---|
| `ReviewRepository.GetForTutorAsync` | the tutor's own list |
| `ReviewRepository.GetTutorRatingAsync` | the average shown on their profile |
| the rating `OUTER APPLY` in `TutorRepository.SearchAsync` | the average shown in Find Tutor |

Missing the third would leave a hidden review still moving a tutor's public score, which
is the whole point of hiding it.

It is **not** hidden from the student who wrote it: `GetForStudentAsync` still returns it,
and My Reviews marks it hidden and shows the reason. Removing someone's own words from
their own list without explanation would not be honest, and they need to know why.

---

## 6b. Cancellation, postponement and audit (Part 12)

Three changes took the schema from 13 tables to **15**. The count of cascading foreign keys
is unchanged at **two** — everything added is `NO ACTION`, for the same reason as everywhere
else: a record must survive the removal of the person who caused it.

### `Booking` — who cancelled, and why

```sql
CancelledAt          DATETIME2(0)  NULL,
CancelledByPersonId  INT           NULL,   -- FK to Person, NO ACTION
CancelReasonCategory VARCHAR(30)   NULL,   -- CHECK against a fixed list
CancelReason         NVARCHAR(200) NULL    -- the optional free-text note
```

A student, the tutor **or** an administrator can cancel a session, so "it was cancelled" is
not useful history without saying by whom. The category is constrained rather than free text
so cancellations can be reported on; the note sits beside it.

`CK_Booking_CancelConsistency` ties the four together: a booking whose status is `Cancelled`
**must** carry a timestamp, an actor and a category, and one that is not cancelled must carry
none of them. Without it a status could be flipped by any route and leave the history blank.

### `PostponementRequest` — the original *and* the proposal, in one row

This is the design decision that matters. The row holds `OriginalDate/Start/End` **and**
`ProposedDate/Start/End`, and **the booking is not touched while the proposal is pending**.

That is what makes a declined — or simply unanswered — proposal harmless: the agreed session
is still exactly what both parties agreed to. An approved one still leaves a record of what
the session used to be, which storing the change directly on `Booking` would destroy.

```sql
CREATE UNIQUE INDEX UQ_Postponement_OnePending
    ON dbo.PostponementRequest (BookingId)
    WHERE [Status] = 'PendingTutorReview';
```

One open proposal per booking, enforced by a **filtered unique index** rather than a check in
the service. Two submissions racing each other would both pass a SELECT-then-INSERT; the
index is what actually stops the second, and the service catches error 2601/2627 and turns it
into a message.

> **A filtered index requires `SET QUOTED_IDENTIFIER ON`.** `sqlcmd` connects with it *off*
> and `SqlCommand` connects with it *on*, so the first install of this script failed with
> `Msg 1934` while the test harness would have succeeded. The script now sets it explicitly
> at the top, which makes both paths behave the same.

### `AuditLog` — written, never changed

```sql
ActorPersonId INT NULL,          -- nullable: the record outlives the account
ActorRole     VARCHAR(15) NOT NULL,   -- includes 'System'
ActionType, EntityType, EntityId,
PreviousState, NewState, Reason, ResponseNote,
IpAddress VARCHAR(45),           -- 45: an IPv6 address with an embedded IPv4
CreatedAt DATETIME2(0)
```

There is **no `UPDATE` and no `DELETE` against this table anywhere in the codebase**. That is
what "ordinary users cannot modify audit records" means in practice — not a permission that
could be got around, but an application that offers no route to do it. Reading is confined to
the administrator area by the existing `Web.config` location rule.

`ActorRole` is read into a `String`, not `UserRole`: the column also allows `System`, and
parsing that into the role enum would throw by design.

**Audit records are written inside the transaction they describe.** One written outside it
survives a rollback and claims something happened that did not.

---

## 7. The two rules a constraint cannot express

Time overlap cannot be written as `UNIQUE`. Both guards run in the **service layer**, inside a `SERIALIZABLE` transaction with locking hints, so two simultaneous accepts cannot both pass the check:

```sql
-- SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRAN;
SELECT TOP 1 b.BookingId
FROM dbo.Booking b WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.SessionRequest r ON r.RequestId = b.RequestId
JOIN dbo.TutorSubject  ts ON ts.TutorSubjectId = r.TutorSubjectId
WHERE ts.TutorId  = @TutorId
  AND b.BookingDate = @Date
  AND b.[Status]  = 'Scheduled'
  AND b.StartTime < @EndTime
  AND @StartTime  < b.EndTime;   -- half-open overlap test
```

- **BR-14** (tutor double-booking) uses the predicate above.
- **BR-12** (student overlapping bookings) uses the same predicate keyed on `StudentId`.
- **BR-09** (availability containment) is
  `@StartTime >= a.StartTime AND @EndTime <= a.EndTime AND a.AvailabilityDate = @Date AND a.IsBooked = 0`.

Each has an integration test (AT-08, AT-11, AT-12, AT-15).

---

## 8. Indexes

```sql
IX_TutorSubject_Subject_Level   (SubjectId, StudyLevelId) INCLUDE (TutorId)   -- Find Tutor
IX_Availability_Slot            (TutorSubjectId, AvailabilityDate, IsBooked)  -- slot lookup
IX_Request_Student_Status       (Status, StudentId)                           -- My Requests
IX_Request_TutorSubject_Status  (TutorSubjectId, Status)                      -- Student Requests
IX_Booking_Date                 (BookingDate, Status)                         -- overlap guards
IX_Booking_Availability         (AvailabilityId)                              -- slot -> booking
IX_Notification_Person_Unread   (PersonId, IsRead)                            -- header bell
```

---

## 9. Seeding, and why no password appears in any `.sql` file

The original script stored the literal strings `HASHED_PW_1`, `HASHED_PW_2`, `HASHED_PW_3`. These are **not hashes** — they are placeholders that look enough like hashes to be left in place by accident, and any account carrying one cannot log in.

They are not migrated. Instead:

1. `Person.PasswordHash` is `VARBINARY(64)`, `PasswordSalt` is `VARBINARY(32)`, and `HashIterations` is stored **per row**, so the work factor can be raised later without invalidating existing accounts.
2. `02_SeedLookups.sql` seeds **only** `Subject` and `StudyLevel`. No accounts, no passwords.
3. Accounts are created by a VB.NET seeding routine that hashes at runtime, reads its development password from a git-ignored config file, and refuses to run outside debug.

**No password literal appears in any `.sql` file in this repository.**

---

## 10. Verification — actual output

Both scripts were run twice in succession against SQL Server 2022 Express. Both runs exited 0.

**Table count — expected 13:**

```
TableCount
-----------
         13

Booking, Notification, Person, Review, SessionRequest, Student, StudentSubject,
StudyLevel, Subject, Tutor, TutorAvailability, TutoringSession, TutorSubject
```

**Foreign keys not using `NO_ACTION` — expected exactly 2:**

```
FkName              ChildTable   OnDelete   OnUpdate
------------------  -----------  ---------  ---------
FK_Student_Person   Student      CASCADE    NO_ACTION
FK_Tutor_Person     Tutor        CASCADE    NO_ACTION
```

**Role integrity triggers:**

```
trg_Student_RoleIntegrity   on Student
trg_Tutor_RoleIntegrity     on Tutor
```

**Trigger behaviour — all six tests:**

| Test | Expected | Actual |
|---|---|---|
| 1. Existing Tutor inserted as Student | Msg 50001 | Msg 50001 — blocked |
| 2. **Multi-row insert, only the *second* row invalid** | Msg 50001 | Msg 50001 — blocked |
| 2b. Rows surviving that multi-row insert | 0 | **0** |
| 3. Valid Student insert | succeeds | succeeded |
| 4. Administrator with no profile row | succeeds | succeeded |
| 5. Administrator inserted into `Student` | Msg 50002 | Msg 50002 — blocked |
| 6. Existing Student inserted as Tutor | Msg 50003 | Msg 50003 — blocked |

**Test 2 is the one that matters.** A row-by-row translation of the original MySQL trigger passes tests 1, 3 and 4, and silently fails test 2 by letting the valid first row through. Test 2b confirms neither row survived.

All test rows were deleted afterwards; the final check returned 0 `Person` and 0 `Student` test rows.

---

## 11. How to build the database

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\01_CreateSchema.sql"
sqlcmd -S ".\SQLEXPRESS" -E -b -i "App_Data\Scripts\02_SeedLookups.sql"
```

Then run the seeding routine to create the four development accounts.

`01_CreateSchema.sql` is **re-runnable** — it drops and recreates every object it owns, in foreign-key-safe order. That also means **re-running it destroys all data**. It is a build script, not a migration script.
