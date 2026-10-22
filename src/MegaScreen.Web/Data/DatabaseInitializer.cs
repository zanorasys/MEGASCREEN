using Dapper;
using Microsoft.Data.SqlClient;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Data;

public sealed class DatabaseInitializer(IConfiguration configuration, IDbConnectionFactory connectionFactory)
{
    public async Task InitializeAsync()
    {
        await EnsureDatabaseAsync();
        using var db = connectionFactory.CreateConnection();
        await db.ExecuteAsync(SchemaSql);

        if (await db.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Clientes;") > 0)
            return;

        db.Open();
        using var tx = db.BeginTransaction();

        var clienteId = await db.ExecuteScalarAsync<int>("""
            INSERT INTO Clientes (Nome, Tipo, Documento, Ativo)
            VALUES (@Nome, @Tipo, @Documento, 1);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new { Nome = "Zanora Soluções LTDA", Tipo = (int)TipoCliente.PJ, Documento = "39.889.901/0001-11" }, tx);

        var centroId = await db.ExecuteScalarAsync<int>("""
            INSERT INTO Filiais (ClienteId, Nome, Cidade, Uf, ImpressoraHabilitada, Ativa)
            VALUES (@ClienteId, 'Unidade Centro', 'Curitiba', 'PR', 1, 1);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new { ClienteId = clienteId }, tx);

        await db.ExecuteAsync("""
            INSERT INTO Filiais (ClienteId, Nome, Cidade, Uf, ImpressoraHabilitada, Ativa)
            VALUES (@ClienteId, 'Unidade Norte', 'Curitiba', 'PR', 0, 1),
                   (@ClienteId, 'Unidade Sul', 'Curitiba', 'PR', 0, 1);
            """, new { ClienteId = clienteId }, tx);

        const string insertUsuario = """
            INSERT INTO Usuarios (FilialId, Nome, Email, SenhaHash, Perfil, LocalAtual, Ativo)
            VALUES (@FilialId, @Nome, @Email, @SenhaHash, @Perfil, @LocalAtual, @Ativo);
            """;
        await db.ExecuteAsync(insertUsuario, new[]
        {
            new { FilialId = centroId, Nome = "Humberto Garotti", Email = "humberto@pontobrweb.com.br", SenhaHash = PasswordHasher.Hash("12345678"), Perfil = (int)PerfilUsuario.Admin, LocalAtual = "Recepção I", Ativo = true },
            new { FilialId = centroId, Nome = "Ana Souza", Email = "ana@empresa.com.br", SenhaHash = PasswordHasher.Hash("12345678"), Perfil = (int)PerfilUsuario.Atendente, LocalAtual = "Guichê 2", Ativo = true },
            new { FilialId = centroId, Nome = "Dr. Carlos Lima", Email = "carlos@empresa.com.br", SenhaHash = PasswordHasher.Hash("12345678"), Perfil = (int)PerfilUsuario.Profissional, LocalAtual = "Consultório 1", Ativo = true }
        }, tx);

        var recepcaoId = await db.ExecuteScalarAsync<int>("""
            INSERT INTO SalasEspera (FilialId, Nome, Apelido)
            VALUES (@FilialId, 'Recepção I', 'REC-1');
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new { FilialId = centroId }, tx);
        var labEsperaId = await db.ExecuteScalarAsync<int>("""
            INSERT INTO SalasEspera (FilialId, Nome, Apelido)
            VALUES (@FilialId, 'Laboratório', 'LAB');
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new { FilialId = centroId }, tx);
        var imgEsperaId = await db.ExecuteScalarAsync<int>("""
            INSERT INTO SalasEspera (FilialId, Nome, Apelido)
            VALUES (@FilialId, 'Imagem', 'IMG');
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """, new { FilialId = centroId }, tx);

        for (var i = 1; i <= 5; i++)
            await db.ExecuteAsync("""
                INSERT INTO PontosAtendimento (SalaEsperaId, Nome, Ativo)
                VALUES (@SalaEsperaId, @Nome, @Ativo);
                """, new { SalaEsperaId = recepcaoId, Nome = $"Guichê {i}", Ativo = i <= 4 }, tx);

        await db.ExecuteAsync("""
            INSERT INTO PontosAtendimento (SalaEsperaId, Nome, Ativo)
            VALUES (@Lab, 'Coleta 1', 1), (@Lab, 'Coleta 2', 1), (@Lab, 'Coleta 3', 1),
                   (@Img, 'Imagem 1', 1), (@Img, 'Imagem 2', 1);
            """, new { Lab = labEsperaId, Img = imgEsperaId }, tx);

        const string insertTela = """
            INSERT INTO Telas (FilialId, Nome, Apelido, Tipo, Predio, Andar, Ramal, Cor, Visivel, SalaEsperaId)
            VALUES (@FilialId, @Nome, @Apelido, @Tipo, @Predio, @Andar, @Ramal, @Cor, @Visivel, @SalaEsperaId);
            """;
        await db.ExecuteAsync(insertTela, new[]
        {
            new { FilialId = centroId, Nome = "Recepção Térreo", Apelido = "REC-T", Tipo = (int)TipoTela.TvHorizontal, Predio = "A", Andar = "Térreo", Ramal = "201", Cor = "#4C5BF5", Visivel = true, SalaEsperaId = (int?)recepcaoId },
            new { FilialId = centroId, Nome = "Totem Entrada", Apelido = "TOT-1", Tipo = (int)TipoTela.TabletVertical, Predio = "A", Andar = "Térreo", Ramal = "202", Cor = "#12A150", Visivel = true, SalaEsperaId = (int?)recepcaoId },
            new { FilialId = centroId, Nome = "Laboratório 1º andar", Apelido = "LAB-1", Tipo = (int)TipoTela.TvHorizontal, Predio = "B", Andar = "1º andar", Ramal = "310", Cor = "#F5892E", Visivel = false, SalaEsperaId = (int?)labEsperaId }
        }, tx);

        const string insertSala = """
            INSERT INTO Salas (FilialId, Nome, Apelido, Predio, Andar, Ramal, Cor)
            VALUES (@FilialId, @Nome, @Apelido, @Predio, @Andar, @Ramal, @Cor);
            """;
        await db.ExecuteAsync(insertSala, new[]
        {
            new { FilialId = centroId, Nome = "Consultório 1", Apelido = "C1", Predio = "A", Andar = "1º", Ramal = "2011", Cor = "#4C5BF5" },
            new { FilialId = centroId, Nome = "Coleta A", Apelido = "COL-A", Predio = "A", Andar = "Térreo", Ramal = "2015", Cor = "#12A150" },
            new { FilialId = centroId, Nome = "Sala de Imagem", Apelido = "IMG-1", Predio = "B", Andar = "1º", Ramal = "3101", Cor = "#F5892E" }
        }, tx);

        const string insertModalidade = """
            INSERT INTO Modalidades (FilialId, Nome, Prefixo, PrioridadeInterna, Ativa, Contador)
            VALUES (@FilialId, @Nome, @Prefixo, @PrioridadeInterna, @Ativa, 0);
            """;
        await db.ExecuteAsync(insertModalidade, new[]
        {
            new { FilialId = centroId, Nome = "Agendamento de Consultas", Prefixo = "A", PrioridadeInterna = (int)Prioridade.Normal, Ativa = true },
            new { FilialId = centroId, Nome = "Retirada de Exames", Prefixo = "P", PrioridadeInterna = (int)Prioridade.Alta, Ativa = true },
            new { FilialId = centroId, Nome = "Informações", Prefixo = "I", PrioridadeInterna = (int)Prioridade.Normal, Ativa = false }
        }, tx);

        await db.ExecuteAsync("""
            INSERT INTO Personalizacoes
                (FilialId, CorFundo, CorTexto, CorDestaque, Fonte, MostrarHorario, MostrarNumero, MostrarOrdem, MostrarSala, Marca)
            VALUES (@FilialId, '#0B0D24', '#FFFFFF', '#4C5BF5', 'Manrope', 1, 1, 1, 1, 'Sua Marca');
            """, new { FilialId = centroId }, tx);

        tx.Commit();
    }

    private async Task EnsureDatabaseAsync()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
        var cs = new SqlConnectionStringBuilder(connectionString);
        var databaseName = cs.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName)) return;

        cs.InitialCatalog = "master";
        await using var master = new SqlConnection(cs.ConnectionString);
        await master.OpenAsync();
        var safeName = databaseName.Replace("]", "]]");
        var exists = await master.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sys.databases WHERE name = @databaseName;", new { databaseName });
        if (exists == 0) await master.ExecuteAsync($"CREATE DATABASE [{safeName}];");
    }

    private const string SchemaSql = """
        IF OBJECT_ID('Clientes', 'U') IS NULL
        BEGIN
            CREATE TABLE Clientes (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clientes PRIMARY KEY,
                Nome nvarchar(150) NOT NULL,
                Tipo int NOT NULL,
                Documento nvarchar(20) NULL,
                Ativo bit NOT NULL CONSTRAINT DF_Clientes_Ativo DEFAULT(1)
            );
        END;

        IF OBJECT_ID('Filiais', 'U') IS NULL
        BEGIN
            CREATE TABLE Filiais (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Filiais PRIMARY KEY,
                ClienteId int NOT NULL,
                Nome nvarchar(120) NOT NULL,
                Cidade nvarchar(80) NULL,
                Uf nvarchar(2) NULL,
                ImpressoraHabilitada bit NOT NULL CONSTRAINT DF_Filiais_Impressora DEFAULT(0),
                Ativa bit NOT NULL CONSTRAINT DF_Filiais_Ativa DEFAULT(1),
                CONSTRAINT FK_Filiais_Clientes FOREIGN KEY (ClienteId) REFERENCES Clientes(Id)
            );
        END;

        IF OBJECT_ID('Usuarios', 'U') IS NULL
        BEGIN
            CREATE TABLE Usuarios (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
                FilialId int NOT NULL,
                Nome nvarchar(120) NOT NULL,
                Email nvarchar(160) NOT NULL,
                SenhaHash nvarchar(max) NOT NULL,
                Perfil int NOT NULL,
                LocalAtual nvarchar(80) NULL,
                Ativo bit NOT NULL CONSTRAINT DF_Usuarios_Ativo DEFAULT(1),
                CONSTRAINT FK_Usuarios_Filiais FOREIGN KEY (FilialId) REFERENCES Filiais(Id)
            );
            CREATE UNIQUE INDEX IX_Usuarios_Email ON Usuarios(Email);
        END;

        IF OBJECT_ID('SalasEspera', 'U') IS NULL
        BEGIN
            CREATE TABLE SalasEspera (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalasEspera PRIMARY KEY,
                FilialId int NOT NULL,
                Nome nvarchar(120) NOT NULL,
                Apelido nvarchar(40) NULL,
                CONSTRAINT FK_SalasEspera_Filiais FOREIGN KEY (FilialId) REFERENCES Filiais(Id)
            );
        END;

        IF OBJECT_ID('PontosAtendimento', 'U') IS NULL
        BEGIN
            CREATE TABLE PontosAtendimento (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PontosAtendimento PRIMARY KEY,
                SalaEsperaId int NOT NULL,
                Nome nvarchar(80) NOT NULL,
                Ativo bit NOT NULL CONSTRAINT DF_PontosAtendimento_Ativo DEFAULT(1),
                CONSTRAINT FK_PontosAtendimento_SalasEspera FOREIGN KEY (SalaEsperaId) REFERENCES SalasEspera(Id) ON DELETE CASCADE
            );
        END;

        IF OBJECT_ID('Telas', 'U') IS NULL
        BEGIN
            CREATE TABLE Telas (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Telas PRIMARY KEY,
                FilialId int NOT NULL,
                Nome nvarchar(120) NOT NULL,
                Apelido nvarchar(40) NULL,
                Tipo int NOT NULL,
                Predio nvarchar(60) NULL,
                Andar nvarchar(60) NULL,
                Ramal nvarchar(20) NULL,
                Cor nvarchar(9) NULL,
                Visivel bit NOT NULL CONSTRAINT DF_Telas_Visivel DEFAULT(1),
                SalaEsperaId int NULL,
                CONSTRAINT FK_Telas_Filiais FOREIGN KEY (FilialId) REFERENCES Filiais(Id),
                CONSTRAINT FK_Telas_SalasEspera FOREIGN KEY (SalaEsperaId) REFERENCES SalasEspera(Id) ON DELETE SET NULL
            );
        END;

        IF OBJECT_ID('Salas', 'U') IS NULL
        BEGIN
            CREATE TABLE Salas (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Salas PRIMARY KEY,
                FilialId int NOT NULL,
                Nome nvarchar(120) NOT NULL,
                Apelido nvarchar(40) NULL,
                Predio nvarchar(60) NULL,
                Andar nvarchar(60) NULL,
                Ramal nvarchar(20) NULL,
                Cor nvarchar(9) NULL,
                CONSTRAINT FK_Salas_Filiais FOREIGN KEY (FilialId) REFERENCES Filiais(Id)
            );
        END;

        IF OBJECT_ID('Modalidades', 'U') IS NULL
        BEGIN
            CREATE TABLE Modalidades (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Modalidades PRIMARY KEY,
                FilialId int NOT NULL,
                Nome nvarchar(120) NOT NULL,
                Prefixo nvarchar(3) NOT NULL,
                PrioridadeInterna int NOT NULL,
                Ativa bit NOT NULL CONSTRAINT DF_Modalidades_Ativa DEFAULT(1),
                Contador int NOT NULL CONSTRAINT DF_Modalidades_Contador DEFAULT(0),
                CONSTRAINT FK_Modalidades_Filiais FOREIGN KEY (FilialId) REFERENCES Filiais(Id)
            );
        END;

        IF OBJECT_ID('Senhas', 'U') IS NULL
        BEGIN
            CREATE TABLE Senhas (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Senhas PRIMARY KEY,
                FilialId int NOT NULL,
                ModalidadeId int NOT NULL,
                Codigo nvarchar(10) NOT NULL,
                Nome nvarchar(120) NULL,
                Prioridade int NOT NULL,
                Status int NOT NULL,
                LocalAtual nvarchar(80) NULL,
                PontoAtendimento nvarchar(80) NULL,
                SalaId int NULL,
                CriadaEm datetime2 NOT NULL,
                ChamadaEm datetime2 NULL,
                ConcluidaEm datetime2 NULL,
                CONSTRAINT FK_Senhas_Filiais FOREIGN KEY (FilialId) REFERENCES Filiais(Id),
                CONSTRAINT FK_Senhas_Modalidades FOREIGN KEY (ModalidadeId) REFERENCES Modalidades(Id),
                CONSTRAINT FK_Senhas_Salas FOREIGN KEY (SalaId) REFERENCES Salas(Id)
            );
            CREATE INDEX IX_Senhas_Filial_Status_CriadaEm ON Senhas(FilialId, Status, CriadaEm);
        END;

        IF OBJECT_ID('Personalizacoes', 'U') IS NULL
        BEGIN
            CREATE TABLE Personalizacoes (
                FilialId int NOT NULL CONSTRAINT PK_Personalizacoes PRIMARY KEY,
                CorFundo nvarchar(9) NOT NULL CONSTRAINT DF_Pers_Fundo DEFAULT('#0B0D24'),
                CorTexto nvarchar(9) NOT NULL CONSTRAINT DF_Pers_Texto DEFAULT('#FFFFFF'),
                CorDestaque nvarchar(9) NOT NULL CONSTRAINT DF_Pers_Destaque DEFAULT('#4C5BF5'),
                Fonte nvarchar(100) NOT NULL CONSTRAINT DF_Pers_Fonte DEFAULT('Manrope'),
                MostrarHorario bit NOT NULL CONSTRAINT DF_Pers_Hora DEFAULT(1),
                MostrarNumero bit NOT NULL CONSTRAINT DF_Pers_Numero DEFAULT(1),
                MostrarOrdem bit NOT NULL CONSTRAINT DF_Pers_Ordem DEFAULT(1),
                MostrarSala bit NOT NULL CONSTRAINT DF_Pers_Sala DEFAULT(1),
                LogoUrl nvarchar(300) NULL,
                FundoUrl nvarchar(300) NULL,
                VideoUrl nvarchar(300) NULL,
                Marca nvarchar(80) NOT NULL CONSTRAINT DF_Pers_Marca DEFAULT('Sua Marca'),
                CONSTRAINT FK_Personalizacoes_Filiais FOREIGN KEY (FilialId) REFERENCES Filiais(Id) ON DELETE CASCADE
            );
        END;

        IF OBJECT_ID('PasswordResetTokens', 'U') IS NULL
        BEGIN
            CREATE TABLE PasswordResetTokens (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
                UsuarioId int NOT NULL,
                TokenHash nvarchar(64) NOT NULL,
                ExpiraEm datetime2 NOT NULL,
                UsadoEm datetime2 NULL,
                CONSTRAINT FK_PasswordResetTokens_Usuarios FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IX_PasswordResetTokens_TokenHash ON PasswordResetTokens(TokenHash);
        END;
        """;
}
