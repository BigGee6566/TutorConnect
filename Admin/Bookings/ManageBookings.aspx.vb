Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Enums
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services
Imports TutorConnect.Utilities

Namespace AdminArea

    ''' <summary>
    ''' Oversight of every request and booking, plus cancellation on behalf of the parties.
    ''' </summary>
    ''' <remarks>
    ''' Requests stay read-only: accepting or declining on a tutor's behalf would put a
    ''' request in a state neither party agreed to, and the notification would name the wrong
    ''' person. Cancelling a booking is different - it is the intervention an administrator
    ''' is actually asked for, and Part 12.7 requires it to be audited. It takes a reason,
    ''' tells both parties, and writes an audit record.
    ''' </remarks>
    Public Class ManageBookings
        Inherits BasePage

        Private ReadOnly _admin As IAdminService = New AdminService()
        Private ReadOnly _bookings As IBookingService = New BookingService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents ddlRequestStatus As DropDownList
        Protected WithEvents rptRequests As Repeater
        Protected WithEvents pnlNoRequests As Panel
        Protected WithEvents rptBookings As Repeater
        Protected WithEvents pnlNoBookings As Panel

        Protected WithEvents pnlCancel As Panel
        Protected WithEvents litCancelId As Literal
        Protected WithEvents ddlReason As DropDownList
        Protected WithEvents txtNote As TextBox
        Protected WithEvents btnConfirmCancel As Button
        Protected WithEvents btnAbandon As Button

        Protected WithEvents pnlMove As Panel
        Protected WithEvents litMoveId As Literal
        Protected WithEvents txtMoveDate As TextBox
        Protected WithEvents txtMoveStart As TextBox
        Protected WithEvents txtMoveEnd As TextBox
        Protected WithEvents txtMoveReason As TextBox
        Protected WithEvents btnConfirmMove As Button
        Protected WithEvents btnAbandonMove As Button

        Private Shared ReadOnly SouthAfrica As CultureInfo = CultureInfo.GetCultureInfo("en-ZA")

        Private Property MovingId As Integer
            Get
                Return CInt(If(ViewState("MovingId"), 0))
            End Get
            Set(value As Integer)
                ViewState("MovingId") = value
            End Set
        End Property

        ''' <summary>
        ''' Which booking the cancel form is for.
        ''' </summary>
        ''' <remarks>
        ''' Only decides what to draw. The service re-reads the booking and re-checks it is
        ''' still Scheduled, so a tampered value cancels nothing that was not cancellable.
        ''' </remarks>
        Private Property CancellingId As Integer
            Get
                Return CInt(If(ViewState("CancellingId"), 0))
            End Get
            Set(value As Integer)
                ViewState("CancellingId") = value
            End Set
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As AdminShell = TryCast(Master, AdminShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Bookings"
                shell.PageSubtitle = "All requests and bookings across the platform"
            End If

            If Not IsPostBack Then
                ddlReason.DataSource = ScheduleChangeReasons.All()
                ddlReason.DataBind()
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim requests As ServiceResult(Of IList(Of SessionRequest)) =
                Await _admin.GetAllRequestsAsync(ddlRequestStatus.SelectedValue).ConfigureAwait(True)

            rptRequests.DataSource = requests.Data
            rptRequests.DataBind()
            rptRequests.Visible = requests.Data.Count > 0
            pnlNoRequests.Visible = requests.Data.Count = 0

            Dim bookings As ServiceResult(Of IList(Of Booking)) =
                Await _admin.GetAllBookingsAsync().ConfigureAwait(True)

            rptBookings.DataSource = bookings.Data
            rptBookings.DataBind()
            rptBookings.Visible = bookings.Data.Count > 0
            pnlNoBookings.Visible = bookings.Data.Count = 0
        End Function

        Protected Shared Function GetDate(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

        Protected Shared Function GetYesNo(value As Object) As String
            Return If(Convert.ToBoolean(value, CultureInfo.InvariantCulture), "Yes", "No")
        End Function

        Protected Shared Function IsScheduled(status As Object) As Boolean
            Return status IsNot Nothing AndAlso
                   String.Equals(status.ToString(), "Scheduled", StringComparison.Ordinal)
        End Function

        ''' <summary>
        ''' Who cancelled and why, in one cell. A dash where there is nothing to say, rather
        ''' than an empty cell that reads as a rendering fault.
        ''' </summary>
        Protected Shared Function DescribeCancellation(item As Object) As String
            Dim booking As Booking = TryCast(item, Booking)
            If booking Is Nothing OrElse booking.Status <> BookingStatus.Cancelled Then Return "-"

            Dim who As String = If(String.IsNullOrWhiteSpace(booking.CancelledByName),
                                   "Unknown", booking.CancelledByName)

            Return $"{ScheduleChangeReasons.Describe(booking.CancelReasonCategory)} ({who})"
        End Function

#Region "Cancelling on behalf of the parties"

        Protected Sub Cancel_Command(sender As Object, e As CommandEventArgs)
            Dim bookingId As Integer
            Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), bookingId)

            CancellingId = bookingId
            litCancelId.Text = bookingId.ToString(CultureInfo.InvariantCulture)
            pnlCancel.Visible = True
            txtNote.Text = String.Empty
        End Sub

        Protected Sub btnAbandon_Click(sender As Object, e As EventArgs) Handles btnAbandon.Click
            CancellingId = 0
            pnlCancel.Visible = False
        End Sub

        Protected Sub Move_Command(sender As Object, e As CommandEventArgs)
            Dim bookingId As Integer
            Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), bookingId)

            MovingId = bookingId
            litMoveId.Text = bookingId.ToString(CultureInfo.InvariantCulture)
            pnlMove.Visible = True
            pnlCancel.Visible = False
            txtMoveReason.Text = String.Empty
        End Sub

        Protected Sub btnAbandonMove_Click(sender As Object, e As EventArgs) Handles btnAbandonMove.Click
            MovingId = 0
            pnlMove.Visible = False
        End Sub

        Protected Sub btnConfirmMove_Click(sender As Object, e As EventArgs) Handles btnConfirmMove.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    pnlMove.Visible = True

                    Dim newDate As Date
                    If Not Date.TryParse(txtMoveDate.Text, SouthAfrica, DateTimeStyles.None, newDate) Then
                        alert.Show(AlertKind.Error, "Choose the new date.")
                        Return
                    End If

                    Dim start As TimeSpan, finish As TimeSpan
                    If Not TimeSpan.TryParse(txtMoveStart.Text, SouthAfrica, start) OrElse
                       Not TimeSpan.TryParse(txtMoveEnd.Text, SouthAfrica, finish) Then
                        alert.Show(AlertKind.Error, "Give both a start and an end time.")
                        Return
                    End If

                    Dim result As ServiceResult = Await _bookings.RescheduleAsync(
                        CurrentPersonId, MovingId, newDate, start, finish,
                        txtMoveReason.Text.Trim()).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

                    If result.Success Then
                        MovingId = 0
                        pnlMove.Visible = False
                    End If

                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub btnConfirmCancel_Click(sender As Object, e As EventArgs) Handles btnConfirmCancel.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim reason As ScheduleChangeReason

                    If Not ScheduleChangeReasons.TryParse(ddlReason.SelectedValue, reason) Then
                        alert.Show(AlertKind.Error, "Choose a reason.")
                        pnlCancel.Visible = True
                        Return
                    End If

                    Dim result As ServiceResult = Await _bookings.CancelAsync(
                        CurrentPersonId, CancellingId, reason, txtNote.Text.Trim()).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

                    If result.Success Then
                        CancellingId = 0
                        pnlCancel.Visible = False
                    Else
                        pnlCancel.Visible = True
                    End If

                    Await BindAsync()
                End Function))
        End Sub

#End Region

    End Class

End Namespace
