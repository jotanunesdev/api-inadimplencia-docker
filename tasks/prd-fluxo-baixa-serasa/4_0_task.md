# Tarefa 4.0: Application — `RequestBaixaCommand` + `SendBaixaToSerasaCommand`

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar os dois primeiros commands do fluxo de baixa: (1) **solicitação** pelo analista (entra na fila de aprovação) e (2) **envio à Serasa** após aprovação (chamada DELETE). Como é a parte crítica do fluxo (regras de senha de transação, elegibilidade, idempotência), seguir **TDD**.

<requirements>
- `RequestBaixaCommand(NumVenda, ParcelaIds, MotivoBaixa, SenhaTransacao, Justificativa?)` retorna `Guid` (id da solicitação pai).
- Handler valida senha de transação (reuso de `ISenhaTransacaoValidator`).
- Valida que cada parcela selecionada está em `NEGATIVADO_SUCESSO` (consulta histórico).
- Valida ausência de baixa ativa via `ExistsActiveAsync` por parcela.
- Cria 1 `SerasaPefinBaixaSolicitacao` por parcela em `AguardandoAprovacao` (persistência atômica via `AddManyAsync`).
- Cria `Ocorrencia` padronizada com `IProtocoloGenerator` e mensagem específica de baixa.
- Dispara `INotificationDispatcher.DispatchManyAsync` para todos os aprovadores configurados (payload JSON com cliente/parcelas/motivo).
- `SendBaixaToSerasaCommand(SolicitacaoId)` é chamado internamente após aprovação; chama gateway, marca `BaixaAguardandoRetorno`; em falha HTTP marca `AprovadaFalhaEnvio` e propaga erro.
- Mensagens de erro padronizadas (`SENHA_INVALIDA`, `NAO_ELEGIVEL`, `JA_EM_APROVACAO`).
</requirements>

## Subtarefas

- [ ] 4.1 **RED**: criar `RequestBaixaCommandHandlerTests` cobrindo: senha inválida/bloqueada/não cadastrada; parcela inexistente; parcela não negativada; duplicidade ativa; sucesso (cria N agregados, persistência, ocorrência, notificações).
- [ ] 4.2 **RED**: criar `SendBaixaToSerasaCommandHandlerTests` cobrindo: gateway sucesso (transição correta); gateway lança `SerasaPefinHttpException` (transição `AprovadaFalhaEnvio`); idempotência (não envia se já em `BaixaAguardandoRetorno`).
- [ ] 4.3 **GREEN**: implementar `RequestBaixaCommand` + `RequestBaixaCommandHandler`.
- [ ] 4.4 **GREEN**: implementar `SendBaixaToSerasaCommand` + `SendBaixaToSerasaCommandHandler`.
- [ ] 4.5 **GREEN**: criar DTOs (`Dtos/RequestBaixaRequest.cs`, `RequestBaixaResponse.cs`) e mensagens padronizadas (`Baixa/BaixaOcorrenciaScripts.cs`).
- [ ] 4.6 **REFACTOR**: extrair helpers compartilhados com `RequestNegativacaoFluxoCommandHandler` quando houver duplicação clara (sem quebrar contexto isolado).
- [ ] 4.7 Registrar handlers em `Infrastructure/DependencyInjection.cs`.

## Detalhes de Implementação

Ver Tech Spec — “Fluxo de Dados” (passos 1–3) e “Endpoints de API” (body de `POST /negativacao/baixa/solicitacoes`). Usar `Application/Features/Negativacao/Commands/RequestNegativacaoFluxoCommandHandler.cs` como template (validação de senha, criação de ocorrência, dispatch de notificações).

## Critérios de Sucesso

- Todos os testes em `Application.Tests/Features/Negativacao/Baixa/Commands/` verdes.
- Solicitação cria N agregados em uma única transação.
- Falha HTTP no envio transiciona corretamente para `AprovadaFalhaEnvio` sem perder a aprovação.
- Mensagens de notificação contêm `mensagem`, `cliente`, `cpfCnpj`, `valorInadimplente`, `motivoBaixa`, `solicitacaoId`, `status`.

## Testes da Tarefa

- [ ] Testes unitários: 5 cenários de erro em `RequestBaixaCommandHandler` + cenário de sucesso.
- [ ] Testes unitários: 3 cenários em `SendBaixaToSerasaCommandHandler` (sucesso/falha/idempotência).
- [ ] Testes verificam que `ExistsActiveAsync` é consultado **antes** de qualquer escrita.
- [ ] Testes verificam que notificação é disparada para **todos** os aprovadores configurados.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/RequestBaixaCommand.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/RequestBaixaCommandHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/SendBaixaToSerasaCommand.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Commands/SendBaixaToSerasaCommandHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Baixa/Dtos/*` (novos)
- `ApiInadimplencia.Application.Tests/Features/Negativacao/Baixa/Commands/*` (novos)
- `ApiInadimplencia.Application/Features/Negativacao/Commands/RequestNegativacaoFluxoCommandHandler.cs` (referência)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (modificado)
