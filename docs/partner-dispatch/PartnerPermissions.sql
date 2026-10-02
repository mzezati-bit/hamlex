-- روی بانک hamlex کامپیوتر انبار اجرا شود.
-- سه مجوز ارسال با همکار را مثل Return.View به جدول Permissions اضافه می‌کند
-- و به همان نقش‌هایی می‌دهد که مجوز مرجوعی متناظر را دارند.

SET NOCOUNT ON;

DECLARE @objectId int = OBJECT_ID(N'dbo.Permissions');
IF @objectId IS NULL
BEGIN
    RAISERROR(N'جدول dbo.Permissions پیدا نشد.', 16, 1);
    RETURN;
END

DECLARE @keyColumn sysname = NULL;
DECLARE @col sysname;
DECLARE @sql nvarchar(max);
DECLARE @found bit = 0;

DECLARE cols CURSOR LOCAL FAST_FORWARD FOR
SELECT c.name
FROM sys.columns c
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = @objectId
  AND c.is_computed = 0
  AND ty.name IN (N'nvarchar', N'varchar', N'nchar', N'char');

OPEN cols;
FETCH NEXT FROM cols INTO @col;
WHILE @@FETCH_STATUS = 0 AND @keyColumn IS NULL
BEGIN
    SET @sql = N'SELECT @foundOut = CASE WHEN EXISTS (SELECT 1 FROM dbo.Permissions WHERE '
        + QUOTENAME(@col) + N' = N''Return.View'') THEN 1 ELSE 0 END';
    SET @found = 0;
    EXEC sp_executesql @sql, N'@foundOut bit OUTPUT', @foundOut = @found OUTPUT;
    IF @found = 1
        SET @keyColumn = @col;
    FETCH NEXT FROM cols INTO @col;
END
CLOSE cols;
DEALLOCATE cols;

IF @keyColumn IS NULL
BEGIN
    RAISERROR(N'مقدار Return.View در جدول Permissions پیدا نشد.', 16, 1);
    RETURN;
END

DECLARE @idColumn sysname = NULL;
SELECT @idColumn = c.name
FROM sys.columns c
WHERE c.object_id = @objectId
  AND c.name = N'Id'
  AND c.is_identity = 0
  AND c.is_computed = 0;

DECLARE @insertCols nvarchar(max) = N'';
DECLARE @selectCols nvarchar(max) = N'';

SELECT
    @insertCols = @insertCols + CASE WHEN @insertCols = N'' THEN N'' ELSE N', ' END + QUOTENAME(c.name),
    @selectCols = @selectCols + CASE WHEN @selectCols = N'' THEN N'' ELSE N', ' END +
        CASE
            WHEN c.name = @keyColumn THEN N'@newName'
            WHEN @idColumn IS NOT NULL AND c.name = @idColumn THEN N'(SELECT ISNULL(MAX(' + QUOTENAME(@idColumn) + N'), 0) + 1 FROM dbo.Permissions)'
            ELSE N's.' + QUOTENAME(c.name)
        END
FROM sys.columns c
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = @objectId
  AND c.is_identity = 0
  AND c.is_computed = 0
  AND ty.name <> N'timestamp'
ORDER BY c.column_id;

DECLARE @pairs TABLE (OldName nvarchar(100), NewName nvarchar(100));
INSERT INTO @pairs (OldName, NewName) VALUES
    (N'Return.View', N'PartnerDispatch.View'),
    (N'Return.Create', N'PartnerDispatch.Create'),
    (N'Return.Edit', N'PartnerDispatch.Edit');

DECLARE @oldName nvarchar(100);
DECLARE @newName nvarchar(100);

DECLARE paircur CURSOR LOCAL FAST_FORWARD FOR
SELECT OldName, NewName FROM @pairs;

OPEN paircur;
FETCH NEXT FROM paircur INTO @oldName, @newName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
        IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE ' + QUOTENAME(@keyColumn) + N' = @newName)
           AND EXISTS (SELECT 1 FROM dbo.Permissions WHERE ' + QUOTENAME(@keyColumn) + N' = @oldName)
        BEGIN
            INSERT INTO dbo.Permissions (' + @insertCols + N')
            SELECT ' + @selectCols + N'
            FROM dbo.Permissions s
            WHERE s.' + QUOTENAME(@keyColumn) + N' = @oldName;
        END

        IF OBJECT_ID(N''dbo.RolePermissions'', N''U'') IS NOT NULL
        BEGIN
            INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
            SELECT rp.RoleId, newP.Id
            FROM dbo.RolePermissions rp
            INNER JOIN dbo.Permissions oldP ON oldP.Id = rp.PermissionId
            INNER JOIN dbo.Permissions newP ON newP.' + QUOTENAME(@keyColumn) + N' = @newName
            WHERE oldP.' + QUOTENAME(@keyColumn) + N' = @oldName
              AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.RolePermissions x
                    WHERE x.RoleId = rp.RoleId AND x.PermissionId = newP.Id
              );
        END';

    EXEC sp_executesql @sql,
        N'@oldName nvarchar(100), @newName nvarchar(100)',
        @oldName = @oldName,
        @newName = @newName;

    FETCH NEXT FROM paircur INTO @oldName, @newName;
END
CLOSE paircur;
DEALLOCATE paircur;

SET @sql = N'
    SELECT *
    FROM dbo.Permissions
    WHERE ' + QUOTENAME(@keyColumn) + N' IN (N''PartnerDispatch.View'', N''PartnerDispatch.Create'', N''PartnerDispatch.Edit'')';
EXEC sp_executesql @sql;
