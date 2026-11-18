# TutorConnect — User Manual

TutorConnect connects students who need help with a subject to tutors who teach it, and
keeps track of the sessions they arrange. There are three roles: **Student**, **Tutor** and
**Administrator**.

The application starts at the Login page. There is no public landing page — nothing here is
visible without signing in.

---

## Getting in

### Signing in

Enter your **username or email address** and your password. Where you land depends on your
role: students go to the student dashboard, tutors to theirs, administrators to the
administration dashboard.

Two things are worth knowing before they surprise you:

- **The failure message is always the same** — *"Incorrect username or password."* It says
  the same thing whether the username does not exist, the password is wrong, or the account
  is locked. That is deliberate: a message that distinguished them would let anyone test
  whether a particular person has an account here.
- **Five failed attempts in fifteen minutes locks the account** for a period, and the
  lockout does not announce itself either. If a password you are sure of is being refused,
  this is the likely reason. Wait, or ask an administrator.

### Registering

**Register New Account** creates a student or a tutor account. Administrators cannot be
registered.

Choose the account type first — the form then asks for what that type needs:

- **Student:** your student number, alongside the usual name, email, cell number and location.
- **Tutor:** your qualification, years of experience and rate per session.

After registering you are returned to Login to sign in. A tutor account is created
**awaiting approval** and cannot be found by students until an administrator approves it.

### Forgotten passwords

Password reset is handled by an administrator. The Forgotten Password page says so plainly
rather than presenting a form that silently does nothing — email delivery is out of scope
for this system.

### The notification bell

The bell in the top bar shows how many unread notifications you have. It appears for every
role. You are notified when something happens that concerns you: a request arrives, is
accepted, declined or cancelled; a session is completed; a review is left; a tutor account
is approved; an account is deactivated.

---

## For students

### Find a tutor

**Find Tutor** searches by subject, study level, location and minimum rating. Results show
the tutor's name, qualification, rate, location and average rating, and are paginated.

Only **approved, active** tutors appear. If a tutor you expected is missing, they are one
or the other.

**View** opens the tutor's profile: their biography, the subjects and levels they teach,
their published availability, and the reviews students have left.

### Request a session

From a tutor's profile, choose a published slot and **Request Session**. You choose the
start and end time within that slot and may add a note about what you need help with.

The rules that will refuse a request, and why:

| If | Then |
|---|---|
| The end time is at or before the start | Refused. A session with no length is not a session |
| The window falls outside the tutor's published slot | Refused. The tutor published that slot, not a different one |
| The slot is in the past | Refused |
| You already have a booking that overlaps | Refused. You cannot be in two sessions at once — the rule is per student, so another student may still take that slot |

A new request is **Pending** until the tutor answers it.

### My Requests

Every request you have made, with its status: **Pending**, **Confirmed**, **Declined** or
**Cancelled**. A declined request shows the tutor's reason where they gave one.

**Cancel** withdraws a request — but only while it is still Pending. Once a tutor has
accepted, it is a commitment on their diary, and cancelling becomes their decision rather
than yours.

### My Bookings

Confirmed sessions: date, time, subject, tutor and status (**Scheduled**, **Completed** or
**Cancelled**). **View** opens the full detail, which also shows the rate, when it was
booked, and — for a cancelled session — who cancelled it, when, and why.

A session that has still to happen also offers **Postpone** and **Cancel**.

### Cancelling a session

**Cancel** asks for a reason before it will go through — timetable or lecture clash, medical,
exam preparation, assignment deadline, or another reason — and lets you add a note of up to
200 characters.

When you confirm:

- the session becomes **Cancelled** and the slot goes back on the tutor's calendar
- **your tutor is told straight away**, with the reason
- the cancellation, the reason and your name are kept on the booking

It cannot be undone, and a cancelled session cannot be reviewed. The tutor and an
administrator can also cancel a session — whoever does it, the other side is notified.

### Asking to move a session

**Postpone** proposes a new date and time. The important part: **your session does not
change**. It stays exactly as agreed until the tutor answers, so if they decline — or never
reply — you still have the session you booked.

Choose the new date, start and end time, give a reason, and optionally add a note. Then:

- the tutor is notified and sees your proposal beside the agreed time
- the booking detail shows **"Waiting for the tutor to answer"**
- you can only have **one** open request per session at a time

If the tutor **approves**, the session moves to your proposed time and you are notified. If
they **decline**, the session stays where it was and you are told why — they have to give a
reason to decline.

**Changed your mind?** While a request is still waiting, **Withdraw this request** takes it
back. Your session is unchanged, the tutor is told so it does not sit in their queue, and you
are free to propose a different time.

A proposal can be refused at the point of approval if you or the tutor have since booked
something else across that time. The system will not double-book either of you.

### Reviews

Once your tutor marks a session **Completed**, a **Write Review** action appears against it.
Give a rating from 1 to 5 and, optionally, a comment.

**One review per booking.** It cannot be edited or replaced afterwards, so it is worth a
moment's thought.

**My Reviews** lists every review you have written. If an administrator has hidden one for
breaching the site's standards, you still see it here, marked as hidden, with the reason —
it is simply no longer shown on the tutor's profile or counted in their rating.

### My Subjects

The subjects and levels you want help with. Each subject-and-level pair can only be added
once.

### My Profile

Your name, email, cell number and location, and a password change. Your student number is
shown but cannot be edited.

---

## For tutors

### Getting approved

A new tutor account is **not visible to students** until an administrator approves it. You
can sign in, complete your profile, add subjects and publish availability in the meantime —
all of it becomes visible the moment approval is granted, with no further step.

### My Subjects

The subjects and levels you teach. You must add at least one before you can publish
availability, because availability is published *against a subject*.

A subject cannot be removed while it still has commitments — pending requests, scheduled
bookings, or published slots. Deal with those first.

### My Availability

Publish the times you are free, as a date with a start and end time against one of your
subjects.

- Slots **may not overlap each other**. A slot that begins exactly when another ends is
  fine — that is not an overlap, and refusing it would stop you filling a day back to back.
- A slot **in the past** cannot be published.
- The end time must be after the start.

**Delete** removes a slot — unless a scheduled booking sits behind it. Deleting that would
strand a student who has already been told their session is confirmed, so it is refused with
an explanation.

### Student Requests

Pending requests from students, with the subject, the date and time, and any note.

**Accept** confirms the session. It creates the booking, marks the slot as taken, and
notifies the student. Two things can make it fail, both honestly:

- **Someone else got there first.** If two students requested the same slot and you accept
  both, exactly one succeeds; the other is refused with a clear reason. The system will not
  double-book you.
- **You narrowed the slot after the request was made.** The window is re-checked at the
  moment of acceptance, not only when the request was created.

**Decline** asks for an optional reason, which the student sees. A request can be answered
once: it cannot be declined after being accepted, or accepted twice.

### Postponement requests

When a student asks to move a session, it appears at the top of **My Sessions** marked
*"Waiting for you"*, showing the agreed time, the time they have asked for, their reason and
any note.

**Answer** opens the decision. You can:

- **Approve** — the session moves to the proposed time, the student is notified, and the slot
  is put on your calendar. Approving is refused if you or the student have since taken
  another session across that window; the system will not double-book either of you.
- **Decline** — the session stays exactly where it was. **A reason is required**: a refusal
  with nothing attached leaves the student with nothing to act on. They are told what you said.

Until you answer, the session is unchanged. You can only answer a request once, and you can
only see requests for your own sessions.

### My Sessions

Your confirmed bookings. **View** opens the detail, including the student's review if one
has been left.

**Cancel** appears on a session that has still to happen. It asks for a reason — timetable
clash, medical, exam preparation, assignment deadline, or another reason — and lets you add a
note. The student is told straight away with your reason, the slot goes back on your
calendar, and the cancellation is recorded against the booking. It cannot be undone.

**Mark Completed** closes a session — but only **after its end time has passed**. A session
still in the future cannot be completed, because it has not happened yet. Completing it
creates the session record and is what allows the student to leave a review.

### My Profile

Your qualification, years of experience, rate per session and biography, along with your
contact details and a password change. Your approval status is shown here.

---

## For administrators

### Dashboard

Six figures — users, tutors awaiting approval, students, bookings, requests and reviews.
Each tile links to the page that manages what it counts.

### Manage Users

Search by name and filter by role and by active state.

- **Approve Tutor** appears against tutors awaiting approval. They become searchable
  immediately.
- **Deactivate** switches an account off. The user cannot sign in, **and any session they
  already have open ends on their next request** — it is checked on every page view, not
  only at sign-in. They are notified.
- **Activate** restores access.

Passwords are never shown and never reset from here.

**You cannot deactivate your own account.** Doing so could lock the last administrator out
of the system with no way back in through the application.

### Manage Subjects

Add and rename subjects and study levels.

A subject that is **in use** — taught, requested, or listed by a student — cannot be
deleted; deleting it would orphan live records. The system says so and offers the
alternative: **deactivate** it instead. A deactivated subject disappears from the dropdowns
for new work while everything that already references it stays intact and readable.

A subject nothing references deletes cleanly. Duplicate names are refused.

### Manage Bookings

Every request and booking in the system, filterable by status. Requests are read-only —
accepting or declining on a tutor's behalf would put a request in a state neither party
agreed to.

A scheduled booking can be **cancelled** or **rescheduled** on the parties' behalf. Both need
a reason, both notify **the student and the tutor** — neither of them asked for it — and both
are written to the audit log. The Cancellation column shows the reason and who cancelled, for
any booking that has been.

**Rescheduling is the override, not the normal route.** The designed way to move a session is
the student proposing and the tutor agreeing. Use this when that is not possible: a venue
change, or a clash neither of them can resolve. The system still refuses to double-book — if
the tutor or the student already has a session across the new time, the move is rejected.

### Audit Log

Who changed what, and why — newest first, filterable by action and by record type.

Cancellations, rescheduling, postponement requests, decisions and withdrawals, tutor
approvals, account activation and deactivation, subject creation, deletion and activation,
and review moderation all appear here — each with the actor, their role, the before and after
state, the reason and the time.

Every option in the Action filter corresponds to something the system actually records. If a
filter returns nothing, nothing of that kind has happened.

**Records cannot be edited or deleted.** The application provides no route to change one, by
any role. The page is administrator-only; a student or tutor requesting it is refused.

### Manage Reviews

Every review, including ones already hidden.

**Hide** removes a review from public view. **A reason is required** — a moderation action
with no stated reason is not an audit trail. Hiding it removes it from the tutor's profile,
from their average rating, and from the rating shown in Find Tutor.

Two things it deliberately does *not* do:

- **It does not delete the review.** The record, who hid it, when, and why are all kept.
- **It does not hide it from its author.** They still see it in My Reviews, marked hidden,
  with the reason.

**Unhide** restores it and clears the reason — a restored review must not keep the
justification for hiding it.

### First-run setup

`Account/Setup.aspx` creates the administrator account on a brand-new installation, and
closes itself permanently the moment one exists. On a running system it shows an
explanation and a link to Login. See the Installation Guide.

---

## Things that are missing on purpose

| Not here | Why |
|---|---|
| Email notification | Out of scope. Notifications are shown in the application, under the bell |
| Payment | Rates are shown so that students can compare tutors. Money changes hands outside the system |
| File upload | No avatars or attachments. Profile pictures are initials in a coloured circle |
| Self-service password reset | Requires email delivery. An administrator resets passwords instead, and the page says so |
