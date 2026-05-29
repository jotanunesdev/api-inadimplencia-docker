# PRD - Fix de Listagem e Detalhe de Solicitacao de Negativacao (Backend)

## Contexto

O frontend depende de duas operacoes que o backend hoje nao entrega corretamente:

1. **Listar solicitacoes pendentes filtrando por `numVenda`/`solicitacaoId`**: o endpoint `GET /inadimplencia/negativacao/solicitacoes` aceita apenas `?status` e o handler `ListSolicitacoesPendentesQueryHandler` retorna `Array.Empty<SolicitacaoPendenteDto>()`. Isso bloqueia a abertura do modal de decisao quando o aprovador clica na notificacao.

2. **Obter UMA solicitacao por id com parcelas**: nao existe endpoint REST `GET /negativacao/solicitacoes/{id}`. O frontend precisa desse contrato para abrir o modal de decisao com os dados completos sem precisar listar tudo.

## Objetivo

Entregar dois pontos REST consistentes para o frontend:

1. `GET /negativacao/solicitacoes` com filtros opcionais (`status`, `numVenda`, `solicitacaoId`, `solicitanteUsername`).
2. `GET /negativacao/solicitacoes/{id}` retornando a solicitacao completa (com parcelas, fiadores, status).

## Escopo

- Adicionar `ListByStatusAsync` (ou metodo equivalente com filtros) em `ISerasaPefinRepository` + implementacao SQL.
- Adicionar `GetByIdComParcelasAsync` em `ISerasaPefinRepository` retornando solicitacao com parcelas (ou reusar `GetByIdAsync` se ja traz parcelas; verificar o modelo).
- Criar `GetSolicitacaoByIdQuery` + handler que retorna DTO compativel com o tipo `NegativacaoSolicitacaoResponse` do frontend.
- Implementar `ListSolicitacoesPendentesQueryHandler` real, com filtros e respeitando `IAprovadoresPolicy` quando aplicavel.
- Expor endpoints REST.
- Definir e respeitar status de erro padrao: `404 NAO_ENCONTRADA`, `403 NAO_AUTORIZADO`.

## Fora do escopo

- Refatoracao de envio por parcela (Entrega B em PRD separado).
- Alteracao do contrato de decisao (`POST /solicitacoes/{id}/decisao`).

## Personas

- Frontend de aprovacao (consumidor REST).

## Criterios de aceite

- `GET /negativacao/solicitacoes?solicitacaoId=GUID` retorna apenas a solicitacao correspondente (ou vazio).
- `GET /negativacao/solicitacoes?numVenda=123&status=AGUARDANDO_APROVACAO` retorna a lista filtrada.
- `GET /negativacao/solicitacoes/{id}` retorna a solicitacao completa (com parcelas) ou 404.
- Testes de integracao cobrindo filtros e detalhe.
- Auditoria mantida (logging existente).
- Performance: index/filtro no DB para evitar full scan em `SerasaPefinSolicitacoes`.

## Dependencias

- Frontend `prd-fix-modal-aprovacao-negativacao-frontend` consome esses endpoints.
