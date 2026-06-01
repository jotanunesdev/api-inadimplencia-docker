# Load Tests com k6 — api-inadimplencia

Suite de testes de carga (smoke / load / stress / spike) usando [k6](https://k6.io).
Os scripts ficam em `loadtests/k6/` e podem ser executados localmente (binário k6) ou
via Docker (sem instalar nada).

> Antes de tudo: **load test deve rodar em ambiente isolado** (DEV ou um container
> dedicado). Nunca rode contra produção sem combinar com o time. A API faz
> chamadas externas (Fluig, RM, Serasa) que podem gerar efeitos colaterais.

---

## 1. Pré-requisitos (você faz)

### 1.1. Ambiente da API
1. Subir a API e dependências:
   ```powershell
   docker compose up -d --build
   ```
2. Verificar saúde:
   ```powershell
   curl http://localhost:8080/health
   curl http://localhost:8080/inadimplencia/health
   ```
3. **Recomendado para load test**: desabilitar autenticação para focar em performance
   pura dos handlers (ou gerar um token válido — ver passo 2).
   No `.env`:
   ```env
   INAD_REQUIRE_AUTHENTICATED=false
   ```
   Reinicie: `docker compose up -d`.

   > Se quiser manter auth ligada, gere um Bearer token via `/inadimplencia/session/entra/login`
   > e exporte em `K6_BEARER_TOKEN` (passo 3.2).

### 1.2. Instalar k6
Escolha **uma** das opções:

**Opção A — binário nativo (Windows, recomendado para resultados mais precisos):**
```powershell
winget install k6 --source winget
# valida
k6 version
```

**Opção B — Docker (sem instalar nada):**
```powershell
docker pull grafana/k6:latest
```

### 1.3. Massa de dados
Os testes batem em endpoints reais que consultam o SQL Server. Garanta que existem:
- pelo menos 1 inadimplência por CPF de teste;
- pelo menos 1 `numVenda` válido;
- pelo menos 1 `nomeCliente` e `responsavel` retornando dados.

Edite `loadtests/k6/data/test-data.json` com valores que existem no banco do seu
ambiente. Sem dados, os endpoints retornam 200 com lista vazia (ainda servem para
medir latência) — mas o ideal é stress real.

---

## 2. Variáveis de ambiente

Os scripts leem estas variáveis (todas opcionais com defaults sensatos):

| Variável            | Default                    | Descrição                                   |
| ------------------- | -------------------------- | ------------------------------------------- |
| `K6_BASE_URL`       | `http://localhost:8080`    | URL base da API                             |
| `K6_BEARER_TOKEN`   | *(vazio)*                  | Token Bearer p/ endpoints autenticados      |
| `K6_VUS`            | depende do script          | Override de VUs                             |
| `K6_DURATION`       | depende do script          | Override de duração                         |

Exemplo PowerShell:
```powershell
$env:K6_BASE_URL = "http://localhost:8080"
$env:K6_BEARER_TOKEN = "eyJhbGciOi..."
```

---

## 3. Executando os testes

Cada script representa um **perfil** de teste. Rode na ordem:
`smoke` → `load` → `stress` → `spike`.

### 3.1. Smoke (sanity check, ~1 min, 1 VU)
Valida que os endpoints respondem e os thresholds básicos passam.
```powershell
k6 run loadtests/k6/smoke.js
```

### 3.2. Load (carga normal, ~5 min, ramp até 20 VUs)
Mede performance sob carga esperada de produção.
```powershell
k6 run loadtests/k6/load.js
```

### 3.3. Stress (~10 min, ramp até 100 VUs)
Encontra o ponto de quebra do sistema.
```powershell
k6 run loadtests/k6/stress.js
```

### 3.4. Spike (~3 min, picos súbitos até 200 VUs)
Avalia recuperação após picos abruptos.
```powershell
k6 run loadtests/k6/spike.js
```

### 3.5. Via Docker (se não instalou k6)
Use `--network host` no Linux ou `host.docker.internal` no Windows:
```powershell
docker run --rm -i `
  -e K6_BASE_URL=http://host.docker.internal:8080 `
  -v ${PWD}/loadtests/k6:/scripts `
  grafana/k6 run /scripts/smoke.js
```

---

## 4. Relatórios

Cada execução gera:
- **Console**: resumo agregado (p95, p99, error rate, RPS).
- **JSON**: `loadtests/k6/results/<script>-<timestamp>.json` (raw métricas).
- **HTML** (opcional): instale o reporter:
  ```powershell
  # gerado automaticamente em loadtests/k6/results/<script>-summary.html
  ```

### 4.1. Dashboard Grafana (opcional, recomendado)
Suba a stack auxiliar (InfluxDB + Grafana com dashboard pronto):
```powershell
docker compose -f loadtests/docker-compose.k6.yml up -d
```
- Grafana: http://localhost:3001 (admin / admin)
- Dashboard ID importado: **2587** (k6 Load Testing Results).

Rode o k6 enviando métricas pro Influx:
```powershell
k6 run --out influxdb=http://localhost:8086/k6 loadtests/k6/load.js
```

Para derrubar a stack:
```powershell
docker compose -f loadtests/docker-compose.k6.yml down -v
```

---

## 5. Thresholds (critérios de aceite)

Os scripts já vêm com thresholds. Falha = exit code != 0 (útil em CI):

| Métrica                    | Smoke   | Load    | Stress  |
| -------------------------- | ------- | ------- | ------- |
| `http_req_failed`          | < 1%    | < 1%    | < 5%    |
| `http_req_duration` p95    | < 500ms | < 800ms | < 2s    |
| `http_req_duration` p99    | < 1s    | < 1.5s  | < 4s    |

Ajuste em `loadtests/k6/lib/config.js` conforme SLO do seu negócio.

---

## 6. Depois do teste (você faz)

1. **Salvar evidências**: copie `results/` para um local versionado (S3, drive...).
2. **Comparar baselines**: rode novamente após qualquer mudança de perf-sensitive
   (ex: adição de índice, mudança de query, upgrade de runtime).
3. **Restaurar config**: se desabilitou auth no passo 1.1.3, reverta `INAD_REQUIRE_AUTHENTICATED=true`.
4. **Limpar containers de apoio**:
   ```powershell
   docker compose -f loadtests/docker-compose.k6.yml down -v
   ```
5. **Verificar logs da API** durante o teste:
   ```powershell
   docker logs -f api-inadimplencia
   ```
   Procure por exceptions, timeouts de DB, throttling do RabbitMQ.

---

## 7. Estrutura

```
loadtests/
├── README.md                       # este arquivo
├── docker-compose.k6.yml           # InfluxDB + Grafana
└── k6/
    ├── smoke.js                    # 1 VU, 1 min
    ├── load.js                     # ramp 0→20 VUs, 5 min
    ├── stress.js                   # ramp 0→100 VUs, 10 min
    ├── spike.js                    # spike 0→200 VUs, 3 min
    ├── data/
    │   └── test-data.json          # CPFs/numVendas válidos (você edita)
    ├── lib/
    │   ├── config.js               # base URL, headers, thresholds
    │   └── checks.js               # helpers de validação
    └── scenarios/
        ├── public-endpoints.js     # health, contracts (sem auth)
        ├── inadimplencia-read.js   # GETs autenticados
        └── proximas-acoes.js       # listagem próximas ações
```
