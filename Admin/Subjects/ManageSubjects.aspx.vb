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

Namespace AdminArea

    ''' <summary>
    ''' Subject and study-level maintenance (AT-25).
    ''' </summary>
    ''' <remarks>
    ''' Deleting something in use is refused with an explanation and deactivation offered,
    ''' rather than allowed to fail on a foreign key. The "In use" column shows which rows
    ''' that applies to before the administrator tries.
    ''' </remarks>
    Public Class ManageSubjects
        Inherits BasePage

        Private ReadOnly _admin As IAdminService = New AdminService()
        Private ReadOnly _catalogue As New SubjectRepository()

        Private _subjectInUse As Dictionary(Of Integer, Boolean)
        Private _levelInUse As Dictionary(Of Integer, Boolean)

        Protected WithEvents alert As AlertMessage
        Protected WithEvents txtNewSubject As TextBox
        Protected WithEvents btnAddSubject As Button
        Protected WithEvents rptSubjects As Repeater
        Protected WithEvents txtNewLevel As TextBox
        Protected WithEvents txtNewOrder As TextBox
        Protected WithEvents btnAddLevel As Button
        Protected WithEvents rptLevels As Repeater

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As AdminShell = TryCast(Master, AdminShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Subjects"
                shell.PageSubtitle = "Maintain the subjects and study levels students choose from"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim subjects As ServiceResult(Of IList(Of Subject)) =
                Await _admin.GetSubjectsAsync().ConfigureAwait(True)

            _subjectInUse = New Dictionary(Of Integer, Boolean)()
            For Each s In subjects.Data
                _subjectInUse(s.SubjectId) =
                    Await _catalogue.IsSubjectInUseAsync(s.SubjectId).ConfigureAwait(True)
            Next

            rptSubjects.DataSource = subjects.Data
            rptSubjects.DataBind()

            Dim levels As ServiceResult(Of IList(Of StudyLevel)) =
                Await _admin.GetStudyLevelsAsync().ConfigureAwait(True)

            _levelInUse = New Dictionary(Of Integer, Boolean)()
            For Each l In levels.Data
                _levelInUse(l.StudyLevelId) =
                    Await _catalogue.IsStudyLevelInUseAsync(l.StudyLevelId).ConfigureAwait(True)
            Next

            rptLevels.DataSource = levels.Data
            rptLevels.DataBind()
        End Function

#Region "Row helpers"

        Protected Shared Function GetSubjectStatus(isActive As Object) As String
            Return If(Convert.ToBoolean(isActive, CultureInfo.InvariantCulture),
                      "<span class=""badge badge--confirmed"">Active</span>",
                      "<span class=""badge badge--cancelled"">Inactive</span>")
        End Function

        Protected Function GetInUse(subjectId As Object) As String
            Dim id As Integer = Convert.ToInt32(subjectId, CultureInfo.InvariantCulture)
            Dim used As Boolean
            If _subjectInUse Is Nothing OrElse Not _subjectInUse.TryGetValue(id, used) Then used = False
            Return If(used, "Yes - cannot be deleted", "No")
        End Function

        Protected Function GetLevelInUse(studyLevelId As Object) As String
            Dim id As Integer = Convert.ToInt32(studyLevelId, CultureInfo.InvariantCulture)
            Dim used As Boolean
            If _levelInUse Is Nothing OrElse Not _levelInUse.TryGetValue(id, used) Then used = False
            Return If(used, "Yes", "No")
        End Function

#End Region

#Region "Actions"

        Protected Sub btnAddSubject_Click(sender As Object, e As EventArgs) Handles btnAddSubject.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult =
                        Await _admin.AddSubjectAsync(CurrentPersonId, txtNewSubject.Text).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    If result.Success Then txtNewSubject.Text = String.Empty
                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub btnAddLevel_Click(sender As Object, e As EventArgs) Handles btnAddLevel.Click
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim sortOrder As Integer
                    Integer.TryParse(txtNewOrder.Text, sortOrder)

                    Dim result As ServiceResult =
                        Await _admin.AddStudyLevelAsync(txtNewLevel.Text, sortOrder).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)

                    If result.Success Then
                        txtNewLevel.Text = String.Empty
                        txtNewOrder.Text = String.Empty
                    End If

                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub Deactivate_Command(sender As Object, e As CommandEventArgs)
            SetActive(ParseId(e.CommandArgument), False)
        End Sub

        Protected Sub Activate_Command(sender As Object, e As CommandEventArgs)
            SetActive(ParseId(e.CommandArgument), True)
        End Sub

        Private Sub SetActive(subjectId As Integer, isActive As Boolean)
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult =
                        Await _admin.SetSubjectActiveAsync(CurrentPersonId, subjectId, isActive).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub Delete_Command(sender As Object, e As CommandEventArgs)
            Dim subjectId As Integer = ParseId(e.CommandArgument)

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult =
                        Await _admin.DeleteSubjectAsync(CurrentPersonId, subjectId).ConfigureAwait(True)

                    ' A refusal here is information, not a fault: it names the alternative.
                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Info), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Private Shared Function ParseId(value As Object) As Integer
            Dim parsed As Integer
            Integer.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), parsed)
            Return parsed
        End Function

#End Region

    End Class

End Namespace
