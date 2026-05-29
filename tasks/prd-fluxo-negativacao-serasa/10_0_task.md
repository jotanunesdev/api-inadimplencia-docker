# Tarefa 10.0: Testes E2E + validação manual UAT

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Validar o fluxo completo **ponta-a-ponta** em ambiente UAT, garantindo que todas as integrações (banco, Serasa UAT, SSE, notificações) funcionam corretamente em conjunto. Inclui suíte de testes de integração automatizados que cobrem cenários críticos e um roteiro de validação manual para os cenários que dependem de tempo (lockout, timeout) ou da Serasa real.

<requirements>
- Suíte de integração rodável via `dotnet test --filter Category=E2E`.
- Roteiro manual documentado em `docs/uat-fluxo-negativacao.md`.
- Cenários cobrem: happy path, rejeição, lockout de senha, falha de envio, webhook reentrante, autoaprovação bloqueada, não-aprovador bloqueado.
- Dados UAT seguem os 8 documentos autorizados (já em `SerasaPefinConstants.UatAuthorizedDocuments`).
- Frontend não é necessário aqui (foco backend); chamadas via `HttpClient` no teste.
</requirements>

## Subtarefas

### Suíte automatizada de E2E (categoria `E2E`)

- [ ] 10.1 Criar projeto/folder `api-inadimplencia.Api.Tests/E2E/FluxoNegativacaoE2ETests.cs` usando `WebApplicationFactory` + DB de teste real.
- [ ] 10.2 **Cenário A — Happy path completo**:
  - Setup: usuário `op1` com senha de transação `123abc`; aprovador `aracy.mendoca` com senha `xyz789`.
  - `op1` faz `GET /negativacao/vendas/{numVenda}/dividas` → confirma elegibilidade.
  - `op1` faz `POST /negativacao/solicitacoes` → 201.
  - Verifica `INAD_NOTIFICACOES` para `aracy.mendoca` e `adriano.oliveira`.
  - `aracy.mendoca` faz `POST /negativacao/solicitacoes/{id}/decisao { APROVAR }` → 200.
  - Mock do `SerasaPefinClient` retorna `transactionId=ABC123`.
  - Verifica status final `EnviadoSerasa` e ocorrência de aprovação.
  - Simula webhook `inclusao/sucesso` → status `NegativadoSucesso`, ambos recebem `RetornoSerasaSucesso`.
- [ ] 10.3 **Cenário B — Rejeição**: `aracy.mendoca` rejeita; verifica status `Rejeitada`, ocorrência criada, solicitante notificado, Serasa **não** chamado.
- [ ] 10.4 **Cenário C — Auto-aprovação bloqueada**: solicitante = aprovador → 403 `SOLICITANTE_NAO_PODE_APROVAR`.
- [ ] 10.5 **Cenário D — Não-aprovador**: usuário comum tenta decidir → 403 `NAO_AUTORIZADO`.
- [ ] 10.6 **Cenário E — Lockout de senha de transação**: 3 chamadas com senha errada em 5min → 4ª retorna `SENHA_BLOQUEADA`.
- [ ] 10.7 **Cenário F — Concorrência**: 2 chamadas paralelas a `POST /negativacao/solicitacoes` para mesma venda → uma 201, outra 409.
- [ ] 10.8 **Cenário G — Falha síncrona Serasa na aprovação**: mock retorna 500 → solicitação fica em `AprovadaFalhaEnvio`, ambos notificados.
- [ ] 10.9 **Cenário H — Webhook reentrante**: enviar mesmo webhook 2x → segunda chamada não duplica notificações.
- [ ] 10.10 **Cenário I — SSE em tempo real**: cliente conectado em `/notifications/stream` recebe evento ≤2s após `Dispatch`.

### Validação manual em UAT

- [ ] 10.11 Documentar roteiro em `docs/uat-fluxo-negativacao.md`:
  - Pré-requisitos (usuários, senhas, vendas elegíveis, mass UAT da Serasa).
  - Passos para cada cenário acima usando `curl`/Postman.
  - Critérios de aceitação observáveis (response codes, registros DB esperados).
- [ ] 10.12 Executar roteiro em UAT real (com Serasa UAT) e anexar evidências (logs + prints).
- [ ] 10.13 Atualizar `tasks.md` checkbox geral.

### Documentação

- [ ] 10.14 Atualizar `README.md` com seção "Fluxo de Negativação Serasa" descrevendo endpoints novos, ordem de migrations e configurações (`Negativacao:UsuariosAprovadores`).

## Detalhes de Implementação

Ver `techspec.md` seção **Abordagem de Testes — E2E** e **Sequenciamento de Desenvolvimento**. Reaproveitar fixtures do `prd-serasa-pefin-completo` Task 9.0.

## Critérios de Sucesso

- Suíte E2E (10.1–10.10) verde no CI/CD.
- Roteiro manual executado em UAT com 100% dos cenários verdes.
- Documento `docs/uat-fluxo-negativacao.md` revisado e aprovado.
- README atualizado.

## Testes da Tarefa

- [ ] **E2E automatizados**: cenários A–I conforme subtarefas.
- [ ] **Smoke test em produção** após deploy (subset dos cenários A e B com dados controlados).

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `api-inadimplencia.Api.Tests/E2E/FluxoNegativacaoE2ETests.cs` (novo)
- `api-inadimplencia.Api.Tests/E2E/Fixtures/FluxoNegativacaoFixture.cs` (novo)
- `docs/uat-fluxo-negativacao.md` (novo)
- `README.md` (modificar)
- `tasks/prd-fluxo-negativacao-serasa/tasks.md` (atualizar progresso)
