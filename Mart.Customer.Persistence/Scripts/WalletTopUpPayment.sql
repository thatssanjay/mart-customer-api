SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'Wallet.WalletTopUpPayment', N'U') IS NULL
BEGIN
    CREATE TABLE [Wallet].[WalletTopUpPayment]
    (
        [WalletTopUpPaymentId] bigint IDENTITY(1,1) NOT NULL,
        [CustomerId] bigint NOT NULL,
        [CustomerWalletId] bigint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PreviousBalance] decimal(18,2) NOT NULL,
        [NewBalance] decimal(18,2) NULL,
        [PaymentMode] varchar(20) NOT NULL,
        [CardLast4] varchar(4) NULL,
        [ReferenceNumber] varchar(100) NULL,
        [PaymentReference] varchar(100) NOT NULL,
        [Status] varchar(20) NOT NULL,
        [WalletTransactionId] bigint NULL,
        [WalletTransactionNumber] varchar(50) NULL,
        [CreatedOn] datetime2 NOT NULL CONSTRAINT [DF_WalletTopUpPayment_CreatedOn] DEFAULT sysutcdatetime(),
        [PaidOn] datetime2 NULL,
        CONSTRAINT [PK_WalletTopUpPayment] PRIMARY KEY ([WalletTopUpPaymentId]),
        CONSTRAINT [FK_WalletTopUpPayment_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[CustomerMaster] ([CustomerId]),
        CONSTRAINT [FK_WalletTopUpPayment_CustomerWallet] FOREIGN KEY ([CustomerWalletId]) REFERENCES [Wallet].[CustomerWallet] ([CustomerWalletId]),
        CONSTRAINT [FK_WalletTopUpPayment_WalletTransaction] FOREIGN KEY ([WalletTransactionId]) REFERENCES [Wallet].[WalletTransaction] ([WalletTransactionId]),
        CONSTRAINT [CK_WalletTopUpPayment_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_WalletTopUpPayment_Details] CHECK
        (
            ([PaymentMode] = 'CARD' AND [CardLast4] IS NOT NULL AND [ReferenceNumber] IS NULL) OR
            ([PaymentMode] IN ('NETBANKING', 'UPI') AND [CardLast4] IS NULL AND [ReferenceNumber] IS NOT NULL)
        )
    );

    CREATE UNIQUE INDEX [UQ_WalletTopUpPayment_PaymentReference]
        ON [Wallet].[WalletTopUpPayment] ([PaymentReference]);
    CREATE UNIQUE INDEX [UQ_WalletTopUpPayment_ExternalReference]
        ON [Wallet].[WalletTopUpPayment] ([PaymentMode], [ReferenceNumber])
        WHERE [ReferenceNumber] IS NOT NULL;
    CREATE UNIQUE INDEX [UQ_WalletTopUpPayment_WalletTransactionId]
        ON [Wallet].[WalletTopUpPayment] ([WalletTransactionId])
        WHERE [WalletTransactionId] IS NOT NULL;
END;

COMMIT TRANSACTION;
