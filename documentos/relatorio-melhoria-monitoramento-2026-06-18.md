# Relatório de melhoria orientado por monitoramento

Data: 2026-06-18

## Escopo analisado

Este relatório foi gerado a partir dos artefatos de observabilidade disponíveis no repositório:

- resultados de carga em `loadtests/k6/results/*` de 31/05/2026;
- instrumentação e endpoints de observabilidade em `api-inadimplencia.Api/Program.cs`;
- auditoria própria de tráfego em `API_TRAFFIC_AUDIT` consumida por `SqlServerTrafficAnalyticsQuery`;
- middleware e pipeline de persistência de monitoramento.

Observação: não há dump de produção do `API_TRAFFIC_AUDIT`, export de Prometheus nem snapshot de Grafana versionados no repositório. As conclusões abaixo se apoiam principalmente nos testes de carga e na implementação atual da observabilidade.

## Resumo executivo

- Até `20` usuários simultâneos a API opera com folga.
- A partir de `100` usuários simultâneos a API entra em degradação visível.
- Em `200` usuários simultâneos a degradação deixa de ser apenas performance e passa a ser indisponibilidade parcial.
- Existem falhas funcionais ou inconsistências de contrato em endpoints específicos de leitura, além da saturação por carga.
- A observabilidade atual mede volume, erro e duração média, mas ainda não mede bem causa raiz de latência alta.

## Evidências objetivas

### 1. Capacidade saudável em baixa e média carga

Com base nos JSONs do k6:

- `smoke`: `120` requests, `0%` de erro, `avg 2.32 ms`, `p95 4.14 ms`, `p99 5.80 ms`.
- `load`: `4282` requests, `0%` de erro, `avg 2.37 ms`, `p95 3.81 ms`, `p99 11.29 ms`.

Conclusão:

- o cenário nominal está muito bom;
- a aplicação não tem gargalo perceptível até a faixa de uso normal testada.

### 2. Degradação clara sob pressão

No `stress-2026-05-31T14-04-41-976Z.json`:

- `82434` requests;
- `136.9 RPS`;
- `27.27%` de falha HTTP;
- `avg 372.74 ms`;
- `p95 1919.40 ms`;
- `p99 2909.19 ms`;
- tempo máximo de request `6748.28 ms`.

No `spike-2026-05-31T14-15-51-201Z.json`:

- `24189` requests;
- `141.6 RPS`;
- `27.57%` de falha HTTP;
- `avg 797.31 ms`;
- `p95 2706.45 ms`;
- `p99 24468.63 ms`;
- tempo máximo de request `41945.75 ms`.

Conclusão:

- o ponto de quebra real está próximo de `137-142 RPS`;
- a aplicação não absorve rajadas abruptas;
- a fila interna e/ou o banco passam a trabalhar em saturação e o tail latency explode.

### 3. Endpoints com maior indício de problema funcional

Nos cenários de leitura do k6 há falhas recorrentes além da latência:

- `GET /inadimplencia/num-venda/{numVenda}` falha em corpo/content-type em alta proporção nos testes de stress e spike;
- `GET /proximas-acoes/` e `GET /proximas-acoes/{numVenda}` passam em `status ok`, mas falham totalmente em `tem corpo` e `content-type json`;
- `GET /negativacao/vendas/{numVenda}/dividas` e `GET /negativacao/solicitacoes` também passam a falhar sob pressão.

Observação importante:

- o `checkRead` do k6 aceita `200` ou `404` como funcional;
- por isso, parte dessas falhas pode significar `404` sem payload JSON e não necessariamente exceção do servidor.

Mesmo assim, isso continua sendo um problema de software ou contrato porque:

- o comportamento não está uniforme entre endpoints equivalentes;
- o monitoramento atual não diferencia claramente `404 esperado`, `404 por ausência de dado`, `404 por rota` e `503 por degradação`.

## Diagnóstico técnico

### 1. Gargalo principal provável: acesso a banco sob concorrência

Indícios:

- o relatório executivo já aponta saturação ao consultar o banco;
- o aumento forte está concentrado em `http_req_waiting`, não em conexão de rede;
- os testes públicos leves continuam bons enquanto endpoints de leitura ricos degradam mais;
- a auditoria analítica prioriza endpoints lentos e com erro, o que sugere que esse tipo de investigação já era esperado no desenho.

Hipótese principal:

- consultas pesadas sem paginação e sem cache estão pressionando SQL Server;
- sob concorrência, a API mantém requests abertas por tempo demais, o que piora pool de conexão, thread pressure e fila.

### 2. Observabilidade útil, mas ainda insuficiente para causa raiz

Hoje o sistema já possui:

- `Serilog`;
- `OpenTelemetry` para tracing e métricas HTTP;
- `/health` e `/metrics`;
- auditoria assíncrona em banco via `RequestMonitoringMiddleware`.

Mas faltam sinais críticos:

- percentis `p95/p99` persistidos na auditoria própria;
- métricas por dependência externa;
- correlação por consulta SQL, client HTTP e integração;
- distinção explícita entre erro funcional, erro técnico e resposta vazia esperada.

### 3. Risco de perder monitoramento exatamente no pico

O pipeline de auditoria usa um channel com capacidade fixa e `TryWrite`. Quando enche, o registro é descartado.

Impacto:

- nos piores momentos, quando a API mais precisa ser observada, parte da telemetria pode sumir;
- isso reduz a capacidade de explicar a causa do incidente depois.

## Melhorias propostas

## Prioridade P0

### 1. Paginar e limitar endpoints de listagem pesada

Aplicar primeiro em:

- `GET /inadimplencia/`
- `GET /proximas-acoes/`
- qualquer endpoint de dashboard/listagem que hoje retorna volume grande em uma chamada.

Ganhos esperados:

- queda imediata de payload e uso de memória;
- menor tempo de consulta no banco;
- menor tempo de retenção de conexão;
- melhor previsibilidade sob carga.

### 2. Revisar queries mais lentas e criar índices orientados às rotas críticas

Foco inicial:

- consultas por `numVenda`;
- consultas por `cpf`;
- consultas por `responsavel`;
- consultas usadas por `proximas-acoes` e `negativacao/vendas/{numVenda}/dividas`.

Ação prática:

- capturar top 10 endpoints lentos via `/traffic-monitoring/dashboard`;
- mapear cada rota para query real no SQL;
- validar plano de execução;
- adicionar índices compostos e cobertura onde houver scan excessivo.

### 3. Implementar proteção de sobrecarga

Adicionar:

- rate limiting;
- limite de concorrência por rota crítica;
- timeout por operação de leitura;
- retorno explícito `429` ou `503` com payload padronizado em vez de deixar requests acumularem por dezenas de segundos.

Ganhos esperados:

- reduz efeito avalanche;
- evita degradação global;
- mantém o sistema previsível em pico.

## Prioridade P1

### 4. Corrigir inconsistências de contrato dos endpoints de leitura

Padronizar resposta para leituras:

- `200` com `{ data: [...] }` para listas;
- `200` com `{ data: {...} }` para detalhe encontrado;
- `404` com `application/json` e payload de erro padronizado quando não existir dado;
- evitar `404` vazio/plain-text em rotas que o frontend ou o k6 tratam como APIs JSON.

Isso reduz:

- falhas artificiais em teste;
- ambiguidade entre ausência de dado e falha técnica;
- retrabalho de clientes consumidores.

### 5. Persistir percentis e separar erro técnico de erro funcional

No dashboard de tráfego hoje há foco em média, máximos e contagem de erro.

Melhoria:

- guardar `p50`, `p95` e `p99` por endpoint e por janela;
- classificar resposta em categorias: `success`, `not_found_expected`, `client_error`, `server_error`, `dependency_error`, `timeout`.

Ganhos:

- média deixa de esconder cauda de latência;
- incidentes ficam explicáveis.

### 6. Instrumentar dependências e não só ASP.NET Core

Adicionar ou validar:

- `HttpClient` instrumentation para Fluig, RM e Serasa;
- `EntityFrameworkCore` instrumentation se EF estiver no caminho das rotas críticas;
- spans customizados para queries Dapper/legacy SQL mais pesadas;
- tags com nome lógico da query, tempo, resultado e dependência alvo.

Ganhos:

- fica claro se a lentidão está na API, no banco ou em integração externa.

## Prioridade P2

### 7. Evitar perda de telemetria no canal assíncrono

O comportamento atual descarta registros quando a fila enche.

Melhorias possíveis:

- expor métrica de `dropped_records`;
- emitir alerta ao primeiro descarte;
- mover persistência para exportação dedicada ou buffer resiliente;
- considerar amostragem controlada em pico em vez de descarte silencioso por tentativa falha.

### 8. Fazer o endpoint `/traffic-monitoring/dashboard` virar ferramenta operacional

Melhorias:

- filtros por rota e status;
- ranking por `p95` e não só média;
- agrupamento por origem (`source_system`);
- visões separadas para `4xx` esperado e `5xx`;
- indicadores de saturação por minuto.

### 9. Transformar os testes de carga em gate recorrente

Executar pelo menos:

- `smoke` por PR relevante;
- `load` antes de release;
- `stress/spike` em janela controlada de homologação.

Critérios mínimos sugeridos:

- `load`: `0%` erro e `p95 < 100 ms` no estado atual;
- `stress`: erro `< 5%` e `p95 < 1 s`;
- `spike`: recuperação rápida, sem cauda acima de `5 s` após estabilização.

## Backlog sugerido

1. Paginar `GET /inadimplencia/` e `GET /proximas-acoes/`.
2. Levantar top queries por rota crítica e revisar plano de execução.
3. Adicionar rate limiting e timeout por rota.
4. Padronizar `404` JSON para endpoints de leitura.
5. Instrumentar `HttpClient` e queries SQL com spans e métricas.
6. Incluir percentis no dashboard de tráfego.
7. Expor métrica e alerta para registros descartados no channel.
8. Rodar nova bateria k6 e comparar antes/depois.

## Resultado esperado após a primeira onda de melhorias

Se o time executar apenas os itens P0 e P1, o resultado esperado é:

- reduzir fortemente a degradação acima de `100` usuários simultâneos;
- empurrar o ponto de quebra para uma faixa maior de concorrência;
- trocar parte das falhas hoje ambíguas por respostas previsíveis;
- melhorar muito a capacidade de diagnosticar gargalo real em produção.
