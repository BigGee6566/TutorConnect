Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace TutorArea

    ''' <summary>
    ''' Incoming requests, with accept and decline (AT-14, AT-15, AT-16).
    ''' </summary>
    ''' <remarks>
    ''' Both actions are postbacks, not links, so neither can be triggered by a GET. The
    ''' ViewStateUserKey set in BasePage ties the postback to this session (SEC-06), and
    ''' the service scopes every write by the signed-in tutor.
    ''' </remarks>
    Public Class StudentRequests
        Inherits TutorPageBase

        Private ReadOnly _requests As IRequestService = New RequestService()
        Private ReadOnly _bookings As IBookingService = New BookingService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents rptRequests As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents btnRefresh As Button
        Protected WithEvents pnlDecline As Panel
        Protected WithEvents litDeclineId As Literal
        Protected WithEvents txtDeclineReason As TextBox
        Protected WithEvents btnConfirmDecline As Button
        Protected WithEvents btnCancelDecline As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As TutorShell = TryCast(Master, TutorShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Student Requests"
                shell.PageSubtitle = "View and respond to requests"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim tutorId As Integer = Await ResolveTutorIdAsync()
            If tutorId = 0 Then Return

            Dim result As ServiceResult(Of IList(Of SessionRequest)) =
                Await _requests.GetForTutorAsync(tutorId).ConfigureAwait(True)

            rptRequests.DataSource = result.Data
            rptRequests.DataBind()

            rptRequests.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0

            ' Keep the sidebar pill honest after an accept or decline.
            Dim shell As TutorShell = TryCast(Master, TutorShell)
            If shell IsNot Nothing Then
                shell.PendingRequestCount =
                    Await _requests.CountPendingForTutorAsync(tutorId).ConfigureAwait(True)
            End If
        End Function

        Protected Sub Accept_Command(sender As Object, e As CommandEventArgs)
            Dim requestId As Integer = ParseId(e.CommandArgument)

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim tutorId As Integer = Await ResolveTutorIdAsync()

                    Dim result As ServiceResult(Of Integer) =
                        Await _bookings.AcceptRequestAsync(tutorId, requestId).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        ''' <summary>
        ''' The request currently being declined, held in ViewState across the two stages.
        ''' </summary>
        ''' <remarks>
        ''' ViewState is signed and tied to this session by the ViewStateUserKey, so the id
        ''' cannot be swapped for another tutor's request. The service scopes the write by
        ''' tutor as well, so even a forged value would change nothing.
        ''' </remarks>
        Private Property DecliningRequestId As Integer
            Get
                Dim value As Object = ViewState("DecliningRequestId")
                Return If(value Is Nothing, 0, CInt(value))
            End Get
            Set(value As Integer)
                ViewState("DecliningRequestId") = value
            End Set
        End Property

        ''' <summary>Opens the reason panel. The decline happens on confirmation.</summary>
        Protected Sub Decline_Command(sender As Object, e As CommandEventArgs)
            DecliningRequestId = ParseId(e.CommandArgument)

            litDeclineId.Text = $"#{DecliningRequestId}"
            txtDeclineReason.Text = String.Empty
            pnlDecline.Visible = True
            alert.Hide()
        End Sub

        Protected Sub btnCancelDecline_Click(sender As Object, e As EventArgs) Handles btnCancelDecline.Click
            DecliningRequestId = 0
            pnlDecline.Visible = False
        End Sub

        Protected Sub btnConfirmDecline_Click(sender As Object, e As EventArgs) Handles btnConfirmDecline.Click
            Dim requestId As Integer = DecliningRequestId
            Dim reason As String = txtDeclineReason.Text.Trim()

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    If requestId <= 0 Then
                        alert.Show(AlertKind.Error, "Choose a request to decline.")
                        Return
                    End If

                    Dim tutorId As Integer = Await ResolveTutorIdAsync()

                    ' An empty reason is passed through as Nothing so the column stays NULL
                    ' rather than holding an empty string, and the student's notification
                    ' omits the "Reason:" clause entirely.
                    Dim result As ServiceResult = Await _bookings.DeclineRequestAsync(
                        tutorId, requestId,
                        If(reason.Length = 0, Nothing, reason)).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

                    If result.Success Then
                        DecliningRequestId = 0
                        pnlDecline.Visible = False
                    End If

                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
            alert.Hide()
        End Sub

        Protected Shared Function GetDate(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

        Private Shared Function ParseId(value As Object) As Integer
            Dim parsed As Integer
            Integer.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), parsed)
            Return parsed
        End Function

    End Class

End Namespace
