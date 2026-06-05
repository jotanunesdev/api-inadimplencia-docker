# Tarefa 9.0: Frontend — modal modo baixa, confirmação com motivo, provider/hook e diferenciação visual

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Habilitar o fluxo de baixa no frontend Fluig (`c:\fluig\trenamento\wcm\layout\jnc_inadimplencia`). Inclui adaptar o modal de seleção, criar combo de motivo, criar provider/hook próprios para baixa e diferenciar visualmente baixa vs negativação na fila de aprovação. Esta tarefa concentra toda a UI exceto o dashboard (tarefa 10.0).

<requirements>
- `NegativacaoDebtsModal` ganha prop `modo: "negativacao" | "baixa"` (default `"negativacao"`).
  - Em `"baixa"`: somente parcelas com `statusSerasa = NEGATIVADO_SUCESSO` ficam selecionáveis; demais ficam desabilitadas com badge informativa.
  - Título “Selecionar dívidas para baixa”; botão final “Solicitar baixa”.
- `NegativacaoConfirmModal` ganha campo `motivoBaixa` (combo obrigatório) quando `modo === "baixa"`.
  - Opções: 01 Pagamento, 02 Renegociação, 03 Solicitação do cliente, 04 Ordem judicial, 19 Renegociação por acordo, 43 Baixa por negociação, 45 Contestação.
  - Botão de confirmação desabilitado até motivo selecionado.
- Novo provider `BaixaDecisionProvider` e hook `useBaixaDecisionFlow` (espelhos dos atuais para negativação).
- Novo `services/baixa.ts` com `createSolicitacaoBaixa`, `decideSolicitacaoBaixa`, `resendBaixa`, `getBaixaById`.
- Novo `types/baixa.ts` com tipos TypeScript alinhados aos DTOs do backend.
- Botão **“Baixa de dívida”** adicionado ao painel de informações do cliente, próximo ao botão atual de negativar.
- Botão **“Reenviar baixa”** visível no detalhe quando `status === "BAIXADO_ERRO"` e tentativas < 3.
- Fila de aprovações: badge/ícone distinto (cor + ícone) para diferenciar baixa de negativação.
- Quando webhook confirma `BAIXADO_SUCESSO`, parcela volta a ficar elegível para negativação (refletido ao reabrir o modal).
- Acessibilidade: `aria-label` em todos os controles novos.
</requirements>

## Subtarefas

- [x] 9.1 Adicionar prop `modo` ao `NegativacaoDebtsModal` e ajustar `resolveElegibilidade` para o novo modo.
- [x] 9.2 Atualizar `NegativacaoConfirmModal` com combo de motivo (renderização condicional).
- [x] 9.3 Criar `types/baixa.ts` e `services/baixa.ts`.
- [x] 9.4 Criar `useBaixaDividas` (similar a `useNegativacaoDividas`), `useBaixaDecisionFlow`, `BaixaDecisionProvider`.
- [x] 9.5 Adicionar botão “Baixa de dívida” no widget de informações do cliente e wire-up com o provider.
- [x] 9.6 Adicionar botão "Reenviar baixa" no detalhe da solicitação (BaixaDecisionModal exibe botão condicional para BAIXADO_ERRO + tentativas < 3).
- [x] 9.7 Diferenciação visual na fila de aprovações (badge `Baixa` vs `Negativação`).
- [x] 9.8 Testes Vitest/RTL para cada componente alterado e novo.

## Detalhes de Implementação

Ver Tech Spec — “Componentes novos (Frontend)” e “Componentes modificados (Frontend)”. Arquivos de referência:
- `src/shared/ui/negativacao/NegativacaoDebtsModal.tsx`
- `src/shared/ui/negativacao/NegativacaoConfirmModal.tsx`
- `src/app/providers/NegativacaoDecisionProvider.tsx`
- `src/shared/hooks/useNegativacaoDecisionFlow.ts`
- `src/shared/hooks/useNegativacaoDividas.ts`
- `src/shared/services/negativacao.ts`

## Critérios de Sucesso

- É possível abrir o modal em modo baixa e selecionar parcelas negativadas.
- O usuário não consegue confirmar sem motivo selecionado.
- Solicitação chega ao backend com `motivoBaixa` correto.
- Fila de aprovações diferencia visualmente baixa vs negativação sem abrir cada card.
- Botão de reenvio só aparece em `BAIXADO_ERRO`.
- Após `BAIXADO_SUCESSO` (webhook simulado em teste), reabrir modal em modo negativação mostra parcela elegível.
- Testes Vitest verdes; sem regressão nos testes existentes da negativação.

## Testes da Tarefa

- [x] Testes Vitest/RTL: `NegativacaoDebtsModal` em modo baixa (eligibilidade invertida).
- [x] Testes Vitest/RTL: `NegativacaoConfirmModal` em modo baixa (combo motivo, validação, submit).
- [x] Testes Vitest: `useBaixaDecisionFlow` (fluxos de aprovar, rejeitar, reenviar).
- [x] Testes Vitest: `services/baixa.ts` (chamadas HTTP corretas).
- [x] Testes Vitest: diferenciação visual na fila de aprovações.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `src/shared/ui/negativacao/NegativacaoDebtsModal.tsx` (modificado)
- `src/shared/ui/negativacao/NegativacaoConfirmModal.tsx` (modificado)
- `src/shared/types/baixa.ts` (novo)
- `src/shared/services/baixa.ts` (novo)
- `src/shared/hooks/useBaixaDividas.ts` (novo)
- `src/shared/hooks/useBaixaDecisionFlow.ts` (novo)
- `src/app/providers/BaixaDecisionProvider.tsx` (novo)
- `src/shared/ui/negativacao/NegativacaoStatusBadge.tsx` (modificado — distinguir baixa)
- Componente do widget de cliente (botão “Baixa de dívida”) — localizar durante a execução
