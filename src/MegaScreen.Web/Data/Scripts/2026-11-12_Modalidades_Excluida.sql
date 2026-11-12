/* =====================================================================
   MegaScreen - Modalidades: exclusão lógica
   Data: 12/11/2026  |  SQL Server 2016+  |  Idempotente (pode rodar 2x)

   Excluida  0 = normal | 1 = excluída pelo usuário (tinha senhas emitidas,
             por isso não pode ser apagada fisicamente). Some do grid e do totem,
             mas continua disponível para o histórico/relatórios de senhas.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.Modalidades', 'Excluida') IS NULL
    ALTER TABLE dbo.Modalidades ADD Excluida bit NOT NULL
        CONSTRAINT DF_Modalidades_Excluida DEFAULT (0) WITH VALUES;
GO

PRINT 'Modalidades: coluna Excluida aplicada.';
GO
