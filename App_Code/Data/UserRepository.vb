Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading.Tasks
Imports TutorConnect.Enums
Imports TutorConnect.Models
Imports TutorConnect.Utilities

Namespace Data

    ''' <summary>
    ''' Reads and writes Person, Student and Tutor rows.
    ''' </summary>
    ''' <remarks>
    ''' Parameterised SQL only, no business rules. Whether a tutor may be approved is a
    ''' service decision; this class only records the outcome.
    ''' </remarks>
    Public Class UserRepository

        Private Const PersonColumns As String =
            "p.PersonId, p.UserName, p.FirstName, p.LastName, p.Gender, p.EmailAddress, " &
            "p.CellNumber, p.Location, p.[Role], p.IsActive, p.FailedLoginCount, " &
            "p.LockoutEndUtc, p.CreatedAt, p.UpdatedAt"

#Region "Reads"

        ''' <summary>
        ''' Finds an account by username or e-mail address. Returns Nothing when no such
        ''' account exists - the caller must not reveal which of the two was wrong.
        ''' </summary>
        Public Async Function FindByUserNameOrEmailAsync(userNameOrEmail As String) As Task(Of Person)
            If String.IsNullOrWhiteSpace(userNameOrEmail) Then Return Nothing

            Const sql As String =
                "SELECT " & PersonColumns & " FROM dbo.Person AS p " &
                "WHERE p.UserName = @Identifier OR p.EmailAddress = @Identifier;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@Identifier", SqlDbType.VarChar, 100, userNameOrEmail.Trim())

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then Return Nothing
                        Return MapPerson(reader)
                    End Using
                End Using
            End Using
        End Function

        Public Async Function GetByIdAsync(personId As Integer) As Task(Of Person)
            Const sql As String =
                "SELECT " & PersonColumns & " FROM dbo.Person AS p WHERE p.PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then Return Nothing
                        Return MapPerson(reader)
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>
        ''' Loads the credential material for one account, separately from the profile.
        ''' </summary>
        Public Async Function GetCredentialsAsync(personId As Integer) As Task(Of PersonCredentials)
            Const sql As String =
                "SELECT PersonId, PasswordHash, PasswordSalt, HashIterations " &
                "FROM dbo.Person WHERE PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then Return Nothing

                        Return New PersonCredentials With {
                            .PersonId = DatabaseHelper.GetInt32(reader, "PersonId"),
                            .PasswordHash = DatabaseHelper.GetBytes(reader, "PasswordHash"),
                            .PasswordSalt = DatabaseHelper.GetBytes(reader, "PasswordSalt"),
                            .HashIterations = DatabaseHelper.GetInt32(reader, "HashIterations")
                        }
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>BR-01. Case-insensitive, because SQL Server's default collation is.</summary>
        Public Async Function UserNameExistsAsync(userName As String,
                                                  Optional excludingPersonId As Integer = 0) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.Person WHERE UserName = @UserName AND PersonId <> @ExcludingPersonId;"
            Return Await ScalarExistsAsync(sql, "@UserName", SqlDbType.VarChar, 50, userName, excludingPersonId).ConfigureAwait(False)
        End Function

        ''' <summary>BR-01.</summary>
        Public Async Function EmailExistsAsync(emailAddress As String,
                                               Optional excludingPersonId As Integer = 0) As Task(Of Boolean)
            Const sql As String =
                "SELECT 1 FROM dbo.Person WHERE EmailAddress = @EmailAddress AND PersonId <> @ExcludingPersonId;"
            Return Await ScalarExistsAsync(sql, "@EmailAddress", SqlDbType.VarChar, 100, emailAddress, excludingPersonId).ConfigureAwait(False)
        End Function

        ''' <summary>
        ''' True when the system already has at least one administrator account.
        ''' </summary>
        ''' <remarks>
        ''' This is what closes the first-run setup page. It counts every administrator row,
        ''' active or not: a deactivated administrator is still an account someone controls,
        ''' and treating the system as unconfigured because the only administrator was
        ''' switched off would let anyone mint a new one.
        ''' </remarks>
        Public Async Function AdministratorExistsAsync() As Task(Of Boolean)
            Const sql As String = "SELECT TOP 1 1 FROM dbo.Person WHERE [Role] = 'Administrator';"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        Private Async Function ScalarExistsAsync(sql As String,
                                                 parameterName As String,
                                                 type As SqlDbType,
                                                 size As Integer,
                                                 value As String,
                                                 excludingPersonId As Integer) As Task(Of Boolean)
            If String.IsNullOrWhiteSpace(value) Then Return False

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, parameterName, type, size, value.Trim())
                    DatabaseHelper.AddParameter(command, "@ExcludingPersonId", SqlDbType.Int, excludingPersonId)

                    Dim found As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                    Return found IsNot Nothing AndAlso Not Convert.IsDBNull(found)
                End Using
            End Using
        End Function

        Public Async Function GetStudentByPersonIdAsync(personId As Integer) As Task(Of StudentProfile)
            Const sql As String =
                "SELECT s.StudentId, s.StudentNo, s.CreatedAt AS StudentCreatedAt, " & PersonColumns & " " &
                "FROM dbo.Student AS s JOIN dbo.Person AS p ON p.PersonId = s.PersonId " &
                "WHERE s.PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then Return Nothing

                        Return New StudentProfile With {
                            .StudentId = DatabaseHelper.GetInt32(reader, "StudentId"),
                            .StudentNo = DatabaseHelper.GetString(reader, "StudentNo"),
                            .PersonId = DatabaseHelper.GetInt32(reader, "PersonId"),
                            .CreatedAt = DatabaseHelper.GetDate(reader, "StudentCreatedAt"),
                            .Account = MapPerson(reader)
                        }
                    End Using
                End Using
            End Using
        End Function

        Public Async Function GetTutorByPersonIdAsync(personId As Integer) As Task(Of TutorProfile)
            Const sql As String =
                "SELECT t.TutorId, t.Qualification, t.ExperienceYears, t.RatePerSession, " &
                "t.Bio, t.IsApproved, t.ApprovedAt, t.CreatedAt AS TutorCreatedAt, " & PersonColumns & " " &
                "FROM dbo.Tutor AS t JOIN dbo.Person AS p ON p.PersonId = t.PersonId " &
                "WHERE t.PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        If Not Await reader.ReadAsync().ConfigureAwait(False) Then Return Nothing
                        Return MapTutor(reader)
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>Admin user list, filtered server-side.</summary>
        Public Async Function SearchAsync(searchTerm As String,
                                          role As UserRole?,
                                          isActive As Boolean?) As Task(Of IList(Of Person))
            Const sql As String =
                "SELECT " & PersonColumns & " FROM dbo.Person AS p " &
                "WHERE (@SearchTerm IS NULL " &
                "       OR p.FirstName LIKE '%' + @SearchTerm + '%' " &
                "       OR p.LastName  LIKE '%' + @SearchTerm + '%' " &
                "       OR p.UserName  LIKE '%' + @SearchTerm + '%' " &
                "       OR p.EmailAddress LIKE '%' + @SearchTerm + '%') " &
                "  AND (@Role IS NULL OR p.[Role] = @Role) " &
                "  AND (@IsActive IS NULL OR p.IsActive = @IsActive) " &
                "ORDER BY p.LastName, p.FirstName;"

            Dim results As New List(Of Person)()

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@SearchTerm", SqlDbType.NVarChar, 100,
                                                DatabaseHelper.ToDbValue(searchTerm))
                    DatabaseHelper.AddParameter(command, "@Role", SqlDbType.VarChar, 15,
                                                If(role.HasValue, CObj(role.Value.ToString()), CObj(DBNull.Value)))
                    DatabaseHelper.AddParameter(command, "@IsActive", SqlDbType.Bit,
                                                If(isActive.HasValue, CObj(isActive.Value), CObj(DBNull.Value)))

                    Using reader As SqlDataReader = Await command.ExecuteReaderAsync().ConfigureAwait(False)
                        While Await reader.ReadAsync().ConfigureAwait(False)
                            results.Add(MapPerson(reader))
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

#End Region

#Region "Writes"

        ''' <summary>
        ''' Creates a Person plus the matching Student or Tutor row in ONE transaction
        ''' (AT-01). A half-created account that can sign in but has no profile is worse
        ''' than a failed registration.
        ''' </summary>
        ''' <param name="studentNo">Required for a Student, ignored otherwise.</param>
        ''' <param name="tutorDetails">Required for a Tutor, ignored otherwise.</param>
        ''' <returns>The new PersonId.</returns>
        Public Async Function CreateAccountAsync(account As Person,
                                                 passwordHash As Byte(),
                                                 passwordSalt As Byte(),
                                                 hashIterations As Integer,
                                                 studentNo As String,
                                                 tutorDetails As TutorProfile) As Task(Of Integer)
            If account Is Nothing Then Throw New ArgumentNullException(NameOf(account))

            Const insertPerson As String =
                "INSERT INTO dbo.Person (UserName, FirstName, LastName, Gender, EmailAddress, " &
                "CellNumber, PasswordHash, PasswordSalt, HashIterations, Location, [Role], IsActive) " &
                "VALUES (@UserName, @FirstName, @LastName, @Gender, @EmailAddress, @CellNumber, " &
                "@PasswordHash, @PasswordSalt, @HashIterations, @Location, @Role, @IsActive); " &
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using transaction As SqlTransaction = connection.BeginTransaction()
                    Try
                        Dim personId As Integer

                        Using command As SqlCommand = DatabaseHelper.CreateCommand(insertPerson, connection, transaction)
                            DatabaseHelper.AddParameter(command, "@UserName", SqlDbType.VarChar, 50, account.UserName)
                            DatabaseHelper.AddParameter(command, "@FirstName", SqlDbType.NVarChar, 50, account.FirstName)
                            DatabaseHelper.AddParameter(command, "@LastName", SqlDbType.NVarChar, 50, account.LastName)
                            DatabaseHelper.AddParameter(command, "@Gender", SqlDbType.VarChar, 10, account.Gender)
                            DatabaseHelper.AddParameter(command, "@EmailAddress", SqlDbType.VarChar, 100, account.EmailAddress)
                            DatabaseHelper.AddParameter(command, "@CellNumber", SqlDbType.VarChar, 15, account.CellNumber)
                            DatabaseHelper.AddParameter(command, "@PasswordHash", SqlDbType.VarBinary, 64, passwordHash)
                            DatabaseHelper.AddParameter(command, "@PasswordSalt", SqlDbType.VarBinary, 32, passwordSalt)
                            DatabaseHelper.AddParameter(command, "@HashIterations", SqlDbType.Int, hashIterations)
                            DatabaseHelper.AddParameter(command, "@Location", SqlDbType.NVarChar, 100, account.Location)
                            DatabaseHelper.AddParameter(command, "@Role", SqlDbType.VarChar, 15, account.Role.ToString())
                            DatabaseHelper.AddParameter(command, "@IsActive", SqlDbType.Bit, account.IsActive)

                            Dim scalar As Object = Await command.ExecuteScalarAsync().ConfigureAwait(False)
                            personId = Convert.ToInt32(scalar, Globalization.CultureInfo.InvariantCulture)
                        End Using

                        Select Case account.Role
                            Case UserRole.Student
                                Const insertStudent As String =
                                    "INSERT INTO dbo.Student (StudentNo, PersonId) VALUES (@StudentNo, @PersonId);"
                                Using command As SqlCommand = DatabaseHelper.CreateCommand(insertStudent, connection, transaction)
                                    DatabaseHelper.AddParameter(command, "@StudentNo", SqlDbType.VarChar, 20, studentNo)
                                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                                    Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                                End Using

                            Case UserRole.Tutor
                                If tutorDetails Is Nothing Then
                                    Throw New ArgumentNullException(NameOf(tutorDetails),
                                        "A Tutor account requires qualification, experience and rate.")
                                End If

                                Const insertTutor As String =
                                    "INSERT INTO dbo.Tutor (PersonId, Qualification, ExperienceYears, " &
                                    "RatePerSession, Bio, IsApproved) " &
                                    "VALUES (@PersonId, @Qualification, @ExperienceYears, @RatePerSession, @Bio, 0);"
                                Using command As SqlCommand = DatabaseHelper.CreateCommand(insertTutor, connection, transaction)
                                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                                    DatabaseHelper.AddParameter(command, "@Qualification", SqlDbType.NVarChar, 100, tutorDetails.Qualification)
                                    DatabaseHelper.AddParameter(command, "@ExperienceYears", SqlDbType.Int, tutorDetails.ExperienceYears)
                                    DatabaseHelper.AddDecimalParameter(command, "@RatePerSession", tutorDetails.RatePerSession)
                                    DatabaseHelper.AddParameter(command, "@Bio", SqlDbType.NVarChar, 1000, DatabaseHelper.ToDbValue(tutorDetails.Bio))
                                    Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                                End Using

                            Case UserRole.Administrator
                                ' No profile row by design (D-05).
                        End Select

                        transaction.Commit()
                        Return personId

                    Catch
                        ' The rollback is guarded. A transaction that SQL Server has already
                        ' doomed - a severe error, or a dropped connection - throws
                        ' "This SqlTransaction has completed" from Rollback, and because that
                        ' happens inside the Catch it REPLACES the original exception. The
                        ' real failure is then invisible, which is exactly how a seeding error
                        ' surfaced here as a meaningless ZombieCheck stack.
                        Try
                            transaction.Rollback()
                        Catch rollbackFailure As InvalidOperationException
                            ' Already dead; nothing to undo, and nothing worth reporting
                            ' over the exception that actually matters.
                        End Try

                        Throw
                    End Try
                End Using
            End Using
        End Function

        Public Async Function UpdateProfileAsync(account As Person) As Task(Of Boolean)
            If account Is Nothing Then Throw New ArgumentNullException(NameOf(account))

            Const sql As String =
                "UPDATE dbo.Person SET FirstName = @FirstName, LastName = @LastName, " &
                "Gender = @Gender, EmailAddress = @EmailAddress, CellNumber = @CellNumber, " &
                "Location = @Location, UpdatedAt = SYSUTCDATETIME() " &
                "WHERE PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@FirstName", SqlDbType.NVarChar, 50, account.FirstName)
                    DatabaseHelper.AddParameter(command, "@LastName", SqlDbType.NVarChar, 50, account.LastName)
                    DatabaseHelper.AddParameter(command, "@Gender", SqlDbType.VarChar, 10, account.Gender)
                    DatabaseHelper.AddParameter(command, "@EmailAddress", SqlDbType.VarChar, 100, account.EmailAddress)
                    DatabaseHelper.AddParameter(command, "@CellNumber", SqlDbType.VarChar, 15, account.CellNumber)
                    DatabaseHelper.AddParameter(command, "@Location", SqlDbType.NVarChar, 100, account.Location)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, account.PersonId)

                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        ''' <summary>SEC-08. Records a failed sign-in and applies a lockout window when the threshold is reached.</summary>
        Public Async Function RegisterFailedLoginAsync(personId As Integer,
                                                       threshold As Integer,
                                                       lockoutMinutes As Integer) As Task
            Const sql As String =
                "UPDATE dbo.Person " &
                "SET FailedLoginCount = FailedLoginCount + 1, " &
                "    LockoutEndUtc = CASE WHEN FailedLoginCount + 1 >= @Threshold " &
                "                         THEN DATEADD(MINUTE, @LockoutMinutes, SYSUTCDATETIME()) " &
                "                         ELSE LockoutEndUtc END " &
                "WHERE PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@Threshold", SqlDbType.Int, threshold)
                    DatabaseHelper.AddParameter(command, "@LockoutMinutes", SqlDbType.Int, lockoutMinutes)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                End Using
            End Using
        End Function

        Public Async Function ResetFailedLoginAsync(personId As Integer) As Task
            Const sql As String =
                "UPDATE dbo.Person SET FailedLoginCount = 0, LockoutEndUtc = NULL WHERE PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                End Using
            End Using
        End Function

        ''' <summary>BR-25. Deactivate, never delete - the tutoring record must survive.</summary>
        Public Async Function SetActiveAsync(personId As Integer, isActive As Boolean) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.Person SET IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME() WHERE PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@IsActive", SqlDbType.Bit, isActive)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

        Public Async Function SetPasswordAsync(personId As Integer,
                                               passwordHash As Byte(),
                                               passwordSalt As Byte(),
                                               hashIterations As Integer) As Task(Of Boolean)
            Const sql As String =
                "UPDATE dbo.Person SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, " &
                "HashIterations = @HashIterations, UpdatedAt = SYSUTCDATETIME() WHERE PersonId = @PersonId;"

            Using connection As SqlConnection = Await DatabaseHelper.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command As SqlCommand = DatabaseHelper.CreateCommand(sql, connection)
                    DatabaseHelper.AddParameter(command, "@PasswordHash", SqlDbType.VarBinary, 64, passwordHash)
                    DatabaseHelper.AddParameter(command, "@PasswordSalt", SqlDbType.VarBinary, 32, passwordSalt)
                    DatabaseHelper.AddParameter(command, "@HashIterations", SqlDbType.Int, hashIterations)
                    DatabaseHelper.AddParameter(command, "@PersonId", SqlDbType.Int, personId)
                    Return Await command.ExecuteNonQueryAsync().ConfigureAwait(False) = 1
                End Using
            End Using
        End Function

#End Region

#Region "Mapping"

        Friend Shared Function MapPerson(reader As SqlDataReader) As Person
            Return New Person With {
                .PersonId = DatabaseHelper.GetInt32(reader, "PersonId"),
                .UserName = DatabaseHelper.GetString(reader, "UserName"),
                .FirstName = DatabaseHelper.GetString(reader, "FirstName"),
                .LastName = DatabaseHelper.GetString(reader, "LastName"),
                .Gender = DatabaseHelper.GetString(reader, "Gender"),
                .EmailAddress = DatabaseHelper.GetString(reader, "EmailAddress"),
                .CellNumber = DatabaseHelper.GetString(reader, "CellNumber"),
                .Location = DatabaseHelper.GetString(reader, "Location"),
                .Role = EnumHelper.Parse(Of UserRole)(DatabaseHelper.GetString(reader, "Role")),
                .IsActive = DatabaseHelper.GetBoolean(reader, "IsActive"),
                .FailedLoginCount = DatabaseHelper.GetInt32(reader, "FailedLoginCount"),
                .LockoutEndUtc = DatabaseHelper.GetNullableDate(reader, "LockoutEndUtc"),
                .CreatedAt = DatabaseHelper.GetDate(reader, "CreatedAt"),
                .UpdatedAt = DatabaseHelper.GetNullableDate(reader, "UpdatedAt")
            }
        End Function

        ''' <summary>
        ''' Maps a Tutor joined to its Person. The query MUST alias the tutor's own
        ''' CreatedAt as TutorCreatedAt: both tables have a CreatedAt column, and
        ''' GetOrdinal resolves a duplicate name to the first match.
        ''' </summary>
        Friend Shared Function MapTutor(reader As SqlDataReader) As TutorProfile
            Return New TutorProfile With {
                .TutorId = DatabaseHelper.GetInt32(reader, "TutorId"),
                .PersonId = DatabaseHelper.GetInt32(reader, "PersonId"),
                .Qualification = DatabaseHelper.GetString(reader, "Qualification"),
                .ExperienceYears = DatabaseHelper.GetInt32(reader, "ExperienceYears"),
                .RatePerSession = DatabaseHelper.GetDecimal(reader, "RatePerSession"),
                .Bio = DatabaseHelper.GetStringOrNothing(reader, "Bio"),
                .IsApproved = DatabaseHelper.GetBoolean(reader, "IsApproved"),
                .ApprovedAt = DatabaseHelper.GetNullableDate(reader, "ApprovedAt"),
                .CreatedAt = DatabaseHelper.GetDate(reader, "TutorCreatedAt"),
                .Account = MapPerson(reader)
            }
        End Function

#End Region

    End Class

End Namespace
