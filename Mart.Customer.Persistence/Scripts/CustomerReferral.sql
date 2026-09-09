IF SCHEMA_ID(N'Referral') IS NULL
    EXEC(N'CREATE SCHEMA [Referral]');
GO

IF OBJECT_ID(N'Referral.CustomerReferral', N'U') IS NULL
BEGIN
    CREATE TABLE [Referral].[CustomerReferral]
    (
        [CustomerReferralId] bigint IDENTITY(1,1) NOT NULL,
        [ReferrerCustomerId] bigint NOT NULL,
        [ReferredMobileNumber] nvarchar(30) NOT NULL,
        [ReferralCode] varchar(32) NOT NULL,
        [Status] varchar(20) NOT NULL,
        [ReferredCustomerId] bigint NULL,
        [CreatedOn] datetime2 NOT NULL CONSTRAINT [DF_CustomerReferral_CreatedOn] DEFAULT sysutcdatetime(),
        [OnboardedOn] datetime2 NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_CustomerReferral_IsActive] DEFAULT 1,
        CONSTRAINT [PK_CustomerReferral] PRIMARY KEY ([CustomerReferralId]),
        CONSTRAINT [FK_CustomerReferral_ReferrerCustomer] FOREIGN KEY ([ReferrerCustomerId]) REFERENCES [customer].[CustomerMaster] ([CustomerId]),
        CONSTRAINT [FK_CustomerReferral_ReferredCustomer] FOREIGN KEY ([ReferredCustomerId]) REFERENCES [customer].[CustomerMaster] ([CustomerId]),
        CONSTRAINT [CK_CustomerReferral_Status] CHECK ([Status] IN ('WAITING', 'ACTIVE'))
    );

    CREATE UNIQUE INDEX [UQ_CustomerReferral_ReferralCode]
        ON [Referral].[CustomerReferral] ([ReferralCode]);
    CREATE UNIQUE INDEX [UQ_CustomerReferral_ActiveMobile]
        ON [Referral].[CustomerReferral] ([ReferrerCustomerId], [ReferredMobileNumber])
        WHERE [IsActive] = 1;
    CREATE INDEX [IX_CustomerReferral_ReferrerCreated]
        ON [Referral].[CustomerReferral] ([ReferrerCustomerId], [CreatedOn] DESC, [CustomerReferralId] DESC);
END;
GO
