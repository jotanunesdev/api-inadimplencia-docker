# Tarefa 6.0: Implementar Integrações Fluig e RM

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>HIGH</complexity>

Implementar HttpClient tipado para integrações Fluig e RM, com circuit breaker usando Polly. Esta tarefa envolve comunicação com APIs externas, retry policies e mascaramento de dados sensíveis.

<requirements>
- Implementar HttpClient tipado para datasets Fluig
- Implementar HttpClient tipado para relatórios RM
- Implementar command handler para geração de relatórios ficha-financeira
- Buscar XML de parâmetros no Fluig via dataset ds_paramsRel
- Usar fallback em ds_paiFilho_controleDeAcessoRMreportsFluig se não encontrar
- Trocar parâmetros COLIGADA e NUMVENDA no XML
- Chamar dsIntegraFacilRM com OPC=6, REPORT, COLIGADA, PARAMETER, FILE=Report.pdf, FILTRO=''
- Retornar URL do PDF gerado
- Implementar circuit breaker com Polly (retry, timeout, fallback)
- Mascarar cookies Fluig em logs/respostas
- Criar DTOs de entrada/saída
- Testes de unidade e integração
</requirements>

## Subtarefas

- [ ] 6.1 Criar interfaces IFluigDatasetGateway e IRmReportGateway
- [ ] 6.2 Implementar HttpClient tipado FluigDatasetClient
- [ ] 6.3 Implementar HttpClient tipado RmReportClient
- [ ] 6.4 Implementar FluigDatasetGateway com Polly
- [ ] 6.5 Implementar RmReportGateway com Polly
- [ ] 6.6 Criar DTOs para commands de relatórios
- [ ] 6.7 Implementar GenerateFichaFinanceiraCommandHandler
- [ ] 6.8 Implementar middleware de mascaramento para cookies Fluig
- [ ] 6.9 Configurar DI para gateways e HttpClient
- [ ] 6.10 Mapear endpoint REST de relatórios
- [ ] 6.11 Escrever testes de unidade
- [ ] 6.12 Escrever testes de integração com WireMock

## Detalhes de Implementação

Referenciar techspec.md seções:
- **Interfaces Principais**: Porta para integração Fluig, Porta para integração RM
- **Endpoints de API**: Relatórios
- **Pontos de Integração**: Fluig (TOTVS), TOTVS RM
- **Abordagem de Testes**: Mock de HttpClient para integrações externas

**Interfaces:**
- Criar `ApiInadimplencia.Application/Abstractions/Integrations/IFluigDatasetGateway.cs`
- Criar `ApiInadimplencia.Application/Abstractions/Integrations/IRmReportGateway.cs`
- Métodos já definidos na techspec

**HttpClient Tipado Fluig:**
- Criar `ApiInadimplencia.Infrastructure/Integrations/Fluig/FluigDatasetClient.cs`
- Endpoint j_security_check para autenticação
- Endpoint dataset-handle/search para datasets
- Datasets: ds_paramsRel, dsIntegraFacilRM, ds_paiFilho_controleDeAcessoRMreportsFluig
- Autenticação via cookies Fluig

**HttpClient Tipado RM:**
- Criar `ApiInadimplencia.Infrastructure/Integrations/Rm/RmReportClient.cs`
- Integração via Fluig datasets
- Operação OPC=6 para gerar PDF
- Retorno: URL do PDF gerado

**FluigDatasetGateway:**
- Criar `ApiInadimplencia.Infrastructure/Integrations/Fluig/FluigDatasetGateway.cs`
- Implementar IFluigDatasetGateway
- Usar IHttpClientFactory com Polly
- Retry com exponential backoff (3 tentativas)
- Timeout 10s
- Fallback para dataset secundário
- Mascarar cookies em logs

**RmReportGateway:**
- Criar `ApiInadimplencia.Infrastructure/Integrations/Rm/RmReportGateway.cs`
- Implementar IRmReportGateway
- Usar IHttpClientFactory com Polly
- Retry (3 tentativas)
- Timeout 30s
- Mascarar XML/parâmetros em logs

**Command Handler Relatórios:**
- Criar `ApiInadimplencia.Application/Features/Relatorios/Commands/GenerateFichaFinanceiraCommandHandler.cs`
- Buscar XML de parâmetros via FluigDatasetGateway
- Fallback para dataset secundário se não encontrar
- Trocar COLIGADA e NUMVENDA no XML
- Chamar RmReportGateway.GenerateReportAsync
- Retornar URL do PDF

**DTOs:**
- Criar `ApiInadimplencia.Application/Features/Relatorios/Dtos/GenerateFichaFinanceiraCommand.cs`
- Criar `ApiInadimplencia.Application/Features/Relatorios/Dtos/RelatorioDto.cs`

**Polly Configuration:**
- Configurar em `ApiInadimplencia.Infrastructure/DependencyInjection.cs`
- AddHttpClient with Polly policies
- Retry: 3 tentativas com exponential backoff
- Timeout: 10s (Fluig), 30s (RM)
- Circuit breaker: 5 falhas consecutivas abre circuito por 30s

**Middleware Mascaramento:**
- Criar `api-inadimplencia.Api/Middleware/SensitiveDataMaskingMiddleware.cs` (parcial, focar em cookies Fluig)
- Mascarar cookies em logs/respostas
- Regex para identificar padrões de cookie

**DI:**
- Registrar IFluigDatasetGateway e IRmReportGateway
- Registrar HttpClient factories

**Endpoint:**
- Atualizar `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs`
- Mapear:
  - `GET /relatorios/ficha-financeira?numVenda=&codColigada=&reportColigada=&reportId=` → GenerateFichaFinanceiraCommand

## Critérios de Sucesso

- FluigDatasetGateway autenticando e buscando datasets
- RmReportGateway gerando PDFs
- Fallback funcionando quando dataset primário não encontrado
- Parâmetros COLIGADA e NUMVENDA trocados corretamente
- Retry funcionando em falhas
- Timeout respeitado
- Cookies mascarados em logs/respostas
- URL do PDF retornada
- Endpoints REST funcionando
- Testes de unidade passam
- Testes de integração passam com WireMock

## Testes da Tarefa

- [ ] Testes de unidade
  - Mock de IHttpClientFactory
  - Testar retry policies
  - Testar timeout
  - Testar circuit breaker
  - Testar mascaramento de cookies
  - Testar troca de parâmetros XML
- [ ] Testes de integração
  - Usar WireMock para mockar Fluig e RM
  - Testar autenticação Fluig
  - Testar busca de datasets
  - Testar fallback para dataset secundário
  - Testar geração de relatório RM
  - Testar endpoint REST via HttpClient

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes
- `ApiInadimplencia.Application/Abstractions/Integrations/IFluigDatasetGateway.cs` (novo)
- `ApiInadimplencia.Application/Abstractions/Integrations/IRmReportGateway.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/Fluig/FluigDatasetClient.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/Fluig/FluigDatasetGateway.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/Rm/RmReportClient.cs` (novo)
- `ApiInadimplencia.Infrastructure/Integrations/Rm/RmReportGateway.cs` (novo)
- `ApiInadimplencia.Application/Features/Relatorios/Commands/GenerateFichaFinanceiraCommandHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Relatorios/Dtos/` (novo)
- `api-inadimplencia.Api/Middleware/SensitiveDataMaskingMiddleware.cs` (novo)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (atualizar)
- `api-inadimplencia.Api/Endpoints/InadimplenciaEndpoints.cs` (atualizar)
