/* =============================================================================
   TutorConnect - 02_SeedLookups.sql
   Purpose: Seed the two lookup tables only - Subject and StudyLevel.

   NO ACCOUNTS. NO PASSWORDS. Per D-07, user accounts are created by the VB.NET
   seeding routine (SeedRunner), which hashes passwords at runtime. No password
   literal ever appears in a .sql file in this repository.

   Re-runnable: inserts only what is missing, so existing SubjectId /
   StudyLevelId values keep pointing at the same rows.
   ============================================================================= */

SET NOCOUNT ON;
GO

USE TutorConnect;
GO

/* Subjects - from the original seed data. */
INSERT INTO dbo.[Subject] (SubjectName, IsActive)
SELECT v.SubjectName, 1
FROM (VALUES
        (N'Mathematics'),
        (N'Information Systems'),
        (N'English')
     ) AS v (SubjectName)
WHERE NOT EXISTS (SELECT 1 FROM dbo.[Subject] s WHERE s.SubjectName = v.SubjectName);
GO

/* Study levels - SortOrder exists because alphabetical order would put
   "First Year" after "Second Year" in any dropdown. */
INSERT INTO dbo.StudyLevel (StudyLevelName, SortOrder)
SELECT v.StudyLevelName, v.SortOrder
FROM (VALUES
        (N'First Year',  1),
        (N'Second Year', 2),
        (N'Third Year',  3)
     ) AS v (StudyLevelName, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.StudyLevel l WHERE l.StudyLevelName = v.StudyLevelName);
GO

/* Keep SortOrder correct even if a row already existed with a wrong value. */
UPDATE l
SET l.SortOrder = v.SortOrder
FROM dbo.StudyLevel l
JOIN (VALUES
        (N'First Year',  1),
        (N'Second Year', 2),
        (N'Third Year',  3)
     ) AS v (StudyLevelName, SortOrder)
  ON v.StudyLevelName = l.StudyLevelName
WHERE l.SortOrder <> v.SortOrder;
GO

PRINT 'Lookup seed complete.';
SELECT SubjectId, SubjectName, IsActive FROM dbo.[Subject] ORDER BY SubjectId;
SELECT StudyLevelId, StudyLevelName, SortOrder FROM dbo.StudyLevel ORDER BY SortOrder;
GO
