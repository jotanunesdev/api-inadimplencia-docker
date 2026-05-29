# Tech Spec - Fix de Listagem e Detalhe de Solicitacao de Negativacao

## Resumo Executivo

Duas mudancas concentradas:

1. Persistencia: adicionar metodo `ListByStatusAsync(status, numVenda?, solicitacaoId?, solicitanteUsername?)` na port `ISerasaPefinRepository` e implementar em `SqlServerSerasaPefinRepository`. Reusar `GetByIdAsync` (verificar se ja inclui parcelas; se nao, expandir).
2. API: substituir handler placeholder por implementacao real + criar `GetSolicitacaoByIdQuery` + handler + endpoint REST.

Tudo segue o padrao CQRS existente (`ICommandHandler` / `IQueryHandler`).

## Arquitetura

### Componentes novos

- `ApiInadimplencia.Application/Features/Negativacao/Queries/GetSolicitacaoByIdQuery.cs`
- `ApiInadimplencia.Application/Features/Negativacao/Queries/GetSolicitacaoByIdQueryHandler.cs`
- `ApiInadimplencia.Application/Features/Negativacao/Dtos/SolicitacaoDetalheDto.cs` (novo DTO contendo parcelas, fiadores, status)
- Migration ou ajuste de index, se necessario para suporte a filtros (`IX_SerasaPefinSolicitacoes_Status_NumVenda`).

### Componentes modificados

- `ApiInadimplencia.Application/Abstractions/Persistence/ISerasaPefinRepository.cs` - adicionar `ListByStatusAsync`.
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinRepository.cs` - implementacao.
- `ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQuery.cs` - acrescentar filtros opcionais.
- `ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQueryHandler.cs` - implementacao real.
- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` - expandir endpoint de listagem e adicionar `GET /solicitacoes/{id}`.

## Design de Implementacao

### Repository

```csharp
Task<IReadOnlyList<SerasaPefinSolicitacaoCompleta>> ListByStatusAsync(
    SerasaPefinStatus? status,
    int? numVenda,
    Guid? solicitacaoId,
    string? solicitanteUsername,
    int take,
    int skip,
    CancellationToken cancellationToken);
```

Implementacao SQL usa parametros nulos e gera SQL com `WHERE` condicional ou monta com `IQueryable`. Ordenar `DtSolicitacao DESC`.

Index sugerido (verificar se ja existe):
```sql
CREATE INDEX IX_SerasaPefinSolicitacoes_Status_NumVenda
  ON SerasaPefinSolicitacoes (Status, NumVendaFk) INCLUDE (DtSolicitacao);
```

### Query de detalhe

```csharp
public sealed record GetSolicitacaoByIdQuery(Guid Id) : IQuery<SolicitacaoDetalheDto?>;

public sealed record SolicitacaoDetalheDto(
    Guid Id,
    int NumVenda,
    string Cliente,
    string CpfMasked,
    string? Cpf,
    string SolicitanteUsername,
    DateTime DtSolicitacao,
    string Status,
    decimal Valor,
    bool IncluirFiadores,
    bool PodeDecidir,
    IReadOnlyList<ParcelaDto> Parcelas,
    IReadOnlyList<FiadorDto> Fiadores);
```

Handler:

1. `var solicitacao = await _repo.GetByIdAsync(query.Id, ct);`
2. Se null, retornar `null` (endpoint mapeia para 404).
3. Recuperar parcelas associadas (do proprio aggregate, ou via `GetDividasElegiveis` por venda + cruzamento de ids da solicitacao).
4. Resolver `PodeDecidir`:
   - `IAprovadoresPolicy.IsAprovador(currentUser)` true E
   - `solicitacao.Status == AguardandoAprovacao` E
   - `solicitacao.SolicitanteUsername != currentUser`
5. Montar DTO.

### Endpoints

```csharp
negativacao.MapGet("/solicitacoes", async (
    [FromQuery] string? status,
    [FromQuery] int? numVenda,
    [FromQuery] Guid? solicitacaoId,
    [FromQuery] string? solicitanteUsername,
    [FromQuery] int? take,
    [FromQuery] int? skip,
    [FromServices] IQueryHandler<ListSolicitacoesPendentesQuery, IReadOnlyList<SolicitacaoPendenteDto>> handler,
    CancellationToken cancellationToken) =>
{
    var q = new ListSolicitacoesPendentesQuery(status, numVenda, solicitacaoId, solicitanteUsername, take ?? 50, skip ?? 0);
    var result = await handler.HandleAsync(q, cancellationToken);
    return Results.Ok(new { data = result });
}).WithName("ListSolicitacoesNegativacao");

negativacao.MapGet("/solicitacoes/{id:guid}", async (
    Guid id,
    [FromServices] IQueryHandler<GetSolicitacaoByIdQuery, SolicitacaoDetalheDto?> handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(new GetSolicitacaoByIdQuery(id), cancellationToken);
    return result is null ? Results.NotFound(new { error = "NAO_ENCONTRADA" }) : Results.Ok(result);
}).WithName("GetSolicitacaoById");
```

### Compatibilidade

- O frontend ja espera shape `{ id, numVenda, cliente, cpfMasked, parcelas: [...], ... }`. Garantir nomes JSON em camelCase (configuracao ja deve estar em `Program.cs`).

## Testes

### Unitarios

- `GetSolicitacaoByIdQueryHandlerTests`: feliz, nao encontrado, podeDecidir true/false.
- `ListSolicitacoesPendentesQueryHandlerTests`: filtros combinados, paginacao, ordenacao.

### Integracao

- `NegativacaoFluxoEndpointsIntegrationTests`: GET por id, listar por numVenda, listar por solicitacaoId, listar com filtros multiplos.

## Sequenciamento

1. Adicionar `ListByStatusAsync` + impl SQL.
2. Implementar `ListSolicitacoesPendentesQueryHandler` real e expandir DTO/query.
3. Criar `GetSolicitacaoByIdQuery` + handler + DTO.
4. Atualizar endpoints REST.
5. Testes integracao.

## Riscos

- `SerasaPefinSolicitacaoCompleta` pode nao agregar parcelas hoje. Verificar e expandir o aggregate (ou usar repositorio de parcelas) sem quebrar invariantes.
- Index novo pode requerer migration. Documentar.
- Mudanca de assinatura do `ListSolicitacoesPendentesQuery` quebra os callers do handler. Buscar usos e atualizar.

## Arquivos relevantes

Novos:

```text
ApiInadimplencia.Application/Features/Negativacao/Queries/GetSolicitacaoByIdQuery.cs
ApiInadimplencia.Application/Features/Negativacao/Queries/GetSolicitacaoByIdQueryHandler.cs
ApiInadimplencia.Application/Features/Negativacao/Dtos/SolicitacaoDetalheDto.cs
ApiInadimplencia.Application.Tests/Features/Negativacao/GetSolicitacaoByIdQueryHandlerTests.cs
```

Modificados:

```text
ApiInadimplencia.Application/Abstractions/Persistence/ISerasaPefinRepository.cs
ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinRepository.cs
ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQuery.cs
ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQueryHandler.cs
ApiInadimplencia.Application.Tests/Features/Negativacao/ListSolicitacoesPendentesQueryHandlerTests.cs
api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs
api-inadimplencia.Api.Tests/Features/Negativacao/NegativacaoFluxoEndpointsIntegrationTests.cs
```
