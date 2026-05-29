# Tarefa 2.0: Estender aggregate `SerasaPefinSolicitacaoCompleta`

<critical>Ler `prd.md` e `techspec.md` desta pasta. Sem isso a tarefa sera invalidada.</critical>

## Visao Geral

<complexity>MEDIUM</complexity>

Adicionar suporte a parcela individual no aggregate de dominio, mantendo invariantes e compatibilidade retroativa.

<requirements>
- Acrescentar propriedades `NumeroParcela`, `ParcelaIdOrigem`, `IdSolicitacaoPai`.
- Atualizar metodo factory `Criar` para receber esses campos (default null para nao quebrar callers).
- Atualizar testes de dominio.
</requirements>

## Subtarefas

- [ ] 2.1 Atualizar a classe `SerasaPefinSolicitacaoCompleta`.
- [ ] 2.2 Adicionar invariantes (ex.: `Valor` e `DataVencimento` da parcela).
- [ ] 2.3 Atualizar `SerasaPefinSolicitacaoCompletaTests`.

## Detalhes de Implementacao

Cuidar para que solicitacoes legadas continuem validas (NumeroParcela null).

## Criterios de Sucesso

- Aggregate compila com novos campos.
- Testes verdes.

## Testes da Tarefa

- [ ] Unit tests do aggregate cobrindo construcao com e sem parcela.

<critical>SEMPRE CRIE E EXECUTE OS TESTES DA TAREFA ANTES DE CONSIDERA-LA FINALIZADA</critical>

## Arquivos relevantes

- `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinSolicitacaoCompleta.cs`
- `ApiInadimplencia.Domain.Tests/SerasaPefin/SerasaPefinSolicitacaoCompletaTests.cs`
