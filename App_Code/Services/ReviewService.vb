Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Validation

Namespace Services

    ''' <summary>
    ''' Reviews of completed bookings.
    ''' </summary>
    ''' <remarks>
    ''' "Reviewable" is one condition: the booking is Completed, belongs to this student,
    ''' and has no review yet. That is evaluated in the database as part of the INSERT, so
    ''' a second submission cannot slip between the check and the write. The UNIQUE
    ''' constraint on Review.BookingId is the final backstop.
    ''' </remarks>
    Public Class ReviewService
        Implements IReviewService

        Private ReadOnly _reviews As ReviewRepository
        Private ReadOnly _bookings As BookingRepository
        Private ReadOnly _tutors As TutorRepository
        Private ReadOnly _notifications As INotificationService

        Public Sub New()
            Me.New(New ReviewRepository(), New BookingRepository(), New TutorRepository(), New NotificationService())
        End Sub

        Public Sub New(reviews As ReviewRepository,
                       bookings As BookingRepository,
                       tutors As TutorRepository,
                       notifications As INotificationService)
            _reviews = reviews
            _bookings = bookings
            _tutors = tutors
            _notifications = notifications
        End Sub

        ''' <inheritdoc/>
        Public Async Function CreateAsync(studentId As Integer,
                                          bookingId As Integer,
                                          rating As Byte,
                                          comment As String) As Task(Of ServiceResult(Of Integer)) _
            Implements IReviewService.CreateAsync

            If studentId <= 0 Then
                Return ServiceResult(Of Integer).Fail("You are not signed in as a student.")
            End If

            Dim validation As ValidationResult = ReviewValidator.Validate(bookingId, rating, comment)
            If Not validation.IsValid Then
                Return ServiceResult(Of Integer).Fail(validation.ToSummary())
            End If

            ' BR-16 / BR-17 / BR-19, evaluated inside the INSERT. A returned id of 0 means
            ' the booking was not reviewable - wrong owner, not Completed, or already
            ' reviewed - and nothing was written.
            Dim reviewId As Integer

            Try
                reviewId = Await _reviews.CreateAsync(bookingId, studentId, rating, comment).ConfigureAwait(False)

            Catch ex As System.Data.SqlClient.SqlException When ex.Number = 2601 OrElse ex.Number = 2627
                ' Two submissions raced; UQ_Review_Booking held the line (AT-21).
                Return ServiceResult(Of Integer).Fail("You have already reviewed this session.")
            End Try

            If reviewId = 0 Then
                Return ServiceResult(Of Integer).Fail(
                    "This session cannot be reviewed. A review is only possible once the " &
                    "tutor has marked the session completed, and only once.")
            End If

            ' BR-27, advisory.
            Dim booking As Booking = Await _bookings.GetByIdAsync(bookingId).ConfigureAwait(False)

            If booking IsNot Nothing Then
                Dim tutorPersonId As Integer =
                    Await _tutors.GetPersonIdForTutorSubjectAsync(
                        Await TutorSubjectIdForBookingAsync(booking)).ConfigureAwait(False)

                If tutorPersonId > 0 Then
                    Await _notifications.NotifyAsync(
                        tutorPersonId,
                        NotificationType.ReviewReceived,
                        "New review",
                        $"A student left a {rating}-star review for your session on " &
                        $"{booking.BookingDate:dd/MM/yyyy}.",
                        "~/Tutor/Dashboard.aspx").ConfigureAwait(False)
                End If
            End If

            Return ServiceResult(Of Integer).Ok(reviewId, "Thank you. Your review has been submitted.")
        End Function

        ''' <inheritdoc/>
        Public Async Function CanReviewAsync(studentId As Integer,
                                             bookingId As Integer) As Task(Of Boolean) _
            Implements IReviewService.CanReviewAsync

            Return Await _reviews.BookingIsReviewableAsync(bookingId, studentId).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForStudentAsync(studentId As Integer) As Task(Of ServiceResult(Of IList(Of Review))) _
            Implements IReviewService.GetForStudentAsync

            Dim rows As IList(Of Review) = Await _reviews.GetForStudentAsync(studentId).ConfigureAwait(False)
            Return ServiceResult(Of IList(Of Review)).Ok(rows)
        End Function

        ''' <inheritdoc/>
        Public Async Function GetForTutorAsync(tutorId As Integer) As Task(Of ServiceResult(Of IList(Of Review))) _
            Implements IReviewService.GetForTutorAsync

            Dim rows As IList(Of Review) = Await _reviews.GetForTutorAsync(tutorId).ConfigureAwait(False)
            Return ServiceResult(Of IList(Of Review)).Ok(rows)
        End Function

        ''' <summary>
        ''' The Booking model carries TutorId but not TutorSubjectId, and the notification
        ''' needs a Person. Resolved through the request the booking came from.
        ''' </summary>
        Private Async Function TutorSubjectIdForBookingAsync(booking As Booking) As Task(Of Integer)
            Dim requests As New RequestRepository()
            Dim request As SessionRequest = Await requests.GetByIdAsync(booking.RequestId).ConfigureAwait(False)
            Return If(request Is Nothing, 0, request.TutorSubjectId)
        End Function

    End Class

End Namespace

