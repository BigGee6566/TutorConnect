Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>
    ''' Rate a completed session (AT-19, AT-20, AT-21).
    ''' </summary>
    ''' <remarks>
    ''' Reaching this page for a booking that is not reviewable sends the student back to
    ''' My Bookings with an explanation rather than showing a form that would be rejected.
    ''' That is a courtesy, not the control: ReviewService re-checks every rule on submit,
    ''' so editing the bookingId in the URL achieves nothing.
    ''' </remarks>
    Public Class WriteReview
        Inherits StudentPageBase

        Private ReadOnly _bookings As IBookingService = New BookingService()
        Private ReadOnly _reviews As IReviewService = New ReviewService()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents pnlForm As Panel
        Protected WithEvents litTutor As Literal
        Protected WithEvents litSubject As Literal
        Protected WithEvents litSession As Literal
        Protected WithEvents rblRating As RadioButtonList
        Protected WithEvents txtComment As TextBox
        Protected WithEvents btnSubmit As Button

        Private ReadOnly Property BookingId As Integer
            Get
                Dim parsed As Integer
                Return If(Integer.TryParse(Request.QueryString("bookingId"), parsed), parsed, 0)
            End Get
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

        Private Async Function LoadAsync() As Task
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Write a Review"
                shell.PageSubtitle = "Tell other students how the session went"
            End If

            Dim studentId As Integer = Await ResolveStudentIdAsync()

            If Not Await _reviews.CanReviewAsync(studentId, BookingId).ConfigureAwait(True) Then
                Response.Redirect("~/Student/Bookings/MyBookings.aspx?notreviewable=1", endResponse:=False)
                Context.ApplicationInstance.CompleteRequest()
                Return
            End If

            Dim result As ServiceResult(Of Booking) =
                Await _bookings.GetForViewerAsync(BookingId, CurrentPersonId).ConfigureAwait(True)

            If Not result.Success Then
                pnlForm.Visible = False
                alert.Show(AlertKind.Error, result.Message)
                Return
            End If

            Dim booking As Booking = result.Data
            Dim culture As CultureInfo = CultureInfo.GetCultureInfo("en-ZA")

            litTutor.Text = booking.TutorName
            litSubject.Text = $"{booking.SubjectName} - {booking.StudyLevelName}"
            litSession.Text = $"{booking.BookingDate.ToString("dd/MM/yyyy", culture)}   {booking.TimeRange}"
        End Function

        Protected Sub btnSubmit_Click(sender As Object, e As EventArgs) Handles btnSubmit.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf SubmitAsync))
        End Sub

        Private Async Function SubmitAsync() As Task
            Dim rating As Byte
            If Not Byte.TryParse(rblRating.SelectedValue, rating) Then
                alert.Show(AlertKind.Error, "Choose a rating from 1 to 5 stars.")
                Return
            End If

            Dim studentId As Integer = Await ResolveStudentIdAsync()

            Dim result As ServiceResult(Of Integer) = Await _reviews.CreateAsync(
                studentId, BookingId, rating, txtComment.Text.Trim()).ConfigureAwait(True)

            If Not result.Success Then
                alert.Show(AlertKind.Error, result.Message)
                Return
            End If

            Response.Redirect("~/Student/Bookings/MyBookings.aspx?reviewed=1", endResponse:=False)
            Context.ApplicationInstance.CompleteRequest()
        End Function

    End Class

End Namespace
