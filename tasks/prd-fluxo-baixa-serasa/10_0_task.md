# Tarefa 10.0: Frontend dashboard charts + E2E Playwright

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Renderizar os dois novos cards no `DashboardPage` usando `@mui/x-charts` (já presente no projeto) e cobrir o fluxo ponta-a-ponta com testes Playwright (front + back), incluindo simulação de webhook. Esta é a tarefa final de integração.

<requirements>
- `MotivosBaixaChart` — gráfico de pizza (`PieChart` do `@mui/x-charts`) consumindo `GET /inadimplencia/dashboard/baixa/motivos`.
  - Exibe os 7 motivos (whitelist) com legenda + tooltip de valor absoluto.
- `NegativacoesVsBaixasChart` — gráfico misto consumindo `GET /inadimplencia/dashboard/baixa/comparativo-mensal`.
  - Composição via `<ChartContainer>` + `<BarPlot />` (negativações) + `<LinePlot />` (baixas), conforme padrão MUI X v8.
  - Eixo X: mês (`YYYY-MM` formatado como `MMM/YY`).
  - Eixos Y duais ou eixo único compartilhado (decidir durante implementação conforme legibilidade).
- Estados: loading, vazio, erro com mensagem amigável (anunciado via `role="status"`/`role="alert"`).
- Acessibilidade: `aria-label`, legenda associada, tabela alternativa oculta para leitores de tela (opcional mas recomendado).
- Cards seguem layout/tema dos demais do dashboard.
- E2E Playwright cobrindo fluxo ponta-a-ponta:
  - Solicitar baixa de 1 parcela → aprovar → mock do gateway Serasa retorna `transactionId` → simular webhook sucesso → verificar parcela volta a “Elegível” no modal de negativação → verificar atualização dos dois cards do dashboard.
</requirements>

## Subtarefas

- [x] 10.1 Criar `pages/main/dashboard/components/MotivosBaixaChart.tsx`.
- [x] 10.2 Criar `pages/main/dashboard/components/NegativacoesVsBaixasChart.tsx` (composição).
- [x] 10.3 Estender `pages/main/dashboard/api.ts` com `fetchMotivosBaixa` e `fetchNegativacoesVsBaixas`.
- [x] 10.4 Wire-up no `DashboardPage` (dois cards novos respeitando layout existente).
- [x] 10.5 Tratamento de estados (loading, vazio, erro) com testes Vitest.
- [ ] 10.6 Testes E2E Playwright cobrindo o fluxo ponta-a-ponta (solicitação → aprovação → webhook simulado → reflexo no modal e dashboard).
- [ ] 10.7 Validação manual em UAT com dados reais.

## Detalhes de Implementação

Ver Tech Spec — “Frontend — componentes novos” e seção “Visualização de gráfico misto MUI X v8”. Documentação confirmada via Context7: `ChartContainer` exige `type` em cada série quando combinando `BarPlot` + `LinePlot`. Referenciar `pages/main/DashboardPage.tsx` para padrão de card, header e legenda.

## Critérios de Sucesso

- Cards renderizam sem erro em estado normal, vazio e de erro.
- Gráfico misto exibe corretamente barras (negativações) e linha (baixas) sobrepostas no mesmo eixo X.
- E2E Playwright executa todo o fluxo sem flakiness e finaliza com estados consistentes.
- Validação manual em UAT confirma cards atualizando após operações reais.
- Sem regressão em testes existentes do dashboard.

## Testes da Tarefa

- [x] Testes Vitest: `MotivosBaixaChart` (loading, vazio, erro, sucesso).
- [x] Testes Vitest: `NegativacoesVsBaixasChart` (loading, vazio, erro, sucesso).
- [ ] Testes Playwright E2E: cenário completo (front + back).
- [ ] Smoke manual em UAT com dados reais.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `src/pages/main/dashboard/components/MotivosBaixaChart.tsx` (novo)
- `src/pages/main/dashboard/components/NegativacoesVsBaixasChart.tsx` (novo)
- `src/pages/main/dashboard/api.ts` (modificado)
- `src/pages/main/DashboardPage.tsx` (modificado)
- `tests/e2e/baixa-fluxo-completo.spec.ts` (novo, Playwright)
- `api-inadimplencia.Api.Tests/E2E/FluxoBaixaE2ETests.cs` (novo, backend, complemento)
