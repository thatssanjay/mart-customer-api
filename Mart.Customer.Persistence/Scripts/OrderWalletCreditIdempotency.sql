-- Manual deployment only. Apply the existing Wallet Engine foundation schema first.
-- No financial history is deleted or rewritten by this script.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'Wallet.WalletOperation', N'U') IS NULL
    THROW 50001, 'Deploy the WalletOperation/WalletOperationComponent foundation schema before this index.', 1;
IF EXISTS (SELECT 1 FROM Wallet.WalletOperation WHERE CustomerOrderId IS NOT NULL GROUP BY OperationKind, CustomerOrderId HAVING COUNT(*) > 1)
    THROW 50002, 'Duplicate order operations require reconciliation before deployment.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Wallet.WalletOperation') AND name = N'UQ_WalletOperation_Order')
    CREATE UNIQUE INDEX UQ_WalletOperation_Order ON Wallet.WalletOperation(OperationKind, CustomerOrderId) WHERE CustomerOrderId IS NOT NULL;
COMMIT TRANSACTION;
