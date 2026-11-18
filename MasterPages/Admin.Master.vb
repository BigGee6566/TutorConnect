Option Strict On
Option Explicit On

Imports TutorConnect.Enums

Namespace MasterPages

    ''' <summary>Shell for the administrator screens.</summary>
    Public Class AdminShell
        Inherits AppMasterBase

        Protected Overrides ReadOnly Property ShellRole As UserRole
            Get
                Return UserRole.Administrator
            End Get
        End Property

    End Class

End Namespace

