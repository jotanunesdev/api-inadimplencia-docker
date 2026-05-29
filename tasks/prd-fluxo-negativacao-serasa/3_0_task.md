# Tarefa 3.0: Infra de autenticação e políticas — `ICurrentUserService`, `IAprovadoresPolicy`, `NegativacaoOptions`

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>LOW</complexity>

Criar a base de infraestrutura para que **qualquer handler** consiga (a) descobrir o username autenticado e (b) decidir se um usuário é aprovador autorizado, ambos via portas abstratas. Inclui também o `NegativacaoOptions` (Options pattern) que centraliza a configuração do fluxo (lista de aprovadores, quorum, regras de senha de transação, dias mínimos de atraso).

<requirements>
- Seguir Clean Architecture: portas em `Application/Abstractions/Auth/`, adapters em `Infrastructure/Auth/`.
- Adicionar `services.AddHttpContextAccessor()` em `DependencyInjection.cs`.
- `NegativacaoOptions` validado via `ValidateDataAnnotations`.
- Lista de aprovadores comparada **case-insensitive**.
- Não introduzir dependência de ASP.NET no `Application` ou `Domain`.
</requirements>

## Subtarefas

- [ ] 3.1 Criar port `Application/Abstractions/Auth/ICurrentUserService.cs` (`string? Username`, `bool IsAuthenticated`).
- [ ] 3.2 Criar port `Application/Abstractions/Auth/IAprovadoresPolicy.cs` (`IsAprovador(username)`, `ListAprovadores()`).
- [ ] 3.3 Criar `Infrastructure/Configuration/NegativacaoOptions.cs`:
  - `string[] UsuariosAprovadores`, `int QuorumAprovacao=1`, `int DiasAtrasoMinimo=60`, `int MaxTentativasSenha=3`, `int LockoutMinutos=15`, `int JanelaTentativasMinutos=5`.
- [ ] 3.4 Criar adapter `Infrastructure/Auth/CurrentUserService.cs` consumindo `IHttpContextAccessor` (ler `HttpContext.User?.Identity?.Name`).
- [ ] 3.5 Criar adapter `Infrastructure/Auth/OptionsAprovadoresPolicy.cs` consumindo `IOptions<NegativacaoOptions>` (comparação `OrdinalIgnoreCase`).
- [ ] 3.6 Em `Infrastructure/DependencyInjection.cs`:
  - `services.AddHttpContextAccessor();`
  - `services.AddOptions<NegativacaoOptions>().Bind(...).ValidateDataAnnotations();`
  - `services.AddScoped<ICurrentUserService, CurrentUserService>();`
  - `services.AddSingleton<IAprovadoresPolicy, OptionsAprovadoresPolicy>();`
- [ ] 3.7 Adicionar seção `Negativacao` em `appsettings.json` e `appsettings.Development.json` com lista `["aracy.mendoca","adriano.oliveira"]`.

## Detalhes de Implementação

Ver `techspec.md` seções **Interfaces Principais** e **Modelos de Dados — `NegativacaoOptions`**.

## Critérios de Sucesso

- Em um endpoint protegido, `ICurrentUserService.Username` retorna o login do usuário autenticado.
- `IAprovadoresPolicy.IsAprovador("aracy.mendoca")` → `true`; `"ARACY.MENDOCA"` → `true`; `"joao"` → `false`.
- `NegativacaoOptions` é lido do `appsettings`; falha de boot se a seção estiver mal formada.
- Nenhuma referência a ASP.NET dentro de `Application/` ou `Domain/`.

## Testes da Tarefa

- [ ] **Unitários** `OptionsAprovadoresPolicyTests`:
  - `IsAprovador` é case-insensitive.
  - `IsAprovador` retorna `false` para username vazio/null.
  - `ListAprovadores` retorna lista imutável.
- [ ] **Unitários** `CurrentUserServiceTests` com `IHttpContextAccessor` mockado:
  - `IsAuthenticated=true` quando `Identity.IsAuthenticated=true`.
  - `Username` retorna `Identity.Name`.
  - Sem `HttpContext` → `Username=null`, `IsAuthenticated=false`.
- [ ] **Integração** (boot): teste de host (`WebApplicationFactory`) verifica que `ICurrentUserService` e `IAprovadoresPolicy` resolvem do DI sem erro.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Abstractions/Auth/ICurrentUserService.cs` (novo)
- `ApiInadimplencia.Application/Abstractions/Auth/IAprovadoresPolicy.cs` (novo)
- `ApiInadimplencia.Infrastructure/Auth/CurrentUserService.cs` (novo)
- `ApiInadimplencia.Infrastructure/Auth/OptionsAprovadoresPolicy.cs` (novo)
- `ApiInadimplencia.Infrastructure/Configuration/NegativacaoOptions.cs` (novo)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (modificar)
- `api-inadimplencia.Api/appsettings.json` (modificar)
- `api-inadimplencia.Api/appsettings.Development.json` (modificar)
