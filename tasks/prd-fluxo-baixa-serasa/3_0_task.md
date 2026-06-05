# Tarefa 3.0: Infrastructure — `SerasaPefinBaixaRepository` + extensão do `SerasaPefinClient` com `DeleteByContractAsync`

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar a porta de persistência do aggregate (`ISerasaPefinBaixaRepository` na Application + implementação em `Infrastructure`) e estender o cliente HTTP da Serasa com o método `DELETE` para baixa por contrato. Sem este código, não há como persistir solicitações nem comunicar a baixa à Serasa.

<requirements>
- `ISerasaPefinBaixaRepository` em `ApiInadimplencia.Application/Abstractions/Persistence/` com métodos descritos na Tech Spec.
- `SerasaPefinBaixaRepository` em `ApiInadimplencia.Infrastructure/Persistence/SqlServer/` usando Dapper, sempre com SQL parametrizado.
- `ExistsActiveAsync` deve detectar duplicidade usando o índice único filtrado da tarefa 1.0.
- Extender `ISerasaPefinGateway` com `DeleteByContractAsync(SerasaBaixaRequest, CancellationToken)`.
- Extender `SerasaPefinClient` com método HTTP `DELETE` que envia headers `creditor-document`, `debtor-document`, `contract-number`, `reason`, `type: PEFIN`, `Authorization: Bearer`. Body vazio.
- Reuso de token via `SerasaPefinTokenCache` (já existente).
- Retry 1× em `401` (reusar padrão atual).
- Mascarar `creditor-document` e `debtor-document` em logs.
- Registrar `SerasaPefinBaixaRepository` em `Infrastructure/DependencyInjection.cs`.
</requirements>

## Subtarefas

- [ ] 3.1 Criar `ISerasaPefinBaixaRepository.cs` com a assinatura completa (Add, AddMany, Update, GetById, GetByTransactionId, ExistsActive, ListByStatus).
- [ ] 3.2 Implementar `SerasaPefinBaixaRepository.cs` (Dapper, isolation level apropriado para `AddMany`).
- [ ] 3.3 Adicionar `DeleteByContractAsync` ao `ISerasaPefinGateway` e ao `SerasaPefinClient` (HttpRequestMessage isolada, sem mutar `DefaultRequestHeaders`).
- [ ] 3.4 Atualizar `SerasaPefinGateway` (orquestra token + DELETE + mascaramento).
- [ ] 3.5 Registrar o repository em `Infrastructure/DependencyInjection.cs`.
- [ ] 3.6 Testes de integração: persistência (insert+read, índice anti-duplicata, update transacional).
- [ ] 3.7 Testes unitários do cliente HTTP usando `HttpMessageHandler` mock.

## Detalhes de Implementação

Ver Tech Spec — “Interfaces Principais” (`ISerasaPefinGateway`, `ISerasaPefinBaixaRepository`) e “Pontos de Integração” (contrato HTTP DELETE). Referenciar `Infrastructure/Integrations/SerasaPefin/SerasaPefinClient.cs` (método `PostMainDebtAsync`) como modelo de implementação isolada.

## Critérios de Sucesso

- Repository persiste solicitações em `SERASA_PEFIN_BAIXAS` e lê de volta corretamente.
- Tentativa de inserir solicitação ativa duplicada (mesma parcela) é rejeitada pelo índice.
- `SerasaPefinClient.DeleteByContractAsync` envia exatamente os headers obrigatórios e desserializa `{ transactionId }`.
- Retry em `401` ocorre uma única vez.
- Build limpo; nenhum erro nos testes de DI.

## Testes da Tarefa

- [ ] Testes de integração (`Infrastructure.Tests`): insert + read; índice único rejeita duplicata; update preserva campos.
- [ ] Testes unitários (`Infrastructure.Tests`): cliente HTTP envia headers corretos, trata `200`, lança `SerasaPefinHttpException` em `4xx/5xx`, retry em `401`.
- [ ] Teste unitário: `SerasaPefinGateway.DeleteByContractAsync` mascara documentos no log e propaga exceção em falha.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Abstractions/Persistence/ISerasaPefinBaixaRepository.cs` (novo)
- `ApiInadimplencia.Application/Abstractions/Integrations/ISerasaPefinGateway.cs` (modificado)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/SerasaPefinBaixaRepository.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinClient.cs` (modificado)
- `ApiInadimplencia.Infrastructure/Integrations/SerasaPefin/SerasaPefinGateway.cs` (modificado)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (modificado)
- `ApiInadimplencia.Infrastructure.Tests/Persistence/SqlServer/SerasaPefinBaixaRepositoryTests.cs` (novo)
- `ApiInadimplencia.Infrastructure.Tests/Integrations/SerasaPefin/SerasaPefinClientDeleteTests.cs` (novo)
