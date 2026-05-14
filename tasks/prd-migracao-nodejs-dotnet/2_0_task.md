# Tarefa 2.0: Implementar Queries de Carteira Inadimplente e Fiadores

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar query handlers para listagem de carteira inadimplente com filtros e consulta de fiadores por venda/CPF. Esta tarefa foca em queries de leitura simples usando Dapper, seguindo o padrão CQRS.

<requirements>
- Implementar query handler para listagem de todas inadimplências com última PROXIMA_ACAO
- Implementar query handler para consulta por CPF/CNPJ (normalizando dígitos)
- Implementar query handler para consulta por NUM_VENDA
- Implementar query handler para consulta por responsável com cor do usuário
- Implementar query handler para consulta por nome do cliente (LIKE)
- Implementar query handler para consulta de fiadores por NUM_VENDA
- Implementar query handler para consulta de fiadores por CPF
- Aplicar regra INADIMPLENTE = 'SIM' (trim/case-insensitive) em todas consultas
- Manter endpoints em ambos os formatos: raiz e /inadimplencia/*
- Criar DTOs de resposta
- Testes de unidade e integração
</requirements>

## Subtarefas

- [ ] 2.1 Criar DTOs para carteira inadimplente e fiadores
- [ ] 2.2 Implementar query handler ListInadimplenciasQuery
- [ ] 2.3 Implementar query handler GetInadimplenciaByCpfQuery
- [ ] 2.4 Implementar query handler GetInadimplenciaByNumVendaQuery
- [ ] 2.5 Implementar query handler GetInadimplenciaByResponsavelQuery
- [ ] 2.6 Implementar query handler GetInadimplenciaByClienteQuery
- [ ] 2.7 Implementar query handler GetFiadoresByNumVendaQuery
- [ ] 2.8 Implementar query handler GetFiadoresByCpfQuery
- [ ] 2.9 Mapear endpoints REST em InadimplenciaEndpoints.cs
- [ ] 2.10 Escrever testes de unidade para query handlers
- [ ] 2.11 Escrever testes de integração com SQL Server

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Modelos de Dados**: DTOs de Requisição/Resposta
- **Endpoints de API**: Carteira Inadimplente e Fiadores
- **Pontos de Integração**: DW.fat_analise_inadimplencia_v4, DW.vw_fiadores_por_venda

**DTOs:**
- Criar `ApiInadimplencia.Application/Features/Inadimplencias/Dtos/InadimplenciaDto.cs`
- Criar `ApiInadimplencia.Application/Features/Fiadores/Dtos/FiadorDto.cs`
- Criar `ApiInadimplencia.Application/Features/Inadimplencias/Dtos/ListInadimplenciasQuery.cs`
- Criar queries individuais para cada filtro

**Query Handlers:**
- Criar `ApiInadimplencia.Application/Features/Inadimplencias/Queries/ListInadimplenciasQueryHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Inadimplencias/Queries/GetInadimplenciaByCpfQueryHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Inadimplencias/Queries/GetInadimplenciaByNumVendaQueryHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Inadimplencias/Queries/GetInadimplenciaByResponsavelQueryHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Inadimplencias/Queries/GetInadimplenciaByClienteQueryHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Fiadores/Queries/GetFiadoresByNumVendaQueryHandler.cs`
- Criar `ApiInadimplencia.Application/Features/Fiadores/Queries/GetFiadoresByCpfQueryHandler.cs`
- Usar Dapper via `ILegacySqlExecutor` para queries
- SQL sempre parametrizado (Dapper DynamicParameters)
- Normalizar CPF/CNPJ removendo não-dígitos

**SQL Queries:**
- Consultar `DW.fat_analise_inadimplencia_v4` para carteira
- Aplicar filtro `WHERE TRIM(INADIMPLENTE) = 'SIM'`
- Consultar `DW.vw_fiadores_por_venda` para fiadores
- Ordenar fiadores por `DATA_CADASTRO DESC, NOME ASC`

**Endpoints:**
- Atualizar `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs`
- Mapear:
  - `GET /inadimplencia` → ListInadimplenciasQuery
  - `GET /inadimplencia/cpf/{cpf}` → GetInadimplenciaByCpfQuery
  - `GET /inadimplencia/num-venda/{numVenda}` → GetInadimplenciaByNumVendaQuery
  - `GET /inadimplencia/responsavel/{nome}` → GetInadimplenciaByResponsavelQuery
  - `GET /inadimplencia/cliente/{nomeCliente}` → GetInadimplenciaByClienteQuery
  - `GET /fiadores/num-venda/{numVenda}` → GetFiadoresByNumVendaQuery
  - `GET /fiadores/cpf/{cpf}` → GetFiadoresByCpfQuery

## Critérios de Sucesso

- Query handlers retornam dados corretos de DW.fat_analise_inadimplencia_v4
- Filtro INADIMPLENTE = 'SIM' aplicado consistentemente
- CPF/CNPJ normalizado corretamente (apenas dígitos)
- Consulta por responsável inclui cor do usuário
- Consulta por nome usa LIKE
- Fiadores ordenados corretamente
- Endpoints REST respondem com dados corretos
- Testes de unidade passam
- Testes de integração passam com SQL Server

## Testes da Tarefa

- [ ] Testes de unidade
  - Mock de ILegacySqlExecutor para testar query handlers
  - Testar normalização de CPF/CNPJ
  - Testar aplicação de filtro INADIMPLENTE
  - Testar ordenação de fiadores
- [ ] Testes de integração
  - Testar queries reais contra SQL Server de teste
  - Testar endpoints REST via HttpClient
  - Testar resposta com dados de carteira
  - Testar resposta com dados de fiadores

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes
- `ApiInadimplencia.Application/Features/Inadimplencias/Dtos/InadimplenciaDto.cs` (novo)
- `ApiInadimplencia.Application/Features/Fiadores/Dtos/FiadorDto.cs` (novo)
- `ApiInadimplencia.Application/Features/Inadimplencias/Queries/` (novo)
- `ApiInadimplencia.Application/Features/Fiadores/Queries/` (novo)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (atualizar)
