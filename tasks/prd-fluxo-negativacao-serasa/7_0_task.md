# Tarefa 7.0: Solicitação de negativação — Command + endpoint + Ocorrência + notificações

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar o **núcleo do fluxo**: o operador de cobrança seleciona parcelas elegíveis, confirma com senha de transação e cria uma solicitação de negativação. O handler `RequestNegativacaoFluxoCommandHandler` orquestra:
1. Validação da senha de transação (com lockout).
2. Re-validação server-side da elegibilidade das parcelas selecionadas.
3. Verificação de duplicidade (não pode haver outra solicitação ativa para a mesma venda).
4. Criação atômica da entidade `SerasaPefinSolicitacaoCompleta` em status `AGUARDANDO_APROVACAO` + Ocorrência com mensagem padronizada.
5. Disparo de notificação (in-app + SSE) para todos os aprovadores autorizados.

Por ser uma task **HIGH**, segue **TDD (red-green-refactor)**: testes primeiro, implementação depois.

<requirements>
- TDD: cada cenário começa com teste falhando antes da implementação.
- Persistência atômica: solicitação + ocorrência na mesma unit-of-work (transação SQL).
- Senha de transação validada **antes** de qualquer escrita.
- Mensagem da Ocorrência segue **exatamente** o template do PRD (incluir "e seus fiadores" se houver fiadores selecionados).
- Endpoint exige usuário autenticado; usuário pode ser qualquer um (não restrito a aprovadores).
- Resposta `201` com `{ solicitacaoId }`. Erros padronizados em `problem+json`.
</requirements>

## Subtarefas

### Fase RED (testes primeiro)

- [ ] 7.1 Escrever testes unitários `RequestNegativacaoFluxoCommandHandlerTests` (FALHANDO):
  - Senha inválida → `SENHA_INVALIDA`, sem escrita.
  - Senha bloqueada → `SENHA_BLOQUEADA`, sem escrita.
  - Sem senha cadastrada → `SENHA_NAO_CADASTRADA`.
  - Parcela não elegível na seleção → `NAO_ELEGIVEL`, sem escrita.
  - Já existe solicitação ativa para a venda → `JA_EM_APROVACAO` (HTTP 409).
  - Sucesso: cria `SerasaPefinSolicitacaoCompleta` (status `AguardandoAprovacao`), cria Ocorrência com texto correto, dispara notificação para cada aprovador.
  - Sucesso com `incluirFiadores=true` → mensagem da ocorrência termina com "...e seus fiadores".
  - Sucesso sem fiadores disponíveis + `incluirFiadores=true` → mensagem **não** inclui sufixo de fiadores; comportamento documentado.
- [ ] 7.2 Escrever testes de integração de endpoint `POST /negativacao/solicitacoes` (FALHANDO):
  - 401 sem autenticação.
  - 400 corpo inválido.
  - 409 quando já existe solicitação ativa.
  - 201 happy path; verificar registros nas tabelas e `INAD_NOTIFICACOES`.

### Fase GREEN (implementação)

- [ ] 7.3 Criar `Application/Features/Negativacao/Commands/RequestNegativacaoFluxoCommand.cs`:
  ```csharp
  public sealed record RequestNegativacaoFluxoCommand(
      int NumVenda,
      IReadOnlyList<long> ParcelaIds,
      bool IncluirFiadores,
      string SenhaTransacao);
  ```
- [ ] 7.4 Criar `RequestNegativacaoFluxoCommandHandler` injetando:
  - `ICurrentUserService` (solicitante).
  - `ISenhaTransacaoValidator` (Task 2.0).
  - `IInadimplenciaQueryService` (re-validar elegibilidade + buscar dados da venda).
  - `ISerasaPefinRepository` (verificar duplicidade + criar `SerasaPefinSolicitacaoCompleta`).
  - `IOcorrenciaRepository` + `IProtocoloGenerator`.
  - `IAprovadoresPolicy` + `INotificationDispatcher`.
  - Wrap em transação `Serializable` (já presente no repository de Serasa) ou unit-of-work simples.
- [ ] 7.5 Criar serviço `NegativacaoOcorrenciaScripts` em `Domain/Negativacao/`:
  - `MontarMensagemSolicitacao(usuario, cliente, numVenda, endereco, parcelas, fiadores?)` retorna a string com placeholders preenchidos.
  - Mascarar CPFs/CNPJs antes de incluir na mensagem.
- [ ] 7.6 Adicionar endpoint em `NegativacaoFluxoEndpoints.cs`:
  - `POST /negativacao/solicitacoes` → 201 `{ solicitacaoId }`.
  - Mapear erros para `problem+json` com `code` apropriado.
- [ ] 7.7 Adicionar query `ListSolicitacoesPendentesQuery(+Handler)` para listar solicitações em `AguardandoAprovacao` (será usado pelos aprovadores na UI).
- [ ] 7.8 Endpoint `GET /negativacao/solicitacoes?status=AGUARDANDO_APROVACAO`.
- [ ] 7.9 Registrar handlers em `DependencyInjection.cs`.

### Fase REFACTOR

- [ ] 7.10 Extrair lógica de validação para método privado/serviço se handler ficar > 80 linhas.
- [ ] 7.11 Garantir log estruturado (`solicitacaoId`, `numVenda`, `username`) sem expor senha.

## Detalhes de Implementação

Ver `techspec.md` seções **Fluxo de Dados** e **Endpoints de API**. Templates de mensagem de ocorrência estão no PRD (RF3.3).

## Critérios de Sucesso

- Todos os testes da fase RED passam após implementação.
- Solicitação criada gera **uma** linha em `SERASA_PEFIN_SOLICITACOES` (status `AGUARDANDO_APROVACAO`) e **uma** em `OCORRENCIAS` (status `Solicitação de negativação`).
- Cada aprovador da config recebe notificação (entrada em `INAD_NOTIFICACOES`).
- Senha errada incrementa `TENTATIVAS_FALHAS`; bloqueio respeitado.
- Reentrada com mesma venda em status ativo retorna 409.
- Logs não exibem senha plain.

## Testes da Tarefa

- [ ] **Unitários** (cobertura RED): conforme 7.1.
- [ ] **Integração de endpoint**: conforme 7.2.
- [ ] **Integração SQL** `RequestNegativacaoFluxoIntegrationTests`:
  - Setup: usuário com senha de transação cadastrada (Task 2.0).
  - Cenário happy path em DB real (UAT) → verifica linhas criadas.
  - Cenário concorrente (2 chamadas paralelas para mesma venda) → uma sucede com 201, outra falha com 409.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Domain/Negativacao/NegativacaoOcorrenciaScripts.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Commands/RequestNegativacaoFluxoCommand.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Commands/RequestNegativacaoFluxoCommandHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQuery(+Handler).cs` (novo)
- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` (modificar)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (modificar)
- `ApiInadimplencia.Application.Tests/Features/Negativacao/Commands/RequestNegativacaoFluxoCommandHandlerTests.cs` (novo)
- `api-inadimplencia.Api.Tests/Endpoints/NegativacaoFluxoEndpointsTests.cs` (novo)
