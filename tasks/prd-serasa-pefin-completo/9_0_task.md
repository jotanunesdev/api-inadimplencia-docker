# Tarefa 9.0: Validação end-to-end Serasa UAT + documentação final

<critical>Ler os arquivos de prd.md e techspec.md desta pasta, se você não ler esses arquivos sua tarefa será invalidada</critical>

## Visão Geral

<complexity>MEDIUM</complexity>

Após as tarefas 1.0 a 8.0 estarem concluídas, validar o módulo Serasa PEFIN
end-to-end contra o ambiente Serasa UAT real, usando os documentos da massa de
testes autorizados. Atualizar documentação operacional, README e técnica.

<requirements>
- Executar fluxo completo: **preview → negativação → webhook simulado → histórico → detalhe**.
- Validar todos os 6 webhooks via `POST /test/simulate-webhook` (com payloads reais coletados do Serasa).
- Documentar resultados (transactionIds, status, capturas) em `docs/serasa-pefin-validacao-uat.md`.
- Validar contrato HTTP comparando com Node (mesma resposta para os mesmos inputs).
- Atualizar `README.md` raiz com seção "Módulo Serasa PEFIN" linkando para PRD/techspec.
- Atualizar `db/README.md` com nota sobre scripts 003/004 já obrigatórios.
- Atualizar variáveis de ambiente em `.env.example` (se existir) ou `docker-compose.yml`.
- Adicionar runbook básico em `docs/serasa-pefin-runbook.md` (token expirado, dedupe, webhook órfão, SSL/proxy).
</requirements>

## Subtarefas

- [ ] 9.1 Executar `POST /test/auth` em UAT → confirmar token Serasa válido
- [ ] 9.2 Executar `GET /vendas/{numVenda}/preview` para venda com mass UAT
- [ ] 9.3 Executar `POST /negativar` (mass UAT) → registrar `transactionId` retornado
- [ ] 9.4 Executar `POST /test/simulate-webhook` para inclusao/sucesso com o `transactionId` acima
- [ ] 9.5 Executar `GET /vendas/{numVenda}/historico` → verificar status `NEGATIVADO_SUCESSO`
- [ ] 9.6 Executar `GET /negativacoes/{id}` → verificar `cadusKey`/`cadusSerie` populados
- [ ] 9.7 Repetir fluxos para os 5 webhooks restantes
- [ ] 9.8 Validar bloqueio de duplicidade: 2º `POST /negativar` da mesma venda → 409
- [ ] 9.9 Validar bloqueio UAT: tentar negativar com documento fora da massa → 400
- [ ] 9.10 Escrever `docs/serasa-pefin-validacao-uat.md` com resultados (logs, screenshots se aplicável)
- [ ] 9.11 Escrever `docs/serasa-pefin-runbook.md`
- [ ] 9.12 Atualizar `README.md` raiz com seção Serasa PEFIN
- [ ] 9.13 Confirmar que `dotnet test` passa todos os testes do módulo

## Detalhes de Implementação

Cenários obrigatórios para o relatório de validação:

| Cenário | Endpoint | Massa | Resultado esperado |
|---|---|---|---|
| Preview elegível | `GET /preview/{numVenda}` | mass UAT, inadimplente | 200, `elegivel=true` |
| Preview não elegível (valor baixo) | idem | venda < R$ 10 | 200, `elegivel=false`, block `VALUE_BELOW_MINIMUM` |
| Preview bloqueado UAT | idem | doc fora da massa | 200, block `UAT_DOCUMENT_NOT_ALLOWED` |
| Inclusão sucesso | `POST /negativar` | mass UAT | 200, `transactionId` real |
| Inclusão duplicada | idem (2x) | mesma venda | 409 |
| Webhook reentrante | simulate-webhook 2x mesmo UUID | — | 200 em ambos, 1 registro persistido |
| Webhook órfão | simulate-webhook com `transactionId` inexistente | — | 200, registro com `PROCESSADO=false` |
| Histórico | `GET /historico/{numVenda}` | após inclusão | 200, lista contém solicitação |
| Detalhe por id | `GET /negativacoes/{id}` | id válido | 200 |
| Detalhe por transactionId | `GET /acompanhamento/{tx}` | tx válido | 200 |
| Rotas teste produção | qualquer `/test/*` | `Env=production` | 404 |

## Critérios de Sucesso

- Os 11 cenários acima passam.
- `docs/serasa-pefin-validacao-uat.md` documenta cada resposta.
- `dotnet test` retorna 0 falhas para todos os testes do módulo Serasa PEFIN.
- Build do Docker funciona, container sobe em < 30s.
- Logs estruturados expõem `numVenda`, `transactionId`, `solicitacaoId`, documento mascarado.
- README raiz menciona o módulo e linka PRD/techspec.

## Testes da Tarefa

- [ ] Smoke test E2E manual: rodar os 11 cenários do relatório
- [ ] Teste integração: `dotnet test --filter Category=SerasaPefin` retorna 0 falhas
- [ ] Teste integração: contagem de testes ≥ 30 (cobertura mínima do módulo)
- [ ] Teste manual: token expirado força renovação no próximo request
- [ ] Teste manual: parar e subir API novamente, repetir fluxo (verificar idempotência geral)

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERÁ-LA FINALIZADA</critical>

## Arquivos relevantes

- `@c:\api-inadimplencia-docker\docs\serasa-pefin-validacao-uat.md` (novo)
- `@c:\api-inadimplencia-docker\docs\serasa-pefin-runbook.md` (novo)
- `@c:\api-inadimplencia-docker\README.md` (atualizar)
- `@c:\api-inadimplencia-docker\db\README.md` (validar)
- `@c:\api-inadimplencia-docker\tasks\prd-serasa-pefin-completo\` (consultar PRD + techspec)
- `@c:\api-inadimplencia\documentos\guia-integracao-serasa-pefin.md` (referência Serasa oficial)
