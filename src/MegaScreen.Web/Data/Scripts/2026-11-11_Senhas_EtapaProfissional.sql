/* =====================================================================
   MegaScreen - Senhas: etapa Recepção -> Profissional
   Data: 11/11/2026  |  SQL Server 2016+  |  Idempotente (pode rodar 2x)

   Etapa              1 = Recepção (padrão) | 2 = Profissional (liberada pela recepção)
   SalaDestinoId      Consultório escolhido pela recepção (NULL = fila geral)
   EncaminhadaEm      Quando a recepção liberou para o profissional (hora SP, vinda da aplicação)
   ChamadaRecepcaoEm  Quando a recepção chamou (preserva a espera média da recepção,
                      pois ChamadaEm é sobrescrita na chamada do profissional)

   Datas: gravadas pela aplicação com DataHora.Agora() (America/Sao_Paulo).
   Nenhum DEFAULT com GETDATE().
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.Senhas', 'Etapa') IS NULL
    ALTER TABLE dbo.Senhas ADD Etapa int NOT NULL
        CONSTRAINT DF_Senhas_Etapa DEFAULT (1) WITH VALUES;
GO

IF COL_LENGTH('dbo.Senhas', 'SalaDestinoId') IS NULL
    ALTER TABLE dbo.Senhas ADD SalaDestinoId int NULL;
GO

IF COL_LENGTH('dbo.Senhas', 'EncaminhadaEm') IS NULL
    ALTER TABLE dbo.Senhas ADD EncaminhadaEm datetime2(7) NULL;
GO

IF COL_LENGTH('dbo.Senhas', 'ChamadaRecepcaoEm') IS NULL
    ALTER TABLE dbo.Senhas ADD ChamadaRecepcaoEm datetime2(7) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Senhas_Etapa')
    ALTER TABLE dbo.Senhas WITH CHECK ADD CONSTRAINT CK_Senhas_Etapa CHECK (Etapa IN (1, 2));
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Senhas_SalaDestino')
BEGIN
    ALTER TABLE dbo.Senhas WITH CHECK ADD CONSTRAINT FK_Senhas_SalaDestino
        FOREIGN KEY (SalaDestinoId) REFERENCES dbo.Salas (Id);
    ALTER TABLE dbo.Senhas CHECK CONSTRAINT FK_Senhas_SalaDestino;
END;
GO

-- Fila do profissional: filial + status + etapa do dia.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Senhas_Filial_Status_Etapa'
                                           AND object_id = OBJECT_ID('dbo.Senhas'))
    CREATE NONCLUSTERED INDEX IX_Senhas_Filial_Status_Etapa
        ON dbo.Senhas (FilialId, Status, Etapa, CriadaEm)
        INCLUDE (SalaDestinoId, EncaminhadaEm);
GO

PRINT 'Senhas: etapa Recepção/Profissional aplicada.';
GO
