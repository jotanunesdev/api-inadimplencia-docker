# Tarefa 5.0: Application — `DecideBaixaCommand` + `ResendBaixaCommand`

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar os commands de **decisão** (aprovador aprova/rejeita com senha de transação) e **reenvio** (solicitante reenvia em `BAIXADO_ERRO`, limite 3 tentativas, sem reaprovação). TDD obrigatório.

<requirements>
- `DecideBaixaCommand(SolicitacaoId, Decisao, SenhaTransacao, Justificativa?)`.
- Handler valida que usuário atual é aprovador (`IAprovadoresPolicy`).
- Valida senha de transação do aprovador.
- Valida estado `AguardandoAprovacao`.
- Solicitante não pode aprovar a própria solicitação, exceto se `IsSuperDecisor`.
- Em aprovação, invoca `SendBaixaToSerasaCommandHandler` (tarefa 4.0) — atomicidade pode usar mesma sessão/transação ou orquestração best-effort.
- Em rejeição, registra justificativa e notifica solicitante.
- `ResendBaixaCommand(SolicitacaoId)`:
  - Usuário deve ser o solicitante original (ou super-decisor).
  - Estado deve ser `BaixadoErro`.
  - `Tentativas < 3`.
  - Incrementa `Tentativas`, invoca `SendBaixaToSerasaCommandHandler`, retorna novo `transactionId`.
- Não exige nova aprovação para reenvio.
</requirements>

## Subtarefas

- [x] 5.1 **RED**: `DecideBaixaCommandHandlerTests` — aprovador inválido, senha, estado inválido, super-decisor, fluxo de aprovação (chama `SendBaixaToSerasaCommand`), rejeição (registra justificativa + notifica solicitante).
- [x] 5.2 **RED**: `ResendBaixaCommandHandlerTests` — solicitante errado, estado errado, limite atingido, sucesso (incrementa tentativas + novo transactionId).
- [x] 5.3 **GREEN**: implementar `DecideBaixaCommand` + handler.
- [x] 5.4 **GREEN**: implementar `ResendBaixaCommand` + handler.
- [x] 5.5 **GREEN**: notificações in-app — “Baixa aprovada”, “Baixa rejeitada (motivo)”, “Baixa reenviada (tentativa X/3)”.
- [x] 5.6 **REFACTOR**: extrair helpers comuns com `DecideNegativacaoCommandHandler` se houver duplicação evidente.
- [x] 5.7 Registrar handlers em DI.

## Detalhes de Implementação

Ver Tech Spec — “Fluxo de Dados” (passos 2 e 5) e “Decisões Principais”. Referenciar `DecideNegativacaoCommandHandler.cs` como template do fluxo de decisão.

## Critérios de Sucesso

- Todos os testes verdes.
- Reenvio nunca passa pela fila de aprovação.
- Limite de 3 tentativas é estritamente respeitado (4ª tentativa falha com mensagem clara).
- Solicitante recebe notificação imediata após cada decisão e após cada reenvio.

## Testes da Tarefa

- [x] Testes unitários: matriz completa de cenários de `DecideBaixaCommandHandler`.
- [x] Testes unitários: cenários de `ResendBaixaCommandHandler` (boundary do limite de 3 inclusive).
- [x] Testes verificam que `SendBaixaToSerasaCommandHandler` é invocado corretamente em aprovação e em reenvio.
- [x] Testes verificam que rejeição NÃO chama `SendBaixaToSerasaCommandHandler`.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/DecideBaixaCommand.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/DecideBaixaCommandHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/ResendBaixaCommand.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/ResendBaixaCommandHandler.cs` (novo)
- `ApiInadimplencia.Application.Tests/Features/Negativacao/Baixa/Commands/DecideBaixaCommandHandlerTests.cs` (novo)
- `ApiInadimplencia.Application.Tests/Features/Negativacao/Baixa/Commands/ResendBaixaCommandHandlerTests.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Commands/DecideNegativacaoCommandHandler.cs` (referência)
