# OMAYA.md

## Resumo
ToolsDelivery/DeliveryHub é uma plataforma multi-tenant de operação de pedidos e entregas para restaurantes, com API .NET, worker de ingestão, frontend React/Vite, rastreamento em tempo real e integrações com marketplaces/WhatsApp.

## Stack
- Backend: .NET 9 nos projetos atualmente observados; `global.json` exige SDK `9.0.121`.
- API: ASP.NET Core Minimal APIs, JWT Bearer, TypedResults, SignalR.
- Aplicação: casos de uso e abstrações sem dependência de infraestrutura.
- Domínio: agregados e regras de negócio em C# (`Pedido`, `Merchant`, `Courier`, `Usuario`).
- Infraestrutura: EF Core/Npgsql, PostgreSQL, Redis, PostGIS/TimescaleDB via Docker.
- Integrações: iFood, 99Food/DiDiFood, WhatsApp e geocodificação de endereços.
- Frontend: React 19, TypeScript, Vite, React Router, Tailwind CSS v4, `@dnd-kit`, SignalR JS, MapLibre.
- Testes: xUnit, coverlet e NetArchTest.

## Entradas para ler primeiro
1. [src/DeliveryHub.Api/Program.cs](src/DeliveryHub.Api/Program.cs) — composição da API, autenticação, DI, endpoints, SignalR e worker embutido.
2. [src/DeliveryHub.Domain/Orders/Pedido.cs](src/DeliveryHub.Domain/Orders/Pedido.cs) — máquina de estados e invariantes do pedido.
3. [src/DeliveryHub.Application/Orders/AvancarPedido.cs](src/DeliveryHub.Application/Orders/AvancarPedido.cs) — orquestra transições e notificação da origem/painel.
4. [src/DeliveryHub.Infrastructure/Persistence/AppDbContext.cs](src/DeliveryHub.Infrastructure/Persistence/AppDbContext.cs) — persistência e filtros multi-tenant.
5. [web/src/pedidos/PainelOperacao.tsx](web/src/pedidos/PainelOperacao.tsx) — composição da operação no frontend.

## Organização importante
- `src/DeliveryHub.Domain`: regras puras; não deve depender de Application, Infrastructure ou API.
- `src/DeliveryHub.Application`: interfaces, casos de uso, DTOs e contratos; coordena repositórios/integradores.
- `src/DeliveryHub.Infrastructure`: EF, Redis, integrações externas, identidade, SignalR e segurança.
- `src/DeliveryHub.Api`: Minimal APIs organizadas por contexto em `*Endpoints.cs`.
- `src/DeliveryHub.Worker`: polling/ingestão de marketplace e health endpoint; também pode ser embutido pela API.
- `web/src/pedidos`: Kanban, card de pedido, mapa, polling, SignalR e rastreamento do entregador.
- `web/src/components`: primitives visuais, incluindo `Cartao`, `Botao`, `Etiqueta` e ícones.
- `web/src/styles/theme.css`: fonte única de tokens visuais, cores, tipografia, raios e sombras.

## Convenções backend
- Preferir Minimal API + `MapGroup` + método de extensão por contexto; não criar Controllers.
- Usar handlers nomeados, `TypedResults` e `CancellationToken` em operações assíncronas.
- Usar `Result`/`Result<T>` para falhas esperadas de negócio; exceções ficam para falhas inesperadas/infra.
- Erros de domínio são constantes com código estável, mensagem humana e `ErrorType`; endpoints convertem para ProblemDetails.
- Escrever via EF/repositório e ler projetando DTOs; leituras devem usar `AsNoTracking` quando aplicável.
- Idempotência deve ser garantida por constraint/operção atômica no banco e por guardas no domínio.
- Nunca confiar em `merchantId` recebido pelo cliente: o tenant vem do JWT/`ITenantContext` e endpoints validam acesso explícito.
- `TenantContextHttp` é usado na API; `TenantContextSistema` só no worker, que opera entre tenants.
- Não alterar assinatura de interface/função sem localizar e atualizar todos os callers.

## Contratos de resposta
- API usa `ProblemDetails`: `title` é mensagem humana e `detail` é o código estável do erro.
- Enums HTTP são serializados como strings via `JsonStringEnumConverter`.
- Frontend centraliza chamadas em [web/src/lib/api.ts](web/src/lib/api.ts), usando `ErroDaApi`.
- Rotas de mutação de pedido representam ações (`/confirmar`, `/pronto`, `/despachar`), não aceitam status arbitrário no body.
- Pedido nasce em `Recebido` e avança de forma monotônica; transições repetidas/atrasadas devem ser seguras.

## Convenções frontend
- Usar componentes existentes e tokens de `theme.css`; não inventar cores/raios/tipografia diretamente sem necessidade.
- `CartaoPedido.tsx` já possui expansão/recolhimento, drag-and-drop, origem, pagamento, endereço, mapa, alocação, cancelamento e ações de estado.
- O card recolhido prioriza número, origem, cliente e total; detalhes de itens/pagamento/endereço ficam na expansão.
- `CartaoPedido` usa `@dnd-kit`; manter botões e áreas interativas fora da disputa com listeners de arraste.
- `ColunaPedidos` distribui sete colunas com `min-w-0`, `basis-44` e rolagem interna nas colunas configuradas.
- `Botao` possui variantes de ação por etapa: confirmar, iniciar preparo, marcar pronto e despachar.
- O rastreamento web usa `useEnviarPosicao`; Wake Lock é proteção complementar, não requisito do GPS.
- Heartbeat de posição usa `ehHeartbeat`; no backend atualiza o cache, mas não grava novo ponto histórico.

## Escopo visual do card pedido / referência Figma
O print mostra um card escuro e compacto com: cabeçalho contendo número e origem, chevron; bloco de cliente; itens com quantidade/nome/preço; resumo de quantidade e pagamento; total destacado; endereço; tempo decorrido; vínculo do entregador; CTA principal largo e menu de ações.

Para implementar esse modelo no card existente:
1. Preservar `CartaoPedido` como ponto único de comportamento e não duplicar regras no `ColunaPedidos`.
2. Reorganizar o JSX em blocos visuais explícitos: header, cliente/itens, pagamento/total, endereço/meta e footer de ação.
3. Manter a expansão como estado local e acessível via teclado; o resumo precisa continuar clicável sem quebrar drag-and-drop.
4. Reusar `SeloOrigem`, `ChipTempoDecorrido`, `Botao`, `IconeLocal`, tokens de `theme.css` e `formatarDinheiro`.
5. Usar `pedido.itens`, `pedido.pagamentoDescricao`, `pedido.pagamentoValorACobrar`, `pedido.enderecoResumido`, `pedido.entregadorNome` e `pedido.status` como fonte de dados; não criar dados visuais paralelos.
6. O CTA deve continuar derivado de `proximoPasso`/`VARIANTE_ACAO`; o menu `...` deve conter apenas ações existentes ou explicitamente solicitadas, sem criar comportamento fictício.
7. Comparar desktop, card estreito e visão do entregador; manter truncamento de endereço/cliente e ações com alvo de toque adequado.
8. Se a referência exigir novos ícones, adicionar ícones isolados em `web/src/components/icones`, seguindo o estilo existente.

## Comandos
### Frontend
```bash
cd web
npm install
npm run dev       # Vite em http://localhost:5273
npm run build     # tsc -b && vite build
npm run lint      # oxlint
npm run preview   # preview em http://localhost:4273
```
O proxy do Vite encaminha `/api` e `/hubs` para `http://localhost:5192`, conforme `web/vite.config.ts`. A API precisa estar nessa porta para login/SignalR funcionarem.

### Backend
```bash
dotnet restore DeliveryHub.sln
dotnet build DeliveryHub.sln
dotnet test DeliveryHub.sln
 dotnet run --project src/DeliveryHub.Api --launch-profile http
```
O perfil HTTP observado usa `http://localhost:5192`; HTTPS também está configurado em `launchSettings.json`. Se o SDK do `global.json` não estiver instalado, não alterar silenciosamente o target: resolver o SDK/ambiente primeiro.

### Infra local
```bash
docker compose up -d
```
Isso inicia PostgreSQL Timescale/PostGIS e Redis. O backend exige `ConnectionStrings:Default` e configurações de integração/JWT por appsettings, User Secrets ou ambiente.

## Gotchas
- O workspace contém artefatos `bin/`/`obj/`; não tratar assemblies gerados como fonte de verdade.
- Há divergência histórica entre documentação/comentários que citam `CLAUDE.md` e o guia efetivo `Docs/ENGINEERING-GUIDE.md`.
- A API pode subir o worker embutido e também existe worker standalone; evitar executar ambos para o mesmo polling.
- O isolamento tenant é crítico: `IgnoreQueryFilters`, SQL cru e rotas com `merchantId` exigem justificativa e validação.
- O cache Redis de posições expira em cinco minutos; ausência de posição não significa necessariamente que o entregador parou.
- Heartbeat renova a saúde do cache sem adicionar pontos ao histórico da rota.
- A URL `/api` do frontend é relativa; erros Vite `ECONNREFUSED` normalmente significam API parada ou porta do proxy divergente.
- O arquivo `README.md` raiz é mínimo; este documento e `Docs/ENGINEERING-GUIDE.md` são as referências de engenharia mais úteis.
- Não expor tokens, senhas ou credenciais em logs, respostas adicionais ou documentação.
