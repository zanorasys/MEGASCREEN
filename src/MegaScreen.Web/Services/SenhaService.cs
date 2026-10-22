using System.Data;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using MegaScreen.Web.Data;
using MegaScreen.Web.Hubs;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Services;

/// <summary>Erro de regra de negócio com mensagem pronta para o usuário (PT-BR).</summary>
public sealed class RegraNegocioException(string mensagem) : Exception(mensagem);

public class SenhaService(IDbConnectionFactory connectionFactory, IHubContext<SenhaHub> hub)
{
    private const string ColunasSenha = """
        s.Id, s.FilialId, s.ModalidadeId, s.Codigo, s.Nome, s.Prioridade, s.Status,
               s.LocalAtual, s.PontoAtendimento, s.SalaId, s.CriadaEm, s.ChamadaEm, s.ConcluidaEm,
               m.Id, m.FilialId, m.Nome, m.Prefixo, m.PrioridadeInterna, m.Ativa, m.Contador
        FROM Senhas s
        INNER JOIN Modalidades m ON m.Id = s.ModalidadeId
        """;

    private const string SelectSenhaComModalidade = "SELECT " + ColunasSenha;

    private static Senha Mapear(Senha senha, Modalidade modalidade)
    {
        senha.Modalidade = modalidade;
        return senha;
    }

    /// <summary>Fila do dia (hora de São Paulo): aguardando + em atendimento.
    /// Ordem: prioritários (idoso/PcD) primeiro, depois prioridade interna da modalidade, depois ordem de chegada.</summary>
    public async Task<List<Senha>> FilaAsync(int filialId)
    {
        var sql = SelectSenhaComModalidade + """

            WHERE s.FilialId = @filialId
              AND s.Status IN (@aguardando, @emAtendimento)
              AND s.CriadaEm >= @hoje AND s.CriadaEm < @amanha
            ORDER BY CASE WHEN s.Prioridade = @normal THEN 1 ELSE 0 END,
                     m.PrioridadeInterna DESC,
                     s.CriadaEm;
            """;
        var hoje = DataHora.Hoje();
        using var db = connectionFactory.CreateConnection();
        var result = await db.QueryAsync<Senha, Modalidade, Senha>(sql, Mapear, new
        {
            filialId,
            aguardando = (int)StatusSenha.Aguardando,
            emAtendimento = (int)StatusSenha.EmAtendimento,
            normal = (int)Prioridade.Normal,
            hoje,
            amanha = hoje.AddDays(1)
        }, splitOn: "Id");
        return result.ToList();
    }

    /// <summary>Últimas senhas chamadas hoje (mais recente primeiro), usado na lateral da TV.</summary>
    public async Task<List<Senha>> UltimasChamadasAsync(int filialId, int quantidade = 5)
    {
        var sql = "SELECT TOP (@quantidade) " + ColunasSenha + """

            WHERE s.FilialId = @filialId
              AND s.ChamadaEm IS NOT NULL
              AND s.ChamadaEm >= @hoje AND s.ChamadaEm < @amanha
            ORDER BY s.ChamadaEm DESC;
            """;
        var hoje = DataHora.Hoje();
        using var db = connectionFactory.CreateConnection();
        var result = await db.QueryAsync<Senha, Modalidade, Senha>(sql, Mapear,
            new { filialId, quantidade, hoje, amanha = hoje.AddDays(1) }, splitOn: "Id");
        return result.ToList();
    }

    public async Task<Senha> EmitirAsync(int filialId, NovaSenhaViewModel vm)
    {
        var agora = DataHora.Agora();
        using var db = connectionFactory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        var modalidade = await db.QuerySingleOrDefaultAsync<Modalidade>("""
            SELECT Id, FilialId, Nome, Prefixo, PrioridadeInterna, Ativa, Contador
            FROM Modalidades
            WHERE Id = @modalidadeId AND FilialId = @filialId AND Ativa = 1;
            """, new { modalidadeId = vm.ModalidadeId, filialId }, tx)
            ?? throw new RegraNegocioException("Modalidade não encontrada ou inativa.");

        // Numeração diária por filial + modalidade (procedure usp_Senha_ProximoNumero / tabela SenhaSequencia).
        var p = new DynamicParameters();
        p.Add("@FilialId", (long)filialId, DbType.Int64);
        p.Add("@ModalidadeId", (long)modalidade.Id, DbType.Int64);
        p.Add("@DataReferencia", agora.Date, DbType.Date);
        p.Add("@Code", Guid.NewGuid(), DbType.Guid);
        p.Add("@Agora", agora, DbType.DateTime2);
        p.Add("@ProximoNumero", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.usp_Senha_ProximoNumero", p, tx, commandType: CommandType.StoredProcedure);
        var numero = p.Get<int>("@ProximoNumero");

        var codigo = $"{modalidade.Prefixo}{numero:D3}";

        var senha = await db.QuerySingleAsync<Senha>("""
            INSERT INTO Senhas
                (FilialId, ModalidadeId, Codigo, Nome, Prioridade, Status, CriadaEm)
            OUTPUT INSERTED.Id, INSERTED.FilialId, INSERTED.ModalidadeId, INSERTED.Codigo,
                   INSERTED.Nome, INSERTED.Prioridade, INSERTED.Status, INSERTED.LocalAtual,
                   INSERTED.PontoAtendimento, INSERTED.SalaId, INSERTED.CriadaEm,
                   INSERTED.ChamadaEm, INSERTED.ConcluidaEm
            VALUES
                (@FilialId, @ModalidadeId, @Codigo, @Nome, @Prioridade, @Status, @CriadaEm);
            """, new
        {
            FilialId = filialId,
            ModalidadeId = modalidade.Id,
            Codigo = codigo,
            Nome = string.IsNullOrWhiteSpace(vm.Nome) ? null : vm.Nome.Trim(),
            Prioridade = (int)vm.Prioridade,
            Status = (int)StatusSenha.Aguardando,
            CriadaEm = agora
        }, tx);

        senha.Modalidade = modalidade;
        tx.Commit();

        await Broadcast(filialId);
        return senha;
    }

    /// <summary>Chama a senha somente se ela ainda estiver aguardando (evita dois guichês chamarem a mesma).</summary>
    public async Task ChamarAsync(int filialId, int senhaId, string ponto, int? salaId = null)
    {
        using var db = connectionFactory.CreateConnection();
        var linhas = await db.ExecuteAsync("""
            UPDATE Senhas
            SET Status = @emAtendimento,
                PontoAtendimento = @ponto,
                SalaId = (SELECT sa.Id FROM Salas sa WHERE sa.Id = @salaId AND sa.FilialId = @filialId),
                ChamadaEm = @agora
            WHERE Id = @senhaId AND FilialId = @filialId AND Status = @aguardando;
            """, new
        {
            senhaId,
            filialId,
            ponto,
            salaId,
            emAtendimento = (int)StatusSenha.EmAtendimento,
            aguardando = (int)StatusSenha.Aguardando,
            agora = DataHora.Agora()
        });

        if (linhas == 0)
            throw new RegraNegocioException("Esta senha não está mais aguardando. Talvez outro atendente já a tenha chamado.");

        var senha = await ObterComModalidadeAsync(db, filialId, senhaId)
            ?? throw new RegraNegocioException("Senha não encontrada.");

        await hub.Clients.Group(Grupo(filialId)).SendAsync("SenhaChamada", SenhaDto.From(senha));
        await Broadcast(filialId);
    }

    public Task ConcluirAsync(int filialId, int senhaId) => AtualizarStatusAsync(filialId, senhaId, StatusSenha.Concluida);

    public async Task AtualizarStatusAsync(int filialId, int senhaId, StatusSenha status)
    {
        var finaliza = status is StatusSenha.Concluida or StatusSenha.NaoCompareceu or StatusSenha.Cancelada;
        using var db = connectionFactory.CreateConnection();
        var linhas = await db.ExecuteAsync("""
            UPDATE Senhas
            SET Status = @status,
                ConcluidaEm = CASE WHEN @finaliza = 1 THEN @agora ELSE ConcluidaEm END
            WHERE Id = @senhaId AND FilialId = @filialId
              AND Status IN (@aguardando, @emAtendimento);
            """, new
        {
            senhaId,
            filialId,
            status = (int)status,
            finaliza,
            agora = DataHora.Agora(),
            aguardando = (int)StatusSenha.Aguardando,
            emAtendimento = (int)StatusSenha.EmAtendimento
        });
        if (linhas == 0)
            throw new RegraNegocioException("Senha não encontrada ou já finalizada.");

        await Broadcast(filialId);
    }

    /// <summary>Rechama sem alterar ChamadaEm (para não distorcer a espera média).</summary>
    public async Task RechamarAsync(int filialId, int senhaId)
    {
        using var db = connectionFactory.CreateConnection();
        var senha = await ObterComModalidadeAsync(db, filialId, senhaId)
            ?? throw new RegraNegocioException("Senha não encontrada.");
        if (senha.Status != StatusSenha.EmAtendimento)
            throw new RegraNegocioException("Só é possível rechamar uma senha em atendimento.");

        await hub.Clients.Group(Grupo(filialId)).SendAsync("SenhaChamada", SenhaDto.From(senha));
        await Broadcast(filialId);
    }

    public async Task MoverAsync(int filialId, int senhaId, string destino)
    {
        using var db = connectionFactory.CreateConnection();
        var linhas = await db.ExecuteAsync("""
            UPDATE Senhas SET LocalAtual = @destino, PontoAtendimento = @destino
            WHERE Id = @senhaId AND FilialId = @filialId
              AND Status IN (@aguardando, @emAtendimento);
            """, new
        {
            senhaId,
            filialId,
            destino,
            aguardando = (int)StatusSenha.Aguardando,
            emAtendimento = (int)StatusSenha.EmAtendimento
        });
        if (linhas == 0) throw new RegraNegocioException("Senha não encontrada ou já finalizada.");
        await Broadcast(filialId);
    }

    private static async Task<Senha?> ObterComModalidadeAsync(IDbConnection db, int filialId, int senhaId)
    {
        var sql = SelectSenhaComModalidade + """

            WHERE s.Id = @senhaId AND s.FilialId = @filialId;
            """;
        return (await db.QueryAsync<Senha, Modalidade, Senha>(sql, Mapear, new { senhaId, filialId }, splitOn: "Id"))
            .FirstOrDefault();
    }

    private async Task Broadcast(int filialId)
    {
        var fila = (await FilaAsync(filialId)).Select(SenhaDto.From).ToList();
        var ultimas = (await UltimasChamadasAsync(filialId)).Select(SenhaDto.From).ToList();
        var grupo = hub.Clients.Group(Grupo(filialId));
        await grupo.SendAsync("FilaAtualizada", fila);
        await grupo.SendAsync("UltimasChamadas", ultimas);
    }

    public static string Grupo(int filialId) => $"filial-{filialId}";
}
