Option Strict On
Option Explicit On

Imports System.Threading.Tasks
Imports TutorConnect.Data
Imports TutorConnect.Models

Namespace Security

    ''' <summary>
    ''' Base for pages inside /Student/.
    ''' </summary>
    ''' <remarks>
    ''' Every student screen needs the StudentId, and every one of them must derive it from
    ''' the authentication ticket rather than from a query string or a hidden field
    ''' (BR-24). Putting that here means no page can quietly do it differently.
    ''' </remarks>
    Public MustInherit Class StudentPageBase
        Inherits BasePage

        Private ReadOnly _users As New UserRepository()
        Private _studentId As Integer = -1

        ''' <summary>
        ''' The signed-in student's StudentId, or 0 when this account has no student row.
        ''' </summary>
        ''' <remarks>Cached for the request: several controls on a page may ask for it.</remarks>
        Protected Async Function ResolveStudentIdAsync() As Task(Of Integer)
            If _studentId >= 0 Then Return _studentId

            Dim profile As StudentProfile =
                Await _users.GetStudentByPersonIdAsync(CurrentPersonId).ConfigureAwait(True)

            _studentId = If(profile Is Nothing, 0, profile.StudentId)
            Return _studentId
        End Function

        Protected Async Function ResolveStudentProfileAsync() As Task(Of StudentProfile)
            Return Await _users.GetStudentByPersonIdAsync(CurrentPersonId).ConfigureAwait(True)
        End Function

    End Class

    ''' <summary>
    ''' Base for pages inside /Tutor/.
    ''' </summary>
    Public MustInherit Class TutorPageBase
        Inherits BasePage

        Private ReadOnly _users As New UserRepository()
        Private _tutorId As Integer = -1

        ''' <summary>The signed-in tutor's TutorId, or 0 when this account has no tutor row.</summary>
        Protected Async Function ResolveTutorIdAsync() As Task(Of Integer)
            If _tutorId >= 0 Then Return _tutorId

            Dim profile As TutorProfile =
                Await _users.GetTutorByPersonIdAsync(CurrentPersonId).ConfigureAwait(True)

            _tutorId = If(profile Is Nothing, 0, profile.TutorId)
            Return _tutorId
        End Function

        Protected Async Function ResolveTutorProfileAsync() As Task(Of TutorProfile)
            Return Await _users.GetTutorByPersonIdAsync(CurrentPersonId).ConfigureAwait(True)
        End Function

    End Class

End Namespace
