using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MegaScreen.Web.Models;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Senha { get; set; } = "";
    public bool LembrarDeMim { get; set; }
}

public class NovaSenhaViewModel
{
    [JsonPropertyName("nome")] public string? Nome { get; set; }
    [JsonPropertyName("modalidadeId")] public int ModalidadeId { get; set; }
    [JsonPropertyName("prioridade")] public Prioridade Prioridade { get; set; } = Prioridade.Normal;
}

public class SenhaDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("codigo")] public string Codigo { get; set; } = "";
    [JsonPropertyName("nome")] public string? Nome { get; set; }
    [JsonPropertyName("modalidade")] public string Modalidade { get; set; } = "";
    [JsonPropertyName("prioridade")] public string Prioridade { get; set; } = "Normal";
    [JsonPropertyName("status")] public string Status { get; set; } = "Aguardando";
    [JsonPropertyName("ponto")] public string? Ponto { get; set; }
    [JsonPropertyName("sala")] public string? Sala { get; set; }
    [JsonPropertyName("criadaEm")] public DateTime CriadaEm { get; set; }
    [JsonPropertyName("chamadaEm")] public DateTime? ChamadaEm { get; set; }

    public static SenhaDto From(Senha s) => new()
    {
        Id = s.Id,
        Codigo = s.Codigo,
        Nome = s.Nome,
        Modalidade = s.Modalidade?.Nome ?? "",
        Prioridade = s.Prioridade.ToString(),
        Status = s.Status.ToString(),
        Ponto = s.PontoAtendimento,
        CriadaEm = s.CriadaEm,
        ChamadaEm = s.ChamadaEm
    };
}

/// <summary>Chamada exibida na TV pública: sem dados além do necessário (nome conforme regra de LGPD).</summary>
public class TvChamadaDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("codigo")] public string Codigo { get; set; } = "";
    [JsonPropertyName("nome")] public string? Nome { get; set; }
    [JsonPropertyName("modalidade")] public string Modalidade { get; set; } = "";
    [JsonPropertyName("ponto")] public string? Ponto { get; set; }
}

/// <summary>Indicadores do painel (atualizados ao vivo).</summary>
public class PainelStatsDto
{
    [JsonPropertyName("aguardando")] public int Aguardando { get; set; }
    [JsonPropertyName("emAtendimento")] public int EmAtendimento { get; set; }
    [JsonPropertyName("concluidosHoje")] public int ConcluidosHoje { get; set; }
    [JsonPropertyName("esperaMediaSegundos")] public int EsperaMediaSegundos { get; set; }
}
