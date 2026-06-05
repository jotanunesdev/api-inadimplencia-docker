# PRD — Fluxo de Baixa de Dívida no Serasa PEFIN

## Visão Geral

Hoje a aplicação de inadimplência permite **negativar** parcelas de uma venda junto ao Serasa PEFIN com fluxo completo de solicitação, aprovação por aprovador, senha de transação e integração assíncrona via webhooks. Não existe, porém, um caminho oficial para **dar baixa (write-off)** de uma dívida já negativada — operação necessária quando o cliente paga, renegocia, há ordem judicial, etc.

Esta funcionalidade entrega o **fluxo de baixa de dívida no Serasa**, espelhando arquitetura, segurança e UX do fluxo de negativação atual, mas com semântica de exclusão (HTTP `DELETE`) e adicionando obrigatoriamente um **motivo da baixa** (conforme tabela oficial Serasa).

Público-alvo: usuários do back-office que hoje já operam o fluxo de negativação. Valor: permitir manter a base do Serasa atualizada, evitar negativações indevidas perdurando após pagamento/acordo e cumprir prazos legais de baixa.

## Objetivos

- **OBJ-1**: Permitir que parcelas com `statusSerasa = NEGATIVADO_SUCESSO` possam ser baixadas pelo back-office, com 100% das solicitações passando por aprovação de aprovador e senha de transação.
- **OBJ-2**: Garantir rastreabilidade total: toda solicitação de baixa registra solicitante, aprovador, motivo, parcela, `transactionId` da Serasa e status final, espelhando a auditoria já existente no fluxo de negativação.
- **OBJ-3**: Atingir tempo médio entre aprovação da baixa e envio do `DELETE` à Serasa ≤ 5s (mesmo SLA do envio de negativação).
- **OBJ-4**: 100% das baixas processadas pela Serasa devem ter seu status final (`BAIXADO_SUCESSO` ou `BAIXADO_ERRO`) refletido na solicitação correspondente via webhook idempotente.
- **OBJ-5**: Zero baixas enviadas sem motivo válido (`reason` ∈ {01, 02, 03, 04, 19, 43, 45}).
- **OBJ-6**: Disponibilizar visibilidade gerencial das baixas no dashboard (gráfico de motivos e gráfico misto comparativo de negativações vs baixas por mês).

## Histórias de Usuário

- **HU-1 — Solicitante**: Como analista de cobrança, quero selecionar uma parcela negativada de um cliente e solicitar sua baixa informando o motivo, para que o cliente que pagou/acordou deixe de constar como negativado no Serasa.
- **HU-2 — Solicitante**: Como analista, quero ver o histórico de solicitações de baixa que abri e seu status atual, para acompanhar quando a baixa foi efetivada.
- **HU-3 — Aprovador**: Como aprovador, quero ver todas as solicitações de baixa pendentes na minha fila de aprovações (junto com as de negativação) e aprovar/rejeitar com senha de transação, para manter o controle financeiro centralizado.
- **HU-4 — Aprovador**: Como aprovador, ao aprovar uma baixa quero confirmar com a mesma senha de transação que uso para negativação, sem precisar de novas credenciais.
- **HU-5 — Solicitante**: Como analista, quero ser notificado quando a baixa for aprovada/rejeitada e quando o Serasa confirmar o resultado final, para informar o cliente sem precisar ficar consultando.
- **HU-6 — Operação**: Como time de operação, quero que parcelas em processo de baixa fiquem visualmente diferenciadas no modal de seleção, evitando dupla solicitação enquanto o webhook não retorna.
- **HU-7 — Solicitante**: Como analista, ao receber uma resposta `BAIXADO_ERRO` do Serasa, quero poder reenviar a solicitação de baixa diretamente da tela de detalhe, sem precisar refazer todo o fluxo de aprovação.
- **HU-8 — Aprovador**: Como aprovador, quero distinguir visualmente solicitações de **baixa** de solicitações de **negativação** na minha fila, para priorizar e tratar cada tipo com o contexto correto.
- **HU-9 — Gestor**: Como gestor de cobrança, quero ver no dashboard quais são os principais motivos de baixa e a evolução de negativações vs baixas nos últimos 12 meses, para acompanhar a saúde da carteira.

### Casos extremos cobertos

- Solicitação de baixa para parcela cujo status mudou (ex.: já em `BAIXA_ENVIADA`) deve ser rejeitada com mensagem clara.
- Webhook chegando antes ou depois do retorno HTTP síncrono — idempotência por `transactionId`/`uuid` (reuso da infraestrutura atual).
- Falha de rede no `DELETE` para a Serasa — registrar erro, manter solicitação em estado retentável.

## Funcionalidades Principais

### F-1. Seleção de parcelas para baixa no modal existente

**O que faz**: O `NegativacaoDebtsModal` ganha um modo de operação `"baixa"`. Quando aberto nesse modo, parcelas que hoje aparecem como "Negativada" (visual *info*, checkbox desabilitado) passam a ser **selecionáveis**, e parcelas ainda não negativadas ficam visualmente inelegíveis.

**Por que é importante**: Reaproveita a UI conhecida pelo usuário (mesmas colunas Parcela/Valor/Vencimento/Dias em Atraso) e mantém um único ponto de seleção de parcelas, reduzindo curva de aprendizado.

**Requisitos funcionais**:
- **RF-1.1**: O modal deve aceitar uma prop `modo: "negativacao" | "baixa"` (default `"negativacao"`).
- **RF-1.2**: Em modo `"baixa"`, apenas parcelas com `statusSerasa = NEGATIVADO_SUCESSO` são selecionáveis; as demais aparecem desabilitadas com badge informativa do motivo (ex.: "Não negativada", "Em baixa").
- **RF-1.3**: O título do modal e o texto do botão de ação ("Salvar" → "Solicitar baixa") devem refletir o modo ativo.
- **RF-1.4**: A seleção deve permitir múltiplas parcelas (granular — uma baixa por parcela).

### F-2. Modal de confirmação com motivo da baixa

**O que faz**: Após confirmar a seleção, abre o modal de confirmação (reuso do `NegativacaoConfirmModal` ou variante) com um **combo obrigatório de motivo da baixa** e os campos de justificativa/senha de transação já existentes na confirmação do fluxo atual.

**Por que é importante**: O `reason` é header obrigatório para a Serasa; ele precisa ser capturado antes do envio e validado no backend para evitar baixas com motivo inválido.

**Requisitos funcionais**:
- **RF-2.1**: O combo de motivo deve oferecer os seguintes 7 motivos (código + descrição amigável):
  - `01` Pagamento da dívida
  - `02` Renegociação da dívida
  - `03` Por solicitação do cliente
  - `04` Ordem judicial
  - `19` Renegociação da dívida por acordo
  - `43` Baixa por negociação
  - `45` Contestação
- **RF-2.2**: O campo motivo é obrigatório; o botão de confirmação fica desabilitado até a seleção.
- **RF-2.3**: O mesmo motivo é aplicado a todas as parcelas selecionadas no envio.
- **RF-2.4**: O combo deve estar visível apenas em modo `"baixa"`.

### F-3. Solicitação de baixa no backend com aprovação

**O que faz**: A solicitação de baixa segue o mesmo pipeline de aprovação do fluxo de negativação — entra na fila de aprovadores, exige senha de transação e gera notificações para solicitante e aprovador.

**Por que é importante**: Garante consistência de governança, audita decisões e reduz risco de baixas indevidas.

**Requisitos funcionais**:
- **RF-3.1**: Apenas usuários com o mesmo papel que hoje pode solicitar negativação podem solicitar baixa.
- **RF-3.2**: Apenas usuários com o mesmo papel de aprovador hoje vigente podem aprovar/rejeitar baixas; a senha de transação do aprovador é obrigatória na decisão.
- **RF-3.3**: O sistema deve impedir uma nova solicitação de baixa para uma parcela que já tenha solicitação de baixa em estado ativo (`AGUARDANDO_APROVACAO`, `APROVADA`, `BAIXA_ENVIADA`, `BAIXA_AGUARDANDO_RETORNO`).
- **RF-3.4**: Solicitações rejeitadas registram justificativa do aprovador e ficam visíveis no histórico.

### F-4. Envio à Serasa via HTTP DELETE

**O que faz**: Após aprovação, o backend envia uma chamada `DELETE` por parcela à Serasa, conforme contrato oficial documentado em `documentos/documentacao-serasa-pefin-v8.md`.

**Por que é importante**: É a operação que efetiva a baixa no bureau; sem ela a aprovação não tem efeito externo.

**Requisitos funcionais**:
- **RF-4.1**: Cada parcela aprovada gera uma chamada `DELETE` individual para o endpoint Serasa de baixa por contrato.
- **RF-4.2**: O `transactionId` retornado pela Serasa deve ser persistido na solicitação imediatamente após o `HTTP 200`.
- **RF-4.3**: A solicitação transiciona para `BAIXA_ENVIADA` → `BAIXA_AGUARDANDO_RETORNO` → `BAIXADO_SUCESSO`/`BAIXADO_ERRO`, reaproveitando os enums já existentes.
- **RF-4.4**: Em falha HTTP no envio, a solicitação fica em estado retentável e o erro é registrado para auditoria.

### F-5. Confirmação assíncrona via webhook

**O que faz**: Os webhooks da Serasa em `/webhooks/baixa/sucesso` e `/webhooks/baixa/erro` (já existentes no backend) finalizam o status da solicitação.

**Por que é importante**: A baixa só é confirmada pela Serasa de forma assíncrona; o usuário precisa ver o resultado real.

**Requisitos funcionais**:
- **RF-5.1**: O `SerasaWebhookHandler` deve processar baixas com idempotência por `uuid` (já implementado para inclusão).
- **RF-5.2**: Quando o webhook é processado, o solicitante recebe notificação com o resultado final (sucesso ou erro com mensagem da Serasa).
- **RF-5.3**: Quando o webhook confirma `BAIXADO_SUCESSO`, a parcela volta a ficar **elegível para uma nova negativação** no modal em modo `"negativacao"` (badge muda de “Negativada” para “Elegível”).
- **RF-5.4**: Quando o webhook confirma `BAIXADO_ERRO`, a solicitação fica acessível para **reenvio** (ver F-7) e o solicitante é notificado com a mensagem de erro retornada pela Serasa.
- **RF-5.5**: Notificações de baixa são apenas **in-app** (sem e-mail), no mesmo padrão do fluxo de negativação atual.

### F-6. Histórico, acompanhamento e diferenciação visual

**O que faz**: O usuário consegue visualizar suas solicitações de baixa e seus status, no mesmo local onde hoje acompanha as solicitações de negativação, com indicação visual clara do tipo de operação.

**Requisitos funcionais**:
- **RF-6.1**: Listagem de solicitações pendentes deve incluir solicitações de baixa, com indicação clara do tipo (`Negativação` vs `Baixa`) — por exemplo, badge colorido distinto e/ou ícone.
- **RF-6.2**: O detalhe da solicitação deve mostrar motivo da baixa, parcela, valor, datas e status atual.
- **RF-6.3**: Na fila de aprovações, baixas e negativações devem ser distinguíveis sem precisar abrir cada item (cor/ícone/badge no card de listagem).

### F-7. Reenvio de baixa em caso de erro

**O que faz**: Quando o Serasa retorna `BAIXADO_ERRO` no webhook, o solicitante pode disparar um **reenvio** da mesma solicitação pela tela de detalhe, sem precisar criar uma nova solicitação nem passar novamente pelo fluxo de aprovação.

**Por que é importante**: Erros transitórios ou de validação podem ser resolvidos sem onerar o aprovador novamente; o aprovador já autorizou aquela parcela com aquele motivo.

**Requisitos funcionais**:
- **RF-7.1**: O detalhe de uma solicitação em status `BAIXADO_ERRO` deve exibir botão **“Reenviar baixa”** visível ao solicitante original.
- **RF-7.2**: O reenvio dispara nova chamada `DELETE` para a Serasa reutilizando o mesmo motivo, parcela e aprovação originais; gera um novo `transactionId`.
- **RF-7.3**: Cada tentativa de reenvio fica registrada no histórico da solicitação (timestamp, `transactionId`, resultado).
- **RF-7.4**: O número de reenvios deve ser limitado (sugestão: 3 tentativas) — ultrapassado o limite, é necessário criar uma nova solicitação.

### F-8. Indicadores de baixa no Dashboard

**O que faz**: O dashboard gerencial ganha dois novos gráficos exclusivos para baixas, oferecendo visão analítica de motivos e tendências.

**Por que é importante**: Permite à gestão entender padrões de baixa (ex.: muitas baixas por “contestação” podem indicar problemas no fluxo de negativação) e acompanhar o saldo líquido de inadimplência ao longo do tempo.

**Requisitos funcionais**:
- **RF-8.1**: Adicionar **Gráfico de Motivos de Baixa** (pizza ou barras horizontais) mostrando a distribuição percentual dos motivos das baixas concluídas com sucesso em uma janela configurável (default: últimos 12 meses). Usar os 7 motivos do RF-2.1; agrupar “sem dados” como vazio com mensagem amigável.
- **RF-8.2**: Adicionar **Gráfico Misto Negativações × Baixas / mês (últimos 12 meses)**: barras representando o total de negativações concluídas (`NEGATIVADO_SUCESSO`) por mês e linha representando o total de baixas concluídas (`BAIXADO_SUCESSO`) por mês.
- **RF-8.3**: Ambos os gráficos devem respeitar o tema (claro/escuro) e a paleta de cores já usados no dashboard.
- **RF-8.4**: Os gráficos devem suportar estados vazios (sem dados no período) e de erro de carregamento, com mensagens textuais legíveis por leitores de tela.
- **RF-8.5**: Os gráficos devem ser acessíveis: legendas associadas, tooltips com valores absolutos e descrição textual alternativa do conjunto de dados (`aria-label` ou tabela equivalente).
- **RF-8.6**: A janela de 12 meses é fixa nesta entrega; filtros por período customizado ficam para evolução futura.

## Experiência do Usuário

- **Solicitante** acessa a tela de informações do cliente, clica em um botão **“Baixa de dívida”** (a ser adicionado próximo ao botão atual de negativar). O `NegativacaoDebtsModal` abre em modo `"baixa"`, listando as parcelas com badge “Negativada” agora selecionáveis. O usuário seleciona uma ou mais parcelas e clica em **“Solicitar baixa”**.
- O modal de confirmação aparece pedindo: combo **“Motivo da baixa”** (obrigatório), justificativa opcional. O solicitante envia; recebe toast/notificação de que a solicitação foi criada.
- **Aprovador** vê a solicitação na mesma fila de aprovações que já usa, com **badge/ícone distinto** indicando que é uma **Baixa** (não uma Negativação) e o motivo selecionado. Aprova/rejeita com senha de transação.
- Após aprovação, o solicitante recebe notificação in-app “Baixa enviada à Serasa”. Quando o webhook chega, recebe “Baixa concluída com sucesso” (e a parcela volta a ficar elegível para nova negativação no modal) ou “Baixa rejeitada pela Serasa: <mensagem>” com botão **“Reenviar baixa”** disponível no detalhe.
- **Gestor** acessa o dashboard e visualiza os dois novos gráficos: motivos de baixa e comparativo de negativações × baixas dos últimos 12 meses, para acompanhar a saúde da operação.

### Acessibilidade
- Todos os controles novos devem ter `aria-label` descritivo (ex.: “Selecionar parcela X para baixa”, “Motivo da baixa”).
- Combos devem ser navegáveis por teclado; mensagens de erro associadas via `aria-describedby`.
- Estados de loading/erro devem ser anunciados a leitores de tela (`role="status"`/`role="alert"`).

## Restrições Técnicas de Alto Nível

- **Integração Serasa PEFIN**: contrato externo imutável; ver `documentos/documentacao-serasa-pefin-v8.md`. Operação é `HTTP DELETE` com headers (`creditor-document`, `debtor-document`, `contract-number` ou `cadus`, `reason`, `type: PEFIN`, `Authorization: Bearer`). Sem body.
- **Autenticação Serasa**: reuso do client/token existente (homologação e produção). Token JWT renovado sob demanda.
- **Webhooks**: endpoints `/webhooks/baixa/sucesso` e `/webhooks/baixa/erro` já existem; processamento idempotente por `uuid`.
- **Senha de transação**: regras de hashing e verificação atuais (PBKDF2) — sem alteração.
- **Frontend Fluig**: a aplicação roda dentro do Fluig (pasta `c:\fluig\trenamento\wcm\layout\jnc_inadimplencia`); novas chamadas HTTP devem passar pelo proxy já configurado (`/inadimplencia/...`).
- **Latência**: envio do `DELETE` à Serasa deve ocorrer em ≤ 5s após aprovação.
- **Compliance**: motivo da baixa é informação obrigatória de auditoria; deve ser persistido junto com a solicitação.

## Fora de Escopo

- Cancelar uma baixa que já foi enviada à Serasa e está aguardando webhook (não há contrato com a Serasa para esta operação no curto prazo).
- Botão de **baixa em lote por cliente** (baixar todas as parcelas negativadas de um cliente em todas as vendas) — operação acontece sempre no contexto de uma venda.
- **Renegativação automática** de uma dívida baixada — após `BAIXADO_SUCESSO` a parcela volta a ser elegível para negativação, mas o usuário precisa abrir uma nova solicitação de negativação manualmente (não há reabertura automática).
- Tela administrativa para gerenciar a lista de motivos disponíveis no combo — a lista é fixa no código nesta entrega.
- Notificações por e-mail/push — apenas in-app, no padrão atual.
- Filtros customizados de período nos gráficos do dashboard — janela fixa de 12 meses nesta entrega.
- Relatório/exportação CSV/Power BI dedicado a baixas — pode ser tratado em PRD futuro.

> Nota: baixa de avalista isolada **não** é fora de escopo, mas será atendida pelo comportamento padrão da Serasa (baixar a dívida principal baixa automaticamente todos os avalistas vinculados). Não haverá UI dedicada para baixar um avalista específico.

## Questões em Aberto

- **Q-1**: Limite exato de reenvios em `BAIXADO_ERRO` (proposto: 3 tentativas) precisa ser validado com a operação.
- **Q-2**: Definição final do design (cor/ícone) para distinguir baixa de negativação na fila de aprovações — alinhar com Design antes da implementação visual.
- **Q-3**: Confirmar se o gráfico de motivos deve ser pizza ou barras horizontais — decisão a ser tomada na Tech Spec / com Design.
