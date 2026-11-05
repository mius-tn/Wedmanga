-- =============================================
-- Admin Panel Stored Procedures & Triggers
-- =============================================

-- =============================================
-- 1. STORIES SPs
-- =============================================

GO
CREATE OR ALTER PROCEDURE sp_Admin_CreateStory
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @Title NVARCHAR(255),
    @Slug NVARCHAR(255),
    @Description NVARCHAR(MAX) = NULL,
    @Author NVARCHAR(100) = NULL,
    @CoverImage NVARCHAR(255) = NULL,
    @Status NVARCHAR(20) = 'Ongoing',
    @Type NVARCHAR(50) = 'Manga',
    @ChapterCount INT = 0,
    @ViewCount INT = 0,
    @RatingAvg FLOAT = 0,
    @CategoryIds NVARCHAR(MAX) = NULL -- Comma separated IDs
AS
BEGIN
    SET NOCOUNT ON;
    
    -- RBAC Check
    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access. Only Admin or Moderator can create stories.', 1;

    -- Validation
    IF EXISTS(SELECT 1 FROM Stories WHERE Slug = @Slug)
        THROW 50002, 'Slug already exists. Please choose a unique title or slug.', 1;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @NewStoryId INT;

        INSERT INTO Stories (Title, Slug, Description, Author, CoverImage, Status, Type, ViewCount, RatingAvg, CreatedAt, IsDeleted)
        VALUES (@Title, @Slug, @Description, @Author, @CoverImage, @Status, @Type, @ViewCount, @RatingAvg, GETUTCDATE(), 0);

        SET @NewStoryId = SCOPE_IDENTITY();

        -- Insert Categories if provided
        IF @CategoryIds IS NOT NULL AND @CategoryIds <> ''
        BEGIN
            INSERT INTO StoryCategories (StoryId, CategoryId)
            SELECT @NewStoryId, CAST(value AS INT)
            FROM STRING_SPLIT(@CategoryIds, ',');
        END

        -- Logging
        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, NewValues, CreatedAt)
        VALUES (@AdminUserId, 'CREATE', 'Stories', CAST(@NewStoryId AS NVARCHAR), 
                CONCAT('{"Title":"', @Title, '", "Slug":"', @Slug, '"}'), GETUTCDATE());

        COMMIT TRAN;
        SELECT @NewStoryId AS StoryId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_Admin_UpdateStory
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @StoryId INT,
    @Title NVARCHAR(255),
    @Slug NVARCHAR(255),
    @Description NVARCHAR(MAX) = NULL,
    @Author NVARCHAR(100) = NULL,
    @CoverImage NVARCHAR(255) = NULL,
    @Status NVARCHAR(20),
    @Type NVARCHAR(50),
    @ViewCount INT,
    @RatingAvg FLOAT,
    @CategoryIds NVARCHAR(MAX) = NULL -- Comma separated IDs
AS
BEGIN
    SET NOCOUNT ON;

    -- RBAC Check
    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access.', 1;

    IF NOT EXISTS(SELECT 1 FROM Stories WHERE StoryId = @StoryId AND IsDeleted = 0)
        THROW 50003, 'Story not found or deleted.', 1;

    -- Duplicate slug check for other stories
    IF EXISTS(SELECT 1 FROM Stories WHERE Slug = @Slug AND StoryId <> @StoryId)
        THROW 50002, 'Slug already exists for another story.', 1;

    BEGIN TRY
        BEGIN TRAN;

        UPDATE Stories
        SET Title = @Title,
            Slug = @Slug,
            Description = @Description,
            Author = @Author,
            CoverImage = ISNULL(@CoverImage, CoverImage), -- Keep old if null
            Status = @Status,
            Type = @Type,
            ViewCount = @ViewCount,
            RatingAvg = @RatingAvg
        WHERE StoryId = @StoryId;

        -- Update Categories (Delete old, insert new)
        IF ISNULL(@CategoryIds, '') <> ''
        BEGIN
            DELETE FROM StoryCategories WHERE StoryId = @StoryId;
            
            INSERT INTO StoryCategories (StoryId, CategoryId)
            SELECT @StoryId, CAST(value AS INT)
            FROM STRING_SPLIT(@CategoryIds, ',');
        END

        -- Logging
        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, NewValues, CreatedAt)
        VALUES (@AdminUserId, 'UPDATE', 'Stories', CAST(@StoryId AS NVARCHAR), 
                CONCAT('{"UpdatedTitle":"', @Title, '"}'), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_Admin_DeleteStory
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @StoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- RBAC Check
    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access. Only Admin or Moderator can delete stories.', 1;

    BEGIN TRY
        BEGIN TRAN;

        -- Soft delete story
        UPDATE Stories SET IsDeleted = 1 WHERE StoryId = @StoryId;

        -- Logging
        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, CreatedAt)
        VALUES (@AdminUserId, 'DELETE', 'Stories', CAST(@StoryId AS NVARCHAR), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

-- =============================================
-- 2. CHAPTER SPs
-- =============================================

CREATE OR ALTER PROCEDURE sp_Admin_CreateChapter
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @StoryId INT,
    @ChapterNumber INT,
    @Title NVARCHAR(255) = NULL,
    @Content NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access.', 1;

    IF EXISTS(SELECT 1 FROM Chapters WHERE StoryId = @StoryId AND ChapterNumber = @ChapterNumber AND IsDeleted=0)
        THROW 50004, 'Chapter number already exists for this story.', 1;

    BEGIN TRY
        BEGIN TRAN;
        DECLARE @NewChapterId INT;

        INSERT INTO Chapters (StoryId, ChapterNumber, Title, Content, ViewCount, CreatedAt, IsDeleted)
        VALUES (@StoryId, @ChapterNumber, @Title, @Content, 0, GETUTCDATE(), 0);

        SET @NewChapterId = SCOPE_IDENTITY();

        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, CreatedAt)
        VALUES (@AdminUserId, 'CREATE', 'Chapters', CAST(@NewChapterId AS NVARCHAR), GETUTCDATE());

        COMMIT TRAN;
        SELECT @NewChapterId AS ChapterId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_Admin_UpdateChapter
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @ChapterId INT,
    @ChapterNumber INT,
    @Title NVARCHAR(255) = NULL,
    @Content NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access.', 1;

    DECLARE @StoryId INT = (SELECT StoryId FROM Chapters WHERE ChapterId = @ChapterId);

    IF EXISTS(SELECT 1 FROM Chapters WHERE StoryId = @StoryId AND ChapterNumber = @ChapterNumber AND ChapterId <> @ChapterId AND IsDeleted=0)
        THROW 50004, 'Chapter number already exists for this story.', 1;

    BEGIN TRY
        BEGIN TRAN;

        UPDATE Chapters
        SET ChapterNumber = @ChapterNumber,
            Title = @Title,
            Content = @Content
        WHERE ChapterId = @ChapterId;

        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, CreatedAt)
        VALUES (@AdminUserId, 'UPDATE', 'Chapters', CAST(@ChapterId AS NVARCHAR), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_Admin_DeleteChapter
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @ChapterId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access.', 1;

    BEGIN TRY
        BEGIN TRAN;

        UPDATE Chapters SET IsDeleted = 1 WHERE ChapterId = @ChapterId;

        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, CreatedAt)
        VALUES (@AdminUserId, 'DELETE', 'Chapters', CAST(@ChapterId AS NVARCHAR), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

-- =============================================
-- 3. CATEGORY SPs
-- =============================================

CREATE OR ALTER PROCEDURE sp_Admin_CreateCategory
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @Name NVARCHAR(100),
    @Slug NVARCHAR(100),
    @Description NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access.', 1;

    IF EXISTS(SELECT 1 FROM Categories WHERE Slug = @Slug)
        THROW 50005, 'Category Slug already exists.', 1;

    BEGIN TRY
        BEGIN TRAN;
        DECLARE @NewCategoryId INT;

        INSERT INTO Categories (Name, Slug, Description, IsDeleted, CreatedAt, UpdatedAt)
        VALUES (@Name, @Slug, @Description, 0, GETUTCDATE(), GETUTCDATE());

        SET @NewCategoryId = SCOPE_IDENTITY();

        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, CreatedAt)
        VALUES (@AdminUserId, 'CREATE', 'Categories', CAST(@NewCategoryId AS NVARCHAR), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_Admin_UpdateCategory
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @CategoryId INT,
    @Name NVARCHAR(100),
    @Slug NVARCHAR(100),
    @Description NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access.', 1;

    IF EXISTS(SELECT 1 FROM Categories WHERE Slug = @Slug AND CategoryId <> @CategoryId)
        THROW 50005, 'Category Slug already exists.', 1;

    BEGIN TRY
        BEGIN TRAN;

        UPDATE Categories
        SET Name = @Name,
            Slug = @Slug,
            Description = @Description,
            UpdatedAt = GETUTCDATE()
        WHERE CategoryId = @CategoryId;

        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, CreatedAt)
        VALUES (@AdminUserId, 'UPDATE', 'Categories', CAST(@CategoryId AS NVARCHAR), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_Admin_DeleteCategory
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @AdminRoleName NOT IN ('Admin', 'Moderator')
        THROW 50001, 'Unauthorized access.', 1;

    BEGIN TRY
        BEGIN TRAN;

        UPDATE Categories SET IsDeleted = 1 WHERE CategoryId = @CategoryId;

        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, CreatedAt)
        VALUES (@AdminUserId, 'DELETE', 'Categories', CAST(@CategoryId AS NVARCHAR), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

-- =============================================
-- 4. USER SPs
-- =============================================

CREATE OR ALTER PROCEDURE sp_Admin_UpdateUserStatus
    @AdminUserId INT,
    @AdminRoleName NVARCHAR(50),
    @TargetUserId INT,
    @Status NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    -- Only Admin can block/activate users
    IF @AdminRoleName <> 'Admin'
        THROW 50006, 'Unauthorized access. Only Admin can update user status.', 1;

    BEGIN TRY
        BEGIN TRAN;

        UPDATE Users SET Status = @Status WHERE UserId = @TargetUserId;

        INSERT INTO AuditLogs (AdminUserId, ActionType, TableName, RecordId, NewValues, CreatedAt)
        VALUES (@AdminUserId, 'UPDATE', 'Users', CAST(@TargetUserId AS NVARCHAR), CONCAT('{"Status":"', @Status, '"}'), GETUTCDATE());

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END
GO

-- =============================================
-- 5. TRIGGERS
-- =============================================

-- Trigger for Auto-Updating Story Rating
CREATE OR ALTER TRIGGER TRG_UpdateStoryRating
ON Ratings
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StoryId INT;
    
    IF EXISTS (SELECT 1 FROM inserted) BEGIN
        SELECT TOP 1 @StoryId = StoryId FROM inserted;
    END ELSE BEGIN
        SELECT TOP 1 @StoryId = StoryId FROM deleted;
    END

    IF @StoryId IS NOT NULL
    BEGIN
        DECLARE @Avg FLOAT = (SELECT ISNULL(AVG(CAST(RatingValue AS FLOAT)), 0) FROM Ratings WHERE StoryId = @StoryId);
        UPDATE Stories SET RatingAvg = ROUND(@Avg, 2) WHERE StoryId = @StoryId;
    END
END
GO

-- Trigger for Auto-Updating Story ViewCount when Chapters are updated (Simulating read increment)
CREATE OR ALTER TRIGGER TRG_UpdateStoryViewCount
ON Chapters
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    IF UPDATE(ViewCount)
    BEGIN
        -- Add the difference between new ViewCount and old ViewCount to the Story ViewCount
        UPDATE S
        SET S.ViewCount = S.ViewCount + (i.ViewCount - d.ViewCount)
        FROM Stories S
        INNER JOIN inserted i ON S.StoryId = i.StoryId
        INNER JOIN deleted d ON i.ChapterId = d.ChapterId
        WHERE i.ViewCount > d.ViewCount;
    END
END
GO
