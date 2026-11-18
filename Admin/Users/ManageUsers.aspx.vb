Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Web
Imports System.Web.UI.WebControls
Imports TutorConnect.Controls
Imports TutorConnect.Enums
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services
Imports TutorConnect.Utilities

Namespace AdminArea

    ''' <summary>
    ''' User administration: approve tutors, activate and deactivate accounts.
    ''' </summary>
    ''' <remarks>
    ''' AT-24. Deactivating blocks the next sign-in and, because BasePage re-checks on
    ''' every request, ends any session already running.
    '''
    ''' No password is ever displayed or reset here - only a one-way hash exists to show.
    ''' </remarks>
    Public Class ManageUsers
        Inherits BasePage

        Private ReadOnly _admin As IAdminService = New AdminService()

        ''' <summary>Which tutors are still unapproved, so the row can offer the action.</summary>
        Private _pendingTutorByPerson As Dictionary(Of Integer, Integer)

        Protected WithEvents alert As AlertMessage
        Protected WithEvents txtSearch As TextBox
        Protected WithEvents ddlRole As DropDownList
        Protected WithEvents ddlActive As DropDownList
        Protected WithEvents btnSearch As Button
        Protected WithEvents rptUsers As Repeater
        Protected WithEvents pnlEmpty As Panel

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            Dim shell As AdminShell = TryCast(Master, AdminShell)
            If shell IsNot Nothing Then
                shell.PageTitle = "Manage Users"
                shell.PageSubtitle = "Approve tutors and control account access"
            End If

            ' The dashboard tiles link here with a filter already chosen.
            If Not IsPostBack Then
                Dim role As String = Request.QueryString("role")
                If ddlRole.Items.FindByValue(role) IsNot Nothing Then ddlRole.SelectedValue = role

                ' "Awaiting approval" is not a stored flag on Person, so the closest
                ' honest filter is active tutors; the Status column then shows which are
                ' still pending.
                If Request.QueryString("pending") = "1" Then ddlActive.SelectedValue = "1"
            End If

            RegisterAsyncTask(New PageAsyncTask(AddressOf BindAsync))
        End Sub

        Private Async Function BindAsync() As Task
            Dim role As UserRole? = Nothing
            If ddlRole.SelectedValue.Length > 0 Then
                role = EnumHelper.Parse(Of UserRole)(ddlRole.SelectedValue)
            End If

            Dim isActive As Boolean? = Nothing
            If ddlActive.SelectedValue.Length > 0 Then
                isActive = ddlActive.SelectedValue = "1"
            End If

            Dim result As ServiceResult(Of IList(Of Person)) = Await _admin.SearchUsersAsync(
                If(txtSearch.Text.Trim().Length = 0, Nothing, txtSearch.Text.Trim()),
                role, isActive).ConfigureAwait(True)

            ' Resolve which of these people are unapproved tutors, so the Approve action
            ' appears on exactly those rows. Only the tutors in the current result set are
            ' looked up.
            _pendingTutorByPerson = New Dictionary(Of Integer, Integer)()

            For Each person In result.Data.Where(Function(p) p.Role = UserRole.Tutor)
                Dim profile As TutorProfile =
                    Await _admin.GetTutorProfileAsync(person.PersonId).ConfigureAwait(True)

                If profile IsNot Nothing AndAlso Not profile.IsApproved Then
                    _pendingTutorByPerson(person.PersonId) = profile.TutorId
                End If
            Next

            rptUsers.DataSource = result.Data
            rptUsers.DataBind()

            rptUsers.Visible = result.Data.Count > 0
            pnlEmpty.Visible = result.Data.Count = 0
        End Function

        Protected Sub btnSearch_Click(sender As Object, e As EventArgs) Handles btnSearch.Click
            alert.Hide()
        End Sub

#Region "Row helpers"

        Protected Function NeedsApproval(item As Object) As Boolean
            Dim person As Person = TryCast(item, Person)
            If person Is Nothing OrElse _pendingTutorByPerson Is Nothing Then Return False
            Return _pendingTutorByPerson.ContainsKey(person.PersonId)
        End Function

        ''' <summary>Words, not a colour: a status has to be readable without seeing it.</summary>
        Protected Function GetStatus(item As Object) As String
            Dim person As Person = TryCast(item, Person)
            If person Is Nothing Then Return String.Empty

            If Not person.IsActive Then
                Return "<span class=""badge badge--cancelled"">Deactivated</span>"
            End If

            If NeedsApproval(item) Then
                Return "<span class=""badge badge--pending"">Awaiting approval</span>"
            End If

            Return "<span class=""badge badge--confirmed"">Active</span>"
        End Function

#End Region

#Region "Actions"

        Protected Sub Approve_Command(sender As Object, e As CommandEventArgs)
            Dim personId As Integer = ParseId(e.CommandArgument)

            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim profile As TutorProfile =
                        Await _admin.GetTutorProfileAsync(personId).ConfigureAwait(True)

                    If profile Is Nothing Then
                        alert.Show(AlertKind.Error, "That account is not a tutor.")
                        Return
                    End If

                    Dim result As ServiceResult =
                        Await _admin.ApproveTutorAsync(CurrentPersonId, profile.TutorId, True).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
                    Await BindAsync()
                End Function))
        End Sub

        Protected Sub Deactivate_Command(sender As Object, e As CommandEventArgs)
            SetActive(ParseId(e.CommandArgument), False)
        End Sub

        Protected Sub Activate_Command(sender As Object, e As CommandEventArgs)
            SetActive(ParseId(e.CommandArgument), True)
        End Sub

        Private Sub SetActive(personId As Integer, isActive As Boolean)
            RegisterAsyncTask(New PageAsyncTask(
                Async Function() As Task
                    Dim result As ServiceResult = Await _admin.SetUserActiveAsync(
                        CurrentPersonId, personId, isActive).ConfigureAwait(True)

                    alert.Show(If(result.Success, AlertKind.Success, AlertKind.Error), result.Message)
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

