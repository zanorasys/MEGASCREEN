using Dapper;
using Microsoft.AspNetCore.SignalR;
using MegaScreen.Web.Data;
using MegaScreen.Web.Hubs;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Services;

public class SenhaService(IDbConnectionFactory connectionFactory, IHubContext<SenhaHub> hub)
{
    public async Task<List<Senha>> FilaAsync(int filialId)
    {
        const string sql = """
            SELECT s.Id, s.FilialId, s.ModalidadeId, s.Codigo, s.Nome, s.Prioridade, s.Status,
                   s.LocalAtual, s.PontoAtendimento, s.SalaId, s.CriadaEm, s.ChamadaEm, s.ConcluidaEm,
                   m.Id, m.FilialId, m.Nome, m.Prefixo, m.PrioridadeInterna, m.Ativa, m.Contador
            FROM Senhas s
            INNER JOIN Modalidades m ON m.Id = s.ModalidadeId
            WHERE s.FilialId = @filialId
              AND s.Status IN (@aguardando, @emAtendimento)
            ORDER BY CASE WHEN s.Prioridade = @normal THEN 1 ELSE 0 END, s.CriadaEm;
            """;

        using var db = connectionFactory.CreateConnection();
        var result = await db.QueryAsync<Senha, Modalidade, Senha>(
            sql,
            (senha, modalidade) =>
            {
                senha.Modalidade = modalidade;
                return senha;
            },
            new
            {
                filialId,
                aguardando = (int)StatusSenha.Aguardando,
                emAtendimento = (int)StatusSenha.EmAtendimento,
                normal = (int)Prioridade.Normal
            },
            splitOn: "Id");

        return result.ToList();
    }

    public async Task<Senha> EmitirAsync(int filialId, NovaSenhaViewModel vm)
    {
        using var db = connectionFactory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        const string incrementSql = """
            UPDATE Modalidades WITH (UPDLOCK, ROWLOCK)
            SET Contador = Contador + 1
            OUTPUT INSERTED.Id, INSERTED.FilialId, INSERTED.Nome, INSERTED.Prefixo,
                   INSERTED.PrioridadeInterna, INSERTED.Ativa, INSERTED.Contador
            WHERE Id = @modalidadeId AND FilialId = @filialId AND Ativa = 1;
            """;

        var modalidade = await db.QuerySingleOrDefaultAsync<Modalidade>(
            incrementSql, new { modalidadeId = vm.ModalidadeId, filialId }, tx);

        if (modalidade is null)
            throw new InvalidOperationException("Modalidade não encontrada ou inativa.");

        var criadaEm = DateTime.Now;
        var codigo = $"{modalidade.Prefixo}{modalidade.Contador:D3}";

        const string insertSql = """
            INSERT INTO Senhas
                (FilialId, ModalidadeId, Codigo, Nome, Prioridade, Status, CriadaEm)
            OUTPUT INSERTED.Id, INSERTED.FilialId, INSERTED.ModalidadeId, INSERTED.Codigo,
                   INSERTED.Nome, INSERTED.Prioridade, INSERTED.Status, INSERTED.LocalAtual,
                   INSERTED.PontoAtendimento, INSERTED.SalaId, INSERTED.CriadaEm,
                   INSERTED.ChamadaEm, INSERTED.ConcluidaEm
            VALUES
                (@FilialId, @ModalidadeId, @Codigo, @Nome, @Prioridade, @Status, @CriadaEm);
            """;

        var senha = await db.QuerySingleAsync<Senha>(insertSql, new
        {
            FilialId = filialId,
            ModalidadeId = modalidade.Id,
            Codigo = codigo,
            vm.Nome,
            Prioridade = (int)vm.Prioridade,
            Status = (int)StatusSenha.Aguardando,
            CriadaEm = criadaEm
        }, tx);

        senha.Modalidade = modalidade;
        tx.Commit();

        await Broadcast(filialId);
        return senha;
    }

    public async Task ChamarAsync(int senhaId, string ponto)
    {
        using var db = connectionFactory.CreateConnection();
        const string updateSql = """
            UPDATE Senhas
            SET Status = @status, PontoAtendimento = @ponto, ChamadaEm = @chamadaEm
            WHERE Id = @senhaId;
            """;
        await db.ExecuteAsync(updateSql, new
        {
            senhaId,
            ponto,
            status = (int)StatusSenha.EmAtendimento,
            chamadaEm = DateTime.Now
        });

        var senha = await ObterComModalidadeAsync(db, senhaId)
            ?? throw new InvalidOperationException("Senha não encontrada.");

        await hub.Clients.Group(Grupo(senha.FilialId)).SendAsync("SenhaChamada", SenhaDto.From(senha));
        await Broadcast(senha.FilialId);
    }

    public async Task ConcluirAsync(int senhaId)
    {
        using var db = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE Senhas
            SET Status = @status, ConcluidaEm = @concluidaEm
            OUTPUT INSERTED.FilialId
            WHERE Id = @senhaId;
            """;
        var filialId = await db.QuerySingleOrDefaultAsync<int?>(sql, new
        {
            senhaId,
            status = (int)StatusSenha.Concluida,
            concluidaEm = DateTime.Now
        });
        if (filialId is null)
            throw new InvalidOperationException("Senha não encontrada.");

        await Broadcast(filialId.Value);
    }

    public async Task AtualizarStatusAsync(int senhaId, StatusSenha status)
    {
        using var db = connectionFactory.CreateConnection();
        const string sql = """
            UPDATE Senhas
            SET Status = @status
            OUTPUT INSERTED.FilialId
            WHERE Id = @senhaId;
            """;
        var filialId = await db.QuerySingleOrDefaultAsync<int?>(sql, new { senhaId, status = (int)status });
        if (filialId is null)
            throw new InvalidOperationException("Senha não encontrada.");

        await Broadcast(filialId.Value);
    }

    private static async Task<Senha?> ObterComModalidadeAsync(System.Data.IDbConnection db, int senhaId)
    {
        const string sql = """
            SELECT s.Id, s.FilialId, s.ModalidadeId, s.Codigo, s.Nome, s.Prioridade, s.Status,
                   s.LocalAtual, s.PontoAtendimento, s.SalaId, s.CriadaEm, s.ChamadaEm, s.ConcluidaEm,
                   m.Id, m.FilialId, m.Nome, m.Prefixo, m.PrioridadeInterna, m.Ativa, m.Contador
            FROM Senhas s
            INNER JOIN Modalidades m ON m.Id = s.ModalidadeId
            WHERE s.Id = @senhaId;
            """;
        return (await db.QueryAsync<Senha, Modalidade, Senha>(
            sql,
            (senha, modalidade) =>
            {
                senha.Modalidade = modalidade;
                return senha;
            },
            new { senhaId },
            splitOn: "Id")).FirstOrDefault();
    }

    private async Task Broadcast(int filialId)
    {
        var fila = (await FilaAsync(filialId)).Select(SenhaDto.From).ToList();
        await hub.Clients.Group(Grupo(filialId)).SendAsync("FilaAtualizada", fila);
    }

    public static string Grupo(int filialId) => $"filial-{filialId}";
}
