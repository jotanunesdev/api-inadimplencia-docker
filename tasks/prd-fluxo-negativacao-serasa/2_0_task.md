# Tarefa 2.0: Senha de Transação — entidade, repositório, hasher PBKDF2 e endpoints

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar o módulo de **Senha de Transação por usuário**: entidade rica `UsuarioSenhaTransacao` (com lockout), porta + adapter de repositório SQL, hasher PBKDF2 nativo (`PasswordHasher<T>`), commands/queries e endpoints REST sob `/configuracoes/senha-transacao`. Esse módulo é pré-requisito para validar a confirmação de solicitação e aprovação no fluxo principal.

<requirements>
- Hash via `Microsoft.AspNetCore.Identity.PasswordHasher<UsuarioSenhaTransacao>` (PBKDF2-HMAC-SHA256, ≥100k iterações por padrão).
- Política de lockout: 3 tentativas inválidas em 5min → bloqueio de 15min (configurável via `NegativacaoOptions`).
- Validação mínima: 6 caracteres alfanuméricos. **Não pode ser igual à senha de login** (regra documentada; verificação fora do escopo desta task — depende de `IUsuarioRepository` se existir).
- Senha **nunca** logada nem retornada pela API.
- Commands/queries seguem CQRS já existente do projeto.
</requirements>

## Subtarefas

- [ ] 2.1 Criar `Domain/Negativacao/UsuarioSenhaTransacao.cs` com:
  - Propriedades privadas (`Username`, `Hash`, `TentativasFalhas`, `BloqueadoAte`, `CriadaEm`, `AtualizadaEm`).
  - Factory `Criar(username, hash)`.
  - Métodos `AtualizarHash(hash)`, `RegistrarTentativaInvalida(maxTentativas, lockoutDuration, utcNow)`, `RegistrarTentativaValida()`, `EstaBloqueado(utcNow)`.
- [ ] 2.2 Criar port `Application/Abstractions/Persistence/ISenhaTransacaoRepository.cs` (`GetByUsernameAsync`, `UpsertAsync`).
- [ ] 2.3 Criar port `Application/Abstractions/Auth/ISenhaTransacaoHasher.cs` (`Hash`, `Verify`).
- [ ] 2.4 Criar adapter `Infrastructure/Auth/Pbkdf2SenhaTransacaoHasher.cs` usando `PasswordHasher<UsuarioSenhaTransacao>`.
- [ ] 2.5 Criar adapter `Infrastructure/Persistence/SqlServer/SenhaTransacaoRepository.cs` (Dapper ou EF + `ILegacySqlExecutor`).
- [ ] 2.6 Criar `Application/Features/Negativacao/Commands/SetSenhaTransacaoCommand` + handler:
  - Recebe `username` (do `ICurrentUserService` — Task 3.0), `senhaAtual?`, `novaSenha`.
  - Valida tamanho mínimo. Se já existe registro, exige `senhaAtual` e valida.
  - Faz hash e upsert.
- [ ] 2.7 Criar query `GetHasSenhaTransacaoQuery` + handler (retorna `bool`, sem expor hash).
- [ ] 2.8 Criar serviço auxiliar `ISenhaTransacaoValidator` (port em Application) que centraliza `ValidateAsync(username, senha, ct)` (verificação + lockout). Será reusado por Task 7.0 e 8.0.
- [ ] 2.9 Criar endpoints em `Api/Endpoints/ConfiguracoesEndpoints.cs`:
  - `GET /configuracoes/senha-transacao` → `200 { hasSenha: bool }`.
  - `POST /configuracoes/senha-transacao` → `204` ou `400` (`SENHA_INVALIDA`, `SENHA_MUITO_CURTA`).
- [ ] 2.10 Registrar tudo em `Infrastructure/DependencyInjection.cs`.

## Detalhes de Implementação

Ver `techspec.md` seções **Interfaces Principais**, **Modelos de Dados — `UsuarioSenhaTransacao`** e **Endpoints de API**.

## Critérios de Sucesso

- `POST /configuracoes/senha-transacao` cria registro com hash; reexecutar com `senhaAtual` correta atualiza; sem `senhaAtual` retorna `400`.
- `GET /configuracoes/senha-transacao` retorna `{ hasSenha: true }` após criação; nunca expõe hash.
- 3 tentativas inválidas em 5min bloqueiam o usuário por 15min (verificável via `ISenhaTransacaoValidator.ValidateAsync` retornar `Bloqueado`).
- Tentativa válida zera `TentativasFalhas`.
- Hash diferente para mesma senha em invocações distintas (salt automático do `PasswordHasher`).
- Senha não aparece em logs nem em respostas.

## Testes da Tarefa

- [ ] **Unitários** (`ApiInadimplencia.Domain.Tests/Negativacao/UsuarioSenhaTransacaoTests.cs`):
  - `Criar` falha se username vazio.
  - `RegistrarTentativaInvalida` aciona lockout após N tentativas dentro da janela.
  - `RegistrarTentativaInvalida` reseta contador se janela expirou.
  - `RegistrarTentativaValida` zera `TentativasFalhas` e `BloqueadoAte`.
  - `EstaBloqueado(utcNow)` retorna correto antes/depois do `BloqueadoAte`.
- [ ] **Unitários** (`ApiInadimplencia.Infrastructure.Tests/Auth/Pbkdf2SenhaTransacaoHasherTests.cs`):
  - `Hash` produz strings diferentes para a mesma senha (salt).
  - `Verify` retorna `true` para par válido, `false` para par inválido.
- [ ] **Unitários** (`ApiInadimplencia.Application.Tests/.../SetSenhaTransacaoCommandHandlerTests.cs`):
  - Senha < 6 chars → erro.
  - Sem registro prévio + sem `senhaAtual` → cria.
  - Com registro prévio sem `senhaAtual` → erro.
  - Com registro prévio e `senhaAtual` válida → atualiza hash.
- [ ] **Integração** (`ApiInadimplencia.Infrastructure.Tests/Persistence/SenhaTransacaoRepositoryIntegrationTests.cs`):
  - Upsert + Get retornam o mesmo registro; segundo upsert atualiza `AtualizadaEm`.
- [ ] **Integração de endpoint** (`api-inadimplencia.Api.Tests/.../ConfiguracoesEndpointsTests.cs`): fluxo `POST` → `GET` → `POST` (atualizar) → `GET`.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Domain/Negativacao/UsuarioSenhaTransacao.cs` (novo)
- `ApiInadimplencia.Application/Abstractions/Persistence/ISenhaTransacaoRepository.cs` (novo)
- `ApiInadimplencia.Application/Abstractions/Auth/ISenhaTransacaoHasher.cs` (novo)
- `ApiInadimplencia.Application/Abstractions/Auth/ISenhaTransacaoValidator.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Commands/SetSenhaTransacaoCommand(+Handler).cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Queries/GetHasSenhaTransacaoQuery(+Handler).cs` (novo)
- `ApiInadimplencia.Infrastructure/Auth/Pbkdf2SenhaTransacaoHasher.cs` (novo)
- `ApiInadimplencia.Infrastructure/Auth/SenhaTransacaoValidator.cs` (novo)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/SenhaTransacaoRepository.cs` (novo)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (modificar)
- `api-inadimplencia.Api/Endpoints/ConfiguracoesEndpoints.cs` (novo)
- `api-inadimplencia.Api/Program.cs` (mapear endpoints)
