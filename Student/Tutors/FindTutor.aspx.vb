Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Data
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>
    ''' Tutor search (AT-08).
    ''' </summary>
    ''' <remarks>
    ''' Paging is done in SQL with OFFSET/FETCH, not by fetching everything and slicing in
    ''' memory, so the page size is what actually travels over the wire.
    '''
    ''' BR-26 is not implemented here: the repository's WHERE clause excludes unapproved
    ''' and deactivated tutors, so this screen cannot show one even by mistake.
    ''' </remarks>
    Public Class FindTutor
        Inherits StudentPageBase

        Private Const PageSize As Integer = 10

        Private ReadOnly _search As ITutorSearchService = New TutorSearchService()
        Private ReadOnly _catalogue As New SubjectRepository()

        Protected WithEvents ddlSubject As DropDownList
        Protected WithEvents ddlStudyLevel As DropDownList
        Protected WithEvents txtQualification As TextBox
        Protected WithEvents txtMinExperience As TextBox
        Protected WithEvents btnSearch As Button
        Protected WithEvents rptTutors As Repeater
        Protected WithEvents pnlEmpty As Panel
        Protected WithEvents btnClear As LinkButton
        Protected WithEvents btnPrev As LinkButton
        Protected WithEvents btnNext As LinkButton
        Protected WithEvents litPage As Literal

        ''' <summary>Current page, held in ViewState so paging survives a postback.</summary>
        Private Property CurrentPage As Integer
            Get
                Dim value As Object = ViewState("Page")
                Return If(value Is Nothing, 1, CInt(value))
            End Get
            Set(value As Integer)
                ViewState("Page") = Math.Max(1, value)
            End Set
        End Property

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            SetHeading()
            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

        Private Async Function LoadAsync() As Task
            If Not IsPostBack Then
                Dim subjects As IList(Of Subject) = Await _catalogue.GetSubjectsAsync().ConfigureAwait(True)
                ddlSubject.DataSource = subjects
                ddlSubject.DataBind()
                ddlSubject.Items.Insert(0, New ListItem("All subjects", "0"))

                Dim levels As IList(Of StudyLevel) = Await _catalogue.GetStudyLevelsAsync().ConfigureAwait(True)
                ddlStudyLevel.DataSource = levels
                ddlStudyLevel.DataBind()
                ddlStudyLevel.Items.Insert(0, New ListItem("All levels", "0"))
            End If

            Await BindResultsAsync()
        End Function

        Private Async Function BindResultsAsync() As Task
            Dim subjectId As Integer? = NullableId(ddlSubject.SelectedValue)
            Dim levelId As Integer? = NullableId(ddlStudyLevel.SelectedValue)
            Dim qualification As String = txtQualification.Text
            Dim minExperience As Integer? = NullableId(txtMinExperience.Text)

            Dim total As Integer = Await _search.CountAsync(
                subjectId, levelId, qualification, minExperience).ConfigureAwait(True)

            Dim lastPage As Integer = Math.Max(1, CInt(Math.Ceiling(total / CDbl(PageSize))))
            If CurrentPage > lastPage Then CurrentPage = lastPage

            Dim result As ServiceResult(Of IList(Of TutorSearchResult)) = Await _search.SearchAsync(
                subjectId, levelId, qualification, minExperience, CurrentPage, PageSize).ConfigureAwait(True)

            rptTutors.DataSource = result.Data
            rptTutors.DataBind()

            rptTutors.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0

            litPage.Text = $"<span class=""pager__item"" aria-current=""page"">{CurrentPage}</span>"

            btnPrev.Enabled = CurrentPage > 1
            btnNext.Enabled = CurrentPage < lastPage
            btnPrev.Attributes("aria-disabled") = (Not btnPrev.Enabled).ToString().ToLowerInvariant()
            btnNext.Attributes("aria-disabled") = (Not btnNext.Enabled).ToString().ToLowerInvariant()
        End Function

        Protected Sub btnSearch_Click(sender As Object, e As EventArgs) Handles btnSearch.Click
            ' A new search always starts at page one; keeping the old page would show an
            ' empty screen whenever the new result set is shorter.
            CurrentPage = 1
        End Sub

        Protected Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
            ddlSubject.SelectedIndex = 0
            ddlStudyLevel.SelectedIndex = 0
            txtQualification.Text = String.Empty
            txtMinExperience.Text = String.Empty
            CurrentPage = 1
        End Sub

        Protected Sub btnPrev_Click(sender As Object, e As EventArgs) Handles btnPrev.Click
            CurrentPage -= 1
        End Sub

        Protected Sub btnNext_Click(sender As Object, e As EventArgs) Handles btnNext.Click
            CurrentPage += 1
        End Sub

#Region "Row helpers"

        Protected Function GetDetailUrl(tutorSubjectId As Object) As String
            Dim id As Integer = Convert.ToInt32(tutorSubjectId, CultureInfo.InvariantCulture)
            Return ResolveUrl($"~/Student/Tutors/TutorDetails.aspx?tutorSubjectId={id}")
        End Function

        Protected Function GetExperience(years As Object) As String
            Dim n As Integer = Convert.ToInt32(years, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode(If(n = 1, "1 year", $"{n} years"))
        End Function

        ''' <summary>Rands, no decimals - the design shows "R150".</summary>
        Protected Function GetRate(rate As Object) As String
            Dim value As Decimal = Convert.ToDecimal(rate, CultureInfo.InvariantCulture)
            Return HttpUtility.HtmlEncode("R" & value.ToString("0.##", CultureInfo.GetCultureInfo("en-ZA")))
        End Function

        Protected Shared Function GetInitials(fullName As Object) As String
            Dim name As String = Convert.ToString(fullName, CultureInfo.InvariantCulture)
            If String.IsNullOrWhiteSpace(name) Then Return "?"

            Dim initials As New StringBuilder()
            For Each part In name.Split(" "c)
                If part.Length > 0 AndAlso initials.Length < 2 Then initials.Append(part(0))
            Next

            Return HttpUtility.HtmlEncode(initials.ToString().ToUpperInvariant())
        End Function

#End Region

        ''' <summary>Zero or blank means "no filter", not "match zero".</summary>
        Private Shared Function NullableId(value As String) As Integer?
            Dim parsed As Integer
            If Not Integer.TryParse(value, parsed) Then Return Nothing
            If parsed <= 0 Then Return Nothing
            Return parsed
        End Function

        Private Sub SetHeading()
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell Is Nothing Then Return
            shell.PageTitle = "Find Tutor"
            shell.PageSubtitle = "Search and find the best tutors"
        End Sub

    End Class

End Namespace
