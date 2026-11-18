Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace TutorArea

    ''' <summary>
    ''' One session in full, with the student's review when there is one.
    ''' </summary>
    ''' <remarks>
    ''' Ownership is decided by GetForViewerAsync from the signed-in PersonId, which already
    ''' authorises a booking's own tutor and refuses everyone else with the same message a
    ''' missing booking gets (BR-24, proved by AT-22).
    ''' </remarks>
    Public Class SessionDetails
        Inherits TutorPageBase

        Private ReadOnly _bookings As IBookingService = New BookingService()
        Private ReadOnly _reviews As New ReviewRepository()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents pnlDetail As Panel
        Protected WithEvents litId As Literal
        Protected WithEvents litStudent As Literal
        Protected WithEvents litSubject As Literal
        Protected WithEvents litDate As Literal
        Protected WithEvents litTime As Literal
        Protected WithEvents badge As StatusBadge
        Protected WithEvents btnComplete As Button
        Protected WithEvents pnlReview As Panel
        Protected WithEvents pnlNoReview As Panel
        Protected WithEvents litStars As Literal
        Protected WithEvents litComment As Literal
        Protected WithEvents litReviewed As Literal

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
            Dim shell As TutorShell = TryCast(Master, TutorShell)
            If shell IsNot Nothing Then shell.PageTitle = "Session Details"

            Dim result As ServiceResult(Of Booking) =
                Await _bookings.GetForViewerAsync(BookingId, CurrentPersonId).ConfigureAwait(True)

            If Not result.Success Then
                pnlDetail.Visible = False
                alert.Show(AlertKind.Error, result.Message)
                Return
            End If

            Dim booking As Booking = result.Data
            Dim culture As CultureInfo = CultureInfo.GetCultureInfo("en-ZA")

            litId.Text = booking.BookingId.ToString(culture)
            litStudent.Text = booking.StudentName
            litSubject.Text = $"{booking.SubjectName} - {booking.StudyLevelName}"
            litDate.Text = booking.BookingDate.ToString("dd/MM/yyyy", culture)
            litTime.Text = booking.TimeRange
            badge.Status = booking.Status.ToString()

            If shell IsNot Nothing Then
                shell.PageSubtitle = $"{booking.SubjectName} with {booking.StudentName}"
            End If

            btnComplete.Visible = booking.Status = BookingStatus.Scheduled AndAlso booking.HasEnded

            Dim review As Review = Await _reviews.GetByBookingAsync(booking.BookingId).ConfigureAwait(True)

            If review Is Nothing Then
                pnlNoReview.Visible = booking.Status = BookingStatus.Completed
                Return
            End If

            pnlReview.Visible = True
            litStars.Text = Stars(review.Rating)
            litComment.Text = If(String.IsNullOrWhiteSpace(review.Comment),
                                 "(no comment left)", review.Comment)
            litReviewed.Text = review.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy", culture)
        End Function

        Protected Sub btnComplete_Click(sender As Object, e As EventArgs) Handles btnComplete.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim tutorId As Integer = Await ResolveTutorIdAsync()

                    Dim result As ServiceResult =
                        Await _bookings.MarkCompletedAsync(tutorId, BookingId).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await LoadAsync()
                End Function))
        End Sub

        Private Shared Function Stars(rating As Byte) As String
            Dim sb As New StringBuilder()
            sb.Append("<span class=""stars"" aria-hidden=""true"">")
            For i As Integer = 1 To 5
                sb.Append(If(i <= rating, "<span class=""stars__on"">&#9733;</span>",
                                          "<span class=""stars__off"">&#9734;</span>"))
            Next
            ' Decorative glyphs, visible number, sr-only phrase - see ManageReviews.GetStars.
            sb.Append($"</span><span class=""stars__value"" aria-hidden=""true"">{rating}/5</span>")
            sb.Append($"<span class=""sr-only"">{rating} out of 5</span>")
            Return sb.ToString()
        End Function

    End Class

End Namespace
