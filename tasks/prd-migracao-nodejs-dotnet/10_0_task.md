# Tarefa 10.0: Validação Final e Documentação

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Validar 100% compatibilidade de contrato REST com frontend, atualizar catálogo de rotas, documentar endpoints no Swagger/OpenAPI, realizar testes E2E de fluxos críticos e limpar código legado. Esta é a tarefa final de validação antes de sugerir a próxima fase.

<requirements>
- Validar 100% compatibilidade de contrato REST com frontend existente
- Atualizar catálogo de rotas InadimplenciaRouteCatalog com status migrado
- Documentar todos endpoints no Swagger/OpenAPI
- Realizar testes E2E de fluxos críticos usando Playwright MCP
- Limpar código legado (LegacySqlOperations)
- Validar Docker multi-stage build
- Validar healthcheck HTTP
- Validar que todos testes passam
- Documentar decisões técnicas em techspec-codebase.md se necessário
- Sugerir próxima fase após validação completa
</requirements>

## Subtarefas

- [ ] 10.1 Validar contrato REST para carteira inadimplente
- [ ] 10.2 Validar contrato REST para ocorrências
- [ ] 10.3 Validar contrato REST para atendimentos
- [ ] 10.4 Validar contrato REST para usuários
- [ ] 10.5 Validar contrato REST para responsáveis
- [ ] 10.6 Validar contrato REST para kanban
- [ ] 10.7 Validar contrato REST para fiadores
- [ ] 10.8 Validar contrato REST para dashboard
- [ ] 10.9 Validar contrato REST para notificações (incluindo SSE)
- [ ] 10.10 Validar contrato REST para relatórios
- [ ] 10.11 Validar contrato REST para Serasa PEFIN
- [ ] 10.12 Atualizar InadimplenciaRouteCatalog com status migrado
- [ ] 10.13 Configurar Swagger/OpenAPI completo
- [ ] 10.14 Realizar testes E2E com Playwright MCP
- [ ] 10.15 Limpar código legado LegacySqlOperations
- [ ] 10.16 Validar Docker build e healthcheck
- [ ] 10.17 Executar todos testes e garantir pass
- [ ] 10.18 Documentar decisões técnicas se necessário
- [ ] 10.19 Criar sugestão para próxima fase

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Endpoints de API**: Lista completa de endpoints
- **Arquivos relevantes**: InadimplenciaRouteCatalog, Dockerfile
- **Abordagem de Testes**: Testes de E2E com Playwright MCP

**Validação Contrato REST:**
- Comparar cada endpoint da API .NET com Node.js original
- Validar estrutura de resposta (camelCase e UPPER_SNAKE aceitos)
- Validar status codes (200, 201, 409, etc.)
- Validar headers
- Validar SSE stream (snapshot inicial, heartbeat, eventos)
- Usar ferramenta de teste ou script automatizado

**InadimplenciaRouteCatalog:**
- Atualizar `ApiInadimplencia.Application/Features/Routes/InadimplenciaRouteCatalog.cs`
- Marcar todos endpoints como MIGRADO
- Documentar qualquer diferença encontrada

**Swagger/OpenAPI:**
- Configurar em `api-inadimplencia.Api/Program.cs`
- Adicionar pacote NuGet: `Swashbuckle.AspNetCore`
- Configurar SwaggerGen com XML comments
- Configurar SwaggerUI
- Documentar todos endpoints com summaries e responses
- Documentar modelos DTOs
- Expor em endpoint `/swagger`

**Testes E2E com Playwright MCP:**
- Criar testes para fluxos críticos:
  - Consulta carteira → registro ocorrência → atribuição responsável → notificação SSE
  - Criação de atendimento com protocolo
  - Geração de relatório ficha-financeira
  - Preview e solicitação de negativação Serasa
  - Webhook Serasa com idempotência
- Usar Playwright MCP para automatizar
- Validar contrato REST via OpenAPI/Swagger
- Testar SSE connection e eventos em tempo real

**Limpeza Código Legado:**
- Remover ou marcar como obsoleto `ApiInadimplencia.Application/Features/Legacy/LegacySqlOperations.cs`
- Remover referências a ILegacySqlExecutor se não mais usado
- Remover código temporário de migração

**Validação Docker:**
- Validar `Dockerfile` multi-stage build
- Validar runtime non-root
- Validar healthcheck HTTP
- Testar build: `docker build -t api-inadimplencia .`
- Testar run: `docker run -p 8080:8080 api-inadimplencia`
- Validar healthcheck: `curl http://localhost:8080/health`

**Executar Todos Testes:**
- Executar testes de unidade: `dotnet test --filter "FullyQualifiedName~Unit"`
- Executar testes de integração: `dotnet test --filter "FullyQualifiedName~Integration"`
- Executar testes E2E: `dotnet test --filter "FullyQualifiedName~E2E"`
- Garantir 100% de pass rate

**Documentação:**
- Atualizar `documentos/techspec-codebase.md` se houver decisões técnicas novas
- Documentar qualquer desvio da techspec original
- Documentar lições aprendidas

**Sugestão Próxima Fase:**
- Após validação completa, sugerir criação de tasks para:
  - Implementação de CI/CD pipeline (GitHub Actions)
  - Configuração de ambientes (Dev/Staging/Production)
  - Secrets management (Azure Key Vault, AWS Secrets Manager, etc.)
  - Monitoramento avançado (Grafana dashboards, alertas)
  - Testes de carga e performance (k6, JMeter)
  - Documentação de operação e runbooks

## Critérios de Sucesso

- 100% compatibilidade de contrato REST validada
- InadimplenciaRouteCatalog atualizado
- Swagger/OpenAPI documentando todos endpoints
- Testes E2E passando
- Código legado removido
- Docker build funcionando
- Healthcheck funcionando
- Todos testes passando (100%)
- Documentação atualizada
- Sugestão para próxima fase criada

## Testes da Tarefa

- [ ] Testes de validação de contrato
  - Script automatizado para comparar endpoints
  - Teste manual de cada endpoint
  - Teste de SSE connection
- [ ] Testes E2E
  - Teste fluxo completo com Playwright MCP
  - Teste de contrato REST via Swagger
  - Teste de SSE eventos em tempo real
  - Teste de webhook Serasa
- [ ] Testes de Docker
  - Teste de build
  - Teste de run
  - Teste de healthcheck

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes
- `ApiInadimplencia.Application/Features/Routes/InadimplenciaRouteCatalog.cs` (atualizar)
- `api-inadimplencia.Api/Program.cs` (atualizar)
- `ApiInadimplencia.Application/Features/Legacy/LegacySqlOperations.cs` (remover)
- `Dockerfile` (validar)
- `documentos/techspec-codebase.md` (atualizar se necessário)
- `tasks/prd-migracao-nodejs-dotnet/proxima-fase-sugestao.md` (novo)

## Sugestão para Próxima Fase

Após validação completa da migração Node.js para .NET 8, sugerir a criação de tasks para:

**CI/CD Pipeline:**
- Configurar GitHub Actions para build, test e deploy
- Configurar automação de release
- Configurar rollback automático

**Ambientes:**
- Configurar ambiente Dev (local)
- Configurar ambiente Staging (homologação)
- Configurar ambiente Production
- Configurar secrets management

**Monitoramento Avançado:**
- Criar dashboards Grafana para:
  - Performance de APIs
  - Health de integrações
  - Volume de notificações
  - Latency de Serasa
- Configurar alertas para:
  - Alta taxa de erros 5xx
  - Falhas de integrações externas
  - Conexões SSE anormais

**Testes de Carga e Performance:**
- Implementar testes de carga com k6 ou JMeter
- Definir baseline de performance
- Configurar testes automatizados em pipeline

**Documentação de Operação:**
- Criar runbooks para operação
- Documentar procedimentos de incident response
- Documentar procedimentos de deployment

Esta próxima fase deve ser criada como um novo PRD e Tech Spec separado, focado em DevOps e operação da API migrada.
