Option Strict On
Option Explicit On

Imports TutorConnect.Enums

Namespace MasterPages

    ''' <summary>Shell for the student screens.</summary>
    Public Class StudentShell
        Inherits AppMasterBase

        Protected Overrides ReadOnly Property ShellRole As UserRole
            Get
                Return UserRole.Student
            End Get
        End Property

    End Class

End Namespace
