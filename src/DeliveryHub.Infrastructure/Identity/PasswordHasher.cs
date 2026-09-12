using System.Security.Cryptography;
using DeliveryHub.Application.Abstractions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace DeliveryHub.Infrastructure.Identity;

// PBKDF2 com HMAC-SHA256. O formato guardado é "iteracoes.salt.hash" em base64,
// para que aumentar o custo no futuro não invalide as senhas já existentes.
internal sealed class PasswordHasher : IPasswordHasher
{
    private const int Iteracoes = 210_000;
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;

    public string Hash(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Derivar(senha, salt, Iteracoes);

        return $"{Iteracoes}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string senha, string hash)
    {
        var partes = hash.Split('.', 3);
        if (partes.Length != 3 || !int.TryParse(partes[0], out var iteracoes))
            return false;

        var salt = Convert.FromBase64String(partes[1]);
        var esperado = Convert.FromBase64String(partes[2]);
        var calculado = Derivar(senha, salt, iteracoes);

        // Comparação em tempo fixo: comparar byte a byte vaza o tamanho do
        // prefixo correto e abre timing attack.
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    private static byte[] Derivar(string senha, byte[] salt, int iteracoes) =>
        KeyDerivation.Pbkdf2(senha, salt, KeyDerivationPrf.HMACSHA256, iteracoes, TamanhoHash);
}

internal sealed class GeradorDeSenha : IGeradorDeSenha
{
    // Sem caracteres ambíguos (O/0, l/1/I): a senha vai ser lida e digitada por
    // uma pessoa, então confusão vira chamado de suporte.
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
    private const int Tamanho = 12;

    public string Gerar() => RandomNumberGenerator.GetString(Alfabeto, Tamanho);
}
