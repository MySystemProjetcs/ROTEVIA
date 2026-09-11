# DeliveryHub — Guia de Engenharia

> **Natureza deste documento:** diretriz, não dogma. Cada seção traz *quando aplicar* e
> *quando não aplicar*. Aplicar um padrão onde ele não resolve problema é custo sem retorno.
>
> As únicas seções marcadas **OBRIGATÓRIO** são as que, se ignoradas, causam perda de
> pedido, vazamento entre tenants ou perda de dinheiro.

---

## 1. Protocolo de desenvolvimento

Nenhum escopo entra na `main` sem os 6 passos fechados:

1. **Contrato** — entrada, saída, máquina de estados
2. **Backend** — implementado contra ambiente real
3. **Validação viva** — fluxo ponta a ponta no sandbox
4. **Fixtures** — payloads reais capturados em `tests/fixtures/`
5. **Testes** — unitário no domínio + integração com os fixtures
6. **Frontend** do escopo

Escopo sem os 6 passos não avança. Sem exceção.

---

## 2. Padrão de endpoints

### Base aprovada

Minimal API + `MapGroup` + extension method por contexto. Sem controllers.

### Forma canônica

```csharp
public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pricing-rules")
            .WithTags("Pricing")
            .RequireAuthorization(Policies.MerchantOperator);

        group.MapGet("/", GetAll);

        group.MapPost("/", Create)
             .AddEndpointFilter<ValidationFilter<CreatePricingRuleRequest>>();
    }

    private static async Task<Ok<IReadOnlyList<PricingRuleDto>>> GetAll(
        IPricingService service,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var rules = await service.GetByMerchantAsync(tenant.MerchantId, ct);
        return TypedResults.Ok(rules);
    }

    private static async Task<Results<Created<PricingRuleDto>, ProblemHttpResult>> Create(
        CreatePricingRuleRequest request,
        IPricingService service,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(tenant.MerchantId, request, ct);

        return result.IsSuccess
            ? TypedResults.Created($"/api/pricing-rules/{result.Value.Id}", result.Value)
            : TypedResults.Problem(result.Error.ToProblemDetails());
    }
}
```

### O que mudou em relação ao rascunho inicial, e por quê

| Mudança | Motivo |
|---|---|
| Métodos nomeados no lugar de lambdas inline | Testáveis isoladamente, stack trace legível, menos closure |
| `TypedResults` no lugar de `Results` | Contrato em tempo de compilação; OpenAPI correto sem atributo manual |
| `Result<T>` no lugar de `try/catch` | Exceção não é fluxo de negócio (ver §3) |
| `CancellationToken` em toda assinatura async | Libera conexão do pool quando o cliente desiste |
| `Policies.X` no lugar de `Roles = "STRING"` | Autorização composta (role + tenant + status), sem string mágica |
| `ITenantContext` injetado | Isolamento estrutural, não por disciplina (ver §5) |
| `ValidationFilter<T>` | Validação antes do handler, sem poluir o corpo |

### Quando NÃO aplicar

- Endpoint interno de health check ou métrica: pode ser lambda simples de uma linha.
- Não crie `Result<T>` para operação que não tem caminho de falha de negócio.

---

## 3. Exceções vs Result

**Regra:** exceção para o inesperado. `Result<T>` para o esperado.

```csharp
// NÃO
throw new InvalidOperationException("Taxa já existe para esta região");

// SIM
return Result.Failure<PricingRuleDto>(PricingErrors.RegionAlreadyConfigured);
```

**Por quê:** `throw`/`catch` em .NET aloca objeto, captura stack trace e desmonta a
pilha. É ordens de grandeza mais lento que retornar valor. Em rota fria, irrelevante.
Em rota de despacho ou ingestão de pedido, é custo real. Mais importante: exceção como
fluxo esconde os caminhos de falha do compilador — `Result<T>` te obriga a tratá-los.

### Errors como constantes

```csharp
public static class PricingErrors
{
    public static readonly Error RegionAlreadyConfigured =
        new("pricing.region_already_configured", "Já existe taxa para esta região.", ErrorType.Conflict);
}
```

Código estável para o frontend traduzir. Mensagem para humano. Tipo para o HTTP status.

### Handler global — o `try/catch` mora aqui, uma vez só

```csharp
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    // registra em Program.cs: builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
}
```

O que chega nesse handler é bug ou falha de infra. Deve gerar alerta.

---

## 4. Idempotência — OBRIGATÓRIO nos escopos abaixo

### Onde é obrigatória

| Contexto | Chave de deduplicação |
|---|---|
| Ingestão de eventos iFood | `(source, external_event_id)` |
| Recebimento de webhook (qualquer origem) | ID do evento da origem |
| Consumo de Outbox | `outbox_message_id` |
| Criação de pedido via API pública | Header `Idempotency-Key` |
| Oferta de corrida ao entregador | `(order_id, courier_id, offer_round)` |
| Lançamento no ledger | `(source_type, source_id)` |

### Onde NÃO é necessária

- `GET`, `HEAD` — idempotentes por natureza
- CRUD administrativo de baixa concorrência (cadastro de região, edição de horário)
- Não instrumente idempotência "por precaução". Custa escrita e índice.

### Os três níveis

**Nível 1 — Constraint de banco. É a única garantia real.**

```sql
CREATE UNIQUE INDEX ux_inbox_event
    ON integration_inbox (source, external_event_id);
```

```csharp
// ON CONFLICT DO NOTHING — não faça SELECT antes de INSERT
var inserted = await _db.Database.ExecuteSqlInterpolatedAsync($"""
    INSERT INTO integration_inbox (id, source, external_event_id, payload, received_at)
    VALUES ({id}, {source}, {externalId}, {payload}::jsonb, now())
    ON CONFLICT (source, external_event_id) DO NOTHING
    """, ct);

if (inserted == 0) return; // já processado, encerra em silêncio
```

> **Anti-padrão crítico:** `if (await db.AnyAsync(...)) return;` seguido de `Insert`.
> Isso é race condition. Dois workers passam pelo `Any` ao mesmo tempo e ambos inserem.
> A verificação e a inserção precisam ser a mesma operação atômica.

**Nível 2 — `Idempotency-Key` para clientes externos.**
Armazene chave + hash do corpo + snapshot da resposta. Requisição repetida com mesma
chave devolve a resposta original. Chave repetida com corpo diferente → `422`.

**Nível 3 — Guardas na máquina de estados.**
Confirmar pedido já confirmado é *no-op de sucesso*, não erro. Transição inválida
retorna `Result.Failure`, nunca exceção.

---

## 5. Isolamento multi-tenant — OBRIGATÓRIO

Nunca dependa do desenvolvedor lembrar de filtrar por tenant.

**Camada 1 — Global Query Filter no EF Core:**

```csharp
modelBuilder.Entity<PricingRule>()
    .HasQueryFilter(x => x.MerchantId == _tenant.MerchantId);
```

**Camada 2 — `ITenantContext` resolvido do JWT, scoped, injetado.**
Nunca leia claim direto no endpoint.

**Camada 3 — Row Level Security no PostgreSQL** para as tabelas sensíveis
(pedido, ledger, entregador). Rede de segurança caso o filtro do EF seja
contornado por SQL cru ou Dapper.

**Camada 4 — Teste de arquitetura:** toda entidade que implementa `ITenantOwned`
precisa ter query filter registrado. Build quebra se faltar.

---

## 6. Acesso a dados e performance

### Leitura

```csharp
// Projete direto para DTO. Nunca carregue entidade completa para ler.
await _db.PricingRules
    .AsNoTracking()
    .Where(x => x.MerchantId == tenantId)
    .Select(x => new PricingRuleDto(x.Id, x.Region, x.Fee))
    .ToListAsync(ct);
```

- `AsNoTracking()` por padrão em toda leitura
- `Select` para DTO — reduz colunas trafegadas e elimina change tracking
- `AsSplitQuery()` quando houver mais de um `Include` de coleção
- Lazy loading **desabilitado** globalmente

### Rotas quentes

Compiled query para caminhos executados a cada segundo (polling, despacho):

```csharp
private static readonly Func<AppDbContext, Guid, CancellationToken, Task<Order?>> _byExternalId =
    EF.CompileAsyncQuery((AppDbContext db, Guid id, CancellationToken ct) =>
        db.Orders.FirstOrDefault(o => o.ExternalId == id));
```

**Dapper é permitido — e preferido — em dois lugares:**
1. Busca de candidatos no despacho (SQL com PostGIS, EF só atrapalha)
2. Relatórios e agregações do dashboard

Escrita continua sempre no EF Core, para manter as invariantes de domínio.

### Serialização

Use **source generator** do `System.Text.Json`. O worker de polling desserializa
continuamente — reflection ali é desperdício puro:

```csharp
[JsonSerializable(typeof(IFoodOrderPayload))]
[JsonSerializable(typeof(IFoodEvent[]))]
internal sealed partial class IFoodJsonContext : JsonSerializerContext;
```

### Índices

Todo `WHERE` de rota quente precisa de índice. Rode `EXPLAIN ANALYZE` antes do merge
em qualquer query nova de despacho, tracking ou relatório.

---

## 7. Estratégia de índices

### Princípio

Índice **nunca** é criado direto no banco. Sempre via migration, versionado no Git.
Índice fora da migration não existe: some no próximo ambiente e ninguém sabe a origem.

### Mapa de rotas quentes

Frequência estimada em operação de 50 restaurantes ativos.

#### Escopo 1 — Ingestão iFood

| Query | Frequência | Índice |
|---|---|---|
| Dedupe de evento no inbox | a cada evento | `UNIQUE (source, external_event_id)` |
| Buscar pedido por ID externo | a cada evento | `UNIQUE (source, external_order_id)` |
| Resolver merchant pelo ID do iFood | a cada evento | `UNIQUE (ifood_merchant_id)` |
| Puxar Outbox pendente | a cada 1 s | parcial `(created_at) WHERE processed_at IS NULL` |
| Cursor de polling por app | a cada 30 s | PK simples |

#### Escopos seguintes

| Query | Frequência | Índice |
|---|---|---|
| Painel de pedidos ativos (KDS) | a cada 2 s por loja | parcial `(merchant_id, created_at) WHERE status IN (...)` |
| Histórico de pedidos do dashboard | alta | `(merchant_id, created_at DESC)` |
| Candidatos a despacho | a cada pedido | GiST em `last_position` + parcial por disponibilidade |
| Entregadores vinculados à loja | a cada despacho | `(merchant_id, status)` |
| Escala ativa no momento | a cada despacho | `(merchant_id, starts_at, ends_at)` |
| Extrato do ledger | média | `(merchant_id, occurred_at DESC)` |
| Dedupe de lançamento financeiro | a cada lançamento | `UNIQUE (source_type, source_id)` |
| Histórico de posição GPS | escrita massiva | hypertable Timescale `(courier_id, time DESC)` |

### Regras de composição

**Ordem das colunas importa.** Igualdade primeiro, range e ordenação por último:

```
(merchant_id, created_at DESC)   ✅  filtra por loja, ordena por data
(created_at DESC, merchant_id)   ❌  o Postgres não consegue usar bem
```

**Índice parcial é o maior ganho barato deste sistema.** Pedido ativo é uma fração
minúscula do total, mas é o que a tela consulta o tempo todo:

```csharp
builder.HasIndex(x => new { x.MerchantId, x.CreatedAt })
       .HasFilter("status IN ('Received','Confirmed','Dispatched','InTransit')")
       .HasDatabaseName("ix_orders_active_by_merchant");
```

O índice cresce com os pedidos *abertos*, não com o histórico. Depois de um ano de
operação ele continua do mesmo tamanho.

**Covering index** quando a query lê poucas colunas — habilita index-only scan e nem
toca na tabela:

```csharp
builder.HasIndex(x => new { x.MerchantId, x.CreatedAt })
       .IncludeProperties(x => new { x.Status, x.TotalAmount });
```

### PostGIS e Timescale

O Npgsql expressa GiST direto no EF:

```csharp
builder.HasIndex(x => x.LastPosition).HasMethod("gist");
```

Hypertable do Timescale exige SQL cru na migration:

```csharp
migrationBuilder.Sql("SELECT create_hypertable('courier_positions', 'time');");
```

### Criação em produção

Migration do EF roda dentro de transação, e `CREATE INDEX CONCURRENTLY` não pode.
Para tabela grande já em produção, migration isolada com transação suprimida:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_orders_active_by_merchant ...",
        suppressTransaction: true);
}
```

Sem isso, você trava a tabela de pedidos no meio do expediente.

### O custo do outro lado

Todo índice **desacelera escrita** e ocupa disco. Não indexe por precaução.

- `courier_positions` recebe milhares de inserts por minuto: índice **só** o da
  hypertable. Nada mais.
- Tabela de auditoria e log: sem índice até existir consulta real.
- Chave estrangeira não gera índice automático no Postgres — crie apenas onde há
  join ou `DELETE CASCADE` frequente.

### Validação, não adivinhação

Este mapa é previsão. Previsão erra.

1. **`pg_stat_statements` habilitado desde o dia 1.** É o que diz a verdade sobre
   quais queries realmente dominam.
2. **Revisão mensal:** top 20 por tempo total acumulado — não por tempo médio.
   Query de 20 ms rodando 100 mil vezes pesa mais que uma de 2 s rodando dez.
3. **`EXPLAIN ANALYZE` obrigatório** no PR de qualquer query nova em despacho,
   tracking ou relatório. Cole o plano na descrição do PR.
4. **`pg_stat_user_indexes`:** índice com `idx_scan = 0` depois de um mês é peso
   morto. Remova via migration.

---

## 8. Orçamento de performance

Números, não impressões. Medidos em p95:

| Operação | Alvo |
|---|---|
| Endpoint de leitura (dashboard) | < 150 ms |
| Endpoint de escrita | < 300 ms |
| Ciclo de polling iFood | < 5 s |
| Ingestão de evento → pedido persistido | < 2 s |
| Decisão de despacho (candidatos + score) | < 500 ms |
| Ingestão de ping GPS | < 50 ms |

Excedeu o alvo em produção: vira issue de prioridade alta, não backlog.

---

## 9. Testes

| Camada | Ferramenta | O que cobre |
|---|---|---|
| Domínio | xUnit puro, sem mock | Máquina de estados, scoring, regras de taxa |
| Mapper / ACL | xUnit + fixtures reais | Payload iFood → domínio |
| Integração | Testcontainers (Postgres + Redis) | Idempotência, query filter, migrations |
| Integração externa | WireMock.NET | Polling, retry, timeout, 429 |
| Arquitetura | NetArchTest | Isolamento de camadas |

### Testes de arquitetura obrigatórios

```csharp
// Domain não referencia Infrastructure
// Nenhum tipo de Integrations.IFood.Contracts aparece em Domain ou Application
// Toda ITenantOwned tem query filter
```

Rodam no CI. Quebram o build.

---

## 10. Segurança e LGPD

- Segredos em User Secrets (dev) e variável de ambiente (produção). Nunca em `appsettings.json`.
- CNH, selfie e documento de entregador: criptografados em repouso, com log de acesso.
- Restaurante **nunca** define credencial de entregador. Fluxo é convite com token e TTL.
- Nenhum dado pessoal em log. Nenhum ID de pedido ou CPF em query string.
