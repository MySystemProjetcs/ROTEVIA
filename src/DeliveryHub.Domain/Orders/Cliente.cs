namespace DeliveryHub.Domain.Orders;

// Minimização de dado pessoal (LGPD): guardamos só o necessário para a entrega
// acontecer. O CPF vem no payload do iFood e é deliberadamente descartado — não
// existe operação de logística que precise dele.
// O localizador é o código curto que o iFood fornece para contato sem expor o
// telefone real do cliente; use ele em vez do número sempre que possível.
public sealed record Cliente(string Nome, string? Telefone, string? Localizador);
