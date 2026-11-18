Option Strict On
Option Explicit On

Namespace Validation

    ''' <summary>
    ''' Shape rules for a review.
    ''' </summary>
    ''' <remarks>
    ''' BR-16 (booking must be Completed), BR-17 (one review per booking) and BR-19 (a
    ''' cancelled booking is never reviewable) all depend on the booking's state, so they
    ''' live in ReviewService and ultimately in the database's UNIQUE constraint. What is
    ''' checked here is only what the form itself can be wrong about.
    ''' </remarks>
    Public NotInheritable Class ReviewValidator

        Public Const MinimumRating As Byte = 1
        Public Const MaximumRating As Byte = 5
        Public Const MaximumCommentLength As Integer = 500

        Private Sub New()
        End Sub

        Public Shared Function Validate(bookingId As Integer,
                                        rating As Byte,
                                        comment As String) As ValidationResult
            Dim result As New ValidationResult()

            If bookingId <= 0 Then
                result.AddError("BookingId", "Choose the session you want to review.")
            End If

            If rating < MinimumRating OrElse rating > MaximumRating Then
                result.AddError("Rating", $"Choose a rating from {MinimumRating} to {MaximumRating} stars.")
            End If

            ' The comment is optional; only its length is constrained, matching
            ' Review.Comment NVARCHAR(500).
            If comment IsNot Nothing AndAlso comment.Length > MaximumCommentLength Then
                result.AddError("Comment",
                    $"Your comment must be {MaximumCommentLength} characters or fewer.")
            End If

            Return result
        End Function

    End Class

End Namespace
