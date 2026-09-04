-- Run manually in the API's configured database before using order wallet credit.
-- Creates missing engine tables only; existing financial rows are never rewritten.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'Wallet.CustomerWallet', N'U') IS NULL
        OR OBJECT_ID(N'Wallet.WalletTransaction', N'U') IS NULL
        OR OBJECT_ID(N'Wallet.WalletBalanceBucket', N'U') IS NULL
        THROW 50001, 'Existing CustomerWallet, WalletTransaction and WalletBalanceBucket tables are required.', 1;
    IF COL_LENGTH(N'Wallet.CustomerWallet', N'StoreId') IS NULL
        THROW 50002, 'Apply CustomerWalletStoreScope.sql before the wallet engine foundation.', 1;

    IF OBJECT_ID(N'Wallet.WalletOperation', N'U') IS NULL
    BEGIN
        CREATE TABLE [Wallet].[WalletOperation] (
            [WalletOperationId] bigint IDENTITY(1,1) NOT NULL,
            [OperationNumber] varchar(50) NOT NULL,
            [OperationKind] varchar(30) NOT NULL,
            [BusinessKey] varchar(150) NOT NULL,
            [CustomerId] bigint NOT NULL,
            [StoreId] bigint NULL,
            [CustomerOrderId] bigint NULL,
            [CustomerOrderPaymentId] bigint NULL,
            [EffectiveAt] datetime2 NOT NULL,
            [Status] varchar(20) NOT NULL,
            [Outcome] varchar(20) NULL,
            [CalculationVersion] nvarchar(100) NOT NULL,
            [CreatedOn] datetime2 NOT NULL,
            [CreatedBy] nvarchar(100) NOT NULL,
            [CompletedOn] datetime2 NULL,
            CONSTRAINT [PK_WalletOperation] PRIMARY KEY ([WalletOperationId]),
            CONSTRAINT [CK_WalletOperation_Status] CHECK (
                ([Status] = 'PROCESSING' AND [Outcome] IS NULL AND [CompletedOn] IS NULL)
                OR ([Status] = 'COMPLETED' AND [Outcome] IN ('CREDITED', 'NO_REWARD') AND [CompletedOn] IS NOT NULL))
        );
    END;

    IF OBJECT_ID(N'Wallet.WalletOperationComponent', N'U') IS NULL
    BEGIN
        CREATE TABLE [Wallet].[WalletOperationComponent] (
            [WalletOperationComponentId] bigint IDENTITY(1,1) NOT NULL,
            [WalletOperationId] bigint NOT NULL,
            [CustomerWalletId] bigint NOT NULL,
            [WalletTypeId] int NOT NULL,
            [StoreId] bigint NULL,
            [ComponentCode] varchar(30) NOT NULL,
            [AllocationKey] varchar(100) NOT NULL,
            [WalletTransactionId] bigint NOT NULL,
            [ExpiryDate] datetime2 NULL,
            [CalculationSnapshotJson] nvarchar(max) NOT NULL,
            CONSTRAINT [PK_WalletOperationComponent] PRIMARY KEY ([WalletOperationComponentId]),
            CONSTRAINT [FK_WalletOperationComponent_WalletOperation_WalletOperationId]
                FOREIGN KEY ([WalletOperationId]) REFERENCES [Wallet].[WalletOperation] ([WalletOperationId]),
            CONSTRAINT [FK_WalletOperationComponent_CustomerWallet_CustomerWalletId]
                FOREIGN KEY ([CustomerWalletId]) REFERENCES [Wallet].[CustomerWallet] ([CustomerWalletId]),
            CONSTRAINT [FK_WalletOperationComponent_WalletTransaction_WalletTransactionId]
                FOREIGN KEY ([WalletTransactionId]) REFERENCES [Wallet].[WalletTransaction] ([WalletTransactionId])
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperation') AND name = N'UQ_WalletOperation_Number')
        CREATE UNIQUE INDEX [UQ_WalletOperation_Number] ON [Wallet].[WalletOperation] ([OperationNumber]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperation') AND name = N'UQ_WalletOperation_Identity')
        CREATE UNIQUE INDEX [UQ_WalletOperation_Identity] ON [Wallet].[WalletOperation] ([OperationKind], [BusinessKey]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperation') AND name = N'UQ_WalletOperation_Order')
        CREATE UNIQUE INDEX [UQ_WalletOperation_Order] ON [Wallet].[WalletOperation] ([OperationKind], [CustomerOrderId]) WHERE [CustomerOrderId] IS NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperation') AND name = N'IX_WalletOperation_CustomerOrderId')
        CREATE INDEX [IX_WalletOperation_CustomerOrderId] ON [Wallet].[WalletOperation] ([CustomerOrderId]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperation') AND name = N'IX_WalletOperation_CustomerOrderPaymentId')
        CREATE INDEX [IX_WalletOperation_CustomerOrderPaymentId] ON [Wallet].[WalletOperation] ([CustomerOrderPaymentId]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperationComponent') AND name = N'UQ_WalletOperationComponent_Identity')
        CREATE UNIQUE INDEX [UQ_WalletOperationComponent_Identity] ON [Wallet].[WalletOperationComponent] ([WalletOperationId], [CustomerWalletId], [ComponentCode], [AllocationKey]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperationComponent') AND name = N'IX_WalletOperationComponent_WalletTransactionId')
        CREATE UNIQUE INDEX [IX_WalletOperationComponent_WalletTransactionId] ON [Wallet].[WalletOperationComponent] ([WalletTransactionId]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperationComponent') AND name = N'IX_WalletOperationComponent_CustomerWalletId')
        CREATE INDEX [IX_WalletOperationComponent_CustomerWalletId] ON [Wallet].[WalletOperationComponent] ([CustomerWalletId]);

    -- Unique indexes fail and roll back the script if historical duplicates exist.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletBalanceBucket') AND name = N'IX_WalletBalanceBucket_SourceTransactionId' AND is_unique = 1)
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletBalanceBucket') AND name = N'IX_WalletBalanceBucket_SourceTransactionId')
            DROP INDEX [IX_WalletBalanceBucket_SourceTransactionId] ON [Wallet].[WalletBalanceBucket];
        CREATE UNIQUE INDEX [IX_WalletBalanceBucket_SourceTransactionId] ON [Wallet].[WalletBalanceBucket] ([SourceTransactionId]);
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletBalanceBucket') AND name = N'IX_WalletBalanceBucket_CustomerWalletId_ExpiryDate')
        CREATE INDEX [IX_WalletBalanceBucket_CustomerWalletId_ExpiryDate] ON [Wallet].[WalletBalanceBucket] ([CustomerWalletId], [ExpiryDate]);
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'Wallet.WalletBalanceBucket') AND name = N'CK_WalletBalanceBucket_OriginalAmount')
        ALTER TABLE [Wallet].[WalletBalanceBucket] WITH CHECK ADD CONSTRAINT [CK_WalletBalanceBucket_OriginalAmount] CHECK ([OriginalAmount] > 0);
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'Wallet.WalletBalanceBucket') AND name = N'CK_WalletBalanceBucket_AvailableAmount')
        ALTER TABLE [Wallet].[WalletBalanceBucket] WITH CHECK ADD CONSTRAINT [CK_WalletBalanceBucket_AvailableAmount] CHECK ([AvailableAmount] >= 0 AND [AvailableAmount] <= [OriginalAmount]);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
