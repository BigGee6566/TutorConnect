Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports Npgsql
Imports System.Threading.Tasks
Imports System.Web
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models

Namespace Services

    ''' <summary>
    ''' Records what changed, who changed it and why (Part 12.7).
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' Until now only review moderation kept that history - deactivating a user, approving
    ''' a tutor or deleting a subject left nothing behind but a notification. This closes
    ''' that asymmetry.
    ''' </para>
    ''' <para>
    ''' Writing an audit record must never be the reason an operation fails. The record is
    ''' written inside the caller's transaction where one exists, so it rolls back with the
    ''' change it describes; where no transaction exists, a failure to write is swallowed and
    ''' the operation stands. Losing one line of history is bad; refusing a cancellation
    ''' because the history could not be written is worse.
    ''' </para>
    ''' </remarks>
    Public Class AuditService
        Implements IAuditService

        Private ReadOnly _audit As AuditRepository

        Public Sub New()
            Me.New(New AuditRepository())
        End Sub

        Public Sub New(audit As AuditRepository)
            _audit = If(audit, New AuditRepository())
        End Sub

        ''' <summary>Entity type names, so a typo cannot split one entity's history in two.</summary>
        Public Const BookingEntity As String = "Booking"
        Public Const PostponementEntity As String = "PostponementRequest"
        Public Const PersonEntity As String = "Person"
        Public Const TutorEntity As String = "Tutor"
        Public Const SubjectEntity As String = "Subject"
        Public Const ReviewEntity As String = "Review"

        ''' <inheritdoc/>
        Public Async Function RecordAsync(actorPersonId As Integer?,
                                          actorRole As String,
                                          action As AuditAction,
                                          entityType As String,
                                          entityId As Integer,
                                          previousState As String,
                                          newState As String,
                                          reason As String,
                                          responseNote As String,
                                          Optional connection As NpgsqlConnection = Nothing,
                                          Optional transaction As NpgsqlTransaction = Nothing) As Task _
            Implements IAuditService.RecordAsync

            Dim entry As New AuditEntry With {
                .ActorPersonId = actorPersonId,
                .ActorRole = If(String.IsNullOrWhiteSpace(actorRole), "System", actorRole),
                .ActionType = action,
                .EntityType = entityType,
                .EntityId = entityId,
                .PreviousState = Truncate(previousState, 400),
                .NewState = Truncate(newState, 400),
                .Reason = Truncate(reason, 200),
                .ResponseNote = Truncate(responseNote, 200),
                .IpAddress = CurrentIpAddress()
            }

            If transaction IsNot Nothing Then
                ' Inside the caller's transaction on purpose: a record written outside it
                ' survives a rollback and claims something happened that did not.
                Await _audit.AppendAsync(entry, connection, transaction).ConfigureAwait(False)
                Return
            End If

            Try
                Await _audit.AppendAsync(entry).ConfigureAwait(False)
            Catch ex As Npgsql.PostgresException
                ' Best effort - see the class remarks.
            End Try
        End Function

        ''' <inheritdoc/>
        Public Async Function SearchAsync(actionType As String,
                                          entityType As String,
                                          actorPersonId As Integer) As Task(Of ServiceResult(Of IList(Of AuditEntry))) _
            Implements IAuditService.SearchAsync

            Dim rows As IList(Of AuditEntry) =
                Await _audit.SearchAsync(actionType, entityType, actorPersonId).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of AuditEntry)).Ok(rows)
        End Function

        ''' <summary>
        ''' The caller's address, where there is a request to read it from.
        ''' </summary>
        ''' <remarks>
        ''' Nothing outside a web request - the tests call the services directly, and an
        ''' invented address would be worse than none.
        ''' </remarks>
        Private Shared Function CurrentIpAddress() As String
            Dim context As HttpContext = HttpContext.Current
            If context Is Nothing OrElse context.Request Is Nothing Then Return Nothing

            Try
                Return context.Request.UserHostAddress
            Catch ex As HttpException
                Return Nothing
            End Try
        End Function

        Private Shared Function Truncate(value As String, limit As Integer) As String
            If String.IsNullOrEmpty(value) Then Return Nothing
            If value.Length <= limit Then Return value

            ' The column is the constraint, not the caller's discipline. A state summary that
            ' grew past the column would otherwise fail the write and lose the record.
            Return value.Substring(0, limit)
        End Function

    End Class

End Namespace
