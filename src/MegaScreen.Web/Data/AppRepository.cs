using Dapper;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Data;

public sealed class AppRepository(IDbConnectionFactory connectionFactory)
{
    private System.Data.IDbConnection Open() => connectionFactory.CreateConnection();

    public async Task<Usuario?> ObterUsuarioPorEmailAsync(string email)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<Usuario>("""
            SELECT TOP (1) u.Id, u.FilialId, u.Nome, u.Email, u.SenhaHash, u.Perfil, u.LocalAtual, u.Ativo
            FROM Usuarios u
            INNER JOIN Filiais f ON f.Id = u.FilialId AND f.Ativa = 1
            INNER JOIN Clientes c ON c.Id = f.ClienteId AND c.Ativo = 1
            WHERE u.Email = @email AND u.Ativo = 1;
            """, new { email });
    }

    public async Task<Usuario?> ObterUsuarioAsync(int usuarioId, int filialId)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<Usuario>("""
            SELECT Id, FilialId, Nome, Email, SenhaHash, Perfil, LocalAtual, Ativo
            FROM Usuarios WHERE Id=@usuarioId;
            """, new { usuarioId, filialId });
    }

    public async Task<Filial?> ObterFilialAsync(int id)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<Filial>("""
            SELECT Id, ClienteId, Nome, Cidade, Uf, ImpressoraHabilitada, Ativa
            FROM Filiais WHERE Id=@id;
            """, new { id });
    }

    public async Task AtualizarPerfilAsync(int id, int filialId, string nome, string? localAtual)
    {
        using var db = Open();
        await db.ExecuteAsync("""
            UPDATE Usuarios SET Nome=@nome, LocalAtual=@localAtual
            WHERE Id=@id AND FilialId=@filialId;
            """, new { id, filialId, nome, localAtual });
    }

    public async Task AtualizarLocalAtualAsync(int id, string? localAtual)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE Usuarios SET LocalAtual=@localAtual WHERE Id=@id;", new { id, localAtual });
    }

    /// <summary>Senhas aguardando hoje (contador do menu).</summary>
    public async Task<int> ContarAguardandoHojeAsync(int filialId)
    {
        using var db = Open();
        var hoje = DataHora.Hoje();
        return await db.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM Senhas
            WHERE FilialId=@filialId AND Status=@aguardando AND CriadaEm>=@hoje AND CriadaEm<@amanha;
            """, new { filialId, aguardando=(int)StatusSenha.Aguardando, hoje, amanha=hoje.AddDays(1) });
    }

    public async Task AlterarSenhaAsync(int id, int filialId, string senhaHash)
    {
        using var db = Open();
        await db.ExecuteAsync("""
            UPDATE Usuarios SET SenhaHash=@senhaHash WHERE Id=@id AND FilialId=@filialId;
            """, new { id, filialId, senhaHash });
    }

    public async Task<List<Usuario>> ListarUsuariosAsync(int filialId)
    {
        using var db = Open();
        return (await db.QueryAsync<Usuario>("""
            SELECT Id, FilialId, Nome, Email, SenhaHash, Perfil, LocalAtual, Ativo
            FROM Usuarios WHERE FilialId=@filialId ORDER BY Nome;
            """, new { filialId })).ToList();
    }

    public async Task SalvarUsuarioAsync(Usuario item, string? novaSenhaHash = null)
    {
        using var db = Open();
        if (item.Id == 0)
        {
            await db.ExecuteAsync("""
                INSERT INTO Usuarios (FilialId,Nome,Email,SenhaHash,Perfil,LocalAtual,Ativo)
                VALUES (@FilialId,@Nome,@Email,@SenhaHash,@Perfil,@LocalAtual,@Ativo);
                """, new
                {
                    item.FilialId, item.Nome, item.Email,
                    SenhaHash = novaSenhaHash ?? throw new InvalidOperationException("Senha obrigatória."),
                    Perfil = (int)item.Perfil, item.LocalAtual, item.Ativo
                });
        }
        else
        {
            await db.ExecuteAsync("""
                UPDATE Usuarios SET Nome=@Nome, Email=@Email, Perfil=@Perfil,
                    LocalAtual=@LocalAtual, Ativo=@Ativo
                WHERE Id=@Id AND FilialId=@FilialId;
                """, new { item.Id, item.FilialId, item.Nome, item.Email, Perfil=(int)item.Perfil, item.LocalAtual, item.Ativo });
            if (!string.IsNullOrWhiteSpace(novaSenhaHash))
                await AlterarSenhaAsync(item.Id, item.FilialId, novaSenhaHash);
        }
    }

    public async Task ExcluirUsuarioAsync(int id, int filialId)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE Usuarios SET Ativo=0 WHERE Id=@id AND FilialId=@filialId;", new { id, filialId });
    }

    public async Task<List<Cliente>> ListarClientesAsync()
    {
        using var db = Open();
        var hoje = DataHora.Hoje();
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
        var rows = (await db.QueryAsync<Cliente>("""
            SELECT c.Id, c.Nome, c.Tipo, c.Documento, c.Ativo,
                   (SELECT COUNT(*) FROM Senhas s INNER JOIN Filiais f ON f.Id=s.FilialId
                     WHERE f.ClienteId=c.Id AND s.CriadaEm>=@inicioMes AND s.CriadaEm<@proximoMes) SenhasMes
            FROM Clientes c ORDER BY c.Nome;
            """, new { inicioMes, proximoMes = inicioMes.AddMonths(1) })).ToList();
        foreach (var c in rows)
            c.Filiais = (await db.QueryAsync<Filial>("""
                SELECT Id, ClienteId, Nome, Cidade, Uf, ImpressoraHabilitada, Ativa
                FROM Filiais WHERE ClienteId=@id ORDER BY Nome;
                """, new { id = c.Id })).ToList();
        return rows;
    }

    public async Task SalvarClienteAsync(Cliente item)
    {
        using var db = Open();
        if (item.Id == 0)
            await db.ExecuteAsync("""
                INSERT INTO Clientes (Nome,Tipo,Documento,Ativo)
                VALUES (@Nome,@Tipo,@Documento,@Ativo);
                """, new { item.Nome, Tipo=(int)item.Tipo, item.Documento, item.Ativo });
        else
            await db.ExecuteAsync("""
                UPDATE Clientes SET Nome=@Nome, Tipo=@Tipo, Documento=@Documento, Ativo=@Ativo
                WHERE Id=@Id;
                """, new { item.Id, item.Nome, Tipo=(int)item.Tipo, item.Documento, item.Ativo });
    }

    public async Task ExcluirClienteAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE Clientes SET Ativo=0 WHERE Id=@id;", new { id });
    }

    public async Task<List<Filial>> ListarFiliaisAsync()
    {
        using var db = Open();
        var list = (await db.QueryAsync<FilialCount>("""
            SELECT f.Id,f.ClienteId,f.Nome,f.Cidade,f.Uf,f.ImpressoraHabilitada,f.Ativa,
                (SELECT COUNT(*) FROM Usuarios u WHERE u.FilialId=f.Id) UsuariosCount,
                (SELECT COUNT(*) FROM Telas t WHERE t.FilialId=f.Id) TelasCount
            FROM Filiais f ORDER BY f.Nome;
            """)).ToList();
        return list.Select(x =>
        {
            x.Usuarios = Enumerable.Range(0,x.UsuariosCount).Select(_=>new Usuario()).ToList();
            x.Telas = Enumerable.Range(0,x.TelasCount).Select(_=>new Tela()).ToList();
            return (Filial)x;
        }).ToList();
    }

    public async Task<List<Filial>> ListarFiliaisAtivasAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<Filial>("""
            SELECT Id,ClienteId,Nome,Cidade,Uf,ImpressoraHabilitada,Ativa
            FROM Filiais WHERE Ativa=1 ORDER BY Nome;
            """)).ToList();
    }

    public async Task SalvarFilialAsync(Filial item)
    {
        using var db = Open();
        if (item.Id == 0)
            await db.ExecuteAsync("""
                INSERT INTO Filiais (ClienteId,Nome,Cidade,Uf,ImpressoraHabilitada,Ativa)
                VALUES (@ClienteId,@Nome,@Cidade,@Uf,@ImpressoraHabilitada,@Ativa);
                """, item);
        else
            await db.ExecuteAsync("""
                UPDATE Filiais SET ClienteId=@ClienteId,Nome=@Nome,Cidade=@Cidade,Uf=@Uf,
                    ImpressoraHabilitada=@ImpressoraHabilitada,Ativa=@Ativa WHERE Id=@Id;
                """, item);
    }

    public async Task ExcluirFilialAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE Filiais SET Ativa=0 WHERE Id=@id;", new { id });
    }

    public async Task<List<Modalidade>> ListarModalidadesAsync(int filialId, bool somenteAtivas=false)
    {
        using var db = Open();
        var sql = """
            SELECT Id,FilialId,Nome,Prefixo,PrioridadeInterna,Ativa,Contador
            FROM Modalidades WHERE FilialId=@filialId
            """ + (somenteAtivas ? " AND Ativa=1" : "") + " ORDER BY Nome;";
        return (await db.QueryAsync<Modalidade>(sql,new { filialId })).ToList();
    }

    public async Task SalvarModalidadeAsync(Modalidade item)
    {
        using var db = Open();
        item.Prefixo = (item.Prefixo ?? "A").Trim().ToUpperInvariant();
        if (item.Id == 0)
            await db.ExecuteAsync("""
                INSERT INTO Modalidades (FilialId,Nome,Prefixo,PrioridadeInterna,Ativa,Contador)
                VALUES (@FilialId,@Nome,@Prefixo,@PrioridadeInterna,@Ativa,0);
                """, new { item.FilialId,item.Nome,item.Prefixo,PrioridadeInterna=(int)item.PrioridadeInterna,item.Ativa });
        else
            await db.ExecuteAsync("""
                UPDATE Modalidades SET Nome=@Nome,Prefixo=@Prefixo,PrioridadeInterna=@PrioridadeInterna,Ativa=@Ativa
                WHERE Id=@Id AND FilialId=@FilialId;
                """, new { item.Id,item.FilialId,item.Nome,item.Prefixo,PrioridadeInterna=(int)item.PrioridadeInterna,item.Ativa });
    }

    public async Task ExcluirModalidadeAsync(int id,int filialId)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE Modalidades SET Ativa=0 WHERE Id=@id AND FilialId=@filialId;", new { id,filialId });
    }

    public async Task<List<Sala>> ListarSalasAsync(int filialId)
    {
        using var db = Open();
        return (await db.QueryAsync<Sala>("""
            SELECT Id,FilialId,Nome,Apelido,Predio,Andar,Ramal,Cor,SalaEsperaId
            FROM Salas WHERE FilialId=@filialId ORDER BY Nome;
            """,new { filialId })).ToList();
    }

    public async Task SalvarSalaAsync(Sala item)
    {
        using var db = Open();
        if (item.Id==0)
            await db.ExecuteAsync("""
                INSERT INTO Salas (FilialId,Nome,Apelido,Predio,Andar,Ramal,Cor,SalaEsperaId)
                VALUES (@FilialId,@Nome,@Apelido,@Predio,@Andar,@Ramal,@Cor,
                        (SELECT se.Id FROM SalasEspera se WHERE se.Id=@SalaEsperaId AND se.FilialId=@FilialId));
                """,item);
        else
            await db.ExecuteAsync("""
                UPDATE Salas SET Nome=@Nome,Apelido=@Apelido,Predio=@Predio,Andar=@Andar,Ramal=@Ramal,Cor=@Cor,
                    SalaEsperaId=(SELECT se.Id FROM SalasEspera se WHERE se.Id=@SalaEsperaId AND se.FilialId=@FilialId)
                WHERE Id=@Id AND FilialId=@FilialId;
                """,item);
    }

    /// <summary>Exclui a sala. Retorna false se ela já foi usada em senhas (FK) e por isso não pôde ser removida.</summary>
    public async Task<bool> ExcluirSalaAsync(int id,int filialId)
    {
        using var db = Open();
        var linhas = await db.ExecuteAsync("DELETE FROM Salas WHERE Id=@id AND FilialId=@filialId AND NOT EXISTS(SELECT 1 FROM Senhas WHERE SalaId=@id);",new { id,filialId });
        return linhas > 0;
    }

    public async Task<List<Tela>> ListarTelasAsync(int filialId)
    {
        using var db = Open();
        var rows = await db.QueryAsync<Tela,SalaEspera?,Tela>("""
            SELECT t.Id,t.FilialId,t.Nome,t.Apelido,t.Tipo,t.Predio,t.Andar,t.Ramal,t.Cor,t.Visivel,t.SalaEsperaId,
                   s.Id,s.FilialId,s.Nome,s.Apelido
            FROM Telas t LEFT JOIN SalasEspera s ON s.Id=t.SalaEsperaId
            WHERE t.FilialId=@filialId ORDER BY t.Nome;
            """,(t,s)=>{ t.SalaEspera=s; return t; },new { filialId },splitOn:"Id");
        return rows.ToList();
    }

    public async Task SalvarTelaAsync(Tela item)
    {
        using var db = Open();
        if (item.Id==0)
            await db.ExecuteAsync("""
                INSERT INTO Telas (FilialId,Nome,Apelido,Tipo,Predio,Andar,Ramal,Cor,Visivel,SalaEsperaId)
                VALUES (@FilialId,@Nome,@Apelido,@Tipo,@Predio,@Andar,@Ramal,@Cor,@Visivel,@SalaEsperaId);
                """,new { item.FilialId,item.Nome,item.Apelido,Tipo=(int)item.Tipo,item.Predio,item.Andar,item.Ramal,item.Cor,item.Visivel,item.SalaEsperaId });
        else
            await db.ExecuteAsync("""
                UPDATE Telas SET Nome=@Nome,Apelido=@Apelido,Tipo=@Tipo,Predio=@Predio,Andar=@Andar,Ramal=@Ramal,
                    Cor=@Cor,Visivel=@Visivel,SalaEsperaId=@SalaEsperaId
                WHERE Id=@Id AND FilialId=@FilialId;
                """,new { item.Id,item.FilialId,item.Nome,item.Apelido,Tipo=(int)item.Tipo,item.Predio,item.Andar,item.Ramal,item.Cor,item.Visivel,item.SalaEsperaId });
    }

    public async Task<Tela?> ObterTelaAsync(int id)
    {
        using var db = Open();
        var rows = await db.QueryAsync<Tela,SalaEspera?,Tela>("""
            SELECT t.Id,t.FilialId,t.Nome,t.Apelido,t.Tipo,t.Predio,t.Andar,t.Ramal,t.Cor,t.Visivel,t.SalaEsperaId,
                   s.Id,s.FilialId,s.Nome,s.Apelido
            FROM Telas t LEFT JOIN SalasEspera s ON s.Id=t.SalaEsperaId
            WHERE t.Id=@id;
            """,(t,s)=>{ t.SalaEspera=s; return t; },new { id },splitOn:"Id");
        return rows.FirstOrDefault();
    }

    /// <summary>Define quais telas pertencem à sala de espera (desvincula as que foram desmarcadas).</summary>
    public async Task AssociarTelasAsync(int salaEsperaId,int filialId,IEnumerable<int> telaIds)
    {
        var ids = telaIds.Distinct().ToArray();
        using var db = Open();
        db.Open();
        using var tx = db.BeginTransaction();
        await db.ExecuteAsync("""
            UPDATE Telas SET SalaEsperaId=NULL
            WHERE FilialId=@filialId AND SalaEsperaId=@salaEsperaId;
            """,new { filialId,salaEsperaId },tx);
        if (ids.Length > 0)
            await db.ExecuteAsync("""
                UPDATE Telas SET SalaEsperaId=@salaEsperaId
                WHERE FilialId=@filialId AND Id IN @ids
                  AND EXISTS (SELECT 1 FROM SalasEspera se WHERE se.Id=@salaEsperaId AND se.FilialId=@filialId);
                """,new { filialId,salaEsperaId,ids },tx);
        tx.Commit();
    }

    public async Task ExcluirTelaAsync(int id,int filialId)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM Telas WHERE Id=@id AND FilialId=@filialId;",new { id,filialId });
    }

    public async Task<List<SalaEspera>> ListarSalasEsperaAsync(int filialId)
    {
        using var db = Open();
        var list=(await db.QueryAsync<SalaEsperaCount>("""
            SELECT s.Id,s.FilialId,s.Nome,s.Apelido,
                (SELECT COUNT(*) FROM Telas t WHERE t.SalaEsperaId=s.Id) TelasCount,
                (SELECT COUNT(*) FROM PontosAtendimento p WHERE p.SalaEsperaId=s.Id) PontosCount
            FROM SalasEspera s WHERE s.FilialId=@filialId ORDER BY s.Nome;
            """,new { filialId })).ToList();
        return list.Select(x=>{
            x.Telas=Enumerable.Range(0,x.TelasCount).Select(_=>new Tela()).ToList();
            x.Pontos=Enumerable.Range(0,x.PontosCount).Select(_=>new PontoAtendimento()).ToList();
            return (SalaEspera)x;
        }).ToList();
    }

    public async Task<List<PontoAtendimento>> ListarPontosAsync(int filialId)
    {
        using var db = Open();
        return (await db.QueryAsync<PontoAtendimento>("""
            SELECT p.Id,p.SalaEsperaId,p.Nome,p.Ativo
            FROM PontosAtendimento p
            INNER JOIN SalasEspera s ON s.Id=p.SalaEsperaId
            WHERE s.FilialId=@filialId ORDER BY s.Nome,p.Nome;
            """,new { filialId })).ToList();
    }

    /// <summary>Salva e devolve o Id da sala de espera.</summary>
    public async Task<int> SalvarSalaEsperaAsync(SalaEspera item)
    {
        using var db = Open();
        if (item.Id==0)
            return await db.ExecuteScalarAsync<int>("""
                INSERT INTO SalasEspera (FilialId,Nome,Apelido) VALUES (@FilialId,@Nome,@Apelido);
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """,item);
        await db.ExecuteAsync("UPDATE SalasEspera SET Nome=@Nome,Apelido=@Apelido WHERE Id=@Id AND FilialId=@FilialId;",item);
        return item.Id;
    }

    public async Task ExcluirSalaEsperaAsync(int id,int filialId)
    {
        using var db = Open();
        db.Open();
        using var tx=db.BeginTransaction();
        await db.ExecuteAsync("UPDATE Telas SET SalaEsperaId=NULL WHERE SalaEsperaId=@id AND FilialId=@filialId;",new { id,filialId },tx);
        await db.ExecuteAsync("UPDATE Salas SET SalaEsperaId=NULL WHERE SalaEsperaId=@id AND FilialId=@filialId;",new { id,filialId },tx);
        await db.ExecuteAsync("DELETE FROM PontosAtendimento WHERE SalaEsperaId=@id;",new { id },tx);
        await db.ExecuteAsync("DELETE FROM SalasEspera WHERE Id=@id AND FilialId=@filialId;",new { id,filialId },tx);
        tx.Commit();
    }

    public async Task SalvarPontoAsync(PontoAtendimento item,int filialId)
    {
        using var db = Open();
        var salaOk=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SalasEspera WHERE Id=@id AND FilialId=@filialId;",new { id=item.SalaEsperaId,filialId });
        if(salaOk==0) throw new InvalidOperationException("Sala de espera inválida.");
        if(item.Id==0)
            await db.ExecuteAsync("INSERT INTO PontosAtendimento (SalaEsperaId,Nome,Ativo) VALUES (@SalaEsperaId,@Nome,@Ativo);",item);
        else
            await db.ExecuteAsync("UPDATE PontosAtendimento SET SalaEsperaId=@SalaEsperaId,Nome=@Nome,Ativo=@Ativo WHERE Id=@Id;",item);
    }

    /// <summary>Pausa / reativa o ponto de atendimento.</summary>
    public async Task AlternarPontoAsync(int id,int filialId)
    {
        using var db=Open();
        await db.ExecuteAsync("""
            UPDATE p SET p.Ativo = CASE WHEN p.Ativo=1 THEN 0 ELSE 1 END
            FROM PontosAtendimento p
            INNER JOIN SalasEspera s ON s.Id=p.SalaEsperaId
            WHERE p.Id=@id AND s.FilialId=@filialId;
            """,new { id,filialId });
    }

    public async Task ExcluirPontoAsync(int id,int filialId)
    {
        using var db=Open();
        await db.ExecuteAsync("""
            DELETE p FROM PontosAtendimento p
            INNER JOIN SalasEspera s ON s.Id=p.SalaEsperaId
            WHERE p.Id=@id AND s.FilialId=@filialId;
            """,new { id,filialId });
    }

    public async Task<PersonalizacaoGeral> ObterPersonalizacaoAsync(int filialId)
    {
        using var db=Open();
        var p=await db.QueryFirstOrDefaultAsync<PersonalizacaoGeral>("""
            SELECT FilialId,CorFundo,CorTexto,CorDestaque,CorSecundaria,Fonte,MostrarHorario,MostrarNumero,MostrarOrdem,MostrarSala,
                   LogoUrl,FundoUrl,VideoUrl,Marca,MidiaUrl,FonteLateralPx,FundoPadrao,NomeNaTv,AvisoChamada,AlertaEsperaMin
            FROM Personalizacoes WHERE FilialId=@filialId;
            """,new { filialId });
        p ??= new PersonalizacaoGeral { FilialId=filialId };
        // Colunas que aceitam NULL no banco: garante valores padrão para as telas.
        if (string.IsNullOrWhiteSpace(p.Marca)) p.Marca = "Sua Marca";
        if (string.IsNullOrWhiteSpace(p.Fonte)) p.Fonte = "Manrope";
        return p;
    }

    public async Task SalvarPersonalizacaoAsync(PersonalizacaoGeral p)
    {
        using var db=Open();
        await db.ExecuteAsync("""
            MERGE Personalizacoes AS target
            USING (SELECT @FilialId AS FilialId) AS src ON target.FilialId=src.FilialId
            WHEN MATCHED THEN UPDATE SET CorFundo=@CorFundo,CorTexto=@CorTexto,CorDestaque=@CorDestaque,CorSecundaria=@CorSecundaria,
                Fonte=@Fonte,MostrarHorario=@MostrarHorario,MostrarNumero=@MostrarNumero,MostrarOrdem=@MostrarOrdem,
                MostrarSala=@MostrarSala,LogoUrl=@LogoUrl,FundoUrl=@FundoUrl,VideoUrl=@VideoUrl,Marca=@Marca,
                MidiaUrl=@MidiaUrl,FonteLateralPx=@FonteLateralPx,FundoPadrao=@FundoPadrao,NomeNaTv=@NomeNaTv,
                AvisoChamada=@AvisoChamada,AlertaEsperaMin=@AlertaEsperaMin,
                AtualizadoEm=@AtualizadoEm
            WHEN NOT MATCHED THEN INSERT
                (FilialId,CorFundo,CorTexto,CorDestaque,CorSecundaria,Fonte,MostrarHorario,MostrarNumero,MostrarOrdem,MostrarSala,LogoUrl,FundoUrl,VideoUrl,Marca,
                 MidiaUrl,FonteLateralPx,FundoPadrao,NomeNaTv,AvisoChamada,AlertaEsperaMin,AtualizadoEm)
                VALUES (@FilialId,@CorFundo,@CorTexto,@CorDestaque,@CorSecundaria,@Fonte,@MostrarHorario,@MostrarNumero,@MostrarOrdem,@MostrarSala,@LogoUrl,@FundoUrl,@VideoUrl,@Marca,
                 @MidiaUrl,@FonteLateralPx,@FundoPadrao,@NomeNaTv,@AvisoChamada,@AlertaEsperaMin,@AtualizadoEm);
            """,new
            {
                p.FilialId,p.CorFundo,p.CorTexto,p.CorDestaque,p.CorSecundaria,p.Fonte,p.MostrarHorario,p.MostrarNumero,p.MostrarOrdem,p.MostrarSala,
                p.LogoUrl,p.FundoUrl,p.VideoUrl,p.Marca,
                p.MidiaUrl,p.FonteLateralPx,p.FundoPadrao,NomeNaTv=(int)p.NomeNaTv,AvisoChamada=(int)p.AvisoChamada,p.AlertaEsperaMin,
                AtualizadoEm=DataHora.Agora()
            });
    }

    public async Task<DashboardStats> ObterDashboardAsync(int filialId)
    {
        using var db=Open();
        var hoje=DataHora.Hoje();
        return await db.QuerySingleAsync<DashboardStats>("""
            SELECT
              ISNULL(SUM(CASE WHEN Status=@Aguardando AND CriadaEm>=@hoje AND CriadaEm<@amanha THEN 1 ELSE 0 END),0) Aguardando,
              ISNULL(SUM(CASE WHEN Status=@EmAtendimento AND CriadaEm>=@hoje AND CriadaEm<@amanha THEN 1 ELSE 0 END),0) EmAtendimento,
              ISNULL(SUM(CASE WHEN Status=@Concluida AND ConcluidaEm>=@hoje AND ConcluidaEm<@amanha THEN 1 ELSE 0 END),0) ConcluidosHoje,
              ISNULL(AVG(CASE WHEN ChamadaEm IS NOT NULL AND CriadaEm>=@hoje AND CriadaEm<@amanha
                    THEN DATEDIFF(second,CriadaEm,ChamadaEm) END),0) EsperaMediaSegundos
            FROM Senhas WHERE FilialId=@filialId;
            """,new { filialId,hoje,amanha=hoje.AddDays(1),Aguardando=(int)StatusSenha.Aguardando,EmAtendimento=(int)StatusSenha.EmAtendimento,Concluida=(int)StatusSenha.Concluida });
    }

    public async Task CriarTokenRecuperacaoAsync(int usuarioId,string tokenHash,DateTime expiraEm)
    {
        using var db=Open();
        // Invalida tokens anteriores ainda não usados e cria o novo (hora de São Paulo).
        await db.ExecuteAsync("""
            UPDATE PasswordResetTokens SET UtilizadoEm=@agora WHERE UsuarioId=@usuarioId AND UtilizadoEm IS NULL;
            INSERT INTO PasswordResetTokens (UsuarioId,TokenHash,ExpiraEm,CriadoEm) VALUES (@usuarioId,@tokenHash,@expiraEm,@agora);
            """,new { usuarioId,tokenHash,expiraEm,agora=DataHora.Agora() });
    }

    public async Task<Usuario?> ObterUsuarioPorTokenRecuperacaoAsync(string tokenHash)
    {
        using var db=Open();
        return await db.QueryFirstOrDefaultAsync<Usuario>("""
            SELECT u.Id,u.FilialId,u.Nome,u.Email,u.SenhaHash,u.Perfil,u.LocalAtual,u.Ativo
            FROM PasswordResetTokens t INNER JOIN Usuarios u ON u.Id=t.UsuarioId
            WHERE t.TokenHash=@tokenHash AND t.UtilizadoEm IS NULL AND t.ExpiraEm>@agora AND u.Ativo=1;
            """,new { tokenHash,agora=DataHora.Agora() });
    }

    public async Task ConsumirTokenRecuperacaoAsync(string tokenHash,int usuarioId,string senhaHash)
    {
        using var db=Open();
        db.Open();
        using var tx=db.BeginTransaction();
        await db.ExecuteAsync("UPDATE Usuarios SET SenhaHash=@senhaHash WHERE Id=@usuarioId;",new { usuarioId,senhaHash },tx);
        await db.ExecuteAsync("UPDATE PasswordResetTokens SET UtilizadoEm=@agora WHERE TokenHash=@tokenHash AND UsuarioId=@usuarioId;",new { tokenHash,usuarioId,agora=DataHora.Agora() },tx);
        tx.Commit();
    }

    private sealed class FilialCount : Filial { public int UsuariosCount { get; set; } public int TelasCount { get; set; } }
    private sealed class SalaEsperaCount : SalaEspera { public int TelasCount { get; set; } public int PontosCount { get; set; } }
}
