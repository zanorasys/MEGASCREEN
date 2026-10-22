using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using MegaScreen.Web.Data;
using MegaScreen.Web.Hubs;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    // Libera somente as páginas públicas de conta. Não usar AllowAnonymousToFolder("/Account"):
    // o AllowAnonymous anula o [Authorize(Roles="Admin")] do SwitchFilial e do Perfil.
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/EsqueciSenha");
    options.Conventions.AllowAnonymousToPage("/Account/RedefinirSenha");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AcessoNegado");
    options.Conventions.AllowAnonymousToPage("/Public/Tv");
    options.Conventions.AllowAnonymousToPage("/Public/Totem");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizeFolder("/Clientes", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Filiais", "AdminOnly");
    options.Conventions.AuthorizePage("/Account/SwitchFilial", "AdminOnly");
}).AddMvcOptions(ValidacaoPtBr.Configurar);

builder.Services.AddSignalR();
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<AppRepository>();
builder.Services.AddScoped<SenhaService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/AcessoNegado";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // Na API devolve 401/403 em vez de redirecionar para a tela de login.
        opt.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        opt.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

// Totem é público: limita emissão de senhas por IP para evitar abuso.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("emitir", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

var ptBr = new CultureInfo("pt-BR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ptBr),
    SupportedCultures = [ptBr],
    SupportedUICultures = [ptBr]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    b => b.UseStatusCodePagesWithReExecute("/Error"));

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapRazorPages();
app.MapHub<SenhaHub>("/hubs/senha");

var api = app.MapGroup("/api/senhas");

// Executa a ação e converte erros de regra de negócio em mensagem PT-BR para o front.
static async Task<IResult> Executar(Func<Task> acao)
{
    try { await acao(); return Results.Ok(); }
    catch (RegraNegocioException ex) { return Results.Json(new { erro = ex.Message }, statusCode: StatusCodes.Status409Conflict); }
}

api.MapGet("/fila", async (HttpContext http, SenhaService svc) =>
    Results.Json((await svc.FilaAsync(http.User.FilialId())).Select(SenhaDto.From)))
    .RequireAuthorization();

api.MapGet("/stats", async (HttpContext http, AppRepository repo) =>
{
    var st = await repo.ObterDashboardAsync(http.User.FilialId());
    return Results.Json(new PainelStatsDto
    {
        Aguardando = st.Aguardando,
        EmAtendimento = st.EmAtendimento,
        ConcluidosHoje = st.ConcluidosHoje,
        EsperaMediaSegundos = st.EsperaMediaSegundos
    });
}).RequireAuthorization();

// TV pública: por tela (filtra pela sala de espera) ou pela filial inteira. Nome conforme LGPD.
app.MapGet("/api/tv/ultimas", async (int? telaId, int? filialId, AppRepository repo, SenhaService svc) =>
{
    int fid;
    int? salaEsperaId = null;
    if (telaId is > 0)
    {
        var tela = await repo.ObterTelaAsync(telaId.Value);
        if (tela is null) return Results.NotFound(new { erro = "Tela não encontrada." });
        if (!tela.Visivel) return Results.Json(new { oculta = true, chamadas = Array.Empty<TvChamadaDto>() });
        fid = tela.FilialId;
        salaEsperaId = tela.SalaEsperaId;
    }
    else if (filialId is > 0) fid = filialId.Value;
    else return Results.BadRequest(new { erro = "Informe a tela ou a filial." });

    var p = await repo.ObterPersonalizacaoAsync(fid);
    return Results.Json(new { oculta = false, chamadas = await svc.UltimasParaTvAsync(fid, salaEsperaId, p) });
}).AllowAnonymous();

api.MapPost("/emitir/{filialId:int}", async (int filialId, NovaSenhaViewModel vm, SenhaService svc) =>
{
    if (vm.ModalidadeId <= 0 || !Enum.IsDefined(typeof(Prioridade), vm.Prioridade))
        return Results.BadRequest(new { erro = "Dados da senha inválidos." });
    try
    {
        var senha = await svc.EmitirAsync(filialId, vm);
        return Results.Json(SenhaDto.From(senha));
    }
    catch (RegraNegocioException ex) { return Results.BadRequest(new { erro = ex.Message }); }
}).AllowAnonymous().RequireRateLimiting("emitir");

api.MapPost("/chamar/{id:int}", (int id, string? ponto, int? salaId, HttpContext http, SenhaService svc) =>
    Executar(() => svc.ChamarAsync(http.User.FilialId(), id,
        string.IsNullOrWhiteSpace(ponto) ? "Guichê" : ponto.Trim(), salaId)))
    .RequireAuthorization();

api.MapPost("/rechamar/{id:int}", (int id, HttpContext http, SenhaService svc) =>
    Executar(() => svc.RechamarAsync(http.User.FilialId(), id)))
    .RequireAuthorization();

api.MapPost("/mover/{id:int}", (int id, string? destino, HttpContext http, SenhaService svc) =>
    string.IsNullOrWhiteSpace(destino)
        ? Task.FromResult(Results.BadRequest(new { erro = "Informe o destino." }))
        : Executar(() => svc.MoverAsync(http.User.FilialId(), id, destino.Trim())))
    .RequireAuthorization();

api.MapPost("/concluir/{id:int}", (int id, HttpContext http, SenhaService svc) =>
    Executar(() => svc.ConcluirAsync(http.User.FilialId(), id)))
    .RequireAuthorization();

api.MapPost("/status/{id:int}/{status:int}", (int id, int status, HttpContext http, SenhaService svc) =>
    !Enum.IsDefined(typeof(StatusSenha), status)
        ? Task.FromResult(Results.BadRequest(new { erro = "Status inválido." }))
        : Executar(() => svc.AtualizarStatusAsync(http.User.FilialId(), id, (StatusSenha)status)))
    .RequireAuthorization();

app.Run();
