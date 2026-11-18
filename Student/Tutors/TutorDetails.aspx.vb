Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Data
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>
    ''' One tutor-subject: who they are, and which future slots are still open.
    ''' </summary>
    ''' <remarks>
    ''' The id in the query string identifies a TutorSubject to read, not a record to
    ''' modify, so it is safe to accept - but the service still refuses any tutor who is
    ''' unapproved or deactivated, and returns the same message either way so an id cannot
    ''' be probed for existence.
    ''' </remarks>
    Public Class TutorDetails
        Inherits StudentPageBase

        Private ReadOnly _search As ITutorSearchService = New TutorSearchService()
        Private ReadOnly _reviews As New ReviewRepository()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents pnlDetail As Panel
        Protected WithEvents litInitials As Literal
        Protected WithEvents litName As Literal
        Protected WithEvents litQualification As Literal
        Protected WithEvents litExperience As Literal
        Protected WithEvents litRate As Literal
        Protected WithEvents litRating As Literal
        Protected WithEvents litSubjectLevel As Literal
        Protected WithEvents litBio As Literal
        Protected WithEvents rptSlots As Repeater
        Protected WithEvents pnlNoSlots As Panel

        Private ReadOnly Property TutorSubjectId As Integer
            Get
                Dim parsed As Integer
                Return If(Integer.TryParse(Request.QueryString("tutorSubjectId"), parsed), parsed, 0)
            End Get
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

        Private Async Function LoadAsync() As Task
            Dim shell As StudentShell = TryCast(Master, StudentShell)

            Dim detail As ServiceResult(Of TutorSearchResult) =
                Await _search.GetDetailAsync(TutorSubjectId).ConfigureAwait(True)

            If Not detail.Success Then
                pnlDetail.Visible = False
                alert.Show(AlertKind.Error, detail.Message)
                If shell IsNot Nothing Then shell.PageTitle = "Tutor Details"
                Return
            End If

            Dim tutor As TutorSearchResult = detail.Data

            If shell IsNot Nothing Then
                shell.PageTitle = tutor.FullName
                shell.PageSubtitle = $"{tutor.SubjectName} - {tutor.StudyLevelName}"
            End If

            litInitials.Text = Initials(tutor.FullName)
            litName.Text = tutor.FullName
            litQualification.Text = tutor.Qualification
            litExperience.Text = If(tutor.ExperienceYears = 1, "1 year", $"{tutor.ExperienceYears} years")
            litRate.Text = "R" & tutor.RatePerSession.ToString("0.##", CultureInfo.GetCultureInfo("en-ZA"))
            litSubjectLevel.Text = $"{tutor.SubjectName} - {tutor.StudyLevelName}"

            litBio.Text = If(String.IsNullOrWhiteSpace(tutor.Bio),
                             "This tutor has not added a description yet.",
                             tutor.Bio)

            Dim rating As TutorRatingSummary =
                Await _reviews.GetTutorRatingAsync(tutor.TutorId).ConfigureAwait(True)

            ' No reviews is stated plainly rather than shown as a zero score, which would
            ' read as a bad rating rather than an absent one.
            litRating.Text = If(rating.AverageRating.HasValue,
                                $"{rating.AverageRating.Value:0.0} out of 5 ({rating.ReviewCount} reviews)",
                                "No reviews yet")

            Dim slots As ServiceResult(Of IList(Of TutorAvailability)) =
                Await _search.GetOpenSlotsAsync(TutorSubjectId).ConfigureAwait(True)

            rptSlots.DataSource = slots.Data
            rptSlots.DataBind()
            rptSlots.Visible = slots.Data.Count > 0
            pnlNoSlots.Visible = slots.Data.Count = 0
        End Function

#Region "Row helpers"

        ''' <summary>
        ''' Each slot links straight to the request screen carrying its own AvailabilityId,
        ''' so a request can never be submitted without a chosen slot.
        ''' </summary>
        Protected Function GetRequestUrl(availabilityId As Object) As String
            Dim id As Integer = Convert.ToInt32(availabilityId, CultureInfo.InvariantCulture)
            Return ResolveUrl(
                $"~/Student/Requests/RequestSession.aspx?tutorSubjectId={TutorSubjectId}&availabilityId={id}")
        End Function

        Protected Shared Function GetDate(value As Object) As String
            Dim d As Date = Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

        Protected Shared Function GetTime(value As Object) As String
            Dim t As TimeSpan = CType(value, TimeSpan)
            Return HttpUtility.HtmlEncode(t.ToString("hh\:mm", CultureInfo.InvariantCulture))
        End Function

        Private Shared Function Initials(fullName As String) As String
            If String.IsNullOrWhiteSpace(fullName) Then Return "?"

            Dim sb As New StringBuilder()
            For Each part In fullName.Split(" "c)
                If part.Length > 0 AndAlso sb.Length < 2 Then sb.Append(part(0))
            Next

            Return sb.ToString().ToUpperInvariant()
        End Function

#End Region

    End Class

End Namespace
