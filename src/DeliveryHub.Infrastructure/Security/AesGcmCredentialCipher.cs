using System.Security.Cryptography;
using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Security;

public sealed class CredentialCipherOptions
{
    public const string SectionName = "Seguranca";

    // Chave simétrica de 32 bytes, base64. Sem SDK novo — AES-GCM já vem no
    // runtime do .NET.
    public required string ChaveCredenciais { get; set; }
}

// Formato do texto guardado: nonce (12 bytes) + tag (16 bytes) + ciphertext,
// tudo em base64. Nonce aleatório a cada chamada — reusar nonce com a mesma
// chave quebra a garantia do GCM.
internal sealed class AesGcmCredentialCipher : ICredentialCipher
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    private readonly byte[] _chave;

    public AesGcmCredentialCipher(IOptions<CredentialCipherOptions> options)
    {
        _chave = Convert.FromBase64String(options.Value.ChaveCredenciais);
    }

    public string Proteger(string valor)
    {
        var textoClaro = System.Text.Encoding.UTF8.GetBytes(valor);
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var cifrado = new byte[textoClaro.Length];
        var tag = new byte[TamanhoTag];

        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Encrypt(nonce, textoClaro, cifrado, tag);

        var saida = new byte[TamanhoNonce + TamanhoTag + cifrado.Length];
        nonce.CopyTo(saida, 0);
        tag.CopyTo(saida, TamanhoNonce);
        cifrado.CopyTo(saida, TamanhoNonce + TamanhoTag);

        return Convert.ToBase64String(saida);
    }

    public string Desproteger(string valor)
    {
        var bytes = Convert.FromBase64String(valor);

        var nonce = bytes.AsSpan(0, TamanhoNonce);
        var tag = bytes.AsSpan(TamanhoNonce, TamanhoTag);
        var cifrado = bytes.AsSpan(TamanhoNonce + TamanhoTag);
        var textoClaro = new byte[cifrado.Length];

        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Decrypt(nonce, cifrado, tag, textoClaro);

        return System.Text.Encoding.UTF8.GetString(textoClaro);
    }
}
