Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Data
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services

Namespace StudentArea

    ''' <summary>The student's preferred subjects (BR-05).</summary>
    Public Class MySubjects
        Inherits StudentPageBase

        Private ReadOnly _students As IStudentService = New StudentService()
        Private ReadOnly _catalogue As New SubjectRepository()

        Protected WithEvents alert As AlertMessage
        Protected WithEvents ddlSubject As DropDownList
        Protected WithEvents ddlStudyLevel As DropDownList
        Protected WithEvents btnAdd As Button
        Protected WithEvents txtNewSubject As TextBox
        Protected WithEvents btnAddNew As Button
        Protected WithEvents rptSubjects As Repeater
        Protected WithEvents pnlEmpty As Panel

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            SetHeading()
            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadAsync))
        End Sub

        Private Async Function LoadAsync() As Task
            If Not IsPostBack Then
                Await BindSubjectsAsync()

                ddlStudyLevel.DataSource = Await _catalogue.GetStudyLevelsAsync().ConfigureAwait(True)
                ddlStudyLevel.DataBind()
            End If

            Await BindListAsync()
        End Function

        ''' <summary>
        ''' Rebinds the subject dropdown, preserving the selection where it still exists.
        ''' </summary>
        ''' <remarks>
        ''' Called again after a subject is created, not only on first load. Without this a
        ''' student who has just added "Accounting" would not find it in the dropdown on the
        ''' very page that created it.
        ''' </remarks>
        Private Async Function BindSubjectsAsync() As Task
            Dim previous As String = ddlSubject.SelectedValue

            ddlSubject.DataSource = Await _catalogue.GetSubjectsAsync().ConfigureAwait(True)
            ddlSubject.DataBind()

            If previous.Length > 0 AndAlso ddlSubject.Items.FindByValue(previous) IsNot Nothing Then
                ddlSubject.SelectedValue = previous
            End If
        End Function

        Private Async Function BindListAsync() As Task
            Dim studentId As Integer = Await ResolveStudentIdAsync()
            If studentId = 0 Then Return

            Dim result As ServiceResult(Of IList(Of StudentSubject)) =
                Await _students.GetSubjectsAsync(studentId).ConfigureAwait(True)

            rptSubjects.DataSource = result.Data
            rptSubjects.DataBind()

            ' An empty result is explained, never left as a bare table head.
            rptSubjects.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
        End Function

        Protected Sub btnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf AddAsync))
        End Sub

        Private Async Function AddAsync() As Task
            Dim studentId As Integer = Await ResolveStudentIdAsync()

            Dim result As ServiceResult = Await _students.AddSubjectAsync(
                studentId,
                ParseId(ddlSubject.SelectedValue),
                ParseId(ddlStudyLevel.SelectedValue)).ConfigureAwait(True)

            alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
            Await BindListAsync()
        End Function

        Protected Sub btnAddNew_Click(sender As Object, e As EventArgs) Handles btnAddNew.Click
            RegisterAsyncTask(New PageAsyncTask(AddressOf AddNewAsync))
        End Sub

        ''' <summary>
        ''' Adds a subject the catalogue does not have yet. The service reuses an existing
        ''' subject when the name matches one, so this cannot quietly create a second
        ''' "Mathematics".
        ''' </summary>
        Private Async Function AddNewAsync() As Task
            Dim studentId As Integer = Await ResolveStudentIdAsync()

            Dim result As ServiceResult = Await _students.AddSubjectByNameAsync(
                studentId,
                CurrentPersonId,
                txtNewSubject.Text,
                ParseId(ddlStudyLevel.SelectedValue)).ConfigureAwait(True)

            alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

            If result.Success Then
                txtNewSubject.Text = String.Empty

                ' The new subject has to appear in the dropdown straight away, or the page
                ' contradicts the message it has just shown.
                Await BindSubjectsAsync()
            End If

            Await BindListAsync()
        End Function

        ''' <summary>Handles the Remove button on each row.</summary>
        Protected Sub Remove_Command(sender As Object, e As CommandEventArgs)
            Dim studentSubjectId As Integer = ParseId(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture))

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim studentId As Integer = Await ResolveStudentIdAsync()

                    Dim result As ServiceResult =
                        Await _students.RemoveSubjectAsync(studentId, studentSubjectId).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindListAsync()
                End Function))
        End Sub

        Private Shared Function ParseId(value As String) As Integer
            Dim parsed As Integer
            Return If(Integer.TryParse(value, parsed), parsed, 0)
        End Function

        Private Sub SetHeading()
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell Is Nothing Then Return
            shell.PageTitle = "My Subjects"
            shell.PageSubtitle = "Manage your preferred subjects"
        End Sub

    End Class

End Namespace
