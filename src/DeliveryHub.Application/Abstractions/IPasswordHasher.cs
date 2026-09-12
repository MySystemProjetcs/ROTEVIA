namespace DeliveryHub.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string senha);
    bool Verificar(string senha, string hash);
}

public interface IGeradorDeSenha
{
    // Usada quando o administrador cria a credencial do dono da loja. A senha
    // em claro só existe nesse retorno — nunca é persistida nem logada.
    string Gerar();
}
