BEGIN TRANSACTION;
GO

ALTER TABLE [Suppliers] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [Suppliers] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [Suppliers] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

UPDATE Staff SET Status = 'Active' WHERE Status = 'Approved';
GO

UPDATE Staff SET Status = 'Inactive' WHERE Status IN ('Pending', 'Rejected') OR Status IS NULL OR LTRIM(RTRIM(Status)) = '';
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Staff]') AND [c].[name] = N'Status');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Staff] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Staff] ADD DEFAULT N'Active' FOR [Status];
GO

ALTER TABLE [Services] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [Services] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [Services] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [PurchaseOrders] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [PurchaseOrders] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [PurchaseOrders] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [PurchaseOrderItems] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [PurchaseOrderItems] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [PurchaseOrderItems] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Products] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [Products] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [Products] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Mechanics] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [Mechanics] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [Mechanics] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Deliveries] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [Deliveries] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [Deliveries] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Categories] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [Categories] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [Categories] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Brands] ADD [DeletedAt] datetime2 NULL;
GO

ALTER TABLE [Brands] ADD [DeletedBy] int NULL;
GO

ALTER TABLE [Brands] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260926220506_AddSoftDeleteAndActiveStaff', N'8.0.8');
GO

COMMIT;
GO

