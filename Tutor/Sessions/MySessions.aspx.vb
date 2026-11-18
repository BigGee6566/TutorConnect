Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Enums
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services
Imports TutorConnect.Utilities

Namespace TutorArea

    ''' <summary>
    ''' The tutor's sessions, with Mark Completed (BR-23), plus their reviews.
    ''' </summary>
    ''' <remarks>
    ''' The screen is called "My Sessions" but lists Bookings - that is the domain
    ''' vocabulary: a TutoringSession row is only written once a booking is completed.
    ''' </remarks>
    Public Class MySessions
        Inherits TutorPageBase

        Private ReadOnly _bookings As IBookingService = New BookingService()
        Private ReadOnly _reviews As IReviewService = New ReviewService()
        Private ReadOnly _tutors As ITutorService = New TutorService()
        Private ReadOnly _postponements As IPostponementService = New PostponementService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents rptSessions As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents btnRefresh As Button
        Protected WithEvents rptReviews As Repeater
        Protected WithEvents pnlNoReviews As Panel
        Protected WithEvents litRating As Literal

        Protected WithEvents pnlPostponements As Panel
        Protected WithEvents rptPostponements As Repeater
        Protected WithEvents pnlAnswer As Panel
        Protected WithEvents litAnswerStudent As Literal
        Protected WithEvents litAnswerOriginal As Literal
        Protected WithEvents litAnswerProposed As Literal
        Protected WithEvents litAnswerReason As Literal
        Protected WithEvents litAnswerNote As Literal
        Protected WithEvents txtResponse As TextBox
        Protected WithEvents btnApprove As Button
        Protected WithEvents btnDecline As Button
        Protected WithEvents btnCloseAnswer As Button

        Protected WithEvents pnlCancelSession As Panel
        Protected WithEvents litCancelId As Literal
        Protected WithEvents ddlCancelReason As DropDownList
        Protected WithEvents txtCancelNote As TextBox
        Protected WithEvents btnConfirmCancelSession As Button
        Protected WithEvents btnAbandonCancel As Button

        ''' <summary>Which session the cancel form is for. Re-checked by the service.</summary>
        Private Property CancellingId As Integer
            Get
                Return CInt(If(ViewState("CancellingId"), 0))
            End Get
            Set(value As Integer)
                ViewState("CancellingId") = value
            End Set
        End Property

        ''' <summary>
        ''' Which proposal the answer form is showing.
        ''' </summary>
        ''' <remarks>
        ''' Held in ViewState only to know which form to redraw. The service resolves the
        ''' tutor from the authentication ticket and re-checks ownership, so a tampered value
        ''' here answers nothing (BR-24).
        ''' </remarks>
        Private Property AnsweringId As Integer
            Get
                Return CInt(If(ViewState("AnsweringId"), 0))
            End Get
            Set(value As Integer)
                ViewState("AnsweringId") = value
            End Set
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As TutorShell = TryCast(Master, TutorShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "My Sessions"
                shell.PageSubtitle = "View your scheduled sessions"
            End If

            If Not IsPostBack Then
                ddlCancelReason.DataSource = ScheduleChangeReasons.All()
                ddlCancelReason.DataBind()
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Protected Shared Function IsScheduled(status As Object) As Boolean
            Return status IsNot Nothing AndAlso
                   String.Equals(status.ToString(), "Scheduled", StringComparison.Ordinal)
        End Function

        Protected Sub Cancel_Command(sender As Object, e As CommandEventArgs)
            Dim bookingId As Integer
            Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), bookingId)

            CancellingId = bookingId
            litCancelId.Text = bookingId.ToString(CultureInfo.InvariantCulture)
            pnlCancelSession.Visible = True
            txtCancelNote.Text = String.Empty
        End Sub

        Protected Sub btnAbandonCancel_Click(sender As Object, e As EventArgs) Handles btnAbandonCancel.Click
            CancellingId = 0
            pnlCancelSession.Visible = False
        End Sub

        Protected Sub btnConfirmCancelSession_Click(sender As Object, e As EventArgs) _
            Handles btnConfirmCancelSession.Click

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim reason As ScheduleChangeReason

                    If Not ScheduleChangeReasons.TryParse(ddlCancelReason.SelectedValue, reason) Then
                        alert.Show(AlertKind.Error, "Choose a reason for cancelling.")
                        pnlCancelSession.Visible = True
                        Return
                    End If

                    ' CurrentPersonId comes from the authentication ticket; the service
                    ' resolves ownership from it, never from the identifier on the form.
                    Dim result As ServiceResult = Await _bookings.CancelAsync(
                        CurrentPersonId, CancellingId, reason, txtCancelNote.Text.Trim()).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

                    If result.Success Then
                        CancellingId = 0
                        pnlCancelSession.Visible = False
                    Else
                        pnlCancelSession.Visible = True
                    End If

                    Await BindAsync()
                End Function))
        End Sub

        Private Async Function BindAsync() As Task
            Dim tutorId As Integer = Await ResolveTutorIdAsync()
            If tutorId = 0 Then Return

            Await BindPostponementsAsync(tutorId).ConfigureAwait(True)

            Dim sessions As ServiceResult(Of IList(Of Booking)) =
                Await _bookings.GetForTutorAsync(tutorId).ConfigureAwait(True)

            rptSessions.DataSource = sessions.Data
            rptSessions.DataBind()
            rptSessions.Visible = sessions.Data.Count > 0
            pnlEmpty.Visible = sessions.Data.Count = 0

            Dim reviews As ServiceResult(Of IList(Of Review)) =
                Await _reviews.GetForTutorAsync(tutorId).ConfigureAwait(True)

            rptReviews.DataSource = reviews.Data
            rptReviews.DataBind()
            rptReviews.Visible = reviews.Data.Count > 0
            pnlNoReviews.Visible = reviews.Data.Count = 0

            Dim rating As ServiceResult(Of TutorRatingSummary) =
                Await _tutors.GetRatingAsync(tutorId).ConfigureAwait(True)

            litRating.Text = If(rating.Data.AverageRating.HasValue,
                                $"{rating.Data.AverageRating.Value:0.0} out of 5 from {rating.Data.ReviewCount} reviews",
                                "No reviews yet")
        End Function

#Region "Postponements (Part 12.5)"

        Private Async Function BindPostponementsAsync(tutorId As Integer) As Task
            Dim proposals As ServiceResult(Of IList(Of PostponementRequest)) =
                Await _postponements.GetForTutorAsync(tutorId).ConfigureAwait(True)

            rptPostponements.DataSource = proposals.Data
            rptPostponements.DataBind()

            ' The whole panel disappears when there is nothing to show, rather than leaving
            ' an empty table on a screen that is mostly about sessions.
            pnlPostponements.Visible = proposals.Data.Count > 0

            If AnsweringId > 0 Then
                Dim answering As PostponementRequest =
                    proposals.Data.FirstOrDefault(Function(p) p.PostponementId = AnsweringId AndAlso p.IsPending)

                If answering Is Nothing Then
                    ' Answered in another tab, or withdrawn.
                    AnsweringId = 0
                    pnlAnswer.Visible = False
                Else
                    pnlAnswer.Visible = True
                    litAnswerStudent.Text = answering.StudentName
                    litAnswerOriginal.Text = answering.OriginalRange
                    litAnswerProposed.Text = answering.ProposedRange
                    litAnswerReason.Text = ScheduleChangeReasons.Describe(answering.ReasonCategory)
                    litAnswerNote.Text = If(String.IsNullOrWhiteSpace(answering.StudentNote),
                                            "-", answering.StudentNote)
                End If
            End If
        End Function

        Protected Sub Answer_Command(sender As Object, e As CommandEventArgs)
            Dim id As Integer
            Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), id)

            AnsweringId = id
            txtResponse.Text = String.Empty
            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Protected Sub btnCloseAnswer_Click(sender As Object, e As EventArgs) Handles btnCloseAnswer.Click
            AnsweringId = 0
            pnlAnswer.Visible = False
        End Sub

        Protected Sub btnApprove_Click(sender As Object, e As EventArgs) Handles btnApprove.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult = Await _postponements.ApproveAsync(
                        CurrentPersonId, AnsweringId, txtResponse.Text.Trim()).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    If result.Success Then AnsweringId = 0
                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub btnDecline_Click(sender As Object, e As EventArgs) Handles btnDecline.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult = Await _postponements.DeclineAsync(
                        CurrentPersonId, AnsweringId, txtResponse.Text.Trim()).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    If result.Success Then AnsweringId = 0
                    Await BindAsync()
                End Function))
        End Sub

        Protected Shared Function DescribeReason(value As Object) As String
            If value Is Nothing Then Return String.Empty

            Dim reason As ScheduleChangeReason = CType(value, ScheduleChangeReason)
            Return HttpUtility.HtmlEncode(ScheduleChangeReasons.Describe(reason))
        End Function

        ''' <summary>Plain words for the proposal's state, rather than the enum name.</summary>
        Protected Shared Function DescribeStatus(item As Object) As String
            Dim proposal As PostponementRequest = TryCast(item, PostponementRequest)
            If proposal Is Nothing Then Return String.Empty

            Select Case proposal.Status
                Case PostponementStatus.PendingTutorReview : Return "Waiting for you"
                Case PostponementStatus.Approved : Return "Approved"
                Case PostponementStatus.Declined : Return "Declined"
                Case Else : Return "Withdrawn"
            End Select
        End Function

#End Region

        ''' <summary>
        ''' BR-23 in the UI: Scheduled, and the end time has passed. The service checks the
        ''' same thing inside the UPDATE, so hiding the button is a courtesy, not the rule.
        ''' </summary>
        Protected Shared Function CanComplete(item As Object) As Boolean
            Dim booking As Booking = TryCast(item, Booking)
            If booking Is Nothing Then Return False
            Return booking.Status = BookingStatus.Scheduled AndAlso booking.HasEnded
        End Function

        Protected Sub Complete_Command(sender As Object, e As CommandEventArgs)
            Dim bookingId As Integer
            Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), bookingId)

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim tutorId As Integer = Await ResolveTutorIdAsync()

                    Dim result As ServiceResult =
                        Await _bookings.MarkCompletedAsync(tutorId, bookingId).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
            alert.Hide()
        End Sub

        Protected Function GetDetailUrl(bookingId As Object) As String
            Dim id As Integer = Convert.ToInt32(bookingId, CultureInfo.InvariantCulture)
            Return ResolveUrl($"~/Tutor/Sessions/SessionDetails.aspx?bookingId={id}")
        End Function

        Protected Shared Function GetDate(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

        Protected Shared Function GetStars(value As Object) As String
            Dim rating As Integer = Convert.ToInt32(value, CultureInfo.InvariantCulture)

            Dim sb As New StringBuilder()
            sb.Append("<span class=""stars"" aria-hidden=""true"">")
            For i As Integer = 1 To 5
                sb.Append(If(i <= rating, "<span class=""stars__on"">&#9733;</span>",
                                          "<span class=""stars__off"">&#9734;</span>"))
            Next
            sb.Append("</span>")
            ' Decorative glyphs, visible number, sr-only phrase - see ManageReviews.GetStars.
            sb.Append($"<span class=""stars__value"" aria-hidden=""true"">{rating}/5</span>")
            sb.Append($"<span class=""sr-only"">{rating} out of 5</span>")

            Return sb.ToString()
        End Function

    End Class

End Namespace
