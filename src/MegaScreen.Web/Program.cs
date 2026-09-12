using Microsoft.AspNetCore.Authentication.Cookies;
using MegaScreen.Web.Data;
using MegaScreen.Web.Hubs;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Public/Tv");
    options.Conventions.AllowAnonymousToPage("/Public/Totem");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizeFolder("/Clientes", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Filiais", "AdminOnly");
});

builder.Services.AddSignalR();

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<AppRepository>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<SenhaService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/Login";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<SenhaHub>("/hubs/senha");

var api = app.MapGroup("/api/senhas");

api.MapGet("/fila/{filialId:int}", async (int filialId, SenhaService svc) =>
    Results.Json((await svc.FilaAsync(filialId)).Select(SenhaDto.From)));

api.MapPost("/emitir/{filialId:int}", async (int filialId, NovaSenhaViewModel vm, SenhaService svc) =>
{
    var senha = await svc.EmitirAsync(filialId, vm);
    return Results.Json(SenhaDto.From(senha));
}).AllowAnonymous();

api.MapPost("/chamar/{id:int}", async (int id, string? ponto, SenhaService svc) =>
{
    await svc.ChamarAsync(id, ponto ?? "Guichê");
    return Results.Ok();
}).RequireAuthorization();

api.MapPost("/concluir/{id:int}", async (int id, SenhaService svc) =>
{
    await svc.ConcluirAsync(id);
    return Results.Ok();
}).RequireAuthorization();

api.MapPost("/status/{id:int}/{status:int}", async (int id, int status, SenhaService svc) =>
{
    await svc.AtualizarStatusAsync(id, (StatusSenha)status);
    return Results.Ok();
}).RequireAuthorization();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();
