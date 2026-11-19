Option Strict On
Option Explicit On

Imports TutorConnect.Enums

Namespace Models

    ''' <summary>
    ''' A user account. Student and Tutor hang off this 1:1 and are mutually exclusive;
    ''' an Administrator has neither profile row (D-05).
    ''' </summary>
    Public Class Person

        Public Property PersonId As Integer
        Public Property UserName As String
        Public Property FirstName As String
        Public Property LastName As String
        Public Property Gender As String
        Public Property EmailAddress As String
        Public Property CellNumber As String

        ''' <summary>
        ''' Town or campus area - East London, Komani, Mthatha. Never a street address.
        ''' This name is canonical across the ERD, schema, UI and validation messages.
        ''' </summary>
        Public Property Location As String

        Public Property Role As UserRole
        Public Property IsActive As Boolean

        ''' <summary>SEC-08. Reset to zero on a successful sign-in.</summary>
        Public Property FailedLoginCount As Integer

        ''' <summary>UTC. Null when the account is not locked out.</summary>
        Public Property LockoutEndUtc As Date?

        Public Property CreatedAt As Date
        Public Property UpdatedAt As Date?

        ''' <summary>Convenience for headings such as "Welcome, Zanele!".</summary>
        Public ReadOnly Property FullName As String
            Get
                Return $"{FirstName} {LastName}".Trim()
            End Get
        End Property

    End Class

    ''' <summary>
    ''' Credential material for one account. Kept apart from <see cref="Person"/> so that
    ''' hashes are only ever loaded by the authentication path and never ride along on a
    ''' profile object that a page might bind to markup.
    ''' </summary>
    Public Class PersonCredentials

        Public Property PersonId As Integer
        Public Property PasswordHash As Byte()
        Public Property PasswordSalt As Byte()
        Public Property HashIterations As Integer

    End Class

    ''' <summary>The Student side of a Person.</summary>
    Public Class StudentProfile

        Public Property StudentId As Integer
        Public Property PersonId As Integer
        Public Property StudentNo As String
        Public Property CreatedAt As Date

        ''' <summary>Populated when the row is loaded joined to Person; otherwise Nothing.</summary>
        Public Property Account As Person

    End Class

    ''' <summary>The Tutor side of a Person.</summary>
    Public Class TutorProfile

        Public Property TutorId As Integer
        Public Property PersonId As Integer
        Public Property Qualification As String
        Public Property ExperienceYears As Integer

        ''' <summary>Rate charged per session, in ZAR.</summary>
        Public Property RatePerSession As Decimal

        ''' <summary>Free text shown under "About Tutor" on the tutor detail screen.</summary>
        Public Property Bio As String

        ''' <summary>
        ''' BR-26. An unapproved tutor never appears in search results and cannot accept
        ''' requests. New tutor registrations start False.
        ''' </summary>
        Public Property IsApproved As Boolean

        Public Property ApprovedAt As Date?
        Public Property CreatedAt As Date

        Public Property Account As Person

    End Class

End Namespace
