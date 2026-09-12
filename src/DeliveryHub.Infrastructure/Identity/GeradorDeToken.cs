using System.Security.Claims;
using System.Text;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Identity;

internal sealed class GeradorDeToken : IGeradorDeToken
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public GeradorDeToken(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public TokenEmitido Gerar(Usuario usuario, Guid? merchantId)
    {
        var agora = _timeProvider.GetUtcNow();
        var expiraEm = agora.AddHours(_options.DuracaoEmHoras);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Papel.ToString())
        };

        // Admin não recebe merchant_id: ele não opera dentro de uma loja, e o
        // acesso amplo dele vem do papel, não da ausência de tenant.
        if (merchantId is not null)
            claims.Add(new Claim(ClaimsDeliveryHub.MerchantId, merchantId.Value.ToString()));

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ChaveAssinatura));

        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Emissor,
            Audience = _options.Audiencia,
            IssuedAt = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descritor);

        return new TokenEmitido(token, expiraEm);
    }
}
