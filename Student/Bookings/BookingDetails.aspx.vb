Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Enums
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services
Imports TutorConnect.Utilities

Namespace StudentArea

    ''' <summary>
    ''' One booking in full, with the review, cancel and postpone actions when they are due.
    ''' </summary>
    ''' <remarks>
    ''' The booking id comes from the query string, so ownership is decided by the service
    ''' from the signed-in PersonId (BR-24). A booking belonging to someone else produces
    ''' the same message as one that does not exist.
    '''
    ''' The buttons appear only when the action applies, but every one of them is checked
    ''' again in the service on postback. Hiding a control is presentation; a hidden control
    ''' can still be posted to.
    ''' </remarks>
    Public Class BookingDetails
        Inherits StudentPageBase

        Private ReadOnly _bookings As IBookingService = New BookingService()
        Private ReadOnly _reviews As IReviewService = New ReviewService()
        Private ReadOnly _postponements As IPostponementService = New PostponementService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents pnlDetail As Panel
        Protected WithEvents litId As Literal
        Protected WithEvents litTutor As Literal
        Protected WithEvents litSubject As Literal
        Protected WithEvents litDate As Literal
        Protected WithEvents litTime As Literal
        Protected WithEvents litRate As Literal
        Protected WithEvents litCreated As Literal
        Protected WithEvents badge As StatusBadge
        Protected WithEvents lnkWriteReview As HyperLink

        Protected WithEvents phCancelled As PlaceHolder
        Protected WithEvents litCancelledAt As Literal
        Protected WithEvents litCancelledBy As Literal
        Protected WithEvents litCancelReason As Literal
        Protected WithEvents litCancelNote As Literal

        Protected WithEvents pnlPostponeState As Panel
        Protected WithEvents litProposed As Literal
        Protected WithEvents litOriginal As Literal
        Protected WithEvents litPostponeReason As Literal
        Protected WithEvents litPostponeStatus As Literal
        Protected WithEvents litTutorResponse As Literal
        Protected WithEvents btnWithdraw As Button

        ''' <summary>
        ''' The open proposal on this booking, so Withdraw knows what to take back.
        ''' </summary>
        ''' <remarks>
        ''' The service re-reads it and re-checks the student owns it, so a tampered value
        ''' withdraws nothing (BR-24).
        ''' </remarks>
        Private Property PendingPostponementId As Integer
            Get
                Return CInt(If(ViewState("PendingPostponementId"), 0))
            End Get
            Set(value As Integer)
                ViewState("PendingPostponementId") = value
            End Set
        End Property

        Protected WithEvents btnShowCancel As Button
        Protected WithEvents btnShowPostpone As Button

        Protected WithEvents pnlCancel As Panel
        Protected WithEvents ddlCancelReason As DropDownList
        Protected WithEvents txtCancelNote As TextBox
        Protected WithEvents btnConfirmCancel As Button
        Protected WithEvents btnAbandonCancel As Button

        Protected WithEvents pnlPostpone As Panel
        Protected WithEvents txtNewDate As TextBox
        Protected WithEvents txtNewStart As TextBox
        Protected WithEvents txtNewEnd As TextBox
        Protected WithEvents ddlPostponeReason As DropDownList
        Protected WithEvents txtPostponeNote As TextBox
        Protected WithEvents btnConfirmPostpone As Button
        Protected WithEvents btnAbandonPostpone As Button

        Private ReadOnly Property BookingId As Integer
            Get
                Dim parsed As Integer
                Return If(Integer.TryParse(Request.QueryString("bookingId"), parsed), parsed, 0)
            End Get
        End Property

        Private Shared ReadOnly SouthAfrica As CultureInfo = CultureInfo.GetCultureInfo("en-ZA")

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            If Not IsPostBack Then
                ddlCancelReason.DataSource = ScheduleChangeReasons.All()
                ddlCancelReason.DataBind()
                ddlPostponeReason.DataSource = ScheduleChangeReasons.All()
                ddlPostponeReason.DataBind()

                ' My Bookings links straight to one of the two panels. Matched against known
                ' values; nothing from the query string is rendered.
                Select Case Request.QueryString("action")
                    Case "cancel" : pnlCancel.Visible = True
                    Case "postpone" : pnlPostpone.Visible = True
                End Select
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

#Region "Display"

        Private Async Function LoadAsync() As Task
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell IsNot Nothing Then shell.PageTitle = "Booking Details"

            Dim result As ServiceResult(Of Booking) =
                Await _bookings.GetForViewerAsync(BookingId, CurrentPersonId).ConfigureAwait(True)

            If Not result.Success Then
                pnlDetail.Visible = False
                pnlCancel.Visible = False
                pnlPostpone.Visible = False
                alert.Show(AlertKind.Error, result.Message)
                Return
            End If

            Dim booking As Booking = result.Data

            litId.Text = booking.BookingId.ToString(SouthAfrica)
            litTutor.Text = booking.TutorName
            litSubject.Text = $"{booking.SubjectName} - {booking.StudyLevelName}"
            litDate.Text = booking.BookingDate.ToString("dd/MM/yyyy", SouthAfrica)
            litTime.Text = booking.TimeRange
            litRate.Text = If(booking.RatePerSession > 0D,
                              booking.RatePerSession.ToString("C", SouthAfrica),
                              "Not recorded")
            litCreated.Text = booking.CreatedAt.ToString("dd/MM/yyyy", SouthAfrica)
            badge.Status = booking.Status.ToString()

            If shell IsNot Nothing Then
                shell.PageSubtitle = $"{booking.SubjectName} with {booking.TutorName}"
            End If

            ShowCancellationHistory(booking)
            Await ShowPostponementStateAsync(booking).ConfigureAwait(True)
            ShowOutcomeMessage()

            ' Both actions belong only to a session that is still going to happen.
            Dim isLive As Boolean = booking.Status = BookingStatus.Scheduled
            btnShowCancel.Visible = isLive AndAlso Not pnlCancel.Visible AndAlso Not pnlPostpone.Visible
            btnShowPostpone.Visible = btnShowCancel.Visible AndAlso Not HasOpenProposal

            Dim studentId As Integer = Await ResolveStudentIdAsync()

            If booking.Status = BookingStatus.Completed Then
                If Await _reviews.CanReviewAsync(studentId, booking.BookingId).ConfigureAwait(True) Then
                    lnkWriteReview.Visible = True
                    lnkWriteReview.NavigateUrl =
                        $"~/Student/Reviews/WriteReview.aspx?bookingId={booking.BookingId}"
                Else
                    alert.Show(AlertKind.Info, "You have already reviewed this session.")
                End If
            End If
        End Function

        ''' <summary>Whether an unanswered proposal exists, so a second cannot be started.</summary>
        Private Property HasOpenProposal As Boolean
            Get
                Return CBool(If(ViewState("HasOpenProposal"), False))
            End Get
            Set(value As Boolean)
                ViewState("HasOpenProposal") = value
            End Set
        End Property

        ''' <summary>
        ''' Confirms an action that has just completed.
        ''' </summary>
        ''' <remarks>
        ''' Matched against known values and never echoed from the URL, so nothing from the
        ''' query string reaches the page (SEC-09).
        ''' </remarks>
        Private Sub ShowOutcomeMessage()
            Select Case Request.QueryString("cancelled")
                Case "1"
                    alert.Show(AlertKind.Success,
                               "The session was cancelled and your tutor has been told.")
                    Return
            End Select

            If Request.QueryString("postponed") = "1" Then
                alert.Show(AlertKind.Success,
                           "Your request has gone to the tutor. The session stays as it is " &
                           "until they answer.")
                Return
            End If

            If Request.QueryString("withdrawn") = "1" Then
                alert.Show(AlertKind.Success,
                           "Request withdrawn. Your session is unchanged and the tutor has been told.")
            End If
        End Sub

        Private Sub ShowCancellationHistory(booking As Booking)
            If booking.Status <> BookingStatus.Cancelled Then
                phCancelled.Visible = False
                Return
            End If

            phCancelled.Visible = True

            litCancelledAt.Text = If(booking.CancelledAt.HasValue,
                                     booking.CancelledAt.Value.ToString("dd/MM/yyyy HH:mm", SouthAfrica),
                                     "Not recorded")
            ' A placeholder rather than an empty cell: a blank looks like a rendering fault.
            litCancelledBy.Text = If(String.IsNullOrWhiteSpace(booking.CancelledByName),
                                     "Not recorded", booking.CancelledByName)
            litCancelReason.Text = If(booking.CancelReasonCategory.HasValue,
                                      ScheduleChangeReasons.Describe(booking.CancelReasonCategory),
                                      "Not recorded")
            litCancelNote.Text = If(String.IsNullOrWhiteSpace(booking.CancelReason),
                                    "-", booking.CancelReason)
        End Sub

        Private Async Function ShowPostponementStateAsync(booking As Booking) As Task
            Dim proposal As PostponementRequest =
                Await _postponements.GetPendingForBookingAsync(booking.BookingId).ConfigureAwait(True)

            HasOpenProposal = proposal IsNot Nothing
            PendingPostponementId = If(proposal Is Nothing, 0, proposal.PostponementId)

            If proposal Is Nothing Then
                pnlPostponeState.Visible = False
                Return
            End If

            pnlPostponeState.Visible = True
            litProposed.Text = proposal.ProposedRange
            litOriginal.Text = proposal.OriginalRange
            litPostponeReason.Text = ScheduleChangeReasons.Describe(proposal.ReasonCategory)
            litPostponeStatus.Text = "Waiting for the tutor to answer"
            litTutorResponse.Text = If(String.IsNullOrWhiteSpace(proposal.TutorResponseNote),
                                       "-", proposal.TutorResponseNote)
        End Function

#End Region

#Region "Showing and hiding the panels"

        Protected Sub btnShowCancel_Click(sender As Object, e As EventArgs) Handles btnShowCancel.Click
            pnlCancel.Visible = True
            pnlPostpone.Visible = False
        End Sub

        Protected Sub btnShowPostpone_Click(sender As Object, e As EventArgs) Handles btnShowPostpone.Click
            pnlPostpone.Visible = True
            pnlCancel.Visible = False

            ' Seeded with the session's own times, so the common case - same slot, different
            ' day - takes one change rather than three.
            txtNewStart.Text = litTime.Text.Split("-"c)(0).Trim()
            txtNewEnd.Text = If(litTime.Text.Contains("-"), litTime.Text.Split("-"c)(1).Trim(), String.Empty)
        End Sub

        Protected Sub btnAbandonCancel_Click(sender As Object, e As EventArgs) Handles btnAbandonCancel.Click
            pnlCancel.Visible = False
        End Sub

        Protected Sub btnAbandonPostpone_Click(sender As Object, e As EventArgs) Handles btnAbandonPostpone.Click
            pnlPostpone.Visible = False
        End Sub

#End Region

#Region "Cancel"

        Protected Sub btnConfirmCancel_Click(sender As Object, e As EventArgs) Handles btnConfirmCancel.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf CancelAsync))
        End Sub

        Private Async Function CancelAsync() As Task
            Dim reason As ScheduleChangeReason

            If Not ScheduleChangeReasons.TryParse(ddlCancelReason.SelectedValue, reason) Then
                alert.Show(AlertKind.Error, "Choose a reason for cancelling.")
                pnlCancel.Visible = True
                Return
            End If

            Dim result As ServiceResult = Await _bookings.CancelAsync(
                CurrentPersonId, BookingId, reason, txtCancelNote.Text.Trim()).ConfigureAwait(True)

            If Not result.Success Then
                alert.Show(AlertKind.Error, result.Message)
                pnlCancel.Visible = True
                Return
            End If

            ' Redirect rather than re-render, so a refresh cannot repost the cancellation.
            Response.Redirect($"~/Student/Bookings/BookingDetails.aspx?bookingId={BookingId}&cancelled=1",
                              endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

#End Region

#Region "Postpone"

        Protected Sub btnConfirmPostpone_Click(sender As Object, e As EventArgs) Handles btnConfirmPostpone.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf ProposeAsync))
        End Sub

        Protected Sub btnWithdraw_Click(sender As Object, e As EventArgs) Handles btnWithdraw.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult = Await _postponements.WithdrawAsync(
                        CurrentPersonId, PendingPostponementId).ConfigureAwait(True)

                    If Not result.Success Then
                        alert.Show(AlertKind.Error, result.Message)
                        Return
                    End If

                    Response.Redirect(
                        $"~/Student/Bookings/BookingDetails.aspx?bookingId={BookingId}&withdrawn=1",
                        endResponse:=False)
                    Context.ApplicationInstance.CompleteRequest()
                End Function))
        End Sub

        Private Async Function ProposeAsync() As Task
            pnlPostpone.Visible = True

            Dim proposedDate As Date
            If Not Date.TryParse(txtNewDate.Text, SouthAfrica, DateTimeStyles.None, proposedDate) Then
                alert.Show(AlertKind.Error, "Choose the new date.")
                Return
            End If

            Dim start As TimeSpan, finish As TimeSpan
            If Not TimeSpan.TryParse(txtNewStart.Text, SouthAfrica, start) OrElse
               Not TimeSpan.TryParse(txtNewEnd.Text, SouthAfrica, finish) Then
                alert.Show(AlertKind.Error, "Give both a start and an end time.")
                Return
            End If

            Dim reason As ScheduleChangeReason
            If Not ScheduleChangeReasons.TryParse(ddlPostponeReason.SelectedValue, reason) Then
                alert.Show(AlertKind.Error, "Choose a reason.")
                Return
            End If

            Dim result As ServiceResult(Of Integer) = Await _postponements.ProposeAsync(
                CurrentPersonId, BookingId, proposedDate, start, finish,
                reason, txtPostponeNote.Text.Trim()).ConfigureAwait(True)

            If Not result.Success Then
                alert.Show(AlertKind.Error, result.Message)
                Return
            End If

            Response.Redirect($"~/Student/Bookings/BookingDetails.aspx?bookingId={BookingId}&postponed=1",
                              endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

#End Region

    End Class

End Namespace
