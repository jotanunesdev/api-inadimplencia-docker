# Tech Spec - Envio Individual por Parcela ao Serasa

## Resumo Executivo

A mudanca afeta o aggregate `SerasaPefinSolicitacaoCompleta`, o builder de payload, o handler de request/decide, o webhook handler e o schema da tabela `SERASA_PEFIN_SOLICITACOES`. A decisao chave e **como modelar a parcela**: como nova coluna do aggregate atual (1 registro = 1 parcela), ou como entidade filha. Esta techspec adota a abordagem **1 registro por parcela** porque mantem a semantica original (cada linha = 1 titulo enviado ao Serasa) e simplifica matching de webhook.

## Modelagem

### Aggregate `SerasaPefinSolicitacaoCompleta`

Acrescentar campos:

- `NumeroParcela int` (numero da parcela na venda)
- `ParcelaIdOrigem string` (id da parcela no sistema de origem; ex.: id do titulo)
- `IdSolicitacaoPai Guid?` (referencia para solicitacao "pai" que agrupa as N parcelas - opcional)

Alterar `Valor` e `DataVencimento` para refletir os da parcela (nao mais o agregado da venda).

### Solicitacao "pai"

Decisao: nao criar uma entidade separada para a "solicitacao pai" no banco. O agrupamento e logico via `IdSolicitacaoPai`. A primeira solicitacao criada (parcela com menor numero) tera `IdSolicitacaoPai = null` e sera referenciada pelas demais. Alternativa: criar tabela `SERASA_PEFIN_SOLICITACOES_PAI` (avaliar trade-off de complexidade na tarefa 1.0).

### Index unico

Atualizar `UX_SERASA_PEFIN_SOLICITACOES_ATIVA`:

```sql
ALTER ... ON dbo.SERASA_PEFIN_SOLICITACOES (
    NumVendaFk, ContractNumber, DocumentoDevedor, DocumentoGarantidor,
    TipoRegistro, NumeroParcela
) WHERE Status IN (...ativos...);
```

## Componentes modificados

### `RequestNegativacaoCommandHandler`

Pseudocodigo do novo fluxo:

```text
parcelas = LoadParcelasElegiveis(command.NumVenda, command.ParcelaIds)
solicitacaoPaiId = null

foreach parcela in parcelas:
    payload = payloadBuilder.BuildMain(parcela, ...)
    s = SerasaPefinSolicitacaoCompleta.Criar(
        numVenda, principal, parcela.id, parcela.numero,
        parcela.valor, parcela.vencimento, ...,
        idSolicitacaoPai: solicitacaoPaiId)
    repo.AddAsync(s)
    if solicitacaoPaiId == null: solicitacaoPaiId = s.Id

    try:
        resp = gateway.PostMainDebt(payload)
        s.MarcarAguardandoRetorno(resp.transactionId)
    catch http:
        s.MarcarFalhaEnvio(...)
    repo.UpdateAsync(s)

    if command.IncluirGarantidores:
        foreach fiador in fiadores:
            payload = payloadBuilder.BuildGuarantor(parcela, fiador, ...)
            sf = SerasaPefinSolicitacaoCompleta.Criar(..., tipoRegistro=Garantidor,
                  documentoGarantidor=fiador.doc, idSolicitacaoPrincipal=s.Id,
                  idSolicitacaoPai: solicitacaoPaiId, numeroParcela=parcela.numero)
            ... mesmo padrao ...

agregar resultados em SerasaSolicitacaoResult[]
```

### `DecideNegativacaoCommandHandler`

- Validar aprovador + senha (mantem).
- Em vez de chamar `_requestNegativacaoHandler` uma vez, ja delega para o RequestHandler novo que itera nas parcelas.
- Apos a iteracao, calcular status agregado:
  - `TodasAguardandoRetorno`: ok
  - `AlgumasFalha`: marcar solicitacao pai como `AprovadaParcial` (novo status?) ou expor agregacao via query
  - `TodasFalha`: `AprovadaFalhaEnvio`
- Notificacao para solicitante + aprovador com resumo `"5 de 5 parcelas enviadas"` ou `"3 de 5 enviadas; 2 com erro"`.

### `PayloadBuilder`

`BuildMain(parcela, ...)` deve gerar payload usando `parcela.valor` e `parcela.dataVencimento` (nao mais `venda.valor` / `venda.dataVencimento`). `contractNumber` pode incluir sufixo `-P{numero}` para identificacao.

### Webhook

`ApplyWebhookTransactionalAsync` ja casa por `transactionId`. Continua valido: cada parcela tem seu proprio `transactionId`. Nao precisa mudar.

## Migracao de dados existentes

Solicitacoes ja criadas antes do deploy:

- Permanecem com `NumeroParcela = null`.
- Index unico ignora NULL via filtro condicional (`AND NumeroParcela IS NOT NULL`).
- Codigo deve tolerar `NumeroParcela = null` para compatibilidade retroativa.

## Testes

### Unitarios

- `RequestNegativacaoCommandHandlerTests`: cenarios feliz (N parcelas), falha parcial, falha total, com/sem fiadores.
- `DecideNegativacaoCommandHandlerTests`: status agregado correto.
- `PayloadBuilderTests`: payload por parcela contem valor/vencimento da parcela.

### Integracao

- `FluxoNegativacaoE2ETests`: cenarios E2E completos.

## Sequenciamento

1. Migration: adicionar colunas `NumeroParcela`, `ParcelaIdOrigem`, `IdSolicitacaoPai` + atualizar index.
2. Aggregate: estender `SerasaPefinSolicitacaoCompleta.Criar` para receber novos campos.
3. Repositorio: persistir/ler os novos campos.
4. `PayloadBuilder` para usar dados da parcela.
5. `RequestNegativacaoCommandHandler` para iterar nas parcelas.
6. `DecideNegativacaoCommandHandler` para agregar status.
7. Testes unit + integration.
8. Documentar comportamento e atualizar OpenAPI.

## Riscos

- **Volume de chamadas Serasa**: N parcelas multiplica chamadas; avaliar rate-limit do gateway e adicionar throttling se necessario.
- **Idempotencia**: garantir que reaprovar nao duplique envios (verificar status atual da parcela antes de chamar gateway).
- **Backwards compatibility**: solicitacoes pre-existentes nao tem `NumeroParcela`. Documentar e tratar.
- **Index migration**: pode requerer downtime curto; planejar com DBA.
- **Estado parcial dificil de reverter**: definir politica clara (retry manual via novo endpoint? auto-retry?).

## Arquivos relevantes

Novos:

```text
ApiInadimplencia.Domain/Negativacao/SerasaSolicitacaoStatusAgregado.cs (enum opcional)
db/migrations/2026XX_serasa_parcela.sql
```

Modificados:

```text
ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacaoCompleta.cs
ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommand.cs
ApiInadimplencia.Application/Features/SerasaPefin/Commands/RequestNegativacaoCommandHandler.cs
ApiInadimplencia.Application/Features/Negativacao/Commands/DecideNegativacaoCommandHandler.cs
ApiInadimplencia.Application/Features/SerasaPefin/Services/IPayloadBuilder.cs
ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinRepository.cs
ApiInadimplencia.Infrastructure/Persistence/SqlServer/Mappings/*
ApiInadimplencia.Application.Tests/Features/SerasaPefin/*
ApiInadimplencia.Application.Tests/Features/Negativacao/Commands/DecideNegativacaoCommandHandlerTests.cs
api-inadimplencia.Api.Tests/E2E/FluxoNegativacaoE2ETests.cs
```
