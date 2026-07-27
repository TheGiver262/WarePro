IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AuditLog', N'PerformedBy') IS NOT NULL
BEGIN
    DECLARE @AuditPerformerForeignKey sysname;
    DECLARE @AuditPerformerDeleteAction nvarchar(60);

    SELECT TOP (1)
        @AuditPerformerForeignKey = foreignKey.name,
        @AuditPerformerDeleteAction = foreignKey.delete_referential_action_desc
    FROM sys.foreign_keys AS foreignKey
    INNER JOIN sys.foreign_key_columns AS foreignKeyColumn
        ON foreignKeyColumn.constraint_object_id = foreignKey.object_id
    INNER JOIN sys.columns AS parentColumn
        ON parentColumn.object_id = foreignKeyColumn.parent_object_id
       AND parentColumn.column_id = foreignKeyColumn.parent_column_id
    WHERE foreignKey.parent_object_id = OBJECT_ID(N'dbo.AuditLog')
      AND parentColumn.name = N'PerformedBy';

    IF EXISTS
       (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.AuditLog')
             AND name = N'PerformedBy'
             AND is_nullable = 0
       )
       OR @AuditPerformerForeignKey IS NULL
       OR @AuditPerformerDeleteAction <> N'SET_NULL'
    BEGIN
        IF @AuditPerformerForeignKey IS NOT NULL
        BEGIN
            DECLARE @DropAuditPerformerForeignKey nvarchar(max) =
                N'ALTER TABLE dbo.AuditLog DROP CONSTRAINT ' + QUOTENAME(@AuditPerformerForeignKey) + N';';
            EXEC sys.sp_executesql @DropAuditPerformerForeignKey;
        END;

        ALTER TABLE dbo.AuditLog ALTER COLUMN PerformedBy INT NULL;
        ALTER TABLE dbo.AuditLog WITH CHECK ADD CONSTRAINT FK_AuditLog_PerformedBy
            FOREIGN KEY (PerformedBy) REFERENCES dbo.AppUser(Id) ON DELETE SET NULL;
    END;
END;
