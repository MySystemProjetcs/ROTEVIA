namespace DeliveryHub.Domain.SharedKernel;

// Coordenadas ficam como decimal simples por enquanto. O tipo geográfico do
// PostGIS entra no Escopo 5 (Dispatch), que é onde o índice GiST passa a
// importar — antes disso seria complexidade sem uso.
public sealed record Endereco(
    string Logradouro,
    string Numero,
    string Bairro,
    string Cidade,
    string Estado,
    string Cep,
    string? Complemento,
    string? Referencia,
    double Latitude,
    double Longitude);
