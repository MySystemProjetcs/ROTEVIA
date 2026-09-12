using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;

namespace DeliveryHub.Api.Identity;

// Cria o primeiro administrador na subida, e só se ainda não houver nenhum.
// Sem credencial padrão embutida: se Admin:Email e Admin:Senha não estiverem
// configurados, nada é criado — senha default em código é a origem clássica de
// sistema invadido no primeiro dia.
internal static class AdminSeeder
{
    public static async Task SemearAdministradorAsync(this WebApplication app)
    {
        var email = app.Configuration["Admin:Email"];
        var senha = app.Configuration["Admin:Senha"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            app.Logger.LogInformation("Admin:Email/Admin:Senha não configurados — nenhum administrador criado.");
            return;
        }

        using var scope = app.Services.CreateScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();

        if (await usuarios.ExisteComEmailAsync(email, CancellationToken.None))
            return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var relogio = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        usuarios.Adicionar(Usuario.Criar(
            email, hasher.Hash(senha), "Administrador", PapelUsuario.AdministradorSistema, relogio.GetUtcNow()));

        await usuarios.SalvarAsync(CancellationToken.None);

        app.Logger.LogInformation("Administrador do sistema criado para {Email}.", email);
    }
}
