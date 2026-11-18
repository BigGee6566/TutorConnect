Option Strict On
Option Explicit On

Namespace Models

    ''' <summary>A subject that can be tutored, e.g. Information Systems.</summary>
    Public Class Subject

        Public Property SubjectId As Integer
        Public Property SubjectName As String

        ''' <summary>
        ''' A subject in use is deactivated rather than deleted (AT-25), so historical
        ''' bookings keep resolving to a name.
        ''' </summary>
        Public Property IsActive As Boolean

    End Class

    ''' <summary>A year of study, e.g. Third Year.</summary>
    Public Class StudyLevel

        Public Property StudyLevelId As Integer
        Public Property StudyLevelName As String

        ''' <summary>
        ''' Drives dropdown order. Alphabetical sorting would put First Year after
        ''' Second Year, which reads as a bug to a user.
        ''' </summary>
        Public Property SortOrder As Integer

    End Class

    ''' <summary>A subject + level combination a student has added to their profile.</summary>
    Public Class StudentSubject

        Public Property StudentSubjectId As Integer
        Public Property StudentId As Integer
        Public Property SubjectId As Integer
        Public Property StudyLevelId As Integer
        Public Property CreatedAt As Date

        Public Property SubjectName As String
        Public Property StudyLevelName As String

    End Class

    ''' <summary>
    ''' A subject + level combination a tutor teaches. This, not the tutor, is the unit
    ''' that search returns and that availability and requests attach to.
    ''' </summary>
    Public Class TutorSubject

        Public Property TutorSubjectId As Integer
        Public Property TutorId As Integer
        Public Property SubjectId As Integer
        Public Property StudyLevelId As Integer
        Public Property CreatedAt As Date

        Public Property SubjectName As String
        Public Property StudyLevelName As String

    End Class

    ''' <summary>
    ''' One row of the "Available Tutors" table. A flat projection built by a join, not
    ''' an entity - search returns one row per TutorSubject match, not per tutor.
    ''' </summary>
    Public Class TutorSearchResult

        Public Property TutorSubjectId As Integer
        Public Property TutorId As Integer
        Public Property PersonId As Integer
        Public Property FullName As String
        Public Property SubjectName As String
        Public Property StudyLevelName As String
        Public Property Qualification As String
        Public Property ExperienceYears As Integer
        Public Property RatePerSession As Decimal

        ''' <summary>
        ''' The tutor's own description, shown under "About Tutor".
        ''' </summary>
        ''' <remarks>
        ''' Populated only by the detail query - the results table does not show it, and
        ''' fetching up to 1000 characters per row for a list would be waste.
        ''' </remarks>
        Public Property Bio As String

        ''' <summary>Null when the tutor has no reviews yet.</summary>
        Public Property AverageRating As Decimal?

        Public Property ReviewCount As Integer

    End Class

End Namespace
