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
               s.Etapa, s.SalaDestinoId, s.EncaminhadaEm, s.ChamadaRecepcaoEm, sd.Nome AS SalaDestinoNome,
               m.Id, m.FilialId, m.Nome, m.Prefixo, m.PrioridadeInterna, m.Ativa, m.Contador
        FROM Senhas s
        INNER JOIN Modalidades m ON m.Id = s.ModalidadeId
        LEFT JOIN Salas sd ON sd.Id = s.SalaDestinoId
        """;

    private const string SelectSenhaComModalidade = "SELECT " + ColunasSenha;

    private static Senha Mapear(Senha senha, Modalidade modalidade)
    {
        senha.Modalidade = modalidade;
        return senha;
    }

    /// <summary>Fila do dia (hora de São Paulo): aguardando + em atendimento, nas duas etapas.
    /// Ordem: prioritários (idoso/PcD) primeiro, depois prioridade interna da modalidade, depois ordem de chegada
    /// (na etapa Profissional, a ordem de chegada é a hora em que a recepção encaminhou).</summary>
    public async Task<List<Senha>> FilaAsync(int filialId)
    {
        var sql = SelectSenhaComModalidade + """

            WHERE s.FilialId = @filialId
              AND s.Status IN (@aguardando, @emAtendimento)
              AND s.CriadaEm >= @hoje AND s.CriadaEm < @amanha
            ORDER BY CASE WHEN s.Prioridade = @normal THEN 1 ELSE 0 END,
                     m.PrioridadeInterna DESC,
                     COALESCE(s.EncaminhadaEm, s.CriadaEm);
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

    /// <summary>Últimas senhas chamadas hoje (mais recente primeiro).
    /// Com salaEsperaId, traz só as chamadas dos guichês dessa sala de espera e das salas (destino) vinculadas a ela
    /// ou sem vínculo. Sem salaEsperaId, traz a filial inteira.</summary>
    public async Task<List<Senha>> UltimasChamadasAsync(int filialId, int? salaEsperaId = null, int quantidade = 5)
    {
        var sql = "SELECT TOP (@quantidade) " + ColunasSenha + """

            WHERE s.FilialId = @filialId
              AND s.ChamadaEm IS NOT NULL
              AND s.ChamadaEm >= @hoje AND s.ChamadaEm < @amanha
              AND (@salaEsperaId IS NULL
                   OR EXISTS (SELECT 1 FROM PontosAtendimento p
                              WHERE p.SalaEsperaId = @salaEsperaId AND p.Nome = s.PontoAtendimento)
                   OR EXISTS (SELECT 1 FROM Salas sa
                              WHERE sa.Id = s.SalaId AND (sa.SalaEsperaId = @salaEsperaId OR sa.SalaEsperaId IS NULL)))
            ORDER BY s.ChamadaEm DESC;
            """;
        var hoje = DataHora.Hoje();
        using var db = connectionFactory.CreateConnection();
        var result = await db.QueryAsync<Senha, Modalidade, Senha>(sql, Mapear,
            new { filialId, salaEsperaId, quantidade, hoje, amanha = hoje.AddDays(1) }, splitOn: "Id");
        return result.ToList();
    }

    /// <summary>Lista para a TV pública, com o nome formatado pela regra da Personalização.</summary>
    public async Task<List<TvChamadaDto>> UltimasParaTvAsync(int filialId, int? salaEsperaId, PersonalizacaoGeral p)
        => (await UltimasChamadasAsync(filialId, salaEsperaId)).Select(s => new TvChamadaDto
        {
            Id = s.Id,
            Codigo = s.Codigo,
            Nome = p.NomePublico(s.Nome),
            Modalidade = s.Modalidade?.Nome ?? "",
            Ponto = s.PontoAtendimento
        }).ToList();

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
                (FilialId, ModalidadeId, Codigo, Nome, Prioridade, Status, Etapa, CriadaEm)
            OUTPUT INSERTED.Id, INSERTED.FilialId, INSERTED.ModalidadeId, INSERTED.Codigo,
                   INSERTED.Nome, INSERTED.Prioridade, INSERTED.Status, INSERTED.LocalAtual,
                   INSERTED.PontoAtendimento, INSERTED.SalaId, INSERTED.CriadaEm,
                   INSERTED.ChamadaEm, INSERTED.ConcluidaEm, INSERTED.Etapa
            VALUES
                (@FilialId, @ModalidadeId, @Codigo, @Nome, @Prioridade, @Status, @Etapa, @CriadaEm);
            """, new
        {
            FilialId = filialId,
            ModalidadeId = modalidade.Id,
            Codigo = codigo,
            Nome = string.IsNullOrWhiteSpace(vm.Nome) ? null : vm.Nome.Trim(),
            Prioridade = (int)vm.Prioridade,
            Status = (int)StatusSenha.Aguardando,
            Etapa = (int)EtapaSenha.Recepcao,
            CriadaEm = agora
        }, tx);

        senha.Modalidade = modalidade;
        tx.Commit();

        await Broadcast(filialId);
        return senha;
    }

    /// <summary>Recepção chama a senha (etapa Recepção) somente se ela ainda estiver aguardando
    /// (evita dois guichês chamarem a mesma). Senhas já encaminhadas ao profissional não podem ser chamadas aqui.</summary>
    public async Task ChamarAsync(int filialId, int senhaId, string ponto)
    {
        using var db = connectionFactory.CreateConnection();
        var linhas = await db.ExecuteAsync("""
            UPDATE Senhas
            SET Status = @emAtendimento,
                PontoAtendimento = @ponto,
                SalaId = NULL,
                ChamadaEm = @agora
            WHERE Id = @senhaId AND FilialId = @filialId
              AND Status = @aguardando AND Etapa = @recepcao;
            """, new
        {
            senhaId,
            filialId,
            ponto,
            emAtendimento = (int)StatusSenha.EmAtendimento,
            aguardando = (int)StatusSenha.Aguardando,
            recepcao = (int)EtapaSenha.Recepcao,
            agora = DataHora.Agora()
        });

        if (linhas == 0)
        {
            var atual = await ObterComModalidadeAsync(db, filialId, senhaId);
            throw new RegraNegocioException(atual?.Etapa == EtapaSenha.Profissional
                ? "Esta senha já foi encaminhada ao profissional e não pode ser chamada pela recepção."
                : "Esta senha não está mais aguardando. Talvez outro atendente já a tenha chamado.");
        }

        await AposChamar(db, filialId, senhaId);
    }

    /// <summary>Recepção libera a senha para o profissional. salaDestinoId nulo = fila geral;
    /// informado = só aquele consultório poderá chamar. A senha volta a "Aguardando" na etapa Profissional.</summary>
    public async Task EncaminharAsync(int filialId, int senhaId, int? salaDestinoId)
    {
        using var db = connectionFactory.CreateConnection();
        if (salaDestinoId is not null)
        {
            var existe = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Salas WHERE Id = @salaDestinoId AND FilialId = @filialId;",
                new { salaDestinoId, filialId });
            if (existe == 0) throw new RegraNegocioException("Sala de destino não encontrada nesta filial.");
        }

        var agora = DataHora.Agora();
        var linhas = await db.ExecuteAsync("""
            UPDATE Senhas
            SET Etapa = @profissional,
                Status = @aguardando,
                SalaDestinoId = @salaDestinoId,
                EncaminhadaEm = @agora,
                ChamadaRecepcaoEm = COALESCE(ChamadaEm, @agora)
            WHERE Id = @senhaId AND FilialId = @filialId
              AND Etapa = @recepcao
              AND Status IN (@aguardando, @emAtendimento);
            """, new
        {
            senhaId,
            filialId,
            salaDestinoId,
            agora,
            profissional = (int)EtapaSenha.Profissional,
            recepcao = (int)EtapaSenha.Recepcao,
            aguardando = (int)StatusSenha.Aguardando,
            emAtendimento = (int)StatusSenha.EmAtendimento
        });
        if (linhas == 0)
            throw new RegraNegocioException("Senha não encontrada, já finalizada ou já encaminhada ao profissional.");

        await Broadcast(filialId);
    }

    /// <summary>Profissional chama: só senhas que já passaram pela recepção (etapa Profissional, aguardando),
    /// da fila geral ou direcionadas para a sala dele.</summary>
    public async Task ChamarProfissionalAsync(int filialId, int senhaId, int salaId)
    {
        using var db = connectionFactory.CreateConnection();
        var sala = await db.QuerySingleOrDefaultAsync<Sala>(
            "SELECT Id, FilialId, Nome FROM Salas WHERE Id = @salaId AND FilialId = @filialId;",
            new { salaId, filialId })
            ?? throw new RegraNegocioException("Sala de atendimento não encontrada.");

        var linhas = await db.ExecuteAsync("""
            UPDATE Senhas
            SET Status = @emAtendimento,
                PontoAtendimento = @ponto,
                SalaId = @salaId,
                ChamadaEm = @agora
            WHERE Id = @senhaId AND FilialId = @filialId
              AND Status = @aguardando AND Etapa = @profissional
              AND (SalaDestinoId IS NULL OR SalaDestinoId = @salaId);
            """, new
        {
            senhaId,
            filialId,
            salaId = sala.Id,
            ponto = sala.Nome,
            emAtendimento = (int)StatusSenha.EmAtendimento,
            aguardando = (int)StatusSenha.Aguardando,
            profissional = (int)EtapaSenha.Profissional,
            agora = DataHora.Agora()
        });

        if (linhas == 0)
        {
            var atual = await ObterComModalidadeAsync(db, filialId, senhaId)
                ?? throw new RegraNegocioException("Senha não encontrada.");
            if (atual.Etapa != EtapaSenha.Profissional)
                throw new RegraNegocioException("Este paciente ainda não passou pela recepção.");
            if (atual.SalaDestinoId is not null && atual.SalaDestinoId != sala.Id)
                throw new RegraNegocioException($"Este paciente foi direcionado para {atual.SalaDestinoNome ?? "outra sala"}.");
            throw new RegraNegocioException("Este paciente não está mais aguardando. Talvez outro profissional já o tenha chamado.");
        }

        await AposChamar(db, filialId, senhaId);
    }

    private async Task AposChamar(IDbConnection db, int filialId, int senhaId)
    {
        var senha = await ObterComModalidadeAsync(db, filialId, senhaId)
            ?? throw new RegraNegocioException("Senha não encontrada.");
        await AvisarChamada(filialId, senha);
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

        await AvisarChamada(filialId, senha);
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

    // Equipe (logada) recebe os dados completos. TVs públicas recebem só o id da senha chamada e
    // buscam a própria lista em /api/tv, já filtrada pela sala de espera e com o nome conforme a LGPD.
    private async Task AvisarChamada(int filialId, Senha senha)
    {
        await hub.Clients.Group(Grupo(filialId)).SendAsync("SenhaChamada", SenhaDto.From(senha));
        await hub.Clients.Group(GrupoTv(filialId)).SendAsync("Chamada", senha.Id);
    }

    private async Task Broadcast(int filialId)
    {
        var fila = (await FilaAsync(filialId)).Select(SenhaDto.From).ToList();
        await hub.Clients.Group(Grupo(filialId)).SendAsync("FilaAtualizada", fila);
        await hub.Clients.Group(GrupoTv(filialId)).SendAsync("Atualizar");
    }

    public static string Grupo(int filialId) => $"filial-{filialId}";
    public static string GrupoTv(int filialId) => $"tv-{filialId}";
}
