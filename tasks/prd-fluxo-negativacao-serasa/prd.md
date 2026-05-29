# PRD — Fluxo de Solicitação e Aprovação de Negativação Serasa

## Visão Geral

Atualmente o módulo `Serasa PEFIN` (ver `prd-serasa-pefin-completo`) expõe a integração técnica com o Serasa (preview, inclusão, webhooks). Falta, porém, o **fluxo de negócio** que controla **quem pode solicitar**, **quem aprova** e **quando** uma negativação é efetivamente disparada.

Esta funcionalidade introduz um fluxo de **solicitação → aprovação → envio Serasa → retorno** com:
- Botão "Negativar" no atendimento, habilitado apenas quando há dívidas elegíveis (>60 dias de atraso).
- Solicitação registrada como Ocorrência e em uma máquina de estados (status na entidade `SerasaPefinSolicitacaoCompleta`).
- Aprovação realizada por usuários autorizados (`aracy.mendoca`, `adriano.oliveira`), com confirmação por **senha de transação** específica.
- Envio à Serasa **somente após aprovação**, reusando o endpoint `POST /serasa-pefin/negativar` já existente.
- Notificações **in-app + SSE realtime** ao solicitante e ao aprovador a cada transição relevante (solicitação criada, aprovado/rejeitado, retorno do Serasa).

**Público-alvo (backend)**: este PRD foca no backend (.NET 9) que sustenta o fluxo. UI será especificada em PRD separado.

## Objetivos

- Permitir que operadores de cobrança solicitem a negativação de clientes com **dívidas elegíveis** (>60 dias de atraso) com rastreabilidade completa.
- Garantir **dupla custódia**: solicitante ≠ aprovador, e aprovação requer senha de transação adicional.
- Reduzir risco operacional: nenhuma chamada à Serasa é disparada sem aprovação de um usuário autorizado.
- Manter histórico auditável de cada decisão (quem solicitou, quem aprovou/rejeitou, quando, com qual script).
- Notificar em tempo real (≤2s) os atores envolvidos a cada mudança de estado.

**Métricas de sucesso**:
- 100% das negativações enviadas ao Serasa têm registro de aprovação anterior em Ocorrência.
- 0% de chamadas à Serasa originadas sem senha de transação validada.
- Tempo médio entre solicitação e decisão (aprovação/rejeição): meta < 1 dia útil.

## Histórias de Usuário

- **Como operador de cobrança**, quero solicitar a negativação de um cliente com dívidas vencidas há >60 dias, para iniciar formalmente o processo.
- **Como operador**, quero ver claramente quais dívidas (parcelas) são elegíveis para negativação, e selecionar somente as que desejo negativar.
- **Como operador**, quero confirmar minha solicitação assinando digitalmente com minha senha de transação, para evitar negativações acidentais.
- **Como aprovador (`aracy.mendoca`/`adriano.oliveira`)**, quero ser notificado em tempo real de novas solicitações de negativação, para agir rapidamente.
- **Como aprovador**, quero abrir o contrato/atendimento, analisar a solicitação e aprovar ou rejeitar, registrando minha decisão com senha de transação.
- **Como aprovador**, quero saber quando o Serasa confirmou a inclusão (ou retornou erro), para acompanhar o caso.
- **Como solicitante**, quero ser notificado quando minha solicitação for aprovada, rejeitada e quando o Serasa devolver o resultado final.

## Funcionalidades Principais

### F1. Consulta de elegibilidade de dívidas (`GET /negativacao/vendas/{numVenda}/dividas`)
- Lista todas as parcelas em aberto da venda, indicando `elegivel: bool` (true se `DIAS_ATRASO > 60`).
- Fonte: `DW.fat_analise_inadimplencia_parcelas`.
- Inclui valor, vencimento, dias de atraso e flag de elegibilidade.
- Retorna também `clientePodeNegativar: bool` (true se ao menos uma parcela elegível).

**Requisitos funcionais**:
- RF1.1 — Endpoint retorna 404 se a venda não existir.
- RF1.2 — Endpoint retorna lista vazia + `clientePodeNegativar=false` se não houver parcela elegível.
- RF1.3 — Apenas usuários autenticados podem consultar.

### F2. Configuração de senha de transação (`/configuracoes/senha-transacao`)
- `POST` define/atualiza senha de transação do usuário autenticado (armazenamento com hash BCrypt/PBKDF2).
- `GET` retorna apenas se há senha cadastrada (`hasSenha: bool`), nunca o hash.
- Senha mínima: 6 caracteres alfanuméricos. Não pode ser igual à senha de login.

**Requisitos funcionais**:
- RF2.1 — Senha persistida em tabela dedicada (`USUARIO_SENHA_TRANSACAO`) com hash + salt.
- RF2.2 — Falhas de validação (3+ tentativas em 5 min) bloqueiam temporariamente o usuário.
- RF2.3 — Tentativas (sucesso e falha) registradas em log de auditoria.

### F3. Solicitação de negativação (`POST /negativacao/solicitacoes`)
- Body: `{ numVenda, parcelaIds: int[], incluirFiadores: bool, senhaTransacao: string }`.
- Backend valida:
  - Todas as parcelas são elegíveis (DIAS_ATRASO > 60).
  - Usuário possui senha de transação cadastrada e a senha confere.
  - Não existe solicitação ativa para a mesma venda (status AGUARDANDO_APROVACAO).
- Cria registro `SerasaPefinSolicitacaoCompleta` com status `AGUARDANDO_APROVACAO`.
- Cria Ocorrência com status `Solicitação de negativação` e descrição padronizada (template do PRD original).
- Dispara notificação para todos os aprovadores autorizados.

**Requisitos funcionais**:
- RF3.1 — `senhaTransacao` validada antes de qualquer escrita.
- RF3.2 — Ocorrência criada referenciando `numVenda` + lista de parcelas + endereço + fiadores (se `incluirFiadores`).
- RF3.3 — Mensagem da ocorrência segue template: *"Eu, {usuario} solicito a negativação do cliente {nome}, venda nº {numVenda}, endereço {end}, para a(as) parcelas: {parcelas} e seus fiadores {fiadores} via Serasa"*.
- RF3.4 — Notificação enviada (in-app + SSE) a cada aprovador autorizado.
- RF3.5 — Retorna `solicitacaoId` (Guid).

### F4. Aprovação ou rejeição (`POST /negativacao/solicitacoes/{id}/decisao`)
- Body: `{ decisao: "APROVAR" | "REJEITAR", senhaTransacao: string, justificativa?: string }`.
- Apenas usuários da lista de aprovadores podem chamar.
- Solicitação deve estar em `AGUARDANDO_APROVACAO`.
- Senha de transação obrigatória em ambos os casos (aprovar/rejeitar).

**Em caso de APROVAR**:
- Atualiza status para `APROVADA`.
- Cria Ocorrência com status `Aprovação Negativação Serasa`, descrição: *"Eu, {aprovador}, aprovo para os devidos fins a negativação do cliente {nome}, portador do CPF nº {cpf}, para a venda {numVenda}, para a(as) parcelas: {parcelas}."*
- Invoca **internamente** o command `RequestNegativacaoCommand` existente (módulo Serasa) com `numVenda` + `incluirGarantidores`.
- Atualiza status para `ENVIADA_SERASA`.
- Notifica solicitante e aprovador: "Solicitação enviada ao Serasa, retorno em breve".

**Em caso de REJEITAR**:
- Atualiza status para `REJEITADA`.
- Cria Ocorrência com status `Rejeição Negativação Serasa` + justificativa.
- Notifica o solicitante (in-app + SSE).

**Requisitos funcionais**:
- RF4.1 — Endpoint só aceita usernames `aracy.mendoca` ou `adriano.oliveira`.
- RF4.2 — Quórum: 1 aprovador basta.
- RF4.3 — Solicitante não pode aprovar a própria solicitação (mesmo se for aprovador).
- RF4.4 — Em caso de erro na chamada Serasa (HTTP 4xx/5xx), status volta para `APROVADA_FALHA_ENVIO` e notifica ambos para nova tentativa manual.

### F5. Tratamento de retorno do Serasa (extensão dos webhooks)
- Estender `SerasaWebhookHandler` existente: ao processar webhook `inclusao/sucesso` ou `inclusao/erro`, identificar a solicitação de negócio correspondente e:
  - Atualizar status final: `FINALIZADA_SUCESSO` ou `FINALIZADA_ERRO`.
  - Disparar notificação ao **solicitante** e ao **aprovador**: "Cliente {nome} foi negativado com sucesso" ou "Erro ao negativar: {mensagem}".

**Requisitos funcionais**:
- RF5.1 — Idempotência preservada (já garantida pelo módulo Serasa).
- RF5.2 — Notificações persistidas mesmo se SSE estiver offline (entrega quando o usuário voltar).

### F6. Listagem de solicitações pendentes (`GET /negativacao/solicitacoes?status=AGUARDANDO_APROVACAO`)
- Permite ao aprovador ver todas as solicitações pendentes.
- Retorna: id, numVenda, cliente, solicitante, dt_solicitacao, parcelas resumidas.

### F7. Notificações in-app + SSE
- Reativar `INotificationRepository` + tabela `INAD_NOTIFICACOES`.
- Reativar `SseHub` para push em tempo real.
- Tipos de notificação novos: `SolicitacaoNegativacao`, `AprovacaoNegativacao`, `RejeicaoNegativacao`, `RetornoSerasaSucesso`, `RetornoSerasaErro`.

## Experiência do Usuário (escopo backend)

- **Latência**: aprovações/solicitações respondem em <500ms (sem incluir chamada Serasa).
- **Tempo real**: notificações SSE entregues em ≤2s após o evento.
- **Erros**: respostas padronizadas (`problem+json`) com `code`, `message`, `field` quando aplicável.
- **Auditoria**: todo evento (solicitação, aprovação, rejeição, falha de senha) é gravado em log estruturado com `userId`, `numVenda`, `solicitacaoId`.

## Restrições Técnicas de Alto Nível

- **Integrações**: reusar `RequestNegativacaoCommand` (módulo Serasa). Não duplicar lógica de chamada HTTP.
- **Persistência**: SQL Server (mesmo banco `dwjnc` do módulo existente). Reusar entidade `SerasaPefinSolicitacaoCompleta` ampliando os status permitidos no constraint `CK_..._STATUS`.
- **Senha de transação**: hash com algoritmo resistente (BCrypt cost ≥ 11 ou PBKDF2-HMAC-SHA256 ≥ 100k iterações). Nunca logar.
- **Aprovadores**: lista hardcoded em `appsettings` (`Negativacao:UsuariosAprovadores: ["aracy.mendoca","adriano.oliveira"]`) — refatorar para tabela em fase futura.
- **Quórum**: 1 de N (configurável via `Negativacao:QuorumAprovacao=1`).
- **Conformidade**: documentos (CPF) mascarados em logs (reutilizar `SensitiveDataMaskingMiddleware`).
- **Idempotência**: webhook do Serasa já é idempotente; novas notificações usam dedupe key (tipo + usuário + numVenda).
- **Segurança**: rate limit em `POST /negativacao/solicitacoes/{id}/decisao` (3 tentativas de senha em 5min).

## Fora de Escopo

- **UI / Frontend**: este PRD trata apenas do backend. Telas (botão Negativar, popup de confirmação, tela de configuração de senha) ficam em PRD separado.
- **E-mail / SMS**: notificações apenas in-app + SSE nesta fase.
- **Quórum múltiplo**: aprovação por 1 usuário basta. Múltiplos aprovadores fica para fase futura.
- **Aprovadores configuráveis via UI**: lista hardcoded por enquanto.
- **Recuperação de senha de transação**: não há recovery automatizado; admin precisa resetar manualmente (fora de escopo).
- **Cancelamento/baixa proativa pós-negativação**: já fora de escopo no PRD Serasa original.
- **Auditoria de mudanças de configuração**: log básico apenas; relatório formal fica para fase futura.

## Questões em Aberto

- **Tabela `DW.fat_analise_inadimplencia_parcelas`**: confirmar nome exato da coluna que indica dias de atraso (`DIAS_ATRASO`?) e se já existe view consumível pela API.
- **Lockout de senha de transação**: 3 tentativas em 5min é razoável? Por quanto tempo o usuário fica bloqueado (15min sugerido)?
- **Aprovador que é também solicitante**: regra "não pode aprovar a própria" precisa confirmação explícita do PO.
- **`APROVADA_FALHA_ENVIO`**: comportamento desejado quando Serasa retorna erro síncrono — auto-retry ou aguardar ação humana?
- **Ocorrência de "Rejeição"**: nome de status sugerido (`Rejeição Negativação Serasa`) — confirmar com produto.
- **Endereço completo**: a fonte (vw_inadimplencia? cadastro de cliente?) precisa ser confirmada para montar a mensagem.
- **Senha de transação compartilhada com outras ações futuras** (estornos, descontos)? Se sim, módulo deve ser genérico (`USUARIO_SENHA_TRANSACAO` e não `USUARIO_SENHA_NEGATIVACAO`).
