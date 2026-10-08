using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Persistence;

// Fábrica usada SÓ em design-time (`dotnet ef migrations add`, `database update`).
// O pipeline de DI da API exige ITenantContext scoped, que o EF Tools não
// consegue resolver pelo root provider — por isso montamos aqui um AppDbContext
// mínimo, com um tenant "sistema" dummy. Em runtime nada passa por aqui.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=deliveryhub;Username=deliveryhub;Password=deliveryhub_dev";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Chave do cifrador é irrelevante em design-time: nenhum valor é
        // criptografado durante a geração/aplicação de migrations.
        var cipherOptions = Options.Create(new CredentialCipherOptions
        {
            ChaveCredenciais = Convert.ToBase64String(new byte[32])
        });

        return new AppDbContext(options, new TenantContextSistema(), new AesGcmCredentialCipher(cipherOptions));
    }
}
