# Tarefa 5.0: Implementar Queries de Dashboard

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar query handlers para KPIs e métricas de dashboard. Esta tarefa foca em queries de leitura complexas usando Dapper, com paginação e validações de segurança.

<requirements>
- Implementar query handler para KPIs principais (vendas por responsável, inadimplência/clientes por empreendimento, status de repasse, blocos, unidades, usuários ativos)
- Implementar query handler para ocorrências por usuário, venda, dia, hora, dia/hora e listagem completa
- Implementar query handler para próximas ações por dia, ações definidas, atendentes por próxima ação
- Implementar query handler para aging, aging detalhes, parcelas inadimplentes, parcelas detalhes
- Implementar query handler para score/saldo, score/saldo detalhes, saldo por mês de vencimento, perfil risco empreendimento
- Aplicar filtros dataInicio e dataFim juntos no formato YYYY-MM-DD
- Limitar limit a máximo 1000
- Usar whitelists/parsers para faixa, qtd e score, nunca SQL livre
- Criar DTOs de resposta
- Testes de unidade e integração
</requirements>

## Subtarefas

- [ ] 5.1 Criar DTOs para dashboard KPIs
- [ ] 5.2 Implementar GetDashboardKpisQueryHandler
- [ ] 5.3 Implementar GetMetricQueryHandler genérico
- [ ] 5.4 Criar whitelists/parsers para faixa, qtd, score
- [ ] 5.5 Implementar query handler para ocorrências (vários filtros)
- [ ] 5.6 Implementar query handler para próximas ações
- [ ] 5.7 Implementar query handler para aging e parcelas
- [ ] 5.8 Implementar query handler para score/saldo
- [ ] 5.9 Mapear endpoints REST de dashboard
- [ ] 5.10 Escrever testes de unidade
- [ ] 5.11 Escrever testes de integração

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Modelos de Dados**: DTOs de Requisição/Resposta
- **Endpoints de API**: Dashboard
- **Pontos de Integração**: DW.fat_analise_inadimplencia_v4
- **Abordagem de Testes**: Query handlers - SQL parametrizado, mapeamento de resultados

**DTOs:**
- Criar `ApiInadimplencia.Application/Features/Dashboard/Dtos/DashboardKpisDto.cs`
- Criar `ApiInadimplencia.Application/Features/Dashboard/Dtos/GetDashboardKpisQuery.cs`
- Criar `ApiInadimplencia.Application/Features/Dashboard/Dtos/GetMetricQuery.cs`
- Criar DTOs específicos para cada métrica

**Whitelists/Parsers:**
- Criar `ApiInadimplencia.Application/Features/Dashboard/Parsers/FaixaParser.cs`
- Criar `ApiInadimplencia.Application/Features/Dashboard/Parsers/QuantidadeParser.cs`
- Criar `ApiInadimplencia.Application/Features/Dashboard/Parsers/ScoreParser.cs`
- Definir valores permitidos (ex: faixa: 0-30, 31-60, 61-90, 91+)
- Validar e lançar exceção se valor não permitido

**Query Handlers KPIs:**
- Criar `ApiInadimplencia.Application/Features/Dashboard/Queries/GetDashboardKpisQueryHandler.cs`
- Query SQL parametrizada para DW.fat_analise_inadimplencia_v4
- Retornar KPIs agregados (COUNT, SUM, AVG)
- Aplicar filtros dataInicio/dataFim se fornecidos

**Query Handler Métricas Genérico:**
- Criar `ApiInadimplencia.Application/Features/Dashboard/Queries/GetMetricQueryHandler.cs`
- Aceitar parâmetro metric (ex: vendas-por-responsavel, inadimplencia-por-empreendimento)
- Usar whitelist para validar metric permitida
- Selecionar query SQL apropriada baseada em metric
- Aplicar parsers para faixa, qtd, score se necessário

**Query Handlers Ocorrências:**
- Criar handlers para:
  - GetOcorrenciasPorUsuarioQuery
  - GetOcorrenciasPorVendaQuery
  - GetOcorrenciasPorDiaQuery
  - GetOcorrenciasPorHoraQuery
  - GetOcorrenciasPorDiaHoraQuery
  - ListOcorrenciasQuery
- Usar GROUP BY e agregações

**Query Handlers Próximas Ações:**
- Criar handlers para:
  - GetProximasAcoesPorDiaQuery
  - GetAcoesDefinidasQuery
  - GetAtendentesPorProximaAcaoQuery

**Query Handlers Aging/Parcelas:**
- Criar handlers para:
  - GetAgingQuery
  - GetAgingDetalhesQuery
  - GetParcelasInadimplentesQuery
  - GetParcelasDetalhesQuery

**Query Handlers Score/Saldo:**
- Criar handlers para:
  - GetScoreSaldoQuery
  - GetScoreSaldoDetalhesQuery
  - GetSaldoPorMesVencimentoQuery
  - GetPerfilRiscoEmpreendimentoQuery

**Validações:**
- Validar formato YYYY-MM-DD para dataInicio/dataFim
- Limitar limit a máximo 1000 (retornar 400 se maior)
- Validar metric contra whitelist
- Validar faixa/qtd/score contra parsers
- SQL sempre parametrizado

**Endpoints:**
- Atualizar `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs`
- Mapear:
  - `GET /dashboard/kpis` → GetDashboardKpisQuery
  - `GET /dashboard/{metric}` → GetMetricQuery

## Critérios de Sucesso

- KPIs retornados corretamente
- Métricas específicas funcionando
- Filtros dataInicio/dataFim aplicados corretamente
- Limit respeitado (máximo 1000)
- Whitelists/parsers validando corretamente
- SQL livre nunca usado
- Queries parametrizadas
- Endpoints REST funcionando
- Testes de unidade passam
- Testes de integração passam

## Testes da Tarefa

- [ ] Testes de unidade
  - Testar parsers de faixa, qtd, score
  - Testar whitelist de métricas
  - Testar validação de limit
  - Testar validação de formato de data
  - Mock de ILegacySqlExecutor
- [ ] Testes de integração
  - Testar queries reais contra SQL Server de teste
  - Testar KPIs com dados reais
  - Testar métricas específicas
  - Testar filtros de data
  - Testar limit de paginação
  - Testar endpoints REST via HttpClient

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes
- `ApiInadimplencia.Application/Features/Dashboard/Dtos/` (novo)
- `ApiInadimplencia.Application/Features/Dashboard/Queries/` (novo)
- `ApiInadimplencia.Application/Features/Dashboard/Parsers/` (novo)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (atualizar)
