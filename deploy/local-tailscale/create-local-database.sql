:setvar AppPassword "SUBSTITUIR_POR_PASSWORD_FORTE"

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'casamento_app')
    CREATE LOGIN [casamento_app] WITH PASSWORD = '$(AppPassword)', CHECK_POLICY = ON;
GO

IF DB_ID(N'CasamentoTatianaDiogo') IS NULL
    CREATE DATABASE [CasamentoTatianaDiogo];
GO

USE [CasamentoTatianaDiogo];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'casamento_app')
    CREATE USER [casamento_app] FOR LOGIN [casamento_app];
GO

ALTER ROLE [db_owner] ADD MEMBER [casamento_app];
GO
