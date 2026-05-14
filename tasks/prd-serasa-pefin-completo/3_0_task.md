# Tarefa 3.0: Refatorar `GetSerasaPreviewQueryHandler` para consultar banco

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

O preview atual chama erroneamente o Serasa. Deve ser refatorado para usar o
`IInadimplenciaQueryService` (Task 2.0) e o `SerasaPefinPayloadBuilder` (já
existe) para validar dados sem chamar Serasa.

Esta tarefa segue **TDD** (red → green → refactor).

<requirements>
- Não fazer nenhuma chamada HTTP externa.
- Retornar 404 quando venda não existe ou não é inadimplente.
- Retornar 200 com `elegivel=true|false` + `blocks=[]` + `garantidores=[]`.
- Mascarar documentos (CPF/CNPJ) com `SerasaPefinPayloadBuilder.MaskDocument`.
- Tipos de `blocks[]`: `UAT_DOCUMENT_NOT_ALLOWED`, `VALUE_BELOW_MINIMUM`, `INVALID_DUE_DATE`, `ACTIVE_DUPLICATE` (cada um com `details`).
- Duplicate check via `ISerasaPefinRepository.ExistsActiveAsync` (best-effort).
- Retornar `missingFields[]` quando endereço estiver incompleto.
- Validar via reuso das funções privadas do PayloadBuilder (refatorar visibilidade se necessário).
</requirements>

## Subtarefas

- [ ] 3.1 Atualizar/criar DTO `SerasaPefinPreviewResponse` com campos do Node (cliente, empreendimento, bloco, unidade, contractNumber, areaInformante, valor, dataVencimento, endereco, garantidores, missingFields, blocks, elegivel)
- [ ] 3.2 Extrair métodos públicos `ValidateMainDebt(input)` e `ValidateGuarantor(input)` do PayloadBuilder (retornando `ValidationResult` em vez de lançar)
- [ ] 3.3 Reescrever `GetSerasaPreviewQueryHandler` sem `ISerasaPefinGateway`
- [ ] 3.4 Mapear venda + fiadores → DTO com mascaramento
- [ ] 3.5 Implementar lista `blocks[]` conforme regras
- [ ] 3.6 Atualizar testes existentes do handler (mocks ajustados)
- [ ] 3.7 Adicionar testes para cada tipo de bloco

## Detalhes de Implementação

Replicar lógica de `serasaPefinService.js:createPreview` (Node), conforme PRD §4 RF-01
e Tech Spec §3.1.

Ordem das validações:
1. Buscar venda → se nula, retornar 404.
2. Buscar fiadores.
3. Validar UAT documents (se `Env=uat`).
4. Validar valor mínimo, data, endereço (PayloadBuilder).
5. Verificar duplicate ativo (try/catch — best effort).
6. Validar cada fiador individualmente.
7. Montar DTO.

## Critérios de Sucesso

- `GET /serasa-pefin/vendas/295/preview` retorna 200 sem chamar Serasa.
- Logs **não** mostram `Obtaining new Serasa PEFIN token`.
- Resposta inclui blocks quando aplicável.
- Resposta tem campos compatíveis com o consumidor do Node (paridade de contrato).
- Tempo de resposta < 500ms.

## Testes da Tarefa

- [ ] Teste unidade: `Handle_VendaNaoInadimplente_Returns404` (handler levanta `NotFoundException` ou retorna `null`)
- [ ] Teste unidade: `Handle_VendaValida_ReturnsElegivelTrue`
- [ ] Teste unidade: `Handle_ValorAbaixoMinimo_AddsValueBelowMinimumBlock`
- [ ] Teste unidade: `Handle_DocumentoNaoUat_AddsUatBlock` (com `Env=uat`)
- [ ] Teste unidade: `Handle_EnderecoIncompleto_PopulatesMissingFields`
- [ ] Teste unidade: `Handle_DuplicateAtivo_AddsActiveDuplicateBlock`
- [ ] Teste unidade: `Handle_NoFiadores_ReturnsEmptyGarantidoresList`
- [ ] Teste unidade: `Handle_FiadorSemEndereco_MarkedElegivelFalse`
- [ ] Teste integração: `GET /vendas/{id}/preview` retorna 200 com payload completo

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Queries\GetSerasaPreviewQueryHandler.cs` (reescrever)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Payloads\SerasaPefinPayloadBuilder.cs` (extrair `Validate*`)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Features\SerasaPefin\Dtos\` (atualizar DTOs)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application.Tests\Features\SerasaPefin\` (novos testes)
- `@c:\api-inadimplencia\src\modules\inadimplencia\services\serasaPefinService.js` (linhas 267-410, referência)
