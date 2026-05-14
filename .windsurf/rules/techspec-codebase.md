---
trigger: model_decision
description: Usar sempre que precisar tomar decisao arquitetural ou tecnica
---

Use `documentos/techspec-codebase.md` como fonte primaria para decisoes tecnicas deste repositorio.

Regras obrigatorias:

- O modulo `inadimplencia` esta sendo migrado de `C:\api-inadimplencia\src\modules\inadimplencia` para C#/.NET 8 neste repositorio.
- A arquitetura-alvo e Clean Architecture + CQRS.
- DDD deve ser aplicado onde ha regra forte: protocolo de atendimento, ocorrencias, kanban, atribuicao de responsavel, notificacoes e Serasa PEFIN.
- Event-Driven deve ser usado onde ha integracao, notificacao e processamento assincrono: scanner de vencidos, SSE, webhooks Serasa, envio Serasa, Fluig/RM e outbox futuro.
- Domain nao pode depender de ASP.NET, SQL Server, Dapper, EF, HTTP ou Docker.
- API chama commands/queries da Application; endpoints nao acessam repositories diretamente.
- Infrastructure implementa portas de Application para SQL Server, Fluig, RM, Serasa, SSE e hosted services.
- SQL deve ser sempre parametrizado.
- Dados sensiveis devem ser mascarados em logs/respostas: CPF/CNPJ, tokens, client secret, cookies Fluig e payloads Serasa.
- Docker deve usar build multi-stage e runtime non-root.
- Antes de alterar contratos REST, conferir o catalogo de rotas e regras no techspec.

Quando houver conflito entre uma decisao pontual e o techspec, atualize primeiro `documentos/techspec-codebase.md` com a nova decisao e entao implemente o codigo.
