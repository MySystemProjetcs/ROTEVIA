namespace DeliveryHub.Application.Abstractions;

// Porta (hexagonal): fornecedor de frota — própria hoje, Uber Direct/Lalamove
// como overflow depois (ver CLAUDE.md §5 e §9). Fluxo oposto ao de
// IOrderSource: não tratar como o mesmo tipo de integração. Assinatura real
// vem no Escopo 5 (Dispatch), quando esse contrato for fechado.
public interface IFleetProvider
{
}
