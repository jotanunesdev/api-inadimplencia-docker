# Tarefa 4.0: Extensão de `SerasaPefinSolicitacaoCompleta` — novos status, campos e métodos de transição

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Ampliar a entidade aggregate root `SerasaPefinSolicitacaoCompleta` (e o enum `SerasaPefinStatus`) para suportar o fluxo de aprovação **antes** do envio à Serasa: novos status (`AGUARDANDO_APROVACAO`, `APROVADA`, `REJEITADA`, `APROVADA_FALHA_ENVIO`), novos campos (`SolicitanteUsername`, `AprovadorUsername`, `DtAprovacao`, `Justificativa`) e métodos de transição. Atualizar o repositório SQL para persistir/carregar esses campos.

<requirements>
- Não quebrar contratos existentes; status legados continuam suportados (PRD reusa entidade).
- Invariantes: `TRANSACTION_ID` e `PAYLOAD_AUDITORIA` permanecem `nullable` enquanto status ∈ `{AGUARDANDO_APROVACAO, APROVADA, REJEITADA}` — só são exigidos a partir de `PENDENTE_ENVIO`.
- Transições inválidas devem lançar `InvalidOperationException` com mensagem clara.
- Migration `db/006` da Task 1.0 já aplicada.
- Repository `SerasaPefinRepository` continua usando `IsolationLevel.Serializable` em escritas com dedupe.
</requirements>

## Subtarefas

- [ ] 4.1 Estender enum `Domain/SerasaPefin/SerasaPefinStatus.cs` com `AguardandoAprovacao`, `Aprovada`, `Rejeitada`, `AprovadaFalhaEnvio`. Atualizar mapeamento `ToString` ↔ string SQL.
- [ ] 4.2 Em `SerasaPefinSolicitacaoCompleta`:
  - Adicionar propriedades `SolicitanteUsername`, `AprovadorUsername`, `DtAprovacao`, `Justificativa`.
  - Adicionar factory `CriarParaAprovacao(numVenda, contractNumber, ..., solicitanteUsername)` que retorna entidade com status `AguardandoAprovacao` e sem `TransactionId`/`PayloadAuditoria`.
  - Adicionar métodos: `MarcarAprovada(string aprovadorUsername, DateTime utcNow)`, `MarcarRejeitada(string aprovadorUsername, string justificativa, DateTime utcNow)`, `MarcarPreparadoParaEnvio(string payloadAuditoria)` (transição `Aprovada → PendenteEnvio`), `MarcarAprovadaFalhaEnvio(string errMessage, int? statusCode)`.
  - Validar transições: `AguardandoAprovacao` só pode ir para `Aprovada`/`Rejeitada`; `Aprovada` só pode ir para `PendenteEnvio`/`AprovadaFalhaEnvio`; `Rejeitada` é estado terminal; etc.
- [ ] 4.3 Atualizar `SerasaPefinRepository`:
  - Persistir/ler novas colunas em `AddAsync` e `UpdateAsync`.
  - Mapper `MapTo<...>` lê novas colunas (lidar com NULL).
  - Sentenças SQL `INSERT`/`UPDATE` ajustadas; manter `Serializable`.
- [ ] 4.4 Atualizar mapeadores DTO em `Application/Features/SerasaPefin/Dtos/` para expor `solicitanteUsername`, `aprovadorUsername`, `dtAprovacao`, `justificativa`.

## Detalhes de Implementação

Ver `techspec.md` seções **Componentes a estender (Domain)** e **Modelos de Dados — `db/006_serasa_pefin_status_extensao.sql`**.

## Critérios de Sucesso

- Compilação verde com novo enum.
- Repository persiste e recupera entidade em status `AguardandoAprovacao` com `TransactionId=NULL` sem violar constraints.
- Transição `AguardandoAprovacao → PendenteEnvio` (pulando `Aprovada`) lança `InvalidOperationException`.
- DTO de detalhe expõe novos campos.

## Testes da Tarefa

- [ ] **Unitários** `SerasaPefinSolicitacaoCompletaTransicoesTests`:
  - `CriarParaAprovacao` cria com status correto e sem `TransactionId`.
  - `MarcarAprovada` exige status atual `AguardandoAprovacao`; preenche `AprovadorUsername` e `DtAprovacao`.
  - `MarcarRejeitada` exige `AguardandoAprovacao`; preenche `Justificativa`.
  - `MarcarPreparadoParaEnvio` exige `Aprovada`.
  - `MarcarAprovadaFalhaEnvio` exige `Aprovada`/`PendenteEnvio` (definir).
  - Transições inválidas lançam exceção com mensagem.
- [ ] **Integração** `SerasaPefinRepositoryNovosCamposTests`:
  - Insert com `AguardandoAprovacao` + `SolicitanteUsername` → roundtrip preservado.
  - Update para `Aprovada` registra `AprovadorUsername` e `DtAprovacao`.
  - Update para `Rejeitada` registra `Justificativa`.
- [ ] **Re-execução** dos testes existentes do `SerasaWebhookHandler`/`SerasaPefinRepository` continua verde (não-regressão).

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinStatus.cs` (modificar)
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacaoCompleta.cs` (modificar)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinRepository.cs` (modificar)
- `ApiInadimplencia.Application/Features/SerasaPefin/Dtos/*.cs` (modificar mapeadores)
- `ApiInadimplencia.Domain.Tests/SerasaPefin/SerasaPefinSolicitacaoCompletaTransicoesTests.cs` (novo)
- `ApiInadimplencia.Infrastructure.Tests/Persistence/SerasaPefinRepositoryNovosCamposTests.cs` (novo)
