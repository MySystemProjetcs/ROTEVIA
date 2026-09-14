using DeliveryHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DeliveryHub.Infrastructure.RealTime;

// O restaurante se conecta e entra no grupo "merchant-{merchantId}".
// O motoboy não se conecta ao Hub — ele faz POST REST e o servidor
// emite para o grupo do merchant dono do pedido.
[Authorize]
public sealed class RastreioHub : Hub
{
    // Sem parâmetro de propósito. Receber o merchantId do cliente deixava
    // qualquer usuário autenticado — inclusive motoboy ou dono de outra loja —
    // entrar no grupo de qualquer restaurante e receber a posição em tempo real
    // dos entregadores dele e o resumo de faturamento. O tenant sai do token
    // (CLAUDE.md §6), nunca de quem chama.
    public Task EntrarNoGrupo()
    {
        if (Context.User.MerchantId() is not { } merchantId)
            throw new HubException("Sessão sem loja.");

        return Groups.AddToGroupAsync(Context.ConnectionId, GrupoDoMerchant(merchantId));
    }

    public Task SairDoGrupo()
    {
        if (Context.User.MerchantId() is not { } merchantId)
            return Task.CompletedTask;

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GrupoDoMerchant(merchantId));
    }

    public static string GrupoDoMerchant(Guid merchantId) => $"merchant-{merchantId}";

    // Método que o servidor chama nos clientes do restaurante.
    public const string MetodoPosicaoAtualizada = "PosicaoAtualizada";

    // Dashboard do dono (resumo: online, pedidos e receita do dia). Mesmo Hub
    // e mesmo grupo do rastreio — é outro assunto, mesmo destinatário.
    public const string MetodoResumoAtualizado = "ResumoAtualizado";
}
