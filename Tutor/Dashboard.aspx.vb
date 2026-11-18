Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports TutorConnect.Controls
Imports TutorConnect.Data
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services
Imports TutorConnect.Utilities

Namespace TutorArea

    ''' <summary>
    ''' The tutor's landing screen: a greeting and five navigation tiles, with a pending
    ''' count on Student Requests.
    ''' </summary>
    Public Class Dashboard
        Inherits BasePage

        Private ReadOnly _authentication As IAuthenticationService = New AuthenticationService()
        Private ReadOnly _users As New UserRepository()
        Private ReadOnly _requests As New RequestRepository()

        Protected WithEvents cardProfile As DashboardCard
        Protected WithEvents cardSubjects As DashboardCard
        Protected WithEvents cardAvailability As DashboardCard
        Protected WithEvents cardRequests As DashboardCard
        Protected WithEvents cardSessions As DashboardCard

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            cardProfile.Icon = Icons.Person
            cardSubjects.Icon = Icons.Books
            cardAvailability.Icon = Icons.Calendar
            cardRequests.Icon = Icons.People
            cardSessions.Icon = Icons.Calendar

            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadDashboardAsync))
        End Sub

        Private Async Function LoadDashboardAsync() As Task
            Dim shell As TutorShell = TryCast(Master, TutorShell)
            If shell IsNot Nothing Then
                shell.PageSubtitle = "Manage your tutoring activities"
            End If

            Dim result As ServiceResult(Of Person) =
                Await _authentication.GetAccountAsync(CurrentPersonId).ConfigureAwait(True)

            If shell IsNot Nothing Then
                shell.PageTitle = If(result.Success, $"Welcome, {result.Data.FirstName}!", "Welcome!")
            End If

            If Not result.Success Then Return

            ' The tutor row carries the TutorId the request count is keyed on.
            Dim profile As TutorProfile =
                Await _users.GetTutorByPersonIdAsync(result.Data.PersonId).ConfigureAwait(True)

            If profile Is Nothing Then Return

            Dim pending As Integer =
                Await _requests.CountPendingForTutorAsync(profile.TutorId).ConfigureAwait(True)

            cardRequests.Count = pending
            If shell IsNot Nothing Then shell.PendingRequestCount = pending
        End Function

    End Class

End Namespace
