Option Strict On
Option Explicit On

Imports Npgsql
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Validation

Namespace Services

    ''' <summary>
    ''' What <see cref="ISubjectCatalogueService.EnsureSubjectAsync"/> did.
    ''' </summary>
    Public NotInheritable Class SubjectOutcome

        Public Sub New(subjectId As Integer, subjectName As String, wasCreated As Boolean)
            Me.SubjectId = subjectId
            Me.SubjectName = subjectName
            Me.WasCreated = wasCreated
        End Sub

        Public ReadOnly Property SubjectId As Integer

        ''' <summary>
        ''' The name as it is stored - which is the <em>existing</em> spelling when a match
        ''' was reused, not what the person typed. The caller shows this so somebody who
        ''' typed "maths" learns they were added to "Mathematics".
        ''' </summary>
        Public ReadOnly Property SubjectName As String

        ''' <summary>False when an existing subject was reused.</summary>
        Public ReadOnly Property WasCreated As Boolean

    End Class

    ''' <summary>
    ''' Turns a subject name typed by a person into a usable SubjectId, creating the subject
    ''' only when nothing matches.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' All three roles can now add a subject, and all three go through here. That is the
    ''' whole point of the class: Find Tutor matches a student to a tutor by
    ''' <c>SubjectId</c> equality (TutorRepository.SearchAsync), so if the student path and
    ''' the tutor path ever normalised names differently, a tutor typing "maths" and a
    ''' student typing "Maths" would land on two subjects and never find each other.
    ''' </para>
    ''' <para>
    ''' A subject created here is live immediately. The audit log records who created it -
    ''' the ActorRole CHECK constraint already permits 'Student' and 'Tutor' - and an
    ''' administrator can deactivate or delete it from Manage Subjects afterwards.
    ''' </para>
    ''' <para>
    ''' What this does <em>not</em> do is recognise synonyms: "Maths" and "Mathematics" are
    ''' different strings and become two subjects. Case and spacing are handled; meaning is
    ''' not. Merging two subjects would need to repoint TutorSubject and StudentSubject and
    ''' is a separate piece of work.
    ''' </para>
    ''' </remarks>
    Public Class SubjectCatalogueService
        Implements ISubjectCatalogueService

        Private ReadOnly _subjects As SubjectRepository
        Private ReadOnly _audit As IAuditService

        Public Sub New()
            Me.New(New SubjectRepository(), New AuditService())
        End Sub

        Public Sub New(subjects As SubjectRepository, audit As IAuditService)
            _subjects = subjects
            _audit = audit
        End Sub

        ''' <inheritdoc/>
        Public Async Function EnsureSubjectAsync(actingPersonId As Integer,
                                                 actorRole As UserRole,
                                                 subjectName As String) As Task(Of ServiceResult(Of SubjectOutcome)) _
            Implements ISubjectCatalogueService.EnsureSubjectAsync

            Dim validation As ValidationResult = SubjectValidator.ValidateName(subjectName)
            If Not validation.IsValid Then
                Return ServiceResult(Of SubjectOutcome).Fail(validation.ToSummary())
            End If

            ' Everything below compares and stores this, never the raw text.
            Dim name As String = SubjectValidator.Normalise(subjectName)

            Dim existing As Subject = Await _subjects.FindByNameAsync(name).ConfigureAwait(False)
            If existing IsNot Nothing Then
                Return ReuseOrRefuse(existing)
            End If

            Dim subjectId As Integer
            Dim lostTheRace As Boolean = False

            Try
                subjectId = Await _subjects.CreateSubjectAsync(name).ConfigureAwait(False)

            Catch ex As Npgsql.PostgresException When TutorConnect.Data.DatabaseHelper.IsUniqueViolation(ex)
                ' Two people named the same new subject at once and the unique index caught
                ' the loser. Both must succeed, on the same row. The recovery cannot happen
                ' here - VB forbids Await inside a Catch - so it is flagged and handled
                ' immediately below.
                lostTheRace = True
            End Try

            If lostTheRace Then
                Dim raced As Subject = Await _subjects.FindByNameAsync(name).ConfigureAwait(False)

                If raced Is Nothing Then
                    ' The index rejected the insert but the row is not there: not the race
                    ' this handles, so do not paper over it.
                    Return ServiceResult(Of SubjectOutcome).Fail(
                        "That subject could not be added. Try again.")
                End If

                Return ReuseOrRefuse(raced)
            End If

            Await _audit.RecordAsync(
                actingPersonId, actorRole.ToString(),
                AuditAction.SubjectCreated,
                AuditService.SubjectEntity, subjectId,
                Nothing, name, Nothing, Nothing).ConfigureAwait(False)

            Return ServiceResult(Of SubjectOutcome).Ok(
                New SubjectOutcome(subjectId, name, wasCreated:=True),
                $"'{name}' was added to the subject list.")
        End Function

        ''' <summary>
        ''' A name that already exists is either reused or refused - never quietly created a
        ''' second time.
        ''' </summary>
        ''' <remarks>
        ''' An inactive subject is one an administrator withdrew on purpose (AT-25).
        ''' Re-creating it by retyping the name would undo a moderation decision, so the
        ''' answer is a refusal that says what happened rather than a silent no-op.
        ''' </remarks>
        Private Shared Function ReuseOrRefuse(existing As Subject) As ServiceResult(Of SubjectOutcome)
            If Not existing.IsActive Then
                Return ServiceResult(Of SubjectOutcome).Fail(
                    $"'{existing.SubjectName}' was withdrawn by an administrator and cannot be added. " &
                    "Ask an administrator to restore it.")
            End If

            Return ServiceResult(Of SubjectOutcome).Ok(
                New SubjectOutcome(existing.SubjectId, existing.SubjectName, wasCreated:=False),
                $"'{existing.SubjectName}' is already on the subject list, so that one was used.")
        End Function

    End Class

End Namespace
