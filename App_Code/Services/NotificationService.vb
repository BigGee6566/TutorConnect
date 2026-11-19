Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models

Namespace Services

    ''' <summary>
    ''' In-app notifications (BR-27).
    ''' </summary>
    ''' <remarks>
    ''' Called by the services that change state, never by a page. Delivery is in-app only;
    ''' email and SMS are out of scope.
    '''
    ''' A notification failing must not undo the thing it describes: if writing the record
    ''' throws, the accept or the review has already been committed and is still correct.
    ''' So <see cref="NotifyAsync"/> reports failure through its result rather than
    ''' propagating, and callers treat it as advisory.
    ''' </remarks>
    Public Class NotificationService
        Implements INotificationService

        Private ReadOnly _notifications As NotificationRepository

        Public Sub New()
            Me.New(New NotificationRepository())
        End Sub

        Public Sub New(notifications As NotificationRepository)
            If notifications Is Nothing Then Throw New ArgumentNullException(NameOf(notifications))
            _notifications = notifications
        End Sub

        ''' <inheritdoc/>
        Public Async Function NotifyAsync(personId As Integer,
                                          notificationType As NotificationType,
                                          title As String,
                                          message As String,
                                          linkUrl As String) As Task(Of ServiceResult) _
            Implements INotificationService.NotifyAsync

            If personId <= 0 Then Return ServiceResult.Fail("No recipient for the notification.")

            Try
                Await _notifications.CreateAsync(New Notification With {
                    .PersonId = personId,
                    .Title = Truncate(title, 150),
                    .Message = Truncate(message, 500),
                    .NotificationType = notificationType,
                    .LinkUrl = linkUrl
                }).ConfigureAwait(False)

                Return ServiceResult.Ok()

            Catch ex As Npgsql.PostgresException
                ' Deliberately swallowed into a failed result: the caller's transaction has
                ' already committed and must not be reported as failed because a
                ' notification could not be written.
                Return ServiceResult.Fail("The notification could not be saved.")
            End Try
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForPersonAsync(personId As Integer,
                                                unreadOnly As Boolean) As Task(Of ServiceResult(Of IList(Of Notification))) _
            Implements INotificationService.GetForPersonAsync

            Dim items As IList(Of Notification) =
                Await _notifications.GetForPersonAsync(personId, unreadOnly).ConfigureAwait(False)

            Return ServiceResult(Of IList(Of Notification)).Ok(items)
        End Function

        ''' <inheritdoc/>
        Public Async Function CountUnreadAsync(personId As Integer) As Task(Of Integer) _
            Implements INotificationService.CountUnreadAsync

            Return Await _notifications.CountUnreadAsync(personId).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Async Function MarkReadAsync(personId As Integer,
                                            notificationId As Integer) As Task(Of ServiceResult) _
            Implements INotificationService.MarkReadAsync

            Dim ok As Boolean = Await _notifications.MarkReadAsync(notificationId, personId).ConfigureAwait(False)
            Return If(ok, ServiceResult.Ok(), ServiceResult.Fail("That notification could not be updated."))
        End Function

        ''' <inheritdoc/>
        Public Async Function MarkAllReadAsync(personId As Integer) As Task(Of ServiceResult) _
            Implements INotificationService.MarkAllReadAsync

            Await _notifications.MarkAllReadAsync(personId).ConfigureAwait(False)
            Return ServiceResult.Ok()
        End Function

        Private Shared Function Truncate(value As String, maximum As Integer) As String
            If String.IsNullOrEmpty(value) Then Return String.Empty
            Return If(value.Length <= maximum, value, value.Substring(0, maximum))
        End Function

    End Class

End Namespace

