# Tarefa 2.0: Domain — aggregate `SerasaPefinBaixaSolicitacao`, VO de motivo e enum de status

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Criar o núcleo de regras de negócio da baixa: aggregate root, value object de motivo (whitelist), enum de status e transições de ciclo de vida. Como é o coração das invariants, deve seguir **TDD (Red-Green-Refactor)**: escrever os testes primeiro em `ApiInadimplencia.Domain.Tests` e só então implementar.

<requirements>
- Aggregate `SerasaPefinBaixaSolicitacao` em `ApiInadimplencia.Domain/SerasaPefin/`.
- VO `SerasaPefinBaixaMotivo` aceitando somente `{1, 2, 3, 4, 19, 43, 45}` (codigo + descricao). Construtor rejeita demais valores.
- Enum `SerasaPefinBaixaStatus`: `AguardandoAprovacao`, `Aprovada`, `Rejeitada`, `AprovadaFalhaEnvio`, `PendenteEnvio`, `BaixaEnviada`, `BaixaAguardandoRetorno`, `BaixadoSucesso`, `BaixadoErro`.
- Factory `CriarParaAprovacao(...)` valida todos os campos obrigatórios (numVenda, contractNumber, devedor, credor, motivo, solicitante).
- Métodos de transição: `MarcarAprovada`, `MarcarRejeitada`, `MarcarPendenteEnvio`, `MarcarBaixaAguardandoRetorno`, `AplicarWebhookSucesso`, `AplicarWebhookErro`, `MarcarFalhaEnvio`, `RegistrarTentativaReenvio`.
- `RegistrarTentativaReenvio` falha quando `Status != BaixadoErro` ou `Tentativas >= 3`.
- Cada transição valida o estado origem (lançar `InvalidOperationException` quando inválida).
- Domain não pode depender de SQL/HTTP/EF.
</requirements>

## Subtarefas

- [ ] 2.1 **RED**: escrever testes em `Domain.Tests/SerasaPefin/SerasaPefinBaixaMotivoTests.cs` cobrindo whitelist e descrições.
- [ ] 2.2 **RED**: escrever testes em `Domain.Tests/SerasaPefin/SerasaPefinBaixaSolicitacaoTests.cs` cobrindo factory inválida, factory válida, cada transição válida/inválida, limite de reenvio.
- [ ] 2.3 **GREEN**: implementar `SerasaPefinBaixaMotivo.cs` (VO) e `SerasaPefinBaixaStatus.cs` (enum).
- [ ] 2.4 **GREEN**: implementar `SerasaPefinBaixaSolicitacao.cs` com factory e transições mínimas para passar os testes.
- [ ] 2.5 **REFACTOR**: revisar nomes, normalizações (digits-only nos documentos) e mensagens de exceção; garantir cobertura ≥ 90%.

## Detalhes de Implementação

Ver Tech Spec — seções “Modelos de Dados” (estrutura do aggregate) e “Considerações Técnicas” (justificativa do aggregate dedicado). Usar `SerasaPefinSolicitacaoCompleta.cs` como referência de estilo (factory + métodos `Marcar*` + invariants), porém criar arquivo separado, sem herança.

## Critérios de Sucesso

- Todos os testes do `Domain.Tests/SerasaPefin/SerasaPefinBaixa*` verdes.
- Build limpo de `ApiInadimplencia.Domain.csproj`.
- Cobertura ≥ 90% dos métodos novos.

## Testes da Tarefa

- [ ] Testes unitários: VO de motivo (whitelist, igualdade, descrição).
- [ ] Testes unitários: factory `CriarParaAprovacao` (campos obrigatórios, normalizações, valor de status inicial).
- [ ] Testes unitários: cada transição em estado válido e em estado inválido (deve lançar).
- [ ] Testes unitários: `RegistrarTentativaReenvio` em `BaixadoErro` com `Tentativas < 3` (incrementa) e `Tentativas == 3` (lança).
- [ ] Testes unitários: `AplicarWebhookSucesso` só transiciona de `BaixaAguardandoRetorno`.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinBaixaSolicitacao.cs` (novo)
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinBaixaMotivo.cs` (novo)
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinBaixaStatus.cs` (novo)
- `ApiInadimplencia.Domain.Tests/SerasaPefin/SerasaPefinBaixaSolicitacaoTests.cs` (novo)
- `ApiInadimplencia.Domain.Tests/SerasaPefin/SerasaPefinBaixaMotivoTests.cs` (novo)
- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacaoV2.cs` (referência)
