namespace DeliveryHub.Application.Abstractions;

// Porta (hexagonal): origem de pedido — iFood hoje, 99Food/Anota AI/WhatsApp/
// PDV próprio depois (ver CLAUDE.md §5). Assinatura real definida no
// Contrato do Escopo 1 (Passo 1), antes da implementação do adapter iFood.
public interface IOrderSource
{
}
