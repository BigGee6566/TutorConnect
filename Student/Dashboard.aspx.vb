Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports TutorConnect.Controls
Imports TutorConnect.MasterPages
Imports TutorConnect.Models
Imports TutorConnect.Security
Imports TutorConnect.Services
Imports TutorConnect.Utilities

Namespace StudentArea

    ''' <summary>
    ''' The student's landing screen: a greeting and six navigation tiles.
    ''' </summary>
    ''' <remarks>
    ''' Reads nothing from the query string. The account is loaded using the PersonId on
    ''' the authentication ticket (BR-24).
    ''' </remarks>
    Public Class Dashboard
        Inherits BasePage

        Private ReadOnly _authentication As IAuthenticationService = New AuthenticationService()

        Protected WithEvents cardProfile As DashboardCard
        Protected WithEvents cardSubjects As DashboardCard
        Protected WithEvents cardFindTutor As DashboardCard
        Protected WithEvents cardRequests As DashboardCard
        Protected WithEvents cardBookings As DashboardCard
        Protected WithEvents cardReviews As DashboardCard

        Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
            ' Icons are assigned here rather than in markup: they are multi-line SVG and
            ' belong with the other presentation decisions, not inline in the page.
            cardProfile.Icon = Icons.Person
            cardSubjects.Icon = Icons.Book
            cardFindTutor.Icon = Icons.Search
            cardRequests.Icon = Icons.Clipboard
            cardBookings.Icon = Icons.Calendar
            cardReviews.Icon = Icons.Star

            RegisterAsyncTask(New PageAsyncTask(AddressOf LoadGreetingAsync))
        End Sub

        Private Async Function LoadGreetingAsync() As Task
            Dim shell As StudentShell = TryCast(Master, StudentShell)
            If shell Is Nothing Then Return

            shell.PageSubtitle = "What would you like to do today?"

            Dim result As ServiceResult(Of Person) =
                Await _authentication.GetAccountAsync(CurrentPersonId).ConfigureAwait(True)

            shell.PageTitle = If(result.Success,
                                 $"Welcome, {result.Data.FirstName}!",
                                 "Welcome!")
        End Function

    End Class

End Namespace
