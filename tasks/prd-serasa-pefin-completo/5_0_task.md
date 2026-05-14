# Tarefa 5.0: Refatorar `RequestNegativacaoCommandHandler`

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Esta é a tarefa central: orquestrar **inclusão completa** (principal + N
avalistas) com persistência atômica antes do envio, chamada real ao Serasa e
atualização pós-resposta. Implementar **TDD** (red → green → refactor).

<requirements>
- Carregar venda + fiadores via `IInadimplenciaQueryService`.
- Construir payloads via `SerasaPefinPayloadBuilder.BuildMainDebt` + `BuildGuarantor` (1 por fiador, quando `IncluirGarantidores=true`).
- Persistir **antes** do envio: 1 row PRINCIPAL + N rows GARANTIDOR (FK `ID_SOLICITACAO_PRINCIPAL`), `STATUS=PENDENTE_ENVIO`, dentro de transação SERIALIZABLE.
- Em caso de dedupe (`SerasaPefinDuplicateActiveException`), retornar HTTP 409 com `existing` populado.
- Em caso de validação (`SerasaPefinValidationException`), retornar HTTP 400 com `missingFields` / `blockedDocuments`.
- Enviar ao Serasa (principal primeiro, depois fiadores).
- Após cada `PostMainDebt`/`PostGuarantor`: chamar `Repository.UpdateAsync` com `MarcarAguardandoRetorno(transactionId)`.
- Em caso de `SerasaPefinHttpException`: `MarcarFalhaEnvio(message, statusCode)` + persistir.
- `Operador` vem de claim `User.Identity.Name` (ou `"system"` em fallback).
- `IdAssociado`, `TipoAssociacao` propagados do fiador.
- Resposta inclui `transactionsId` por solicitação e status agregado.
</requirements>

## Subtarefas

- [ ] 5.1 Reescrever DTOs (`RequestNegativacaoCommand`, `RequestNegativacaoResponse`) com `incluirGarantidores`, lista de retorno
- [ ] 5.2 Reescrever `RequestNegativacaoCommandHandler` com fluxo PRD §4 RF-02
- [ ] 5.3 Mapear exceções de domínio → ProblemDetails (Endpoint nivel API com status codes corretos)
- [ ] 5.4 Adicionar testes unitários cobrindo cenários sucesso, falha HTTP, falha de validação, dedupe
- [ ] 5.5 Adicionar logs estruturados (`Serasa.Inclusion {NumVenda} {SolicitacaoId} {TransactionId}`)
- [ ] 5.6 Aplicar mascaramento ao serializar `PAYLOAD_AUDITORIA`

## Detalhes de Implementação

Ver Tech Spec §3.2 (fluxo) e referência Node em
`serasaPefinService.js:createPendingSolicitations` + `submitSerasaInclusion`.

Sequência mínima do handler:
```
1. venda = queryService.GetVendaAsync(numVenda)
2. if venda == null → DomainNotFoundException
3. fiadores = queryService.ListFiadoresAsync(numVenda)
4. (payloadPrincipal, jsonPrincipal) = builder.BuildMainDebt(...)
5. payloadsGarantidor = fiadores.Select(f => builder.BuildGuarantor(...))
6. ID_principal = await repository.AddAsync(SolicitacaoPrincipal)
7. foreach garantidor: repository.AddAsync(SolicitacaoGarantidor with FK)
8. response = await client.PostMainDebtAsync(payloadPrincipal, token)
9. solicitacaoPrincipal.MarcarAguardandoRetorno(response.TransactionId)
   await repository.UpdateAsync(solicitacaoPrincipal)
10. foreach garantidor:
      respG = await client.PostGuarantorAsync(payloadG, token)
      garantidor.MarcarAguardandoRetorno(respG.TransactionId)
      await repository.UpdateAsync(garantidor)
11. return RequestNegativacaoResponse(...)
```

## Critérios de Sucesso

- `POST /serasa-pefin/negativar` com mass document UAT (`00001209523`) retorna 200 com `transactionId` populado.
- Banco mostra `STATUS=AGUARDANDO_RETORNO` após sucesso.
- Banco mostra `STATUS=NEGATIVADO_ERRO` quando Serasa retorna 4xx/5xx.
- Tentativa duplicada concorrente: uma sucede, outra recebe 409.
- `PAYLOAD_AUDITORIA` armazenado com documentos mascarados.
- Cobertura de testes ≥ 80% no handler.

## Testes da Tarefa

- [ ] Teste unidade: `Handle_ValidInput_PersistsAndPosts`
- [ ] Teste unidade: `Handle_ValidationFail_NoPersist_ThrowsValidationException`
- [ ] Teste unidade: `Handle_DuplicateActive_PropagatesDuplicateException`
- [ ] Teste unidade: `Handle_HttpFailure_MarksSolicitacaoFailed`
- [ ] Teste unidade: `Handle_VendaNotFound_ThrowsDomainNotFoundException`
- [ ] Teste unidade: `Handle_IncluirGarantidoresFalse_OnlyMainDebtPosted`
- [ ] Teste unidade: `Handle_GuarantorHttpFailure_MainStillSucceeds_GuarantorMarkedFailed`
- [ ] Teste integração: `POST /negativar` end-to-end com banco real (mass UAT mock client)

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Commands\RequestNegativacaoCommandHandler.cs` (reescrever)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Dtos\RequestNegativacaoCommand.cs` (atualizar)
- `@c:\api-inadimplencia-docker\api-inadimplencia.Api\Endpoints\InadimplenciaEndpoints.cs` (mapear exceções → status)
- `@c:\api-inadimplencia\src\modules\inadimplencia\services\serasaPefinService.js` (linhas 410-650, referência)
