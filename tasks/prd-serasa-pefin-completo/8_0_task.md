# Tarefa 8.0: Rotas de Teste (UAT-only)

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>LOW</complexity>

Implementar as 4 rotas auxiliares de diagnóstico/teste, **bloqueadas em
produção** (somente acessíveis quando `SerasaPefin:Env == "uat"`).

<requirements>
- `POST /serasa-pefin/test/auth`: força obtenção de novo token Serasa (ignora cache). Retorna `{ accessToken, expiresIn, tokenType }` com `accessToken` mascarado (`***últimos-6-chars`).
- `POST /serasa-pefin/test/debt`: aceita payload JSON arbitrário, envia direto para `POST /collection/debt/`. Retorna resposta crua.
- `GET /serasa-pefin/test/documents`: lista os 8 documentos UAT autorizados (de `SerasaPefinConstants.UatAuthorizedDocuments`).
- `POST /serasa-pefin/test/simulate-webhook`: aceita `{ eventType, resultado, payload }` e simula chamada ao `SerasaWebhookHandler` interno.
- Todas as rotas:
  - Retornam **404 Not Found** quando `SerasaPefin:Env != "uat"`.
  - Logam invocação com `Operador` do usuário autenticado (ou `anonymous`).
- Endpoints **NÃO** ficam expostos no Swagger em produção (`.ExcludeFromDescription()` condicional).
</requirements>

## Subtarefas

- [ ] 8.1 Criar `MapSerasaPefinTestRoutes` em `InadimplenciaEndpoints.cs` (grupo separado)
- [ ] 8.2 Implementar `POST /test/auth` (limpa cache + chama `SerasaPefinClient.GetTokenAsync`)
- [ ] 8.3 Implementar `POST /test/debt` (proxy direto ao Serasa, sem validações/persistência)
- [ ] 8.4 Implementar `GET /test/documents` (retorna constante)
- [ ] 8.5 Implementar `POST /test/simulate-webhook` (encapsula `SerasaWebhookHandler.HandleAsync`)
- [ ] 8.6 Filtro de habilitação por `Env=uat` (via `IOptions<SerasaPefinOptions>` + `RouteHandlerFilter` ou check inline)
- [ ] 8.7 Testes unitários (Env=prod → 404, Env=uat → 200/expected)

## Detalhes de Implementação

Ver Tech Spec §8 (arquivos) e PRD §4 RF-05.

Padrão de filtro UAT-only:
```csharp
group.AddEndpointFilter(async (ctx, next) =>
{
    var options = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<SerasaPefinOptions>>();
    if (!string.Equals(options.Value.Env, "uat", StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound();
    }

    return await next(ctx);
});
```

Mascaramento do token (não retornar token completo nem em UAT):
```csharp
var masked = token.Length > 8 ? $"***{token[^6..]}" : "***";
```

## Critérios de Sucesso

- Com `SerasaPefin:Env=uat`: todas as 4 rotas respondem 200.
- Com `SerasaPefin:Env=production`: todas retornam 404.
- `GET /test/documents` retorna lista com 8 strings.
- `POST /test/auth` força novo token (verificável via log "Obtaining new Serasa PEFIN token").
- `POST /test/simulate-webhook` invoca o mesmo `SerasaWebhookHandler` usado pelos endpoints reais.

## Testes da Tarefa

- [ ] Teste unidade: `TestAuth_Uat_Returns200_WithMaskedToken`
- [ ] Teste unidade: `TestAuth_NonUat_Returns404`
- [ ] Teste unidade: `TestDocuments_ReturnsExactly8Documents`
- [ ] Teste unidade: `TestSimulateWebhook_DelegatesToWebhookHandler`
- [ ] Teste integração: `POST /test/debt` envia payload e retorna corpo Serasa
- [ ] Teste integração: `GET /test/documents` retorna lista correta

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\api-inadimplencia.Api\Endpoints\InadimplenciaEndpoints.cs` (adicionar grupo `/test`)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Infrastructure\Integrations\SerasaPefin\SerasaPefinTokenCache.cs` (expor `Clear()`)
- `@c:\api-inadimplencia-docker\ApiInadimplencia.Domain\SerasaPefin\SerasaPefinConstants.cs` (já tem `UatAuthorizedDocuments`)
- `@c:\api-inadimplencia\src\modules\inadimplencia\controllers\serasaPefinController.js` (referência das rotas de teste)
