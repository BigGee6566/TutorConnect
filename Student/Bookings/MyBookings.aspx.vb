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

Namespace StudentArea

    ''' <summary>The student's confirmed sessions.</summary>
    Public Class MyBookings
        Inherits StudentPageBase

        Private ReadOnly _bookings As IBookingService = New BookingService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents rptBookings As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents btnRefresh As Button

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "My Bookings"
                shell.PageSubtitle = "View your scheduled sessions"
            End If

            If Not IsPostBack AndAlso Request.QueryString("reviewed") = "1" Then
                alert.Show(AlertKind.Success, "Thank you. Your review has been submitted.")
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim studentId As Integer = Await ResolveStudentIdAsync()
            If studentId = 0 Then Return

            Dim result As ServiceResult(Of IList(Of Booking)) =
                Await _bookings.GetForStudentAsync(studentId).ConfigureAwait(True)

            rptBookings.DataSource = result.Data
            rptBookings.DataBind()

            rptBookings.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
        End Function

        Protected Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
            alert.Hide()
        End Sub

        Protected Function GetDetailUrl(bookingId As Object) As String
            Dim id As Integer = Convert.ToInt32(bookingId, CultureInfo.InvariantCulture)
            Return ResolveUrl($"~/Student/Bookings/BookingDetails.aspx?bookingId={id}")
        End Function

        ''' <summary>
        ''' A link into the detail page with the cancel or postpone panel already open.
        ''' </summary>
        ''' <remarks>
        ''' The action name is a literal from this file, never anything the browser sent, and
        ''' the detail page matches it against known values rather than echoing it.
        ''' </remarks>
        Protected Function GetActionUrl(bookingId As Object, action As String) As String
            Dim id As Integer = Convert.ToInt32(bookingId, CultureInfo.InvariantCulture)
            Return ResolveUrl($"~/Student/Bookings/BookingDetails.aspx?bookingId={id}&action={action}")
        End Function

        ''' <summary>
        ''' Whether the row is a session that has still to happen.
        ''' </summary>
        ''' <remarks>
        ''' Only decides whether to draw the buttons. The services check it again on postback,
        ''' because a hidden control can still be posted to.
        ''' </remarks>
        Protected Shared Function IsScheduled(status As Object) As Boolean
            Return status IsNot Nothing AndAlso
                   String.Equals(status.ToString(), "Scheduled", StringComparison.Ordinal)
        End Function

        Protected Shared Function GetDate(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

    End Class

End Namespace
