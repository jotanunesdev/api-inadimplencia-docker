# PRD — Migração completa do módulo Serasa PEFIN para .NET

## 1. Contexto

O módulo Serasa PEFIN expõe negativações de inadimplentes via API Serasa Experian.
Já existe uma implementação **Node.js de referência** em
`c:\api-inadimplencia\src\modules\inadimplencia\` que serve como **fonte de verdade
de regras de negócio**. A implementação atual em .NET é parcial (apenas stubs
HTTP) e precisa atingir **paridade funcional completa** com o Node antes da
substituição em produção.

Documentação Serasa oficial: `c:\api-inadimplencia\documentos\guia-integracao-serasa-pefin.md`.

## 2. Objetivo

Entregar em .NET 9 o módulo Serasa PEFIN com **paridade 100%** ao Node, incluindo:

- **Preview** baseado em consulta ao DW (sem chamada à Serasa).
- **Inclusão** (negativação principal e avalistas) com persistência transacional.
- **Webhooks** idempotentes para os 6 eventos (inclusão/avalista/baixa × sucesso/erro).
- **Histórico e detalhe** consultando o banco local.
- **Rotas de teste** auxiliares para diagnóstico em UAT.

## 3. Personas

- **Cliente API (frontend / outros serviços)**: consome `/serasa-pefin/*`.
- **Operador de cobrança**: dispara negativações via UI; vê histórico e detalhe.
- **Serasa (sistema externo)**: envia webhooks para callbacks de eventos.
- **DevOps / Operação**: usa rotas de teste para diagnóstico em UAT.

## 4. Requisitos funcionais

### RF-01 Preview (`GET /serasa-pefin/vendas/{numVenda}/preview`)
- Buscar inadimplente em `DW.fat_analise_inadimplencia_v4` (apenas `INADIMPLENTE='SIM'`).
- Buscar fiadores em `DW.vw_fiadores_por_venda` (FIADOR / CONJUGE / CESSIONARIO / COOBRIGADO).
- Validar: documento UAT autorizado (quando `Env=uat`), valor mínimo (R$ 10,00),
  formato de data, endereço completo, duplicidade ativa (`STATUS IN ('PENDENTE_ENVIO','ENVIADO_SERASA','AGUARDANDO_RETORNO')`).
- Mascarar documentos de devedor / credor / fiador (CPF/CNPJ).
- Retornar `elegivel: boolean`, `blocks: [...]`, `garantidores: [...]`, campos da venda.
- **NÃO chamar Serasa.**

### RF-02 Inclusão (`POST /serasa-pefin/negativar`)
- Receber `numVenda` + `incluirGarantidores: bool`.
- Carregar dados via Query Service (mesma fonte do preview).
- Construir 1 payload principal (`POST /collection/debt/`) e N payloads de avalista (`POST /collection/debt/guarantor`).
- Em **uma transação SERIALIZABLE**: validar duplicidade ativa + inserir linhas com `STATUS=PENDENTE_ENVIO` antes do envio.
- Após resposta da Serasa: atualizar `TRANSACTION_ID`, `STATUS=AGUARDANDO_RETORNO`, `PAYLOAD_AUDITORIA` (com docs mascarados).
- Em caso de erro HTTP: marcar `STATUS=NEGATIVADO_ERRO` e gravar `ERROR_MESSAGE` + `ERROR_STATUS_CODE`.
- Idempotência por índice único filtrado `UX_SERASA_PEFIN_SOLICITACOES_ATIVA`.

### RF-03 Histórico e detalhe
- `GET /serasa-pefin/vendas/{numVenda}/historico`: lista solicitações da venda (banco), ordenadas por `DT_CRIACAO DESC`.
- `GET /serasa-pefin/negativacoes/{id}`: detalhe de uma solicitação (banco), com payload de auditoria e payload do webhook.
- `GET /serasa-pefin/acompanhamento/{transactionId}`: detalhe por `TRANSACTION_ID`.

### RF-04 Webhooks
- 6 endpoints `POST`:
  - `/serasa-pefin/webhooks/inclusao/{sucesso|erro}`
  - `/serasa-pefin/webhooks/avalista/{sucesso|erro}`
  - `/serasa-pefin/webhooks/baixa/{sucesso|erro}`
- Cada chamada:
  1. Persistir webhook em `SERASA_PEFIN_WEBHOOKS` (UUID do payload como chave de idempotência).
  2. Se `UUID` já processado, retornar 200 sem reprocessar (idempotência).
  3. Resolver `MATCHED_SOLICITACAO_ID` via `TRANSACTION_ID`.
  4. Atualizar `STATUS` da solicitação conforme tipo do webhook.
  5. Marcar `PROCESSADO=1` ou registrar `MENSAGEM_ERRO`.

### RF-05 Rotas de teste (somente UAT)
- `POST /serasa-pefin/test/auth`: força obtenção de token Serasa.
- `POST /serasa-pefin/test/debt`: envia payload arbitrário a `/collection/debt/`.
- `GET /serasa-pefin/test/documents`: lista os 8 documentos UAT autorizados.
- `POST /serasa-pefin/test/simulate-webhook`: simula payload de webhook.
- Bloqueadas quando `SerasaPefin:Env != "uat"`.

## 5. Requisitos não-funcionais

- **Persistência**: SQL Server (`dwjnc`), tabelas `dbo.SERASA_PEFIN_SOLICITACOES` e `dbo.SERASA_PEFIN_WEBHOOKS` (scripts em `db/003_*.sql` e `db/004_*.sql`).
- **Transações**: `IsolationLevel.Serializable` em escritas que envolvem dedupe.
- **Mascaramento**: documentos devem aparecer mascarados em logs e em `PAYLOAD_AUDITORIA`.
- **Idempotência**: webhook reentrante não pode duplicar atualização.
- **Concorrência**: índice único filtrado garante exclusão de solicitação ativa duplicada.
- **Observabilidade**: log estruturado com `numVenda`, `transactionId`, `documento mascarado`.
- **Ambiente**: a configuração `SerasaPefin:Env` controla validação UAT e fallback SSL.

## 6. Regras de negócio críticas

| Regra | Origem |
|---|---|
| Apenas vendas com `INADIMPLENTE='SIM'` | `serasaPefinModel.js:findInadimplenciaByNumVenda` |
| Valor mínimo R$ 10,00 | `serasaPefinPayloadBuilder.js:SERASA_CONSTANTS.MIN_VALUE` |
| `CATEGORY_ID='FI'` (Financiamento) | `serasaPefinPayloadBuilder.js:SERASA_CONSTANTS` |
| `DEBT_TYPE='PEFIN'` | idem |
| 8 documentos autorizados em UAT | `serasaPefinPayloadBuilder.js:UAT_TEST_DOCUMENTS` |
| Tipos de fiador válidos | `serasaPefinModel.js:findGuarantorsByNumVenda` |
| Documento sempre digits-only | `serasaPefinPayloadBuilder.js:digitsOnly` |
| Dedupe por (NumVenda, ContractNumber, DocDevedor, DocGarantidor, TipoRegistro) + status ativo | `UX_SERASA_PEFIN_SOLICITACOES_ATIVA` |

## 7. Fora do escopo

- Reativar `MassTransit Outbox` (desativado por outras razões).
- Implementar baixa proativa (`POST /collection/debt/{transactionId}/cancel`) — apenas
  estrutura/status; o endpoint específico de baixa fica para fase futura.
- Refatorar `SerasaPefinSolicitacao` original (legado) — mantida; nova entidade
  `SerasaPefinSolicitacaoCompleta` substitui.

## 8. Critérios de aceitação globais

- Build verde, testes unitários e de integração passando.
- `GET /preview/295` retorna dados reais do DW + flags de validação.
- `POST /negativar` com mass UAT retorna 200/201 + `TRANSACTION_ID` real do Serasa.
- Reentrada de webhook não duplica registros.
- Tentativa de envio duplicado é bloqueada com HTTP 409.
- Rotas de teste retornam 404 quando `Env != uat`.
