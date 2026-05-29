# Tarefa 3.0: `GetSolicitacaoByIdQuery` + handler + DTO `SolicitacaoDetalheDto`

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Criar a query CQRS, handler e DTO que retorna a solicitacao com parcelas e fiadores para o frontend.

<requirements>
- Criar `GetSolicitacaoByIdQuery(Guid Id) : IQuery<SolicitacaoDetalheDto?>`.
- Criar handler que monta o DTO a partir do aggregate + parcelas elegiveis.
- Resolver `PodeDecidir` usando `IAprovadoresPolicy` + `ICurrentUserService` + regra `SOLICITANTE_NAO_PODE_APROVAR`.
- Registrar no container DI.
</requirements>

## Subtarefas

- [ ] 3.1 Criar `SolicitacaoDetalheDto` com a forma esperada pelo frontend.
- [ ] 3.2 Criar query e handler.
- [ ] 3.3 Resolver parcelas (verificar como sao persistidas hoje; expandir aggregate se necessario).
- [ ] 3.4 Registrar handler em `Application/DependencyInjection.cs`.

## Detalhes de Implementacao

- Para parcelas: hoje o handler de aprovacao tem TODO "Get actual parcela IDs". Resolver isso aqui: se a solicitacao guarda `parcelaIds` no payload de auditoria, ler dali; se nao, expor metodo no repositorio ou armazenar como coluna JSON. Documentar a decisao.
- DTO em camelCase para serializacao JSON.

## Criterios de Sucesso

- Handler retorna DTO completo.
- Parcelas reais batem com o que o frontend mostra (numero, valor, vencimento, diasAtraso).

## Testes da Tarefa

- [ ] Unit test feliz.
- [ ] Unit test nao encontrado -> null.
- [ ] Unit test `podeDecidir` true/false (aprovador vs solicitante, status diferente de AguardandoAprovacao).

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/Negativacao/Queries/GetSolicitacaoByIdQuery.cs`
- `ApiInadimplencia.Application/Features/Negativacao/Queries/GetSolicitacaoByIdQueryHandler.cs`
- `ApiInadimplencia.Application/Features/Negativacao/Dtos/SolicitacaoDetalheDto.cs`
- `ApiInadimplencia.Application/DependencyInjection.cs`
- `ApiInadimplencia.Application.Tests/Features/Negativacao/GetSolicitacaoByIdQueryHandlerTests.cs`
