BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107105859_AddAdaptiveCaloriePlan'
)
BEGIN
    ALTER TABLE [goals] ADD [CaloriePlanJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107105859_AddAdaptiveCaloriePlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107105859_AddAdaptiveCaloriePlan', N'8.0.33');
END;
GO

COMMIT;
GO

