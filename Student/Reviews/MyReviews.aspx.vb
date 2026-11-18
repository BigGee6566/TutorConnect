Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>Reviews this student has submitted.</summary>
    Public Class MyReviews
        Inherits StudentPageBase

        Private ReadOnly _reviews As IReviewService = New ReviewService()

        Protected WithEvents rptReviews As Repeater
        Protected WithEvents pnlEmpty As Panel

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Reviews"
                shell.PageSubtitle = "Ratings you have given your tutors"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim studentId As Integer = Await ResolveStudentIdAsync()
            If studentId = 0 Then Return

            Dim result As ServiceResult(Of IList(Of Review)) =
                Await _reviews.GetForStudentAsync(studentId).ConfigureAwait(True)

            rptReviews.DataSource = result.Data
            rptReviews.DataBind()

            rptReviews.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
        End Function

        ''' <summary>
        ''' Gold stars plus the number in text, because colour and shape alone are not an
        ''' accessible signal.
        ''' </summary>
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

        ''' <summary>
        ''' Tells the author when their review has been hidden, and why.
        ''' </summary>
        ''' <remarks>
        ''' The tutor's list and every rating average exclude a hidden review, but its
        ''' author still sees it here. Quietly removing it from their own list would leave
        ''' them thinking it was never saved.
        ''' </remarks>
        Protected Shared Function GetHiddenNotice(item As Object) As String
            Dim review As Review = TryCast(item, Review)
            If review Is Nothing OrElse Not review.IsHidden Then Return String.Empty

            Dim reason As String = HttpUtility.HtmlEncode(
                If(String.IsNullOrWhiteSpace(review.HiddenReason),
                   "No reason was recorded.", review.HiddenReason))

            Return "<br /><span class=""badge badge--cancelled"">Hidden by an administrator</span>" &
                   $"<br /><span class=""page-subtitle"">{reason}</span>"
        End Function

        Protected Shared Function GetDate(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

        Protected Shared Function GetCreated(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(
                d.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

    End Class

End Namespace
