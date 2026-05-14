# Tarefa 4.0: Corrigir `SerasaPefinClient` para endpoints reais

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

O `SerasaPefinClient` atual aponta para endpoints inexistentes
(`/api/preview/{id}`, `/api/negativacao`). Corrigir para os endpoints reais da
Serasa documentados em `@c:\api-inadimplencia\documentos\guia-integracao-serasa-pefin.md`:

- `POST {CollectionBaseUrl}/debt/` (inclusão principal)
- `POST {CollectionBaseUrl}/debt/guarantor` (inclusão avalista)

Remover `GetPreviewAsync` (preview não chama Serasa).

<requirements>
- Método `PostMainDebtAsync(payload, token, ct)` retorna `SerasaInclusionResponse { TransactionId, Status }`.
- Método `PostGuarantorAsync(payload, token, ct)` retorna o mesmo DTO.
- Cabeçalhos: `Authorization: Bearer {token}`, `Accept: application/json`, `Content-Type: application/json`.
- Em caso de erro HTTP, lançar `SerasaPefinHttpException` com `StatusCode`, `Body`, `Message`.
- `SerasaPefinOptions.PreviewEndpoint` removida (breaking change controlado).
- Suportar retry em `401` (recriar token) via `SerasaPefinGateway`.
</requirements>

## Subtarefas

- [ ] 4.1 Remover `GetPreviewAsync` do `SerasaPefinClient`
- [ ] 4.2 Remover `PreviewEndpoint` de `SerasaPefinOptions` + atualizar `.env`/`appsettings*`
- [ ] 4.3 Adicionar `PostMainDebtAsync` e `PostGuarantorAsync`
- [ ] 4.4 Criar `SerasaPefinHttpException` com detalhes
- [ ] 4.5 Refatorar `SerasaPefinGateway`: remover `GetPreviewAsync`, adicionar `PostMainDebtAsync` / `PostGuarantorAsync`
- [ ] 4.6 Adicionar testes unitários com `HttpMessageHandler` mock

## Detalhes de Implementação

URLs corretas (montadas via `CollectionBaseUrl.TrimEnd('/') + "/debt/"` e `... + "/debt/guarantor"`).

Resposta esperada (com base no Node `serasaPefinHttpClient.js`):
```json
{
  "transactionId": "uuid",
  "status": "ACCEPTED"
}
```

Ver Tech Spec §3.2 (fluxo) e §5.7 (URLs).

## Critérios de Sucesso

- `POST` para `/debt/` chega no host `api.serasa.dev` (validável via log).
- Resposta 200/201 é desserializada corretamente para `SerasaInclusionResponse`.
- Resposta 4xx/5xx levanta `SerasaPefinHttpException` com `Body` preservado para troubleshooting.
- Build verde sem referências a `PreviewEndpoint`.

## Testes da Tarefa

- [ ] Teste unidade: `PostMainDebtAsync_Returns200_DeserializesResponse`
- [ ] Teste unidade: `PostMainDebtAsync_Returns400_ThrowsSerasaPefinHttpException`
- [ ] Teste unidade: `PostGuarantorAsync_SendsToGuarantorEndpoint`
- [ ] Teste unidade: `PostMainDebtAsync_SetsAuthorizationHeader`
- [ ] Teste integração (manual UAT): chamada real retorna `transactionId` para mass document

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\Integrations\SerasaPefin\SerasaPefinClient.cs` (reescrever)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\Integrations\SerasaPefin\SerasaPefinGateway.cs` (refatorar)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\Configuration\SerasaPefinOptions.cs` (remover PreviewEndpoint)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure.Tests\Integrations\SerasaPefin\` (novos testes)
- `@c:\api-inadimplencia\src\modules\inadimplencia\services\serasaPefinHttpClient.js` (referência)
- `@c:\api-inadimplencia\documentos\guia-integracao-serasa-pefin.md` (contrato)
