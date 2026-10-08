#!/usr/bin/env bash
# Sobe todo o ambiente local do DeliveryHub em um comando:
#   Postgres + Redis (Docker) -> migrations -> API (+ worker embutido) -> frontend
# Ctrl+C para a API e o frontend. Postgres/Redis seguem no Docker.
set -euo pipefail
cd "$(dirname "$0")"

DOTNET="${DOTNET:-dotnet}"
export ASPNETCORE_ENVIRONMENT="Development"
# Credenciais batem com o docker-compose.yml. Env vence user-secrets, então o
# ambiente sobe igual em qualquer máquina, sem depender de secret local.
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=deliveryhub;Username=deliveryhub;Password=deliveryhub_dev"
export ConnectionStrings__Redis="localhost:6379"
export Worker__HealthUrl="http://localhost:5010"

# Não deixe o Vite iniciar apontando para uma API antiga que já ocupa a porta.
# Isso costuma acontecer após mudar o TargetFramework/SDK: o navegador continua
# atendido, mas as rotas novas respondem 404 até reiniciar o processo anterior.
if command -v lsof >/dev/null 2>&1 && lsof -nP -iTCP:5300 -sTCP:LISTEN >/dev/null 2>&1; then
  echo "ERRO: já existe um processo ouvindo em http://localhost:5300."
  lsof -nP -iTCP:5300 -sTCP:LISTEN
  echo "Pare o processo antigo com Ctrl+C no terminal que o iniciou e execute ./dev.sh novamente."
  exit 1
fi

pids=()
cleanup() {
  echo ""
  echo "==> Encerrando API e frontend..."
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
  wait 2>/dev/null || true
  echo "==> Parados. Postgres/Redis continuam no Docker (docker compose stop para parar)."
}
trap cleanup INT TERM

echo "==> 0/4  Verificando Docker..."
if ! docker info >/dev/null 2>&1; then
  echo "    Docker offline. Abrindo o Docker Desktop..."
  open -a Docker 2>/dev/null || true
  echo -n "    Aguardando o daemon responder"
  until docker info >/dev/null 2>&1; do
    echo -n "."
    sleep 2
  done
  echo " pronto."
fi

echo "==> 1/4  Subindo Postgres + Redis (Docker)..."
docker compose up -d

echo "==> Aguardando Postgres ficar pronto..."
until docker compose exec -T postgres pg_isready -U deliveryhub -d deliveryhub >/dev/null 2>&1; do
  sleep 1
done
echo "    Postgres pronto."

echo "==> 2/4  Aplicando migrations..."
$DOTNET tool restore
$DOTNET ef database update \
  --project src/DeliveryHub.Infrastructure \
  --startup-project src/DeliveryHub.Api

echo "==> 3/4  Compilando e subindo API (+ worker) em http://localhost:5300 ..."
$DOTNET build DeliveryHub.sln --nologo -v:q
$DOTNET run --project src/DeliveryHub.Api --no-build --urls http://localhost:5300 &
pids+=($!)

echo "==> 4/4  Subindo frontend (Vite) em http://localhost:5273 ..."
( cd web && { [ -d node_modules ] || npm install; } && npm run dev ) &
pids+=($!)

echo ""
echo "======================================================="
echo "  Ambiente no ar:"
echo "   API ............ http://localhost:5300"
echo "   Worker health .. http://localhost:5010/health"
echo "   Frontend ....... http://localhost:5273"
echo "   Postgres ....... localhost:5432    Redis: localhost:6379"
echo ""
echo "  Ctrl+C para parar API + frontend."
echo "======================================================="
wait
