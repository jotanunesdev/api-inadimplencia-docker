# Tarefa 3.0: Implementar Commands de Ocorrências e Atendimentos

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar command handlers para criação/atualização/exclusão de ocorrências e criação de atendimentos com geração de protocolo transacional. Esta tarefa envolve regras de domínio complexas e exige TDD (Red-Green-Refactor) devido à alta complexidade.

<requirements>
- Implementar entidade de domínio Ocorrencia com validações
- Implementar command handler para criação de ocorrência com FK guard
- Implementar command handler para atualização de ocorrência
- Implementar command handler para exclusão de ocorrência
- Implementar entidade de domínio Atendimento com geração de protocolo
- Implementar command handler para criação de atendimento com geração de protocolo transacional
- Implementar geração de protocolo AAAAMMDD##### com isolamento SERIALIZABLE
- Implementar interface IProtocoloGenerator
- Validar FK de venda antes de inserir ocorrência (retornar 409)
- Salvar snapshot JSON da venda em DADOS_VENDA
- Criar DTOs de entrada/saída
- Testes de unidade e integração (TDD recomendado)
</requirements>

## Subtarefas

- [ ] 3.1 Criar entidade de domínio Ocorrencia com validações
- [ ] 3.2 Criar DTOs para commands de ocorrências
- [ ] 3.3 Implementar CreateOcorrenciaCommandHandler
- [ ] 3.4 Implementar UpdateOcorrenciaCommandHandler
- [ ] 3.5 Implementar DeleteOcorrenciaCommandHandler
- [ ] 3.6 Criar interface IProtocoloGenerator
- [ ] 3.7 Criar entidade de domínio Atendimento
- [ ] 3.8 Criar DTOs para command de atendimento
- [ ] 3.9 Implementar CreateAtendimentoCommandHandler com transação SERIALIZABLE
- [ ] 3.10 Mapear endpoints REST para ocorrências e atendimentos
- [ ] 3.11 Escrever testes de unidade (TDD)
- [ ] 3.12 Escrever testes de integração

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Modelos de Dados**: Entidades de Domínio Principais (Ocorrencia, Atendimento)
- **Endpoints de API**: Ocorrências e Atendimentos
- **Pontos de Integração**: dbo.OCORRENCIAS, dbo.ATENDIMENTOS
- **Abordagem de Testes**: Cenários de teste críticos (protocolo transacional, FK guard)

**Entidade Ocorrencia:**
- Criar `ApiInadimplencia.Domain/Ocorrencias/Ocorrencia.cs`
- Campos: Id, NumVendaFk, NomeUsuarioFk, Descricao, StatusOcorrencia, DtOcorrencia, HoraOcorrencia, ProximaAcao, Protocolo
- Método de fábrica estático `Criar` com validações
- Validar campos obrigatórios não nulos/vazios
- Validar formato de data/hora

**Entidade Atendimento:**
- Criar `ApiInadimplencia.Domain/Atendimentos/Atendimento.cs`
- Campos: Id, Protocolo, Cpf, NumVendaFk, DadosVendaJson, CriadoEm
- Método de fábrica assíncrono `CriarAsync` com `IProtocoloGenerator`
- Protocolo formato AAAAMMDD#####
- Serializar dadosVenda para JSON

**IProtocoloGenerator:**
- Criar `ApiInadimplencia.Application/Abstractions/IProtocoloGenerator.cs`
- Método: `Task<string> GerarProtocoloAsync(CancellationToken ct)`
- Implementação em Infrastructure usando SQL com isolamento SERIALIZABLE
- Usar `UPDLOCK`, `HOLDLOCK` para evitar race conditions
- Query: `UPDATE protocolo_seq SET seq = seq + 1; SELECT AAAAMMDD + LPAD(seq, 5, '0')`

**Command Handlers Ocorrências:**
- Criar `ApiInadimplencia.Application/Features/Ocorrencias/Commands/CreateOcorrenciaCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Ocorrencias/Commands/UpdateOcorrenciaCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Ocorrencias/Commands/DeleteOcorrenciaCommandHandler.cs`
- Validar FK de venda antes de inserir (query SQL parametrizada)
- Retornar 409 Conflict se venda não existe
- Usar EF Core repository para persistência

**Command Handler Atendimento:**
- Criar `ApiInadimplencia.Application/Features/Atendimentos/Commands/CreateAtendimentoCommandHandler.cs`
- Usar transação com isolamento SERIALIZABLE
- Chamar IProtocoloGenerator dentro da transação
- Persistir Atendimento com protocolo gerado
- Commit transação apenas se protocolo gerado com sucesso

**DTOs:**
- Criar `ApiInadimplencia.Application/Features/Ocorrencias/Dtos/CreateOcorrenciaCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Ocorrencias/Dtos/UpdateOcorrenciaCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Ocorrencias/Dtos/OcorrenciaDto.cs`
- Criar `ApiInadimplencia.Application/Features/Atendimentos/Dtos/CreateAtendimentoCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Atendimentos/Dtos/AtendimentoDto.cs`

**Endpoints:**
- Atualizar `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs`
- Mapear:
  - `POST /ocorrencias` → CreateOcorrenciaCommand
  - `PUT /ocorrencias/{id}` → UpdateOcorrenciaCommand
  - `DELETE /ocorrencias/{id}` → DeleteOcorrenciaCommand
  - `GET /ocorrencias/{id}` → Query (implementar query handler simples)
  - `GET /ocorrencias/num-venda/{numVenda}` → Query
  - `GET /ocorrencias/protocolo/{protocolo}` → Query
  - `POST /atendimentos` → CreateAtendimentoCommand
  - `GET /atendimentos/cpf/{cpf}` → Query (implementar query handler simples)
  - `GET /atendimentos/num-venda/{numVenda}` → Query
  - `GET /atendimentos/protocolo/{protocolo}` → Query
  - `GET /atendimentos/cliente/{nomeCliente}` → Query

## Critérios de Sucesso

- Entidades de domínio com validações funcionando
- FK de venda validada antes de inserir ocorrência
- Protocolo gerado com formato AAAAMMDD#####
- Geração de protocolo transacional (SERIALIZABLE) sem race conditions
- Ocorrências criadas/atualizadas/excluídas corretamente
- Atendimentos criados com protocolo único
- Snapshot JSON da venda salvo
- Endpoints REST funcionando
- Testes de unidade passam (TDD)
- Testes de integração passam com SQL Server

## Testes da Tarefa

- [ ] Testes de unidade (TDD - escrever antes da implementação)
  - Testar validações de Ocorrencia
  - Testar validações de Atendimento
  - Testar mock de IProtocoloGenerator
  - Testar FK guard em CreateOcorrenciaCommandHandler
  - Testar geração de protocolo com formato correto
- [ ] Testes de integração
  - Testar criação de ocorrência com FK válida
  - Testar criação de ocorrência com FK inválida (409)
  - Testar geração de protocolo transacional com concorrência
  - Testar criação de atendimento com snapshot JSON
  - Testar endpoints REST via HttpClient

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>
<critical>PARA TAREFAS COMPLEXIDADE HIGH, SEGUIR PROCESSO RED-GREEN-REFACTOR (TDD) ONDE OS TESTES SÃO CRIADOS ANTES DA IMPLEMENTAÇÃO</critical>

## Arquivos relevantes
- `ApiInadimplencia.Domain/Ocorrencias/Ocorrencia.cs` (novo)
- `ApiInadimplencia.Domain/Atendimentos/Atendimento.cs` (novo)
- `ApiInadimplencia.Application/Abstractions/IProtocoloGenerator.cs` (novo)
- `ApiInadimplencia.Application/Features/Ocorrencias/Commands/` (novo)
- `ApiInadimplencia.Application/Features/Ocorrencias/Dtos/` (novo)
- `ApiInadimplencia.Application/Features/Atendimentos/Commands/` (novo)
- `ApiInadimplencia.Application/Features/Atendimentos/Dtos/` (novo)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/Repositories/OcorrenciaRepository.cs` (novo)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/Repositories/AtendimentoRepository.cs` (novo)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/ProtocoloGenerator.cs` (novo)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (atualizar)
