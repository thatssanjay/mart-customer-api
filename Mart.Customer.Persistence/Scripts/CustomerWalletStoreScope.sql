-- Apply separately before deploying the Store-scoped wallet code. No EF migration is required.
-- Stop application writers during this change.
-- Existing MART_WALLET balances must be assigned to their authoritative Store first;
-- this script never guesses a Store or moves/duplicates a balance or audit record.
SET XACT_ABORT ON;

IF COL_LENGTH(N'Wallet.CustomerWallet', N'StoreId') IS NULL
    ALTER TABLE [Wallet].[CustomerWallet] ADD [StoreId] bigint NULL;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM [Wallet].[CustomerWallet] w
        JOIN [Wallet].[WalletType] t ON t.Id = w.WalletTypeId
        WHERE (UPPER(t.Code) = 'MART_WALLET' AND (w.StoreId IS NULL OR w.StoreId <= 0))
           OR (UPPER(t.Code) <> 'MART_WALLET' AND w.StoreId IS NOT NULL)
    )
        THROW 50001, 'Resolve legacy wallet StoreId assignments before continuing. MART_WALLET requires a Store; other wallet types require NULL.', 1;

    -- Support either a unique constraint or a unique index in existing installations.
    IF EXISTS (SELECT 1 FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'Wallet.CustomerWallet') AND name = N'UQ_CustomerWallet')
        ALTER TABLE [Wallet].[CustomerWallet] DROP CONSTRAINT [UQ_CustomerWallet];
    ELSE IF EXISTS (SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'Wallet.CustomerWallet') AND name = N'UQ_CustomerWallet')
        DROP INDEX [UQ_CustomerWallet] ON [Wallet].[CustomerWallet];

    -- Deliberately unfiltered: SQL Server also enforces uniqueness for NULL StoreId,
    -- keeping non-MART wallets unique per customer/type.
    CREATE UNIQUE INDEX [UQ_CustomerWallet]
        ON [Wallet].[CustomerWallet] ([CustomerId], [WalletTypeId], [StoreId]);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
