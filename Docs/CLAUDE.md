# CLAUDE.md — DeliveryHub

> Este arquivo é lido automaticamente em toda sessão do Claude Code.
> Leia também `ENGINEERING-GUIDE.md` na raiz antes de escrever qualquer código.

---

## 1. O que estamos construindo

Plataforma SaaS multi-tenant de gestão logística de delivery para pequenos e médios
restaurantes. Recebe pedidos do iFood, centraliza a operação de entrega, faz despacho
automático para entregadores e rastreia a entrega em tempo real.

**Não é** um marketplace. **Não é** app de consumidor final. É ferramenta de gestão
para o lojista.

### Fluxo central

```
iFood (e futuras origens)
        ↓ API
   Ingestão / ACL
        ↓
     Pedido
        ↓
    Despacho
        ↓
    Entregador
        ↓
 Coleta → Entrega → Finalização
```

---

## 2. Como trabalhamos — regra inegociável

Nenhum escopo entra na `main` sem os 6 passos fechados, nesta ordem:

1. **Contrato** — o que entra, o que sai, qual máquina de estados
2. **Backend** — implementado contra o ambiente real (sandbox iFood)
3. **Validação viva** — fluxo ponta a ponta rodando de verdade
4. **Fixtures** — payloads reais capturados em `tests/fixtures/`
5. **Testes** — unitário no domínio + integração usando os fixtures do passo 4
6. **Frontend** do escopo

**Não pule passos. Não comece o escopo seguinte com o anterior incompleto.**
Se eu pedir algo fora do escopo atual, me lembre desta regra antes de implementar.

O passo 4 é o que separa teste honesto de teste inventado: não escreva asserção sobre
o payload do iFood antes de ter o JSON real em mãos.

---

## 3. Stack travada

| Camada | Escolha |
|---|---|
| Backend | .NET 9, monólito modular |
| Banco | PostgreSQL 16 + PostGIS |
| Cache / locks / geo em tempo real | Redis |
| Série temporal (GPS) | TimescaleDB (extensão do Postgres) |
| Tempo real | SignalR |
| Frontend | Vite + React + TypeScript (SPA) |
| Mobile | Capacitor sobre a mesma base web |
| Infra | Railway (MVP) |

### Decisões já tomadas — não reabrir sem me consultar

- **Monólito modular, não microserviços.** Time pequeno. Extraímos serviço quando um
  contexto provar que precisa escalar sozinho (será o Tracking).
- **Sem MongoDB.** Postgres com `jsonb` cobre o caso e evita segunda operação.
- **Vite, não Next.js.** Capacitor empacota estáticos; não há servidor Node no
  dispositivo. Server Components e API Routes não funcionam lá.
- **Autenticação por Bearer token, nunca cookie.** Precisa funcionar em origem
  `capacitor://`. Cookie `SameSite` quebra no app.
- **App iFood do tipo Centralizado**, não Distribuído. Centralizado é o modelo SaaS:
  uma credencial, N lojas, e webhook só existe nessa modalidade.
- **Roteirização com OSRM self-hosted.** Google/Mapbox só na navegação final do
  motoboy. Custo de API destrói margem em SMB.

---

## 4. Estrutura da solução

```
DeliveryHub/
├── CLAUDE.md
├── ENGINEERING-GUIDE.md
├── src/
│   ├── DeliveryHub.Domain/              # zero dependências externas
│   │   ├── Orders/
│   │   ├── Couriers/
│   │   ├── Merchants/
│   │   └── SharedKernel/                # ValueObjects, Result, DomainEvent
│   ├── DeliveryHub.Application/
│   │   ├── Orders/
│   │   └── Abstractions/                # IOrderSource, IFleetProvider
│   ├── DeliveryHub.Infrastructure/
│   │   ├── Persistence/                 # EF Core, Outbox
│   │   └── Integrations/
│   │       └── IFood/
│   │           ├── Auth/
│   │           ├── Polling/
│   │           ├── Contracts/           # DTOs do iFood — NÃO VAZAM
│   │           └── IFoodOrderMapper.cs  # anti-corruption layer
│   ├── DeliveryHub.Api/
│   └── DeliveryHub.Worker/              # polling em processo separado
├── web/                                 # Vite + React
└── tests/
    ├── DeliveryHub.Domain.Tests/
    ├── DeliveryHub.Integration.Tests/
    └── fixtures/ifood/
```

### Regra de isolamento

Nenhum tipo de `Integrations/IFood/Contracts/` pode aparecer em `Domain` ou
`Application`. O domínio conhece `Pedido` e `Entrega` — nunca `IFoodOrderPayload`.

Crie teste de arquitetura (NetArchTest) validando isso **no primeiro commit**.
O build quebra se alguém esquecer.

---

## 5. Duas portas, não uma

O núcleo não conhece "iFood". Conhece abstrações:

```csharp
IOrderSource      // origem de pedido: iFood, 99Food, Anota AI, WhatsApp, PDV próprio
IFleetProvider    // frota: própria, Uber Direct, Lalamove
```

iFood é origem de pedido. Uber Direct é fornecedor de frota. São fluxos opostos —
não trate como o mesmo tipo de integração.

---

## 6. Modelo de domínio — pontos críticos

### Entregador não pertence ao restaurante

```csharp
Courier {                    // identidade global, chave natural: CPF
  Id, Cpf, Nome, Telefone,
  Cnh, Veiculo, StatusCadastral, StatusVerificacaoDocumentos
}

CourierMerchantLink {        // o vínculo
  CourierId, MerchantId,
  Tipo,                      // Dedicado | Avulso | Regional
  Status,                    // Convidado | Ativo | Suspenso
  VinculadoEm, VinculadoPor
}
```

Motivo: o mesmo motoboy trabalha para vários restaurantes sem cadastro duplicado, o
documento é verificado uma única vez, e o modelo aberto futuro (motoboy avulso da
região) entra como `Tipo` novo sem tocar no motor de despacho.

### Restaurante nunca cria credencial de entregador

```
Restaurante cadastra CPF/telefone
        ↓
Sistema cria Courier (ou reusa se o CPF já existe)
        ↓
Convite por SMS/WhatsApp, token com TTL
        ↓
Motoboy define a própria senha
        ↓
Link fica Ativo
```

Segurança e LGPD. Não implemente de outra forma.

### Isolamento multi-tenant — 4 camadas

1. Global Query Filter no EF Core por `MerchantId`
2. `ITenantContext` scoped, resolvido do JWT — nunca ler claim direto no endpoint
3. Row Level Security no Postgres nas tabelas sensíveis
4. Teste de arquitetura: toda `ITenantOwned` tem query filter registrado

No modelo Centralizado o iFood entrega os eventos de todos os clientes num fluxo só.
Quem separa é o nosso código. Isso não é exagero.

---

## 7. ESCOPO ATUAL — Escopo 1: ingestão de pedidos iFood

**Só isso. Nada de despacho, nada de entregador, nada de frontend.**

### Credenciais

```bash
dotnet user-secrets set "IFood:ClientId" "<clientId>"
dotnet user-secrets set "IFood:ClientSecret" "<clientSecret>"
```

App de teste tipo **Centralizado**. Módulos habilitados: Order, Events, Merchant.
Nunca commitar secret. Nunca logar secret.

### Entregas

- Autenticação OAuth `client_credentials`, com renovação derivada do `expiresIn` de
  cada resposta — nunca de tempo fixo. O iFood avisa que pode mudar os prazos a
  qualquer momento; hoje o access token dura 3h. Apps centralizados não recebem
  refresh token — apenas re-autenticar.
- Worker de polling com heartbeat de 30s
- Acknowledgment **depois** de persistir, nunca antes
- Inbox idempotente com constraint única `(source, external_event_id)`
- Busca de detalhe do pedido
- ACL: `IFoodOrderPayload` → `Pedido`
- Persistência + Outbox
- Máquina de estados do pedido e confirmação de volta ao iFood

### Armadilhas conhecidas — trate desde o início

**Token e onboarding.** Sempre que o app recebe permissão de um merchant novo, é
preciso solicitar um access token novo — o token em cache não enxerga a loja nova.
Onboarding deve invalidar o cache e confirmar listando os merchants. A permissão leva
até 10 minutos para propagar do lado do iFood: se o merchant não aparecer na listagem,
aguardar e gerar outro token antes de tratar como erro.

**Polling é heartbeat.** Sem requisição regular a cada 30s a loja é marcada offline e
para de receber pedidos. O worker não pode ser função serverless com cold start.

**Desacople ingestão de processamento.** Grave no inbox rápido, reconheça, processe em
background. Processar antes de reconhecer estoura a janela de 30s com volume alto.

**Idempotência é constraint de banco, não `if`.**

```csharp
// ERRADO — race condition
if (await db.Inbox.AnyAsync(x => x.ExternalId == id)) return;
await db.Inbox.AddAsync(...);

// CERTO
INSERT ... ON CONFLICT (source, external_event_id) DO NOTHING
```

### Definition of Done

- [ ] Pedido gerado no sandbox aparece no banco em menos de 30s
- [ ] Confirmação enviada muda o status no Gestor de Pedidos do iFood
- [ ] Evento duplicado não cria pedido duplicado (teste explícito)
- [ ] Worker reinicia sem perder posição do polling
- [ ] Fixtures de todos os tipos de evento capturados
- [ ] Testes: mapper e máquina de estados unitários; polling com WireMock
- [ ] Métrica "último polling bem-sucedido" exposta
- [ ] Teste de arquitetura rodando no CI

---

## 8. Convenções de código

Detalhe completo em `ENGINEERING-GUIDE.md`. Resumo do que mais aparece:

- **Minimal API** com `MapGroup` + extension method por contexto. Sem controllers.
- **Métodos nomeados**, não lambdas inline nos endpoints.
- **`TypedResults`**, não `Results`.
- **`Result<T>` para falha de negócio; exceção só para o inesperado.** `try/catch` por
  endpoint é proibido — existe um `IExceptionHandler` global.
- **`CancellationToken` em toda assinatura async.** Sem exceção.
- **Policy-based authorization** (`Policies.X`), nunca `Roles = "STRING"`.
- **Leitura:** `AsNoTracking()` + projeção para DTO. Nunca carregar entidade completa
  para ler.
- **Serialização:** source generator do `System.Text.Json` no worker de polling.
- **Índices sempre via migration**, nunca criados à mão no banco.
- **Dapper permitido** em busca de candidatos do despacho e em relatórios. Escrita
  sempre no EF Core.

### Orçamento de performance (p95)

| Operação | Alvo |
|---|---|
| Endpoint de leitura | < 150 ms |
| Endpoint de escrita | < 300 ms |
| Ciclo de polling | < 5 s |
| Evento → pedido persistido | < 2 s |
| Decisão de despacho | < 500 ms |
| Ingestão de ping GPS | < 50 ms |

---

## 9. Roadmap — não antecipar

1. **Identity + Tenancy + RBAC**
2. **Merchant + Courier** (cadastro, escalas, taxas por região)
3. **Order** + máquina de estados + Outbox
4. **Adapter iFood** ← *estamos aqui, começando por este*
5. **Dispatch** com simulador antes de produção
6. **Tracking** + SignalR
7. **Routing** (OSRM)
8. **Ledger** + dashboard de métricas
9. **Uber Direct** como frota de overflow

O motor de despacho precisa de oferta com TTL e reserva do entregador, senão gera
corrida duplicada em pico. É classe de domínio pura, testável, sem I/O.

O Ledger é append-only, partidas dobradas. Nunca `UPDATE saldo`.

---

## 10. O que NÃO fazer

- Não crie abstração para problema que ainda não existe
- Não instale pacote sem me perguntar
- Não implemente Catalog, Shipping ou Financial do iFood agora — cada módulo
  habilitado vira critério de homologação depois
- Não use `localStorage` para token (quebra no Capacitor)
- Não escreva teste que só repete a implementação
- Não gere código de escopo futuro "já que estamos aqui"
- Não coloque dado pessoal em log, URL ou query string
- Não use vermelho na identidade visual (conflita com o iFood)

---

## 11. Identidade visual

Índigo `#4F46E5`, lima `#A3E635`. Arquivos em `assets/logo/`.
