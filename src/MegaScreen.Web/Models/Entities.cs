using System.ComponentModel.DataAnnotations;
using MegaScreen.Web.Infrastructure;

namespace MegaScreen.Web.Models;

public enum TipoCliente { PJ = 1, PF = 2 }
public enum PerfilUsuario { Admin = 1, Atendente = 2, Profissional = 3 }
public enum TipoTela { TvHorizontal = 1, TabletVertical = 2 }
public enum Prioridade { Normal = 1, Idoso = 2, Alta = 3 }
public enum StatusSenha { Aguardando = 1, EmAtendimento = 2, Concluida = 3, NaoCompareceu = 4, Cancelada = 5 }

public class Cliente
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Nome { get; set; } = "";
    public TipoCliente Tipo { get; set; } = TipoCliente.PJ;
    [Display(Name = "CNPJ / CPF"), StringLength(20)] public string? Documento { get; set; }
    public bool Ativo { get; set; } = true;
    public ICollection<Filial> Filiais { get; set; } = new List<Filial>();
}

public class Filial
{
    public int Id { get; set; }
    [Display(Name = "Cliente")] public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    [Required, StringLength(120)] public string Nome { get; set; } = "";
    [StringLength(80)] public string? Cidade { get; set; }
    [Display(Name = "UF"), StringLength(2)] public string? Uf { get; set; }
    public bool ImpressoraHabilitada { get; set; }
    public bool Ativa { get; set; } = true;
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Tela> Telas { get; set; } = new List<Tela>();
    public ICollection<SalaEspera> SalasEspera { get; set; } = new List<SalaEspera>();
    public ICollection<Modalidade> Modalidades { get; set; } = new List<Modalidade>();
}

public class Usuario
{
    public int Id { get; set; }
    public int FilialId { get; set; }
    public Filial? Filial { get; set; }
    [Required, StringLength(120)] public string Nome { get; set; } = "";
    [Display(Name = "E-mail"), Required, StringLength(160), EmailAddress] public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Atendente;
    [Display(Name = "Local atual"), StringLength(80)] public string? LocalAtual { get; set; }
    public bool Ativo { get; set; } = true;
}

public class Tela
{
    public int Id { get; set; }
    public int FilialId { get; set; }
    [Required, StringLength(120)] public string Nome { get; set; } = "";
    [StringLength(40)] public string? Apelido { get; set; }
    public TipoTela Tipo { get; set; } = TipoTela.TvHorizontal;
    [Display(Name = "Prédio"), StringLength(60)] public string? Predio { get; set; }
    [StringLength(60)] public string? Andar { get; set; }
    [StringLength(20)] public string? Ramal { get; set; }
    [StringLength(9)] public string? Cor { get; set; } = "#4C5BF5";
    public bool Visivel { get; set; } = true;
    public int? SalaEsperaId { get; set; }
    public SalaEspera? SalaEspera { get; set; }
}

public class SalaEspera
{
    public int Id { get; set; }
    public int FilialId { get; set; }
    [Required, StringLength(120)] public string Nome { get; set; } = "";
    [StringLength(40)] public string? Apelido { get; set; }
    public ICollection<Tela> Telas { get; set; } = new List<Tela>();
    public ICollection<PontoAtendimento> Pontos { get; set; } = new List<PontoAtendimento>();
}

public class PontoAtendimento
{
    public int Id { get; set; }
    [Display(Name = "Sala de espera")] public int SalaEsperaId { get; set; }
    [Required, StringLength(80)] public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public class Sala
{
    public int Id { get; set; }
    public int FilialId { get; set; }
    [Required, StringLength(120)] public string Nome { get; set; } = "";
    [StringLength(40)] public string? Apelido { get; set; }
    [Display(Name = "Prédio"), StringLength(60)] public string? Predio { get; set; }
    [StringLength(60)] public string? Andar { get; set; }
    [StringLength(20)] public string? Ramal { get; set; }
    [StringLength(9)] public string? Cor { get; set; } = "#4C5BF5";
}

public class Modalidade
{
    public int Id { get; set; }
    public int FilialId { get; set; }
    [Required, StringLength(120)] public string Nome { get; set; } = "";
    [Required, StringLength(3)] public string Prefixo { get; set; } = "A";
    public Prioridade PrioridadeInterna { get; set; } = Prioridade.Normal;
    public bool Ativa { get; set; } = true;
    public int Contador { get; set; }
}

public class Senha
{
    public int Id { get; set; }
    public int FilialId { get; set; }
    public int ModalidadeId { get; set; }
    public Modalidade? Modalidade { get; set; }
    [Required, StringLength(10)] public string Codigo { get; set; } = "";
    [StringLength(120)] public string? Nome { get; set; }
    public Prioridade Prioridade { get; set; } = Prioridade.Normal;
    public StatusSenha Status { get; set; } = StatusSenha.Aguardando;
    [StringLength(80)] public string? LocalAtual { get; set; }
    [StringLength(80)] public string? PontoAtendimento { get; set; }
    public int? SalaId { get; set; }
    public DateTime CriadaEm { get; set; } = DataHora.Agora();
    public DateTime? ChamadaEm { get; set; }
    public DateTime? ConcluidaEm { get; set; }
}

public class PersonalizacaoGeral
{
    public int FilialId { get; set; }
    [StringLength(9)] public string CorFundo { get; set; } = "#0B0D24";
    [StringLength(9)] public string CorTexto { get; set; } = "#FFFFFF";
    [StringLength(9)] public string CorDestaque { get; set; } = "#4C5BF5";
    [StringLength(100)] public string Fonte { get; set; } = "Manrope";
    public bool MostrarHorario { get; set; } = true;
    public bool MostrarNumero { get; set; } = true;
    public bool MostrarOrdem { get; set; } = true;
    public bool MostrarSala { get; set; } = true;
    [StringLength(300)] public string? LogoUrl { get; set; }
    [StringLength(300)] public string? FundoUrl { get; set; }
    [StringLength(300)] public string? VideoUrl { get; set; }
    [StringLength(80)] public string Marca { get; set; } = "Sua Marca";
}

public class DashboardStats
{
    public int Aguardando { get; set; }
    public int EmAtendimento { get; set; }
    public int ConcluidosHoje { get; set; }
    public int EsperaMediaSegundos { get; set; }
}
