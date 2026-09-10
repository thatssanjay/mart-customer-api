IF SCHEMA_ID(N'customer') IS NULL
    EXEC(N'CREATE SCHEMA [customer]');
GO

IF OBJECT_ID(N'customer.InvoiceSerialCounter', N'U') IS NULL
BEGIN
    CREATE TABLE [customer].[InvoiceSerialCounter]
    (
        [CounterName] varchar(20) NOT NULL,
        [CurrentSerial] bigint NOT NULL,
        CONSTRAINT [PK_InvoiceSerialCounter] PRIMARY KEY ([CounterName]),
        CONSTRAINT [CK_InvoiceSerialCounter_CurrentSerial]
            CHECK ([CurrentSerial] >= 0 AND [CurrentSerial] <= 99999999)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM [customer].[InvoiceSerialCounter]
    WHERE [CounterName] = 'Invoice'
)
BEGIN
    DECLARE @InitialInvoiceSerial bigint =
    (
        SELECT ISNULL(MAX(TRY_CONVERT(bigint, [InvoiceNumber])), 0)
        FROM [customer].[CustomerOrder]
        WHERE LEN([InvoiceNumber]) = 8
          AND [InvoiceNumber] NOT LIKE '%[^0-9]%'
    );

    INSERT INTO [customer].[InvoiceSerialCounter] ([CounterName], [CurrentSerial])
    VALUES ('Invoice', @InitialInvoiceSerial);
END;
GO
