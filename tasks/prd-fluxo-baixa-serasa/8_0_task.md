# Tarefa 8.0: Dashboard backend — queries `GetMotivosBaixa` e `GetNegativacoesVsBaixas` + endpoints

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar as duas queries CQRS que alimentam os novos cards do dashboard, consumindo as views criadas na tarefa 1.0. Esta tarefa é paralelizável com 3.0–7.0 desde que a 1.0 esteja concluída.

<requirements>
- `GetMotivosBaixaQuery(meses=12)` → handler consulta `vw_serasa_pefin_baixa_motivos` e retorna lista `{ motivo, descricao, qtd, percentual }`.
- `GetNegativacoesVsBaixasQuery(meses=12)` → handler consulta `vw_serasa_pefin_negativacao_baixa_mensal` e retorna lista `{ anoMes, qtdNegativacoes, qtdBaixas }`.
- Endpoints:
  - `GET /inadimplencia/dashboard/baixa/motivos?meses=12`
  - `GET /inadimplencia/dashboard/baixa/comparativo-mensal?meses=12`
- `meses` aceita 1..24 (default 12); valores fora da faixa → 400.
- Resposta em formato `{ data: [...] }` para alinhar com padrão do dashboard atual.
- SQL parametrizado; usar `ISqlConnectionFactory` existente.
- Mock-friendly: handlers recebem dependência de leitura (não SQL direto no construtor).
</requirements>

## Subtarefas

- [x] 8.1 Criar DTOs em `Features/Dashboard/Dtos/` (`MotivoBaixaDto`, `NegativacaoBaixaMensalDto`).
- [x] 8.2 Implementar `GetMotivosBaixaQueryHandler`.
- [x] 8.3 Implementar `GetNegativacoesVsBaixasQueryHandler`.
- [x] 8.4 Adicionar os dois endpoints (preferir um arquivo `DashboardBaixaEndpoints.cs` ou estender o existente).
- [x] 8.5 Testes unitários dos handlers com mock de conexão (validar SQL parametrizado e shape do DTO).
- [x] 8.6 Testes E2E HTTP cobrindo: meses default, meses fora de faixa (400), resposta vazia (sem dados).

## Detalhes de Implementação

Ver Tech Spec — “Modelos de Dados” (Views) e “Endpoints de API”. Padrão de query handler: `Features/Dashboard/Queries/GetDashboardKpisQueryHandler.cs`.

## Critérios de Sucesso

- Endpoints retornam dados consistentes com as views.
- Janela de 12 meses default funciona; `meses=24` também válido; `meses=0` ou `meses=25` retorna 400.
- Quando não há baixas: ambos endpoints respondem 200 com `data: []`.
- Testes verdes.

## Testes da Tarefa

- [x] Testes unitários: handlers (shape do DTO, parâmetros SQL).
- [x] Testes de integração: queries reais contra views (smoke + dados sintéticos).
- [x] Testes E2E HTTP: 200 default, 400 fora de faixa, 200 com `data: []`.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/Dashboard/Queries/GetMotivosBaixaQueryHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Dashboard/Queries/GetNegativacoesVsBaixasQueryHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Dashboard/Dtos/MotivoBaixaDto.cs` (novo)
- `ApiInadimplencia.Application/Features/Dashboard/Dtos/NegativacaoBaixaMensalDto.cs` (novo)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (ou novo `DashboardBaixaEndpoints.cs`)
- `ApiInadimplencia.Application.Tests/Features/Dashboard/*` (novos)
- `api-inadimplencia.Api.Tests/Features/Dashboard/*` (novos)
