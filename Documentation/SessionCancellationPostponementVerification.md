# Part 12 — Verification report

Cancellation, postponement, subject management, notifications and audit logging.

**Standard applied:** a feature is not reported as complete because the interface shows it.
Every "Fully functional and persistent" below was re-read in a **new sign-in session** after
the action, and the automated checks re-read from the database rather than trusting the
response to the post.

**Stack note.** The brief describes React, TypeScript, Express and PostgreSQL. This
repository is ASP.NET Web Forms (VB.NET) on .NET Framework 4.8.1 with SQL Server, and stays
that way; the brief's *requirements* were applied, its stack was not.

---

## 1. Summary by feature

| Feature | Frontend | Backend | Database | Authorisation | Notification | Audit | Test result | Classification |
|---|---|---|---|---|---|---|---|---|
| Student subject management (§12.1) | Existing | Existing + inactive guard added | `StudentSubject` | Student id from the ticket | n/a | n/a | 3 new tests pass | **Fully functional and persistent** |
| Session visibility (§12.2) | Extended | Extended | `Booking` + new columns | Owner, tutor or admin only | n/a | n/a | Covered by AT-22 and the walkthrough | **Fully functional and persistent** |
| Cancellation (§12.3) | **New** | **Rewritten** | `Booking` + 2 columns, 2 CHECKs | Owner, tutor or admin | Other party told; an admin tells **both** | Yes | 14 new tests pass | **Fully functional and persistent** |
| Postponement proposal (§12.4) | **New** | **New** | **New table** | Student owner only | Tutor told | Yes | 6 new tests pass | **Fully functional and persistent** |
| Student withdrawal (§12.4) | **New** | **New** | Same table | Student owner only | Tutor told | Yes | 5 new tests pass | **Fully functional and persistent** |
| Tutor decision (§12.5) | **New** | **New** | Same table | Assigned tutor only | Student told | Yes | 8 new tests pass | **Fully functional and persistent** |
| Notifications (§12.6) | Existing | Existing + 5 new types | `Notification` | Own only | — | n/a | Walkthrough + unit tests | **Fully functional and persistent** |
| Audit log (§12.7) | **New page** | **New** | **New table** | Administrator only | n/a | — | 3 tests + 3 access checks | **Fully functional and persistent** |
| Administrator actions audited (§12.7) | Filters on the audit page | **New** | `AuditLog` | Administrator only | n/a | Yes | 4 new tests | **Fully functional and persistent** |
| Administrator rescheduling (§12.7) | **New** | **New** | `Booking`, serialisable move | Administrator only | **Both** parties told | Yes | 6 new tests pass | **Fully functional and persistent** |

---

## 2. What was actually wrong before this phase

Inspection found more missing than the brief assumed, and these are the substantive fixes.

| # | Finding | Severity | Fix |
|---|---|---|---|
| 1 | **`BookingService.CancelAsync` existed but nothing called it.** No page in the application could cancel a booking — it was unreachable code. | **High** | Reached from three screens, each with a reason and an audit record |
| 2 | **The wrong party was notified.** It told the *student* whoever had cancelled, so a student cancelling their own session was notified about it and the tutor heard nothing. | **High** | `NotifyCounterpartyAsync` tells whoever did not press the button; an administrator cancelling tells both |
| 3 | **No `Subject.IsActive` guard anywhere.** Deactivating a subject only removed it from dropdowns — a post could still add it to a student's list, request a session for it, or publish availability against it. | **High** | Blocked in `StudentService`, `RequestService` and `AvailabilityService`, with a test for each |
| 4 | **Cancelling a session left an open postponement proposal live**, so a tutor could later "approve" a move for a booking that no longer existed. | **Medium** | Cancellation closes any pending proposal in the same transaction; a test proves the late approval is refused |
| 5 | No record of **who** cancelled, and no reason category — only free text. | **Medium** | `CancelledByPersonId` + `CancelReasonCategory`, with a CHECK that a cancelled booking must carry both |
| 6 | **No audit log at all.** Only review moderation recorded who/when/why. | **Medium** | `dbo.AuditLog`, written by every cancellation, proposal and decision |
| 7 | **`GetForViewerAsync` treated an administrator as a stranger** — it admitted only the owning student and the assigned tutor. Both `CancelAsync` and `RescheduleAsync` call it first, so **administrator cancellation never worked** and the Cancel button on Manage Bookings would have failed. | **High** | The check now admits an administrator, with the role read from the database by `PersonId` and never taken from the request |

| 8 | **`AdminService` wrote no audit records at all** — zero. Meanwhile the Audit Log page offered filters for tutor approval, account activation and review moderation. Those options could never match anything. | **Medium** | Every administrator mutation now records who did it: account activate/deactivate, tutor approval, subject create/delete/activate/deactivate, review hide/restore. `actingPersonId` is a **required** parameter on each — an optional one is how an audit trail becomes anonymous by accident |
| 9 | **A tutor had no way to cancel a session.** `CancelAsync` supported them and the tests proved the service worked, but no screen called it for a tutor — the feature was unreachable for that role. | **Medium** | Cancel action and reason form added to the tutor's My Sessions |
| 10 | **`UserRepository.CreateAccountAsync` destroyed its own error reports.** Its `Catch` called `transaction.Rollback()` unguarded; when SQL Server has already doomed the transaction, `Rollback` throws *"This SqlTransaction has completed"* — and because that happens inside the `Catch`, it **replaces** the original exception. A seeding failure surfaced as a meaningless `ZombieCheck` stack with no trace of the real cause. | **Medium** | The rollback is guarded, so the exception that matters survives. Every other catch-block rollback in the codebase was audited at the same time and is already guarded |

> **Finding 7 was originally reported as working.** It was written, and it was described in
> this document as delivered, but no test exercised it and the walkthrough only had the
> administrator *view* a cancellation the student had made. It was caught later, by the
> administrator-rescheduling tests failing — three of them, all saying "the change did
> nothing", while the three matching *negative* tests passed. That asymmetry is what a
> feature that always fails looks like. Two tests now cover the gap directly: an
> administrator can open a booking, and an administrator can cancel one.

---

## 3. Database

Three schema changes; 13 tables became **15**, still with exactly **2** cascading foreign keys.

**`dbo.PostponementRequest`** holds the **original and the proposed** window in one row. That
is the point of the table: the booking is untouched while a proposal is pending, so a
proposal that is declined — or never answered — leaves the agreed session exactly as it was,
and an approved one still records what the session used to be.

- `CHECK` on status: `PendingTutorReview`, `Approved`, `Declined`, `Cancelled`.
- **Filtered unique index** `UQ_Postponement_OnePending` on `BookingId WHERE Status = 'PendingTutorReview'`.
  One open proposal per booking is enforced by the database, not by a service check — two
  submissions racing each other would both pass a SELECT-then-INSERT.

**`dbo.AuditLog`** — actor, role, action, entity, before, after, reason, response note, IP,
timestamp. There is **no UPDATE and no DELETE against this table anywhere in the codebase**.
That is what "ordinary users cannot modify audit records" means here: the application offers
no route to change one, and reading them requires the Administrator role.

**`dbo.Booking`** gained `CancelledByPersonId` and `CancelReasonCategory`, plus
`CK_Booking_CancelConsistency`: a cancelled booking must say who cancelled it and why, and a
live one must not pretend to have been cancelled.

### Transactions

| Operation | Guarantee |
|---|---|
| Cancellation | One transaction: status, slot release, closing any open proposal, and the audit record. A failure leaves none of it |
| Postponement approval | `SERIALIZABLE` with `UPDLOCK, HOLDLOCK`, the same shape as `AcceptRequestAsync`. Re-checks the proposal is still pending and that **neither party** has since been booked into that window, then moves the booking and creates the slot |
| Proposal | Ownership is a condition of the `INSERT` itself, so a booking belonging to someone else writes nothing |

---

## 4. Test results

| Suite | Result |
|---|---|
| MSTest — **110 tests** (was 79; 31 added) | **110 passed, 0 failed** |
| `Verify-Phase12.ps1` — both §12.9 walkthroughs | **29 passed, 0 failed** |
| Schema rebuild | 15 tables, 2 cascading FKs, filtered index present |
| Build | 0 errors, 0 warnings |

### The concurrency case

`TwoConcurrentApprovalsForOneWindowProduceOneBooking` starts two approvals for the same
window before awaiting either — awaiting them in turn would prove nothing. Exactly one wins
and the tutor is not double-booked. This is the AT-15 pattern applied to the new flow: a
read-then-write with no serialisable transaction passes every sequential test here and still
double-books.

### What the walkthroughs prove that the unit tests cannot

Student proposes → **new sign-in** → proposal still there and the session has **not** moved →
tutor sees it, approves → **new sign-in** → the booking's own Date row now shows the approved
date → student cancels with a reason → **new sign-in** → cancellation, reason and who
cancelled all still shown → tutor notified → administrator sees both the booking and the
audit rows → a student and a tutor are each refused the audit log.

---

## 5. Remaining defects and limitations

Stated rather than hidden.

| Item | Severity | Position |
|---|---|---|
| **Postponement approval is conflict-checked, not availability-bound** | **Low** | A deliberate decision, recorded here. §12.5 says a tutor "cannot approve outside their availability"; taken literally a student could only ever propose times the tutor had already published, which makes the feature nearly unusable. Approval instead requires the window to be free for **both** parties, and the tutor pressing Approve *is* the availability decision. The slot is created inside the same transaction |
| **Administrator rescheduling has no reason *category*** | **Low** | Cancellation uses the fixed category list; rescheduling takes free text only, because the categories ("medical", "exam preparation") describe a *student's* circumstances and read oddly for a venue change. The reason is still mandatory and audited |
| **Audit log has no date-range filter or paging** | **Low** | Filters by action and record type, capped at 200 rows, newest first |
| **IP address is only recorded inside a web request** | **Low** | The tests call services directly, where there is no request to read one from. An invented address would be worse than none |
| **`sqlcmd` needs `SET QUOTED_IDENTIFIER ON`** | **Low** — fixed | The filtered index cannot be created without it (Msg 1934), and sqlcmd connects with it off while `SqlCommand` connects with it on. A plain script install failed where the test harness succeeded. Now set explicitly at the top of the schema script |

---

## 6. Final feature checklist

| | Item | Evidence |
|---|---|---|
| ✅ | Students can add and view subjects | Existing, still passing |
| ✅ | Duplicate subjects prevented | BR-05 test + unique index |
| ✅ | Inactive subjects cannot be selected | **New** — 3 tests, service-level, not dropdown-level |
| ✅ | Session details consistent across views | Student, tutor and admin views all read one repository |
| ✅ | Booking history includes cancellation information | Who, why, note and timestamp; shown on all three views |
| ✅ | Students must provide a valid cancellation reason | Enum in the signature, CHECK in the column, test for both |
| ✅ | Cancellation status persists after refresh | Walkthrough re-reads in a new session |
| ✅ | Tutors receive cancellation notifications | Test asserts the tutor's count rises and the student's does not |
| ✅ | Students can submit postponement proposals | 6 tests + walkthrough |
| ✅ | Original booking details preserved | Asserted while pending and again after approval |
| ✅ | Tutors can approve or decline | 8 tests + walkthrough |
| ✅ | Tutors cannot approve conflicting time slots | Tutor clash and student clash tested separately |
| ✅ | Students receive postponement decisions | Notification counts asserted for both outcomes |
| ✅ | Booking dates update only after tutor approval | Asserted before and after |
| ✅ | Authorisation-protected | Foreign student, foreign tutor and cross-role cases all refused |
| ✅ | Transactionally safe | Concurrency test; failures roll back whole |
| ✅ | Notifications persist in the database | `dbo.Notification`, pre-existing |
| ✅ | Audit records are created | Asserted per action, and read back on the admin page |
| ✅ | Audit records cannot be edited by ordinary users | No UPDATE/DELETE path exists; page is administrator-only, verified |
| ✅ | Direct object-reference attacks rejected | Ownership is a condition of the SQL, not a separate check |
| ✅ | All new functionality has automated tests | 31 MSTest + 29 walkthrough checks |

Everything above was verified after a fresh sign-in, not merely observed on screen.

### Also delivered, beyond the checklist

| Item | Why |
|---|---|
| **Administrator rescheduling** (§12.7) | Listed among the audited actions. Moves a scheduled session under the same serialisable transaction as a postponement approval, so an administrator cannot double-book a tutor either. A reason is mandatory, **both** parties are notified — neither of them asked for it — and any open proposal about that booking is closed |
| **Student withdrawal of a proposal** (§12.4) | A student who changes their mind no longer has to ask the tutor to decline. The tutor is told, because it disappears from a queue they were about to answer, and the booking is left untouched |
