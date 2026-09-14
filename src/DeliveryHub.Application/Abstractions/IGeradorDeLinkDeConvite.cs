namespace DeliveryHub.Application.Abstractions;

// Porta pra montar a URL pública do convite — fica em Infrastructure porque
// depende de configuração (Frontend:BaseUrl), e Application não tem
// dependência nenhuma de infraestrutura (nem IConfiguration).
public interface IGeradorDeLinkDeConvite
{
    string ConstruirUrl(Guid linkId, string token);
}
