BEGIN TRANSACTION;
GO

ALTER TABLE [user_profiles] ADD [DoctorAwareOfPlan] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [user_profiles] ADD [HealthScreeningAt] datetime2 NULL;
GO

ALTER TABLE [user_profiles] ADD [MedicalConditions] int NOT NULL DEFAULT 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261117025750_AddStructuredHealthScreening', N'8.0.33');
GO

COMMIT;
GO

