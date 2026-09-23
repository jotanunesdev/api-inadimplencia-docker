# Recupera Jota — alterações do backend preparadas para envio

Data: 23/09/2026. Branch mantida por escolha da usuária: `feature/dashboard-metricas-producao`.
Este registro resume alterações locais acumuladas; não comprova deploy nem execução de scripts no banco.

## Escopo conferido no diff

- `GetMetricQuery`, `GetMetricQueryHandler` e `InadimplenciaEndpoints`: filtro opcional `semResponsavel`, validações de combinação com responsável e aceitação das categorias da carteira. Endpoints existentes preservados.
- `CarteiraAtuacaoSql`: usa `dbo.OCORRENCIAS.PROXIMA_ACAO`, com último registro que possui próxima ação ordenado por data, hora, próxima ação e ID. Separa agendamento futuro de agendamento vencido/hoje. Data vencida não é prova de execução. Registro de contato histórico permanece separado quando o ciclo atual não é comprovado.
- Aptidão continua baseada em `SCORE`; unidade operacional continua sendo a venda. Ações são identificadas pela venda, inclusive quando não há responsável atual.
- `CarteiraAtuacaoSql`, `CarteiraJuridicaSql`, `RecuperaDashboardSql` e `ValorRecuperadoSql`: filtro de ausência de responsável por `NOT EXISTS`, sem multiplicar vendas por vínculos.
- `ValorRecuperadoSql`: resumo e detalhamento usam `DIAS_ATRASO > 30` de `dw.ficha_financeira_valor_recuperado`; detalhamento inclui o atraso. Valores continuam vindo da fonte validada; não se transforma queda do saldo em pagamento presumido.
- Testes de Application e de integração somente leitura atualizados para essas regras.

## Validação nesta preparação

- 145 testes selecionados de Application passaram, sem falhas ou ignorados.
- Build da API com `--no-restore`: aprovado, com dois avisos NU1603 de HealthChecks e um CS8602 em `SensitiveDataMaskingMiddleware`, fora do diff desta entrega.
- Testes de integração com SQL real não foram executados nesta preparação. Não foram alterados dados, schema, containers, credenciais ou configurações de ambiente.

## Limitações preservadas

Agendamento não comprova atuação realizada. Contatos históricos não confirmam automaticamente atuação no ciclo atual. Recuperação financeira representa posição atual por mês de baixa: a carga integral da fonte pode alterar meses passados após estorno, sem oferecer histórico contábil imutável. Push não configura homologação nem publica a API; os destinos de banco e integrações precisam ser conferidos antes de qualquer deploy.
