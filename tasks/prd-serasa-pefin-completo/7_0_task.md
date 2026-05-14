# Tarefa 7.0: Webhooks Serasa PEFIN (6 endpoints + handler idempotente)

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar os 6 endpoints de webhook que o Serasa chama de volta após cada
evento, com **idempotência** por `UUID` extraído do payload, persistência em
`SERASA_PEFIN_WEBHOOKS` e atualização atômica da solicitação correspondente.

Segue **TDD** (red → green → refactor).

<requirements>
- 6 endpoints `POST` mapeados em `InadimplenciaEndpoints`:
  - `/serasa-pefin/webhooks/inclusao/sucesso`
  - `/serasa-pefin/webhooks/inclusao/erro`
  - `/serasa-pefin/webhooks/avalista/sucesso`
  - `/serasa-pefin/webhooks/avalista/erro`
  - `/serasa-pefin/webhooks/baixa/sucesso`
  - `/serasa-pefin/webhooks/baixa/erro`
- Cada endpoint chama um único `SerasaWebhookHandler.HandleAsync(eventType, resultado, rawJson)`.
- Idempotência: se já existe registro em `SERASA_PEFIN_WEBHOOKS` com mesmo `UUID`, retornar 200 sem processar novamente.
- Se `TRANSACTION_ID` não corresponde a nenhuma solicitação, persistir webhook com `PROCESSADO=false` + `MENSAGEM_ERRO=NoMatchingTransaction` e retornar 200 (Serasa não pode receber 4xx que cause retry indefinido).
- Em caso de erro inesperado no processamento, persistir webhook com `PROCESSADO=false` + `MENSAGEM_ERRO=<detalhe>` e retornar 200.
- Atualização do status conforme tipo:
  - inclusao/sucesso → `NegativadoSucesso`
  - inclusao/erro → `NegativadoErro`
  - avalista/sucesso → `NegativadoSucesso` (registro do garantidor)
  - avalista/erro → `NegativadoErro`
  - baixa/sucesso → `BaixadoSucesso`
  - baixa/erro → `BaixadoErro`
- Extrair `cadusKey`, `cadusSerie`, `errorMessage`, `errorStatusCode` do payload quando aplicável.
- Persistir webhook + atualizar solicitação dentro de **uma transação SERIALIZABLE**.
</requirements>

## Subtarefas

- [ ] 7.1 Criar `WebhookEventType` enum (`Inclusao`, `Avalista`, `Baixa`) e `WebhookResultado` enum (`Sucesso`, `Erro`)
- [ ] 7.2 Criar DTO `SerasaWebhookPayload` (uuid, transactionId, cadusKey?, cadusSerie?, errorMessage?, errorStatusCode?)
- [ ] 7.3 Criar `SerasaWebhookHandler` em `Application/Features/SerasaPefin/Webhooks/`
- [ ] 7.4 Estender `ISerasaPefinRepository` com `WebhookExistsByUuidAsync(uuid, ct)` e `ApplyWebhookTransactionalAsync(solicitacao, webhookRecord, ct)`
- [ ] 7.5 Implementar idempotência via `SELECT WHERE UUID = @uuid` antes do INSERT
- [ ] 7.6 Mapear 6 rotas em `InadimplenciaEndpoints.cs`
- [ ] 7.7 Testes unitários do handler (cada combinação de eventType × resultado)
- [ ] 7.8 Testes de integração: webhook reentrante não duplica, webhook órfão é persistido com erro

## Detalhes de Implementação

Ver Tech Spec §3.3 (fluxo webhook), §5.2 (idempotência) e §5.5 (mascaramento).

Estrutura do handler:
```csharp
public sealed class SerasaWebhookHandler(
    ISerasaPefinRepository repository,
    ILogger<SerasaWebhookHandler> logger)
{
    public async Task<WebhookResult> HandleAsync(
        WebhookEventType eventType,
        WebhookResultado resultado,
        string rawJson,
        CancellationToken ct)
    {
        var payload = ParsePayload(rawJson);

        if (await repository.WebhookExistsByUuidAsync(payload.Uuid, ct))
        {
            return WebhookResult.AlreadyProcessed(payload.Uuid);
        }

        var solicitacao = await repository.GetByTransactionIdAsync(payload.TransactionId, ct);
        if (solicitacao is null)
        {
            await PersistOrphan(eventType, payload, rawJson, ct);
            return WebhookResult.NoMatchingTransaction;
        }

        ApplyToSolicitacao(solicitacao, eventType, resultado, payload, rawJson);

        await repository.ApplyWebhookTransactionalAsync(solicitacao, BuildRecord(...), ct);
        return WebhookResult.Processed;
    }
}
```

Eventos do Node em `serasaPefinService.js:mapEventTypeToStatus` servem de referência.

## Critérios de Sucesso

- 6 endpoints retornam 200 para payloads válidos.
- `SERASA_PEFIN_WEBHOOKS` recebe linha por chamada (mesmo se órfã).
- Mesmo UUID enviado duas vezes resulta em 1 linha em `SERASA_PEFIN_WEBHOOKS` e 1 atualização em `SERASA_PEFIN_SOLICITACOES`.
- `STATUS` da solicitação muda conforme matriz (eventType × resultado).
- `CADUS_KEY` e `CADUS_SERIE` persistidos quando presentes no payload.
- Endpoint nunca retorna 5xx mesmo com payload malformado.

## Testes da Tarefa

- [ ] Teste unidade: `Handle_InclusaoSucesso_UpdatesStatusToNegativadoSucesso`
- [ ] Teste unidade: `Handle_InclusaoErro_UpdatesStatusToNegativadoErro_CapturesErrorMessage`
- [ ] Teste unidade: `Handle_AvalistaSucesso_AppliesToGuarantorRecord`
- [ ] Teste unidade: `Handle_BaixaSucesso_UpdatesStatusToBaixadoSucesso`
- [ ] Teste unidade: `Handle_BaixaErro_UpdatesStatusToBaixadoErro`
- [ ] Teste unidade: `Handle_DuplicateUuid_ReturnsAlreadyProcessed_NoSecondUpdate`
- [ ] Teste unidade: `Handle_NoMatchingTransaction_PersistsOrphanWebhook`
- [ ] Teste unidade: `Handle_MalformedJson_PersistsErrorRow_Returns200`
- [ ] Teste integração: `POST /webhooks/inclusao/sucesso` reentrante não duplica registros
- [ ] Teste integração: SERIALIZABLE bloqueia race condition entre 2 webhooks simultâneos

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Webhooks\SerasaWebhookHandler.cs` (reescrever)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Webhooks\SerasaWebhookCommands.cs` (novo)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Dtos\SerasaWebhookDto.cs` (atualizar)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Abstractions\Persistence\ISerasaPefinRepository.cs` (adicionar `WebhookExistsByUuidAsync`, `ApplyWebhookTransactionalAsync`)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\Persistence\SqlServer\SerasaPefinRepository.cs` (implementar novos métodos)
- `@c:\api-inadimplencia-docker\api-inadimplencia.Api\Endpoints\InadimplenciaEndpoints.cs` (mapear 6 rotas)
- `@c:\api-inadimplencia\src\modules\inadimplencia\controllers\serasaPefinController.js` (linhas 188-289, referência)
