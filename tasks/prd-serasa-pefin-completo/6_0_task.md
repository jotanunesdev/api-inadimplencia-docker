# Tarefa 6.0: Endpoints de Histórico e Detalhe

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Refatorar `GetSerasaHistoricoQueryHandler` e `GetSerasaAcompanhamentoQueryHandler`
para usarem o `ISerasaPefinRepository` (banco local), e adicionar um novo
endpoint `GET /serasa-pefin/negativacoes/{id}` para detalhe por `ID`.

<requirements>
- `GET /serasa-pefin/vendas/{numVenda}/historico`: lista `Repository.ListByNumVendaAsync` ordenado `DT_CRIACAO DESC`.
- `GET /serasa-pefin/acompanhamento/{transactionId}`: detalhe via `Repository.GetByTransactionIdAsync` → 404 se nulo.
- `GET /serasa-pefin/negativacoes/{id}`: detalhe via `Repository.GetByIdAsync(Guid)` → 404 se nulo.
- DTO retorna documentos mascarados, status canônico (`PENDENTE_ENVIO` etc), payload de auditoria e webhook payload (já mascarado).
- Suporte a `Guid` inválido → 400.
</requirements>

## Subtarefas

- [ ] 6.1 Criar `GetNegativacaoByIdQuery` + handler usando `Repository.GetByIdAsync`
- [ ] 6.2 Refatorar `GetSerasaHistoricoQueryHandler` (remover dependência do gateway, usar Repository)
- [ ] 6.3 Refatorar `GetSerasaAcompanhamentoQueryHandler` (usar Repository)
- [ ] 6.4 Atualizar DTOs (`SerasaPefinDetalheDto`) com novos campos
- [ ] 6.5 Mapear endpoint no `InadimplenciaEndpoints.cs`
- [ ] 6.6 Testes unitários para os 3 handlers
- [ ] 6.7 Testes integração HTTP (200/404/400)

## Detalhes de Implementação

Ver Tech Spec §3.4 (histórico e detalhe).

Mapper sugerido:
```csharp
static SerasaPefinDetalheDto Map(SerasaPefinSolicitacaoCompleta s) => new(
    Id: s.Id,
    NumVenda: s.NumVendaFk,
    TipoRegistro: s.TipoRegistro.ToDbValue(),
    Status: s.Status.ToDbValue(),
    DocumentoDevedorMascarado: SerasaPefinPayloadBuilder.MaskDocument(s.DocumentoDevedor),
    ...,
    PayloadAuditoria: JsonElement.Parse(s.PayloadAuditoria),
    WebhookPayload: s.WebhookPayload is null ? null : JsonElement.Parse(s.WebhookPayload));
```

## Critérios de Sucesso

- `GET /vendas/295/historico` retorna lista correta após inclusão.
- `GET /negativacoes/{id}` válido retorna 200 com payload.
- `GET /negativacoes/{id}` inexistente retorna 404 problem+json.
- `GET /acompanhamento/abc-123` inexistente retorna 404.

## Testes da Tarefa

- [ ] Teste unidade: `Historico_NoRows_ReturnsEmpty`
- [ ] Teste unidade: `Historico_Multiple_OrderedDesc`
- [ ] Teste unidade: `GetById_NotFound_ReturnsNull`
- [ ] Teste unidade: `Acompanhamento_FoundByTransactionId_ReturnsDto`
- [ ] Teste integração: `GET /historico` retorna 200 com lista
- [ ] Teste integração: `GET /negativacoes/{id}` retorna 404 quando não existe

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Queries\GetSerasaHistoricoQueryHandler.cs` (refatorar)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Queries\GetSerasaAcompanhamentoQueryHandler.cs` (refatorar)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Queries\GetNegativacaoByIdQueryHandler.cs` (novo)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Dtos\` (atualizar)
- `@c:\api-inadimplencia-docker\api-inadimplencia.Api\Endpoints\InadimplenciaEndpoints.cs` (adicionar rota)
