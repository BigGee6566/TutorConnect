/* =============================================================================
   TutorConnect - 01_CreateSchema.sql
   Target : Microsoft SQL Server 2016 or later (developed against SQL Server 2022 Express)
   Purpose: Create the TutorConnect database from empty, in one pass.

   This script is RE-RUNNABLE. It drops every object it owns, in foreign-key-safe
   order, then recreates it. Running it twice in a row must succeed both times.

   WARNING: re-running DESTROYS all data in the tables below. It is a build script,
            not a migration script.

   Design decisions implemented here are recorded, with reasons, in
   /Documentation/DatabaseDesign.md. The short version:

     D-01  Rewritten in T-SQL from the MySQL original. Not a find-and-replace.
     D-02  SessionRequest carries the requested slot (AvailabilityId + date + times).
     D-03  Option A - TutoringSession survives as the delivery record; Review -> Booking.
     D-04  Person.Role / IsActive / lockout + audit columns; Tutor.IsApproved.
     D-05  Role integrity enforced by SET-BASED AFTER triggers (SQL Server has no
           BEFORE trigger and no FOR EACH ROW).
     D-07  No password literal appears anywhere in this file. Accounts are created
           by the seeding routine, which hashes at runtime.
     D-10  Review does NOT store StudentId - it is derived through Booking, so it
           cannot disagree with the student who owns the booking.

   Naming: the attribute recording where a person is based is Location, never
   Address. The Phase 2 ERD used "Address"; this script is the canonical source.
   ============================================================================= */

SET NOCOUNT ON;

/* Required for the filtered index on PostponementRequest. sqlcmd connects with
   QUOTED_IDENTIFIER OFF, and SQL Server refuses to create a filtered index under that
   setting (Msg 1934) - the script aborted there the first time it was run. SqlCommand
   connects with it ON, so the test harness would have succeeded while a plain sqlcmd
   install failed; setting it explicitly makes both paths behave the same.
   It is a session setting, so this carries to every batch below. */
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   1. Database
   --------------------------------------------------------------------------- */
IF DB_ID(N'TutorConnect') IS NULL
BEGIN
    PRINT 'Creating database TutorConnect...';
    EXEC (N'CREATE DATABASE TutorConnect;');
END
ELSE
    PRINT 'Database TutorConnect already exists - reusing it.';
GO

USE TutorConnect;
GO

/* ---------------------------------------------------------------------------
   2. Drop existing objects, children first.
      Triggers are dropped implicitly with their table.
   --------------------------------------------------------------------------- */
DROP TABLE IF EXISTS dbo.AuditLog;
DROP TABLE IF EXISTS dbo.PostponementRequest;
DROP TABLE IF EXISTS dbo.Review;
DROP TABLE IF EXISTS dbo.TutoringSession;
DROP TABLE IF EXISTS dbo.Booking;
DROP TABLE IF EXISTS dbo.SessionRequest;
DROP TABLE IF EXISTS dbo.TutorAvailability;
DROP TABLE IF EXISTS dbo.TutorSubject;
DROP TABLE IF EXISTS dbo.StudentSubject;
DROP TABLE IF EXISTS dbo.Notification;
DROP TABLE IF EXISTS dbo.Tutor;
DROP TABLE IF EXISTS dbo.Student;
DROP TABLE IF EXISTS dbo.StudyLevel;
DROP TABLE IF EXISTS dbo.[Subject];
DROP TABLE IF EXISTS dbo.Person;
GO

/* ---------------------------------------------------------------------------
   3. Core identity
   --------------------------------------------------------------------------- */

-- Person is the single account record. Student / Tutor hang off it 1:1 and are
-- mutually exclusive; an Administrator has neither (D-05).
CREATE TABLE dbo.Person
(
    PersonId         INT            IDENTITY(1,1) CONSTRAINT PK_Person PRIMARY KEY,
    UserName         VARCHAR(50)    NOT NULL CONSTRAINT UQ_Person_UserName UNIQUE,
    FirstName        NVARCHAR(50)   NOT NULL,
    LastName         NVARCHAR(50)   NOT NULL,
    Gender           VARCHAR(10)    NOT NULL CONSTRAINT CK_Person_Gender
                         CHECK (Gender IN ('Male','Female','Other')),
    EmailAddress     VARCHAR(100)   NOT NULL CONSTRAINT UQ_Person_Email UNIQUE,
    CellNumber       VARCHAR(15)    NOT NULL,

    -- D-07: PBKDF2 output, never a password and never text.
    -- HashIterations is stored per row so the work factor can be raised later
    -- without invalidating existing accounts.
    PasswordHash     VARBINARY(64)  NOT NULL,
    PasswordSalt     VARBINARY(32)  NOT NULL,
    HashIterations   INT            NOT NULL CONSTRAINT DF_Person_Iterations DEFAULT (100000),

    -- Town or campus area (East London, Komani, Mthatha), not a postal address.
    Location         NVARCHAR(100)  NOT NULL,

    [Role]           VARCHAR(15)    NOT NULL CONSTRAINT CK_Person_Role
                         CHECK ([Role] IN ('Student','Tutor','Administrator')),
    IsActive         BIT            NOT NULL CONSTRAINT DF_Person_IsActive DEFAULT (1),

    -- SEC-08 brute-force resistance.
    FailedLoginCount INT            NOT NULL CONSTRAINT DF_Person_FailedLogin DEFAULT (0),
    LockoutEndUtc    DATETIME2(0)   NULL,

    CreatedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_Person_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt        DATETIME2(0)   NULL
);
GO

CREATE TABLE dbo.Student
(
    StudentId  INT          IDENTITY(1,1) CONSTRAINT PK_Student PRIMARY KEY,
    StudentNo  VARCHAR(20)  NOT NULL CONSTRAINT UQ_Student_StudentNo UNIQUE,
    -- UNIQUE enforces the 1:1 with Person.
    PersonId   INT          NOT NULL CONSTRAINT UQ_Student_PersonId UNIQUE,
    CreatedAt  DATETIME2(0) NOT NULL CONSTRAINT DF_Student_CreatedAt DEFAULT (SYSUTCDATETIME()),

    -- One of only two cascading deletes in the schema: a Student row is
    -- meaningless without its Person, and this is a single cascade path.
    CONSTRAINT FK_Student_Person FOREIGN KEY (PersonId)
        REFERENCES dbo.Person (PersonId) ON DELETE CASCADE ON UPDATE NO ACTION
);
GO

CREATE TABLE dbo.Tutor
(
    TutorId         INT            IDENTITY(1,1) CONSTRAINT PK_Tutor PRIMARY KEY,
    PersonId        INT            NOT NULL CONSTRAINT UQ_Tutor_PersonId UNIQUE,
    Qualification   NVARCHAR(100)  NOT NULL,
    ExperienceYears INT            NOT NULL CONSTRAINT CK_Tutor_ExperienceYears CHECK (ExperienceYears >= 0),
    -- Renamed from the original StuPerSession, which reads as a student count.
    RatePerSession  DECIMAL(10,2)  NOT NULL CONSTRAINT CK_Tutor_Rate CHECK (RatePerSession >= 0),
    Bio             NVARCHAR(1000) NULL,
    IsApproved      BIT            NOT NULL CONSTRAINT DF_Tutor_IsApproved DEFAULT (0),
    ApprovedAt      DATETIME2(0)   NULL,
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Tutor_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_Tutor_Person FOREIGN KEY (PersonId)
        REFERENCES dbo.Person (PersonId) ON DELETE CASCADE ON UPDATE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   4. Lookups
   --------------------------------------------------------------------------- */

CREATE TABLE dbo.[Subject]
(
    SubjectId   INT           IDENTITY(1,1) CONSTRAINT PK_Subject PRIMARY KEY,
    SubjectName NVARCHAR(100) NOT NULL CONSTRAINT UQ_Subject_Name UNIQUE,
    -- Admin deactivates rather than deletes a subject that is in use (AT-25).
    IsActive    BIT           NOT NULL CONSTRAINT DF_Subject_IsActive DEFAULT (1)
);
GO

CREATE TABLE dbo.StudyLevel
(
    StudyLevelId   INT          IDENTITY(1,1) CONSTRAINT PK_StudyLevel PRIMARY KEY,
    StudyLevelName NVARCHAR(50) NOT NULL CONSTRAINT UQ_StudyLevel_Name UNIQUE,
    -- First Year sorts before Second Year; alphabetical order would not.
    SortOrder      INT          NOT NULL CONSTRAINT DF_StudyLevel_SortOrder DEFAULT (0)
);
GO

/* ---------------------------------------------------------------------------
   5. Subject enrolment
   --------------------------------------------------------------------------- */

CREATE TABLE dbo.StudentSubject
(
    StudentSubjectId INT          IDENTITY(1,1) CONSTRAINT PK_StudentSubject PRIMARY KEY,
    StudentId        INT          NOT NULL,
    SubjectId        INT          NOT NULL,
    StudyLevelId     INT          NOT NULL,
    CreatedAt        DATETIME2(0) NOT NULL CONSTRAINT DF_StudentSubject_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_StudentSubject_Student FOREIGN KEY (StudentId)
        REFERENCES dbo.Student (StudentId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_StudentSubject_Subject FOREIGN KEY (SubjectId)
        REFERENCES dbo.[Subject] (SubjectId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_StudentSubject_StudyLevel FOREIGN KEY (StudyLevelId)
        REFERENCES dbo.StudyLevel (StudyLevelId) ON DELETE NO ACTION ON UPDATE NO ACTION,

    -- BR-05: a student cannot add the same subject + level twice.
    CONSTRAINT UQ_StudentSubject UNIQUE (StudentId, SubjectId, StudyLevelId)
);
GO

CREATE TABLE dbo.TutorSubject
(
    TutorSubjectId INT          IDENTITY(1,1) CONSTRAINT PK_TutorSubject PRIMARY KEY,
    TutorId        INT          NOT NULL,
    SubjectId      INT          NOT NULL,
    StudyLevelId   INT          NOT NULL,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_TutorSubject_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_TutorSubject_Tutor FOREIGN KEY (TutorId)
        REFERENCES dbo.Tutor (TutorId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_TutorSubject_Subject FOREIGN KEY (SubjectId)
        REFERENCES dbo.[Subject] (SubjectId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_TutorSubject_StudyLevel FOREIGN KEY (StudyLevelId)
        REFERENCES dbo.StudyLevel (StudyLevelId) ON DELETE NO ACTION ON UPDATE NO ACTION,

    CONSTRAINT UQ_TutorSubject UNIQUE (TutorId, SubjectId, StudyLevelId)
);
GO

/* ---------------------------------------------------------------------------
   6. Scheduling
   --------------------------------------------------------------------------- */

CREATE TABLE dbo.TutorAvailability
(
    AvailabilityId   INT          IDENTITY(1,1) CONSTRAINT PK_TutorAvailability PRIMARY KEY,
    TutorSubjectId   INT          NOT NULL,
    AvailabilityDate DATE         NOT NULL,
    StartTime        TIME(0)      NOT NULL,
    EndTime          TIME(0)      NOT NULL,
    -- Set when a booking consumes the slot (BR-10), cleared if that booking is cancelled.
    IsBooked         BIT          NOT NULL CONSTRAINT DF_TutorAvailability_IsBooked DEFAULT (0),
    CreatedAt        DATETIME2(0) NOT NULL CONSTRAINT DF_TutorAvailability_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_TutorAvailability_TutorSubject FOREIGN KEY (TutorSubjectId)
        REFERENCES dbo.TutorSubject (TutorSubjectId) ON DELETE NO ACTION ON UPDATE NO ACTION,

    -- BR-06
    CONSTRAINT CK_TutorAvailability_Times CHECK (EndTime > StartTime)
);
GO

/*
   SessionRequest carries the requested slot itself (D-02), not just a pointer to
   the availability row. This denormalisation is deliberate: a tutor may later
   delete or edit an availability row, and the request must still show what was
   actually asked for.
*/
CREATE TABLE dbo.SessionRequest
(
    RequestId      INT           IDENTITY(1,1) CONSTRAINT PK_SessionRequest PRIMARY KEY,
    StudentId      INT           NOT NULL,
    TutorSubjectId INT           NOT NULL,
    AvailabilityId INT           NOT NULL,
    RequestedDate  DATE          NOT NULL,
    StartTime      TIME(0)       NOT NULL,
    EndTime        TIME(0)       NOT NULL,
    [Status]       VARCHAR(10)   NOT NULL CONSTRAINT DF_SessionRequest_Status DEFAULT ('Pending'),
    StudentMessage NVARCHAR(500) NULL,
    -- CreatedAt is the "Date Requested" column shown in the UI.
    CreatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_SessionRequest_CreatedAt DEFAULT (SYSUTCDATETIME()),
    RespondedAt    DATETIME2(0)  NULL,
    DeclineReason  NVARCHAR(200) NULL,

    CONSTRAINT FK_SessionRequest_Student FOREIGN KEY (StudentId)
        REFERENCES dbo.Student (StudentId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_SessionRequest_TutorSubject FOREIGN KEY (TutorSubjectId)
        REFERENCES dbo.TutorSubject (TutorSubjectId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_SessionRequest_Availability FOREIGN KEY (AvailabilityId)
        REFERENCES dbo.TutorAvailability (AvailabilityId) ON DELETE NO ACTION ON UPDATE NO ACTION,

    CONSTRAINT CK_SessionRequest_Status
        CHECK ([Status] IN ('Pending','Confirmed','Declined','Cancelled')),
    CONSTRAINT CK_SessionRequest_Times CHECK (EndTime > StartTime)
);
GO

CREATE TABLE dbo.Booking
(
    BookingId      INT           IDENTITY(1,1) CONSTRAINT PK_Booking PRIMARY KEY,
    -- One request generates at most one booking.
    RequestId      INT           NOT NULL CONSTRAINT UQ_Booking_RequestId UNIQUE,
    AvailabilityId INT           NOT NULL,
    BookingDate    DATE          NOT NULL,
    StartTime      TIME(0)       NOT NULL,
    EndTime        TIME(0)       NOT NULL,
    [Status]       VARCHAR(10)   NOT NULL CONSTRAINT DF_Booking_Status DEFAULT ('Scheduled'),
    CompletedAt    DATETIME2(0)  NULL,

    /* Cancellation. CancelledByPersonId records WHO cancelled - a student, the tutor or an
       administrator can all do it, and "the session was cancelled" is not useful history
       without saying by whom. CancelReasonCategory is constrained to a fixed list so the
       reason can be reported on; CancelReason stays the free-text note beside it. */
    CancelledAt          DATETIME2(0)  NULL,
    CancelledByPersonId  INT           NULL,
    CancelReasonCategory VARCHAR(30)   NULL,
    CancelReason         NVARCHAR(200) NULL,

    CreatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_Booking_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_Booking_Request FOREIGN KEY (RequestId)
        REFERENCES dbo.SessionRequest (RequestId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_Booking_Availability FOREIGN KEY (AvailabilityId)
        REFERENCES dbo.TutorAvailability (AvailabilityId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_Booking_CancelledBy FOREIGN KEY (CancelledByPersonId)
        REFERENCES dbo.Person (PersonId) ON DELETE NO ACTION ON UPDATE NO ACTION,

    CONSTRAINT CK_Booking_Status
        CHECK ([Status] IN ('Scheduled','Completed','Cancelled')),
    CONSTRAINT CK_Booking_Times CHECK (EndTime > StartTime),
    CONSTRAINT CK_Booking_CancelCategory
        CHECK (CancelReasonCategory IS NULL OR CancelReasonCategory IN
               ('TimetableClash','Medical','ExamPreparation','AssignmentDeadline','Other')),
    -- A cancelled booking must say who cancelled it and why; a live one must not pretend to.
    CONSTRAINT CK_Booking_CancelConsistency CHECK (
        ([Status] = 'Cancelled' AND CancelledAt IS NOT NULL
             AND CancelledByPersonId IS NOT NULL AND CancelReasonCategory IS NOT NULL)
        OR
        ([Status] <> 'Cancelled' AND CancelledAt IS NULL
             AND CancelledByPersonId IS NULL AND CancelReasonCategory IS NULL))
);
GO

/*
   D-03 Option A. TutoringSession is evidence the booking was DELIVERED. Exactly
   one row, written only when the tutor marks the booking Completed. It is not a
   duplicate of Booking; it is the completion artefact.
*/
CREATE TABLE dbo.TutoringSession
(
    SessionId   INT            IDENTITY(1,1) CONSTRAINT PK_TutoringSession PRIMARY KEY,
    BookingId   INT            NOT NULL CONSTRAINT UQ_TutoringSession_BookingId UNIQUE,
    SessionDate DATE           NOT NULL,
    TutorNotes  NVARCHAR(1000) NULL,
    CreatedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_TutoringSession_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_TutoringSession_Booking FOREIGN KEY (BookingId)
        REFERENCES dbo.Booking (BookingId) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   7. Reviews
   --------------------------------------------------------------------------- */

/*
   D-03 / D-10. Review points at Booking, not Session, so "reviewable" is a single
   condition: Booking.Status = 'Completed' AND no review exists for it.

   Review deliberately does NOT store StudentId. The original model stored both
   Student_ID and Session_ID with nothing forcing them to agree, which allowed a
   review to be filed against another student's booking. The reviewing student is
   derived: Review -> Booking -> SessionRequest -> Student.
*/
CREATE TABLE dbo.Review
(
    ReviewId  INT           IDENTITY(1,1) CONSTRAINT PK_Review PRIMARY KEY,
    -- BR-17: at most one review per booking, enforced by the database, not only by code.
    BookingId INT           NOT NULL CONSTRAINT UQ_Review_BookingId UNIQUE,
    Rating    TINYINT       NOT NULL CONSTRAINT CK_Review_Rating CHECK (Rating BETWEEN 1 AND 5),
    Comment   NVARCHAR(500) NULL,
    CreatedAt DATETIME2(0)  NOT NULL CONSTRAINT DF_Review_CreatedAt DEFAULT (SYSUTCDATETIME()),

    -- An administrator hides an unacceptable review rather than deleting it, so the
    -- audit trail survives. A hidden review is excluded from the tutor's public list and
    -- from every rating average, but is still visible to the student who wrote it.
    IsHidden     BIT           NOT NULL CONSTRAINT DF_Review_IsHidden DEFAULT (0),
    HiddenReason NVARCHAR(200) NULL,
    HiddenAt     DATETIME2(0)  NULL,
    HiddenBy     INT           NULL,

    CONSTRAINT FK_Review_Booking FOREIGN KEY (BookingId)
        REFERENCES dbo.Booking (BookingId) ON DELETE NO ACTION ON UPDATE NO ACTION,

    -- NO ACTION, like every foreign key outside the two Person cascades: an
    -- administrator's account being removed must not take the audit trail with it.
    CONSTRAINT FK_Review_HiddenBy FOREIGN KEY (HiddenBy)
        REFERENCES dbo.Person (PersonId) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   8. Notifications (BR-27)
   --------------------------------------------------------------------------- */

CREATE TABLE dbo.Notification
(
    NotificationId   INT           IDENTITY(1,1) CONSTRAINT PK_Notification PRIMARY KEY,
    PersonId         INT           NOT NULL,
    Title            NVARCHAR(150) NOT NULL,
    [Message]        NVARCHAR(500) NOT NULL,
    -- Constrained like every other status column. Without this, a value the enums do not
    -- know reaches EnumHelper.Parse, which fails loudly by design - correct behaviour for
    -- a code/database mismatch, but it surfaces as an HTTP 500 on the notifications page.
    -- Verified: inserting an unmapped value here did exactly that. Better to refuse the
    -- write than to serve the error.
    NotificationType VARCHAR(30)   NOT NULL CONSTRAINT CK_Notification_Type
                         CHECK (NotificationType IN (
                             'RequestReceived','RequestAccepted','RequestDeclined',
                             'RequestCancelled','BookingCompleted','BookingCancelled',
                             'ReviewReceived','TutorApproved','AccountDeactivated',
                             'BookingPostponeRequested','BookingPostponeApproved',
                             'BookingPostponeDeclined','BookingPostponeWithdrawn',
                             'BookingRescheduled')),
    LinkUrl          NVARCHAR(300) NULL,
    IsRead           BIT           NOT NULL CONSTRAINT DF_Notification_IsRead DEFAULT (0),
    CreatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_Notification_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_Notification_Person FOREIGN KEY (PersonId)
        REFERENCES dbo.Person (PersonId) ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   9. Postponement requests

   A student proposes a new date and time; the tutor approves or declines it.

   Both the ORIGINAL and the PROPOSED values live in this row. That is the point of the
   table: the booking is not touched while a proposal is pending, so a proposal that is
   declined - or simply never answered - leaves the agreed session exactly as it was, and
   an approved one still leaves a record of what the session used to be.
   --------------------------------------------------------------------------- */

CREATE TABLE dbo.PostponementRequest
(
    PostponementId   INT           IDENTITY(1,1) CONSTRAINT PK_PostponementRequest PRIMARY KEY,
    BookingId        INT           NOT NULL,
    -- Denormalised from Booking so the tutor's queue and the student's list can be filtered
    -- without joining three tables to find out whose proposal this is.
    StudentId        INT           NOT NULL,
    TutorId          INT           NOT NULL,

    OriginalDate     DATE          NOT NULL,
    OriginalStart    TIME(0)       NOT NULL,
    OriginalEnd      TIME(0)       NOT NULL,

    ProposedDate     DATE          NOT NULL,
    ProposedStart    TIME(0)       NOT NULL,
    ProposedEnd      TIME(0)       NOT NULL,

    ReasonCategory   VARCHAR(30)   NOT NULL,
    StudentNote      NVARCHAR(200) NULL,

    [Status]         VARCHAR(20)   NOT NULL
                         CONSTRAINT DF_Postponement_Status DEFAULT ('PendingTutorReview'),
    CreatedAt        DATETIME2(0)  NOT NULL
                         CONSTRAINT DF_Postponement_CreatedAt DEFAULT (SYSUTCDATETIME()),
    TutorResponseNote NVARCHAR(200) NULL,
    RespondedAt      DATETIME2(0)  NULL,

    CONSTRAINT FK_Postponement_Booking FOREIGN KEY (BookingId)
        REFERENCES dbo.Booking (BookingId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_Postponement_Student FOREIGN KEY (StudentId)
        REFERENCES dbo.Student (StudentId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_Postponement_Tutor FOREIGN KEY (TutorId)
        REFERENCES dbo.Tutor (TutorId) ON DELETE NO ACTION ON UPDATE NO ACTION,

    CONSTRAINT CK_Postponement_Status
        CHECK ([Status] IN ('PendingTutorReview','Approved','Declined','Cancelled')),
    CONSTRAINT CK_Postponement_Category
        CHECK (ReasonCategory IN
               ('TimetableClash','Medical','ExamPreparation','AssignmentDeadline','Other')),
    CONSTRAINT CK_Postponement_Times CHECK (ProposedEnd > ProposedStart),
    -- A resolved proposal has a decision time; a pending one has not.
    CONSTRAINT CK_Postponement_Responded CHECK (
        ([Status] = 'PendingTutorReview' AND RespondedAt IS NULL)
        OR ([Status] <> 'PendingTutorReview' AND RespondedAt IS NOT NULL))
);
GO

/* One unresolved proposal per booking, enforced by the database rather than by a check in
   the service. Two submissions racing each other would both pass a SELECT-then-INSERT;
   this index is what actually stops the second one. */
CREATE UNIQUE INDEX UQ_Postponement_OnePending
    ON dbo.PostponementRequest (BookingId)
    WHERE [Status] = 'PendingTutorReview';
GO

/* ---------------------------------------------------------------------------
   10. Audit log

   Written by the application, never updated or deleted by it. There is no UPDATE or
   DELETE statement against this table anywhere in the codebase, which is what
   "ordinary users cannot modify audit records" means in practice - the application
   offers no route to do so, and the account it connects as is the application's own.
   --------------------------------------------------------------------------- */

CREATE TABLE dbo.AuditLog
(
    AuditId        INT           IDENTITY(1,1) CONSTRAINT PK_AuditLog PRIMARY KEY,
    -- Nullable: a record must survive the removal of the person who caused it.
    ActorPersonId  INT           NULL,
    ActorRole      VARCHAR(15)   NOT NULL,
    ActionType     VARCHAR(40)   NOT NULL,
    EntityType     VARCHAR(30)   NOT NULL,
    EntityId       INT           NOT NULL,
    -- Free text on purpose: these hold a rendered before/after, not a parsed structure.
    PreviousState  NVARCHAR(400) NULL,
    NewState       NVARCHAR(400) NULL,
    Reason         NVARCHAR(200) NULL,
    ResponseNote   NVARCHAR(200) NULL,
    IpAddress      VARCHAR(45)   NULL,   -- 45 chars: an IPv6 address with an embedded IPv4
    CreatedAt      DATETIME2(0)  NOT NULL
                       CONSTRAINT DF_AuditLog_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_AuditLog_Actor FOREIGN KEY (ActorPersonId)
        REFERENCES dbo.Person (PersonId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT CK_AuditLog_ActorRole
        CHECK (ActorRole IN ('Student','Tutor','Administrator','System'))
);
GO

/* ---------------------------------------------------------------------------
   11. Indexes
   --------------------------------------------------------------------------- */

CREATE INDEX IX_TutorSubject_Subject_Level  ON dbo.TutorSubject (SubjectId, StudyLevelId) INCLUDE (TutorId);
CREATE INDEX IX_Availability_Slot           ON dbo.TutorAvailability (TutorSubjectId, AvailabilityDate, IsBooked);
CREATE INDEX IX_Request_Student_Status      ON dbo.SessionRequest ([Status], StudentId);
CREATE INDEX IX_Request_TutorSubject_Status ON dbo.SessionRequest (TutorSubjectId, [Status]);
CREATE INDEX IX_Booking_Date                ON dbo.Booking (BookingDate, [Status]);
CREATE INDEX IX_Notification_Person_Unread  ON dbo.Notification (PersonId, IsRead);

-- The tutor's pending queue, and the student's own list of proposals.
CREATE INDEX IX_Postponement_Tutor_Status   ON dbo.PostponementRequest (TutorId, [Status]);
CREATE INDEX IX_Postponement_Student        ON dbo.PostponementRequest (StudentId, [Status]);
-- The audit page reads newest-first, filtered by entity or by actor.
CREATE INDEX IX_AuditLog_Created            ON dbo.AuditLog (CreatedAt DESC);
CREATE INDEX IX_AuditLog_Entity             ON dbo.AuditLog (EntityType, EntityId);

-- Supports the BR-12 student-overlap guard, which locks by student and date.
CREATE INDEX IX_Booking_Availability        ON dbo.Booking (AvailabilityId);
GO

/* ---------------------------------------------------------------------------
   10. Role integrity (D-05)

   SQL Server has NO BEFORE trigger and NO FOR EACH ROW. These triggers are
   AFTER INSERT, UPDATE and work against the "inserted" pseudo-table as a SET.
   A row-by-row translation of the original MySQL triggers would check only the
   first row of a multi-row insert and silently let the rest through.

   SET XACT_ABORT ON ensures THROW rolls the statement back reliably.
   --------------------------------------------------------------------------- */

CREATE TRIGGER dbo.trg_Student_RoleIntegrity
ON dbo.Student
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN dbo.Tutor t ON t.PersonId = i.PersonId)
        THROW 50001, 'This person is already registered as a Tutor and cannot also be a Student.', 1;

    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN dbo.Person p ON p.PersonId = i.PersonId
               WHERE p.[Role] <> 'Student')
        THROW 50002, 'Person.Role must be Student for a row in dbo.Student.', 1;
END
GO

CREATE TRIGGER dbo.trg_Tutor_RoleIntegrity
ON dbo.Tutor
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN dbo.Student s ON s.PersonId = i.PersonId)
        THROW 50003, 'This person is already registered as a Student and cannot also be a Tutor.', 1;

    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN dbo.Person p ON p.PersonId = i.PersonId
               WHERE p.[Role] <> 'Tutor')
        THROW 50004, 'Person.Role must be Tutor for a row in dbo.Tutor.', 1;
END
GO

PRINT 'TutorConnect schema created successfully.';
GO
