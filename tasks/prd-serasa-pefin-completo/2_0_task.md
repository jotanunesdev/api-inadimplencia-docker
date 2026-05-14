# Tarefa 2.0: Query Service de Inadimplência

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Implementar a abstração `IInadimplenciaQueryService` que consulta o Data Warehouse
para obter os dados necessários ao preview e à negativação:

- `DW.fat_analise_inadimplencia_v4` → cabeçalho da venda inadimplente.
- `DW.vw_fiadores_por_venda` → lista de fiadores elegíveis.

Esta camada substitui o uso (incorreto) do `ISerasaPefinGateway.GetPreviewAsync`.

<requirements>
- Apenas vendas com `UPPER(TRIM(INADIMPLENTE)) = 'SIM'` são retornadas.
- Fiadores são filtrados por `TIPO_ASSOCIACAO` ∈ {FIADOR, CONJUGE, CESSIONARIO, COOBRIGADO, CO-OBRIGADO, CO OBRIGADO} (Latin1_General_CI_AI).
- Documentos retornados sempre `digits-only`.
- DTO de endereço com `zipCode, addressLine, district, city, state, complement?, number?`.
- Decimal/datas tipados (sem strings).
- Suporta `numVenda` nulo → retorna `null` (e não exceção).
</requirements>

## Subtarefas

- [ ] 2.1 Criar interface `IInadimplenciaQueryService` em `Application/Abstractions/Persistence/`
- [ ] 2.2 Criar DTOs `InadimplenciaQueryResult` e `FiadorQueryResult` (records imutáveis)
- [ ] 2.3 Implementar `InadimplenciaQueryService` (ADO.NET + `SqlServerConnectionFactory`)
- [ ] 2.4 Registrar no DI (`ApiInadimplencia.Infrastructure/DependencyInjection.cs`)
- [ ] 2.5 Criar testes de integração contra o DW real
- [ ] 2.6 Testes unitários para mapeamento de linhas (com `SqlDataReader` mock se viável)

## Detalhes de Implementação

Queries SQL devem replicar:
- `serasaPefinModel.js:findInadimplenciaByNumVenda` (referência Node)
- `serasaPefinModel.js:findGuarantorsByNumVenda` (referência Node)

Ver Tech Spec §3.1 (fluxo de preview) e §8 (arquivos).

## Critérios de Sucesso

- `GetVendaAsync(295, ct)` retorna venda 295 quando inadimplente, `null` quando não.
- `ListFiadoresAsync(295, ct)` retorna fiadores filtrados pelos 6 tipos válidos.
- Documentos retornados são `digits-only` (sem máscara/pontuação).
- Performance: ambas as queries < 200ms para uma venda típica.

## Testes da Tarefa

- [ ] Teste unidade: parse de endereço (campos opcionais nulos / preenchidos)
- [ ] Teste integração: `GetVendaAsync_ExistingInadimplente_ReturnsRow`
- [ ] Teste integração: `GetVendaAsync_NotInadimplente_ReturnsNull`
- [ ] Teste integração: `ListFiadoresAsync_ReturnsValidTypesOnly`
- [ ] Teste integração: `ListFiadoresAsync_NoFiadores_ReturnsEmpty`

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia\src\modules\inadimplencia\models\serasaPefinModel.js` (referência)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Application\Abstractions\Persistence\IInadimplenciaQueryService.cs` (novo)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\Persistence\SqlServer\InadimplenciaQueryService.cs` (novo)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\DependencyInjection.cs` (registrar DI)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure.Tests\Persistence\SqlServer\InadimplenciaQueryServiceIntegrationTests.cs` (novo)
