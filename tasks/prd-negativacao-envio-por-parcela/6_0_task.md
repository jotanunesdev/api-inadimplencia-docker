# Tarefa 6.0: Atualizar `DecideNegativacaoCommandHandler` (status agregado + notificacao)

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Apos o envio por parcela, agregar resultados e atualizar status do agrupamento + mensagem de notificacao.

<requirements>
- Apos `RequestNegativacaoCommandHandler` rodar, agregar `SerasaSolicitacaoResult[]` recebidos.
- Marcar status da solicitacao pai:
  - Todas `AguardandoRetorno` -> manter `Aprovada` + AguardandoRetorno por agregacao.
  - Misturado -> `AprovadaParcial` (novo status no enum, opcional) ou registrar via campo dedicado.
  - Todas falha -> `AprovadaFalhaEnvio`.
- Mensagem da notificacao: `"{enviadas} de {total} parcelas enviadas ao Serasa"`.
- Mensagem da ocorrencia: listar parcelas reais (substituir placeholder `new List<long> { 1 }`).
- Manter validacao de aprovador, senha, status, "solicitante nao pode aprovar".
</requirements>

## Subtarefas

- [ ] 6.1 Definir como capturar a lista de parcelas reais da solicitacao (vem do banco apos tarefa 3.0).
- [ ] 6.2 Implementar agregacao de status.
- [ ] 6.3 Atualizar `NegativacaoOcorrenciaScripts.MontarMensagemAprovacao` se necessario.
- [ ] 6.4 Atualizar mensagem de notificacao.

## Detalhes de Implementacao

Considerar adicionar enum `StatusAgregadoSolicitacao { TodasEnviadas, ParcialEnviado, TodasFalha }` para clarear codigo, mesmo que persistencia continue por linha.

## Criterios de Sucesso

- Mensagem refletem realidade da operacao.
- Ocorrencia lista parcelas corretas.

## Testes da Tarefa

- [ ] Testes unitarios cobrindo cada cenario de agregacao.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Application/Features/Negativacao/Commands/DecideNegativacaoCommandHandler.cs`
- `ApiInadimplencia.Domain/Negativacao/NegativacaoOcorrenciaScripts.cs`
- `ApiInadimplencia.Application.Tests/Features/Negativacao/Commands/DecideNegativacaoCommandHandlerTests.cs`
