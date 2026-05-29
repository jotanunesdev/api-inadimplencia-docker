# Tarefa 6.0: Consulta de dívidas elegíveis — `GetDividasElegiveisQuery` + endpoint

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>LOW</complexity>

Implementar a consulta que alimenta o passo inicial do fluxo: lista as parcelas em aberto de uma venda, marcando cada uma com `elegivel: bool` (true se `DIAS_ATRASO > NegativacaoOptions.DiasAtrasoMinimo`, default 60). Esse endpoint é a fonte de verdade que a UI consome para habilitar o botão "Negativar" e renderizar a listagem de seleção.

<requirements>
- Fonte: `DW.fat_analise_inadimplencia_parcelas` (validar nome real da coluna de dias de atraso — ver questão aberta no PRD).
- Reusar `IInadimplenciaQueryService` (já existente) — adicionar método novo, não criar serviço duplicado.
- Endpoint protegido (usuário autenticado).
- Resposta inclui `clientePodeNegativar: bool` (= existe ao menos uma parcela elegível) e dados resumidos da venda (nome cliente, CPF mascarado, contractNumber).
- 404 quando venda não existe; lista vazia + flag `false` quando não há parcela elegível.
</requirements>

## Subtarefas

- [ ] 6.1 Adicionar método em `Application/Abstractions/Persistence/IInadimplenciaQueryService.cs`:
  ```csharp
  Task<DividasElegiveisDto?> GetDividasElegiveisAsync(int numVenda, int diasAtrasoMinimo, CancellationToken ct);
  ```
- [ ] 6.2 Implementar em `Infrastructure/Persistence/SqlServer/InadimplenciaQueryService.cs`:
  - Query parametrizada em `DW.fat_analise_inadimplencia_parcelas` filtrando `NUM_VENDA = @numVenda` e parcelas em aberto.
  - Retorna parcelas com colunas `PARCELA_ID`, `VALOR`, `VENCIMENTO`, `DIAS_ATRASO`, `Elegivel = (DIAS_ATRASO > @diasAtrasoMinimo)`.
  - Trazer também resumo da venda (cliente, CPF, contract).
- [ ] 6.3 Criar `Application/Features/Negativacao/Queries/GetDividasElegiveisQuery(+Handler).cs`:
  - Handler injeta `IInadimplenciaQueryService` + `IOptions<NegativacaoOptions>`.
  - Aplica máscara de CPF antes de retornar (reutilizar helper `MaskDocument` do módulo Serasa).
- [ ] 6.4 Criar DTOs: `DividasElegiveisResponse { numVenda, cliente, cpfMasked, contractNumber, clientePodeNegativar, parcelas[] }` e `ParcelaElegivelDto { id, valor, vencimento, diasAtraso, elegivel }`.
- [ ] 6.5 Criar endpoint em `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs`:
  - `GET /negativacao/vendas/{numVenda:int}/dividas` → 200/404.
- [ ] 6.6 Registrar query handler em `DependencyInjection.cs`.

## Detalhes de Implementação

Ver `techspec.md` seções **Endpoints de API** e **Componentes a reativar/refatorar** (item `InadimplenciaQueryService`).

## Critérios de Sucesso

- `GET /negativacao/vendas/{x}/dividas` para venda existente retorna 200 com lista de parcelas e `clientePodeNegativar` correto.
- Para venda inexistente: 404.
- Para venda sem parcelas elegíveis: 200 com `clientePodeNegativar=false`, parcelas listadas com `elegivel=false`.
- CPF mascarado na resposta (ex: `123.***.***-09`).
- Latência < 500ms para venda típica.

## Testes da Tarefa

- [ ] **Unitários** `GetDividasElegiveisQueryHandlerTests` com `IInadimplenciaQueryService` mockado:
  - Venda inexistente → handler retorna `null`.
  - Sem parcela > 60 dias → `clientePodeNegativar=false`.
  - Com pelo menos 1 parcela > 60 dias → `clientePodeNegativar=true`.
  - CPF mascarado.
- [ ] **Integração SQL** `InadimplenciaQueryServiceDividasTests`:
  - Setup: inserir registros mock em `DW.fat_analise_inadimplencia_parcelas` (ou usar venda real conhecida em UAT).
  - Validar query retorna número correto de parcelas + `Elegivel` calculado.
- [ ] **Integração de endpoint** `NegativacaoFluxoEndpointsTests`:
  - `GET /negativacao/vendas/{x}/dividas` autenticado retorna 200 com schema correto.
  - Sem autenticação → 401.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Abstractions/Persistence/IInadimplenciaQueryService.cs` (modificar)
- `ApiInadimplencia.Infrastructure/Persistence/SqlServer/InadimplenciaQueryService.cs` (modificar)
- `ApiInadimplencia.Application/Features/Negativacao/Queries/GetDividasElegiveisQuery.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Queries/GetDividasElegiveisQueryHandler.cs` (novo)
- `ApiInadimplencia.Application/Features/Negativacao/Dtos/DividasElegiveisResponse.cs` (novo)
- `api-inadimplencia.Api/Endpoints/NegativacaoFluxoEndpoints.cs` (novo)
- `ApiInadimplencia.Infrastructure/DependencyInjection.cs` (modificar)
