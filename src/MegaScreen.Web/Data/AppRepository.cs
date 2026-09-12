using Dapper;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Data;

public sealed class AppRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<Usuario?> ObterUsuarioPorEmailAsync(string email)
    {
        const string sql = """
            SELECT TOP (1) Id, FilialId, Nome, Email, SenhaHash, Perfil, LocalAtual, Ativo
            FROM Usuarios
            WHERE Email = @email AND Ativo = 1;
            """;
        using var db = connectionFactory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<Usuario>(sql, new { email });
    }

    public async Task<List<Usuario>> ListarUsuariosAsync(int filialId)
    {
        const string sql = """
            SELECT u.Id, u.FilialId, u.Nome, u.Email, u.SenhaHash, u.Perfil, u.LocalAtual, u.Ativo,
                   f.Id, f.ClienteId, f.Nome, f.Cidade, f.Uf, f.ImpressoraHabilitada, f.Ativa
            FROM Usuarios u
            INNER JOIN Filiais f ON f.Id = u.FilialId
            WHERE u.FilialId = @filialId
            ORDER BY u.Nome;
            """;
        using var db = connectionFactory.CreateConnection();
        var rows = await db.QueryAsync<Usuario, Filial, Usuario>(
            sql, (u, f) => { u.Filial = f; return u; }, new { filialId }, splitOn: "Id");
        return rows.ToList();
    }

    public async Task<List<Cliente>> ListarClientesAsync()
    {
        const string sql = """
            SELECT c.Id, c.Nome, c.Tipo, c.Documento, c.Ativo,
                   f.Id, f.ClienteId, f.Nome, f.Cidade, f.Uf, f.ImpressoraHabilitada, f.Ativa
            FROM Clientes c
            LEFT JOIN Filiais f ON f.ClienteId = c.Id
            ORDER BY c.Nome, f.Nome;
            """;
        using var db = connectionFactory.CreateConnection();
        var mapa = new Dictionary<int, Cliente>();
        await db.QueryAsync<Cliente, Filial?, Cliente>(sql, (c, f) =>
        {
            if (!mapa.TryGetValue(c.Id, out var item))
            {
                item = c;
                item.Filiais = new List<Filial>();
                mapa.Add(c.Id, item);
            }
            if (f is not null && f.Id != 0)
                item.Filiais.Add(f);
            return item;
        }, splitOn: "Id");
        return mapa.Values.ToList();
    }

    public async Task<List<Filial>> ListarFiliaisAsync()
    {
        const string sql = """
            SELECT f.Id, f.ClienteId, f.Nome, f.Cidade, f.Uf, f.ImpressoraHabilitada, f.Ativa,
                   (SELECT COUNT(1) FROM Usuarios u WHERE u.FilialId = f.Id) AS UsuariosCount,
                   (SELECT COUNT(1) FROM Telas t WHERE t.FilialId = f.Id) AS TelasCount
            FROM Filiais f
            ORDER BY f.Nome;
            """;
        using var db = connectionFactory.CreateConnection();
        var rows = (await db.QueryAsync<FilialComContagens>(sql)).ToList();
        return rows.Select(x =>
        {
            x.Usuarios = Enumerable.Range(0, x.UsuariosCount).Select(_ => new Usuario()).ToList();
            x.Telas = Enumerable.Range(0, x.TelasCount).Select(_ => new Tela()).ToList();
            return (Filial)x;
        }).ToList();
    }

    public async Task<List<Modalidade>> ListarModalidadesAsync(int filialId, bool somenteAtivas = false)
    {
        var sql = """
            SELECT Id, FilialId, Nome, Prefixo, PrioridadeInterna, Ativa, Contador
            FROM Modalidades
            WHERE FilialId = @filialId
            """ + (somenteAtivas ? " AND Ativa = 1" : "") + " ORDER BY Nome;";
        using var db = connectionFactory.CreateConnection();
        return (await db.QueryAsync<Modalidade>(sql, new { filialId })).ToList();
    }

    public async Task<List<Sala>> ListarSalasAsync(int filialId)
    {
        const string sql = """
            SELECT Id, FilialId, Nome, Apelido, Predio, Andar, Ramal, Cor
            FROM Salas
            WHERE FilialId = @filialId
            ORDER BY Nome;
            """;
        using var db = connectionFactory.CreateConnection();
        return (await db.QueryAsync<Sala>(sql, new { filialId })).ToList();
    }

    public async Task<List<Tela>> ListarTelasAsync(int filialId)
    {
        const string sql = """
            SELECT t.Id, t.FilialId, t.Nome, t.Apelido, t.Tipo, t.Predio, t.Andar, t.Ramal, t.Cor,
                   t.Visivel, t.SalaEsperaId,
                   s.Id, s.FilialId, s.Nome, s.Apelido
            FROM Telas t
            LEFT JOIN SalasEspera s ON s.Id = t.SalaEsperaId
            WHERE t.FilialId = @filialId
            ORDER BY t.Nome;
            """;
        using var db = connectionFactory.CreateConnection();
        var rows = await db.QueryAsync<Tela, SalaEspera?, Tela>(
            sql, (t, s) => { t.SalaEspera = s; return t; }, new { filialId }, splitOn: "Id");
        return rows.ToList();
    }

    public async Task<List<SalaEspera>> ListarSalasEsperaAsync(int filialId)
    {
        const string sql = """
            SELECT s.Id, s.FilialId, s.Nome, s.Apelido,
                   (SELECT COUNT(1) FROM Telas t WHERE t.SalaEsperaId = s.Id) AS TelasCount,
                   (SELECT COUNT(1) FROM PontosAtendimento p WHERE p.SalaEsperaId = s.Id) AS PontosCount
            FROM SalasEspera s
            WHERE s.FilialId = @filialId
            ORDER BY s.Nome;
            """;
        using var db = connectionFactory.CreateConnection();
        var rows = (await db.QueryAsync<SalaEsperaComContagens>(sql, new { filialId })).ToList();
        return rows.Select(x =>
        {
            x.Telas = Enumerable.Range(0, x.TelasCount).Select(_ => new Tela()).ToList();
            x.Pontos = Enumerable.Range(0, x.PontosCount).Select(_ => new PontoAtendimento()).ToList();
            return (SalaEspera)x;
        }).ToList();
    }

    private sealed class FilialComContagens : Filial
    {
        public int UsuariosCount { get; set; }
        public int TelasCount { get; set; }
    }

    private sealed class SalaEsperaComContagens : SalaEspera
    {
        public int TelasCount { get; set; }
        public int PontosCount { get; set; }
    }
}
