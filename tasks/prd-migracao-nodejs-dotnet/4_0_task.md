# Tarefa 4.0: Implementar Commands de Usuários, Responsáveis e Kanban

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar command handlers para gestão de usuários, atribuição de responsáveis e gestão de kanban. Esta tarefa envolve validações de permissões, upserts idempotentes e eventos de domínio.

<requirements>
- Implementar command handler para upsert idempotente de usuários
- Validar perfis admin e operador apenas
- Validar COR_HEX no formato #RRGGBB
- Atribuir perfil admin para USER_CODE = wffluig quando perfil não informado
- Atribuir perfil operador para demais usuários quando perfil não informado
- Retornar 200 com exists: true se usuário já existe, 201 se criado
- Implementar command handler para atribuição de responsáveis
- Validar adminUserCode e que usuário admin existe com PERFIL = admin
- Fazer upsert por NUM_VENDA_FK via MERGE
- Disparar evento de domínio ResponsavelAtribuidoEvent
- Implementar command handler para gestão de kanban
- Fazer upsert por NUM_VENDA_FK + PROXIMA_ACAO
- Normalizar status para todo, inProgress, done
- Aceitar aliases em PT-BR e EN para status
- Usar STATUS_DATA no formato YYYY-MM-DD
- Criar DTOs de entrada/saída
- Testes de unidade e integração
</requirements>

## Subtarefas

- [ ] 4.1 Criar entidade de domínio Usuario com validações
- [ ] 4.2 Criar DTOs para commands de usuários
- [ ] 4.3 Implementar UpsertUsuarioCommandHandler
- [ ] 4.4 Implementar UpdateUsuarioCommandHandler
- [ ] 4.5 Implementar DeleteUsuarioCommandHandler
- [ ] 4.6 Criar entidade de domínio Responsavel com eventos
- [ ] 4.7 Criar DTOs para commands de responsáveis
- [ ] 4.8 Implementar UpsertResponsavelCommandHandler
- [ ] 4.9 Implementar UpdateResponsavelCommandHandler
- [ ] 4.10 Implementar DeleteResponsavelCommandHandler
- [ ] 4.11 Criar entidade de domínio KanbanStatus
- [ ] 4.12 Criar DTOs para commands de kanban
- [ ] 4.13 Implementar UpsertKanbanStatusCommandHandler
- [ ] 4.14 Mapear endpoints REST
- [ ] 4.15 Escrever testes de unidade
- [ ] 4.16 Escrever testes de integração

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Modelos de Dados**: Entidades de Domínio Principais (Responsavel)
- **Endpoints de API**: Usuários, Responsáveis, Kanban
- **Pontos de Integração**: dbo.USUARIO, dbo.VENDA_RESPONSAVEL, dbo.KANBAN_STATUS
- **Abordagem de Testes**: Cenários de teste críticos (upsert idempotente, validação admin, normalização status)

**Entidade Usuario:**
- Criar `ApiInadimplencia.Domain/Users/Usuario.cs`
- Campos: UserCode, Nome, Perfil, CorHex
- Validar perfil: apenas 'admin' ou 'operador'
- Validar COR_HEX: formato #RRGGBB (com ou sem #)
- Método de fábrica estático com validações

**Entidade Responsavel:**
- Criar `ApiInadimplencia.Domain/Responsaveis/Responsavel.cs`
- Campos: NumVendaFk, Username, AtribuidoEm, AtribuidoPor
- Método `Attribuir` que dispara `ResponsavelAtribuidoEvent`
- Evento já existe em `ApiInadimplencia.Domain/Events/InadimplenciaEvents.cs`

**Entidade KanbanStatus:**
- Criar `ApiInadimplencia.Domain/Kanban/KanbanStatusEntity.cs`
- Campos: NumVendaFk, ProximaAcao, Status, StatusData
- Normalizar status para todo, inProgress, done
- Mapear aliases: 'a fazer', 'fazendo', 'pronto', 'todo', 'inprogress', 'in_progress', 'done'

**Command Handlers Usuários:**
- Criar `ApiInadimplencia.Application/Features/Usuarios/Commands/UpsertUsuarioCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Usuarios/Commands/UpdateUsuarioCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Usuarios/Commands/DeleteUsuarioCommandHandler.cs`
- Upsert idempotente por USER_CODE ou NOME
- Retornar 200 com exists: true se já existe
- Retornar 201 se criado
- Atribuir perfil padrão: admin para wffluig, operador para demais

**Command Handlers Responsáveis:**
- Criar `ApiInadimplencia.Application/Features/Responsaveis/Commands/UpsertResponsavelCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Responsaveis/Commands/UpdateResponsavelCommandHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Responsaveis/Commands/DeleteResponsavelCommandHandler.cs`
- Validar adminUserCode existe e tem PERFIL = admin
- Usar MERGE SQL para upsert por NUM_VENDA_FK
- Disparar ResponsavelAtribuidoEvent via IEventBus

**Command Handlers Kanban:**
- Criar `ApiInadimplencia.Application/Features/Kanban/Commands/UpsertKanbanStatusCommandHandler.cs`
- Upsert por NUM_VENDA_FK + PROXIMA_ACAO
- Normalizar status usando KanbanStatus enum/VO
- Validar STATUS_DATA formato YYYY-MM-DD

**DTOs:**
- Criar `ApiInadimplencia.Application/Features/Usuarios/Dtos/UpsertUsuarioCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Usuarios/Dtos/UsuarioDto.cs`
- Criar `ApiInadimplencia.Application/Features/Responsaveis/Dtos/UpsertResponsavelCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Responsaveis/Dtos/ResponsavelDto.cs`
- Criar `ApiInadimplencia.Application/Features/Kanban/Dtos/UpsertKanbanStatusCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Kanban/Dtos/KanbanStatusDto.cs`

**Endpoints:**
- Atualizar `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs`
- Mapear:
  - `GET /usuarios` → Query
  - `POST /usuarios` → UpsertUsuarioCommand
  - `GET /usuarios/{nome}` → Query
  - `PUT /usuarios/{nome}` → UpdateUsuarioCommand
  - `DELETE /usuarios/{nome}` → DeleteUsuarioCommand
  - `GET /responsaveis` → Query
  - `POST /responsaveis` → UpsertResponsavelCommand
  - `GET /responsaveis/{numVenda}` → Query
  - `PUT /responsaveis/{numVenda}` → UpdateResponsavelCommand
  - `DELETE /responsaveis/{numVenda}` → DeleteResponsavelCommand
  - `GET /kanban-status` → Query
  - `POST /kanban-status` → UpsertKanbanStatusCommand

## Critérios de Sucesso

- Usuários validados com perfis admin/operador apenas
- COR_HEX validado corretamente
- Upsert idempotente funcionando
- Perfil padrão atribuído corretamente
- Responsáveis validados com adminUserCode
- Evento ResponsavelAtribuidoEvent disparado
- MERGE SQL funcionando para responsáveis
- Status kanban normalizados corretamente
- Aliases PT-BR/EN aceitos
- Endpoints REST funcionando
- Testes de unidade passam
- Testes de integração passam

## Testes da Tarefa

- [ ] Testes de unidade
  - Testar validações de Usuario (perfil, COR_HEX)
  - Testar upsert idempotente de usuários
  - Testar validação de admin em responsáveis
  - Testar evento ResponsavelAtribuidoEvent
  - Testar normalização de status kanban
  - Testar aliases PT-BR/EN
- [ ] Testes de integração
  - Testar criação/atualização/exclusão de usuários
  - Testar upsert de responsáveis com MERGE
  - Testar atribuição com usuário inválido
  - Testar upsert de kanban com normalização
  - Testar endpoints REST via HttpClient

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes
- `ApiInadimplencia.Domain/Users/Usuario.cs` (novo)
- `ApiInadimplencia.Domain/Responsaveis/Responsavel.cs` (novo)
- `ApiInadimplencia.Domain/Kanban/KanbanStatusEntity.cs` (novo)
- `ApiInadimplencia.Application/Features/Usuarios/Commands/` (novo)
- `ApiInadimplencia.Application/Features/Usuarios/Dtos/` (novo)
- `ApiInadimplencia.Application/Features/Responsaveis/Commands/` (novo)
- `ApiInadimplencia.Application/Features/Responsaveis/Dtos/` (novo)
- `ApiInadimplencia.Application/Features/Kanban/Commands/` (novo)
- `ApiInadimplencia.Application/Features/Kanban/Dtos/` (novo)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/Repositories/UsuarioRepository.cs` (novo)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/Repositories/ResponsavelRepository.cs` (novo)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/Repositories/KanbanStatusRepository.cs` (novo)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (atualizar)
