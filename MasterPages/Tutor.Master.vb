Option Strict On
Option Explicit On

Imports TutorConnect.Enums

Namespace MasterPages

    ''' <summary>Shell for the tutor screens.</summary>
    Public Class TutorShell
        Inherits AppMasterBase

        Protected Overrides ReadOnly Property ShellRole As UserRole
            Get
                Return UserRole.Tutor
            End Get
        End Property

    End Class

End Namespace

