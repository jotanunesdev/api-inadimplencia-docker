# Tarefa 8.0: Decisão (aprovar/rejeitar) — Command + endpoint + refatorar `RequestNegativacaoCommandHandler`

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar a etapa de **decisão** do fluxo. Aprovador autorizado abre uma solicitação pendente, confirma sua decisão (aprovar ou rejeitar) com senha de transação. Se aprovar, o sistema:
1. Marca a solicitação como `Aprovada`.
2. Cria Ocorrência "Aprovação Negativação Serasa".
3. **Reutiliza** `RequestNegativacaoCommandHandler` (módulo `prd-serasa-pefin-completo`) para enviar à API Serasa, atualizando para `EnviadoSerasa`/`AguardandoRetorno`.
4. Notifica solicitante e aprovador sobre o envio.
5. Em caso de erro síncrono Serasa, transita para `AprovadaFalhaEnvio` e notifica para nova ação manual.

Se rejeitar:
1. Marca como `Rejeitada` com `Justificativa`.
2. Cria Ocorrência "Rejeição Negativação Serasa".
3. Notifica o solicitante.

Esta task também **refatora** `RequestNegativacaoCommandHandler` para aceitar uma `solicitacaoIdExistente` (modo "reuso") quando invocada após aprovação, evitando criar nova linha.

Task **HIGH** → **TDD (red-green-refactor)**.

<requirements>
- TDD obrigatório.
- `RequestNegativacaoCommandHandler` deve continuar funcionando para o fluxo direto (sem solicitação prévia) — backward compat.
- Solicitante **não pode** aprovar a própria solicitação.
- Apenas usernames listados em `NegativacaoOptions.UsuariosAprovadores` podem decidir.
- Senha de transação obrigatória em ambas as decisões.
- Em erro síncrono do Serasa, status volta a `AprovadaFalhaEnvio` (não fica preso em estado intermediário).
- Operação não-atômica entre "aprovar" e "enviar Serasa": aprovação é commitada **antes** da chamada HTTP; falha de envio é tratada como nova transição de estado.
</requirements>

## Subtarefas

### Fase RED

- [ ] 8.1 Escrever testes `DecideNegativacaoCommandHandlerTests` (FALHANDO):
  - Usuário não-aprovador → `NAO_AUTORIZADO`.
  - Solicitação inexistente → `NAO_ENCONTRADA`.
  - Solicitação não em `AguardandoAprovacao` → `JA_DECIDIDA`.
  - Aprovador = solicitante → `SOLICITANTE_NAO_PODE_APROVAR`.
  - Senha inválida → `SENHA_INVALIDA`.
  - APROVAR happy path: status muda para `Aprovada` → `EnviadoSerasa`; Ocorrência criada; ambos notificados.
  - APROVAR com falha síncrona Serasa: status final `AprovadaFalhaEnvio`; Ocorrência indica falha; ambos notificados.
  - REJEITAR happy path: status muda para `Rejeitada`; Ocorrência criada; solicitante notificado; **Serasa não é chamado**.
- [ ] 8.2 Escrever testes para `RequestNegativacaoCommandHandler` modo reuso:
  - Quando `solicitacaoIdExistente` informado, NÃO cria nova linha; atualiza `MarcarPreparadoParaEnvio` na existente.
  - Comportamento atual (sem `solicitacaoIdExistente`) preservado.

### Fase GREEN

- [ ] 8.3 Criar `Application/Features/Negativacao/Commands/DecideNegativacaoCommand.cs`:
  ```csharp
  public sealed record DecideNegativacaoCommand(
      Guid SolicitacaoId,
      DecisaoNegativacao Decisao,    // APROVAR | REJEITAR
      string SenhaTransacao,
      string? Justificativa);
  ```
- [ ] 8.4 Criar `DecideNegativacaoCommandHandler` injetando:
  - `ICurrentUserService`, `IAprovadoresPolicy`, `ISenhaTransacaoValidator`.
  - `ISerasaPefinRepository`, `IOcorrenciaRepository`, `IProtocoloGenerator`.
  - `INotificationDispatcher`.
  - **`ICommandHandler<RequestNegativacaoCommand, RequestNegativacaoResponse>`** (reuso) — chamada após `MarcarAprovada` e commit.
- [ ] 8.5 Refatorar `RequestNegativacaoCommandHandler`:
  - Adicionar parâmetro opcional `Guid? SolicitacaoIdExistente` ao `RequestNegativacaoCommand`.
  - Quando preenchido: carregar entidade existente, executar `MarcarPreparadoParaEnvio` (com `PayloadAuditoria`), pular `AddAsync`. Demais passos preservados.
  - Quando `null`: comportamento atual.
- [ ] 8.6 Adicionar endpoint em `NegativacaoFluxoEndpoints.cs`:
  - `POST /negativacao/solicitacoes/{id:guid}/decisao` → 200/400/401/403/404/409.
- [ ] 8.7 Mensagens de Ocorrência (em `NegativacaoOcorrenciaScripts`): templates de aprovação e rejeição (PRD).
- [ ] 8.8 Em caso de exceção HTTP/timeout do Serasa, capturar e transitar para `AprovadaFalhaEnvio` (já há suporte da Task 4.0). Notificar ambos.
- [ ] 8.9 Registrar handler em `DependencyInjection.cs`.

### Fase REFACTOR

- [ ] 8.10 Extrair sub-fluxos (aprovação, rejeição, envio) em métodos privados claros.
- [ ] 8.11 Garantir que logs incluem `solicitacaoId`, `decisao`, `aprovador` (mas nunca senha).

## Detalhes de Implementação

Ver `techspec.md` seções **Fluxo de Dados** (passo "Aprovador") e **Decisões Principais**. Mensagens de ocorrência seguem PRD (RF4.APROVAR/REJEITAR).

## Critérios de Sucesso

- Todos os testes da fase RED passam.
- APROVAR cria 2 ocorrências no fluxo total (1 da solicitação Task 7.0 + 1 da aprovação) e dispara chamada Serasa via `RequestNegativacaoCommand`.
- REJEITAR cria ocorrência de rejeição, **não** chama Serasa, notifica apenas solicitante.
- Falha Serasa síncrona deixa solicitação em `AprovadaFalhaEnvio` e notifica ambos.
- `RequestNegativacaoCommandHandler` continua passando os testes existentes (não-regressão).
- Idempotência: clicar em "decidir" duas vezes na mesma solicitação → segunda chamada retorna 409 `JA_DECIDIDA`.

## Testes da Tarefa

- [ ] **Unitários** RED: conforme 8.1 e 8.2.
- [ ] **Integração endpoint** `DecideNegativacaoEndpointTests`:
  - 403 quando usuário não é aprovador.
  - 200 happy path APROVAR (com mock do `SerasaPefinClient`).
  - 200 happy path REJEITAR.
  - 200 com `AprovadaFalhaEnvio` quando mock Serasa devolve 5xx.
- [ ] **Não-regressão**: `RequestNegativacaoCommandHandlerTests` existente continua verde com a refatoração.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/Negativacao/Commands/DecideNegativacaoCommand.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Commands/DecideNegativacaoCommandHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommand.cs` (modificar — novo parâmetro opcional)
- `ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommandHandler.cs` (modificar — modo reuso)
- `ApiInadimplencia.Domain/Negativacao/NegativacaoOcorrenciaScripts.cs` (modificar — templates de aprovação/rejeição)
- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` (modificar)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (modificar)
- `ApiInadimplencia.Application.Tests/Features/Negativacao/Commands/DecideNegativacaoCommandHandlerTests.cs` (novo)
- `api-inadimplencia.Api.Tests/Endpoints/DecideNegativacaoEndpointTests.cs` (novo)
