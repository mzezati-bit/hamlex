USE [hamlex];
GO

IF OBJECT_ID(N'dbo.PartnerDispatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartnerDispatches
    (
        Id INT IDENTITY(1,1) NOT NULL,
        WaybillId INT NOT NULL,
        FreightCompanyName NVARCHAR(200) NULL,
        PartnerReceiptNumber NVARCHAR(100) NULL,
        PartnerReceiptDate NVARCHAR(20) NULL,
        TehranOperatorName NVARCHAR(200) NULL,
        UnloadName NVARCHAR(200) NULL,
        UnloadMobile NVARCHAR(50) NULL,
        PartnerFreight DECIMAL(18, 0) NOT NULL
            CONSTRAINT DF_PartnerDispatches_PartnerFreight DEFAULT (0),
        SettlementType NVARCHAR(50) NULL,
        SettlementStatus NVARCHAR(20) NOT NULL
            CONSTRAINT DF_PartnerDispatches_SettlementStatus DEFAULT (N'باز'),
        DeliveryStatus NVARCHAR(20) NOT NULL
            CONSTRAINT DF_PartnerDispatches_DeliveryStatus DEFAULT (N'تحویل نشده'),
        CreatedAt NVARCHAR(50) NULL,
        CONSTRAINT PK_PartnerDispatches PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_PartnerDispatches_WaybillId UNIQUE (WaybillId)
    );
END
GO

IF OBJECT_ID(N'dbo.Waybills', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_PartnerDispatches_Waybills'
    )
BEGIN
    ALTER TABLE dbo.PartnerDispatches
    ADD CONSTRAINT FK_PartnerDispatches_Waybills
        FOREIGN KEY (WaybillId) REFERENCES dbo.Waybills (Id);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PartnerDispatches_TehranOperator'
      AND object_id = OBJECT_ID(N'dbo.PartnerDispatches')
)
    CREATE NONCLUSTERED INDEX IX_PartnerDispatches_TehranOperator
    ON dbo.PartnerDispatches (TehranOperatorName);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PartnerDispatches_Settlement'
      AND object_id = OBJECT_ID(N'dbo.PartnerDispatches')
)
    CREATE NONCLUSTERED INDEX IX_PartnerDispatches_Settlement
    ON dbo.PartnerDispatches (SettlementType, SettlementStatus);
GO
