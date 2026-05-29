# Tarefa 2.0: Implementar `ListSolicitacoesPendentesQueryHandler` real

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Substituir o handler placeholder que retorna `Array.Empty<>` por uma implementacao funcional que aceita filtros e usa o novo `ListByStatusAsync`.

<requirements>
- Atualizar `ListSolicitacoesPendentesQuery` para aceitar `Status`, `NumVenda`, `SolicitacaoId`, `SolicitanteUsername`, `Take`, `Skip` (opcionais).
- Implementar handler chamando `ListByStatusAsync`.
- Mapear `SerasaPefinSolicitacaoCompleta` para `SolicitacaoPendenteDto`.
- Atualizar callers/registros DI se a assinatura mudar.
</requirements>

## Subtarefas

- [ ] 2.1 Expandir record `ListSolicitacoesPendentesQuery`.
- [ ] 2.2 Implementar handler.
- [ ] 2.3 Atualizar testes existentes do handler.

## Detalhes de Implementacao

- `Status` deve mapear string -> `SerasaPefinStatus` (default `AguardandoAprovacao`).
- Quando `SolicitacaoId` informado, ignorar outros filtros que conflitem.
- Ordenar por `DtSolicitacao DESC`.

## Criterios de Sucesso

- Endpoint passa a retornar dados reais.
- Filtros funcionam combinados.

## Testes da Tarefa

- [ ] Unit tests do handler para cada combinacao de filtros.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQuery.cs`
- `ApiInadimplencia.Application/Features/Negativacao/Queries/ListSolicitacoesPendentesQueryHandler.cs`
- `ApiInadimplencia.Application.Tests/Features/Negativacao/ListSolicitacoesPendentesQueryHandlerTests.cs`
