# PRD: Migração do Módulo Inadimplencia de Node.js para .NET 8

## Visão Geral

Este PRD define a migração do módulo `inadimplencia` de uma API Node.js/Express para uma API C#/.NET 8. O módulo atual é utilizado pelo frontend `jnc_inadimplencia` para gestão de carteira inadimplente, registro de atendimentos e ocorrências, atribuição de responsáveis, dashboard, notificações em tempo real, relatórios RM/TOTVS e integração Serasa PEFIN.

A migração visa modernizar a stack tecnológica para melhorar manutenibilidade e escalabilidade, mantendo 100% de compatibilidade com o frontend existente. A nova API seguirá os princípios de Clean Architecture + CQRS, com separação clara de domínios e adapters de infraestrutura.

## Objetivos

- **Modernização da Stack**: Migrar de Node.js/Express para .NET 8/ASP.NET Core para aproveitar benefícios de tipagem estática, performance e ecossistema Microsoft
- **Manutenibilidade**: Implementar Clean Architecture + CQRS para separação de responsabilidades, facilitando manutenção e evolução do código
- **Escalabilidade**: Preparar a arquitetura para escalabilidade horizontal, especialmente para notificações SSE e processamento assíncrono
- **Compatibilidade Total**: Manter 100% de compatibilidade com o contrato REST atual, garantindo que o frontend continue funcionando sem modificações
- **Melhores Práticas**: Adotar padrões modernos de desenvolvimento, testes automatizados e containerização via Docker

## Histórias de Usuário

**Equipe Interna (Desenvolvedores/DevOps):**
- Como desenvolvedor, quero que o código seja organizado em camadas claras para facilitar manutenção e evolução
- Como desenvolvedor, quero testes automatizados para garantir qualidade e reduzir bugs
- Como DevOps, quero que a API seja containerizada para facilitar deployment e escalabilidade

**Frontend jnc_inadimplencia:**
- Como consumidor da API, quero que o contrato REST seja mantido 100% compatível para não precisar modificar o frontend
- Como usuário do frontend, quero continuar recebendo notificações em tempo real via SSE
- Como usuário do frontend, quero que todos os endpoints atuais continuem funcionando

**Administradores do Sistema:**
- Como administrador, quero continuar atribuindo responsáveis para vendas inadimplentes
- Como administrador, quero continuar gerenciando usuários e permissões
- Como administrador, quero continuar visualizando dashboard com KPIs e relatórios

## Funcionalidades Principais

### 1. Carteira Inadimplente
**O que faz**: Consulta e gestão de carteira de clientes inadimplentes
**Por que é importante**: É o core business do módulo, utilizado pelo frontend para listar e filtrar inadimplências

**Requisitos funcionais:**
1. A API deve suportar listagem de todas inadimplências com última `PROXIMA_ACAO`
2. A API deve permitir consulta por CPF/CNPJ, normalizando somente dígitos
3. A API deve permitir consulta por `NUM_VENDA`
4. A API deve permitir consulta por responsável, com cor do usuário
5. A API deve permitir consulta por nome do cliente usando `LIKE`
6. A regra `INADIMPLENTE = 'SIM'` (trim/case-insensitive) deve ser aplicada consistentemente em todas consultas
7. A API deve manter endpoints em ambos os formatos: raiz e `/inadimplencia/*`

### 2. Ocorrências
**O que faz**: Registro de ocorrências em vendas inadimplentes
**Por que é importante**: Permite documentar interações e ações tomadas com clientes

**Requisitos funcionais:**
1. A API deve aceitar criação de ocorrência com campos obrigatórios: `NUM_VENDA_FK`, `NOME_USUARIO_FK`, `DESCRICAO`, `STATUS_OCORRENCIA`, `DT_OCORRENCIA`, `HORA_OCORRENCIA`
2. A API deve aceitar campos opcionais: `PROXIMA_ACAO`, `PROTOCOLO`
3. A API deve aceitar payload em camelCase e UPPER_SNAKE
4. A API deve validar FK de venda antes de inserir ocorrência, retornando 409 em caso de falha
5. A API deve suportar atualização e exclusão de ocorrências
6. A API deve permitir listagem de ocorrências por venda, protocolo e ID

### 3. Atendimentos
**O que faz**: Registro de atendimentos com geração de protocolo único
**Por que é importante**: Permite rastreabilidade de interações com clientes

**Requisitos funcionais:**
1. A API deve gerar `PROTOCOLO` no formato `AAAAMMDD#####`
2. A geração de protocolo deve ser transacional, com isolamento `SERIALIZABLE`, usando `UPDLOCK`/`HOLDLOCK`
3. A API deve salvar snapshot JSON da venda em `DADOS_VENDA`
4. A API deve permitir consulta de atendimentos por CPF, `NUM_VENDA`, protocolo e nome do cliente

### 4. Usuários
**O que faz**: Gestão de usuários e permissões
**Por que é importante**: Controla acesso e responsabilidades no sistema

**Requisitos funcionais:**
1. A API deve aceitar apenas perfis `admin` e `operador`
2. A API deve validar `COR_HEX` no formato `#RRGGBB`, aceitando com ou sem `#`
3. A API deve fazer upsert idempotente por `USER_CODE` ou `NOME`
4. A API deve retornar `200` com `exists: true` se usuário já existe, `201` se criado
5. A API deve atribuir perfil `admin` para usuário `USER_CODE = wffluig` quando perfil não informado
6. A API deve atribuir perfil `operador` para demais usuários quando perfil não informado

### 5. Responsáveis
**O que faz**: Atribuição de responsáveis para vendas inadimplentes
**Por que é importante**: Permite distribuir workload e rastreabilidade

**Requisitos funcionais:**
1. A API deve exigir `adminUserCode` para atribuição
2. A API deve validar que o usuário admin existe e tem `PERFIL = admin`
3. A API deve fazer upsert por `NUM_VENDA_FK` via `MERGE`
4. A API deve criar notificação `VENDA_ATRIBUIDA` para novo responsável quando responsável mudar
5. A API deve remover notificações de atribuicao do responsável anterior
6. A API deve remover notificações relacionadas ao responsável anterior na remoção

### 6. Kanban
**O que faz**: Gestão de status de vendas em kanban
**Por que é importante**: Permite visualizar progresso e próximas ações

**Requisitos funcionais:**
1. A API deve fazer upsert por `NUM_VENDA_FK` + `PROXIMA_ACAO`
2. A API deve normalizar status para `todo`, `inProgress`, `done`
3. A API deve aceitar aliases em PT-BR e EN para status
4. A API deve usar `STATUS_DATA` no formato `YYYY-MM-DD`

### 7. Fiadores
**O que faz**: Consulta de fiadores por venda
**Por que é importante**: Permite identificar garantidores de vendas inadimplentes

**Requisitos funcionais:**
1. A API deve permitir consulta por `NUM_VENDA`
2. A API deve permitir consulta por CPF
3. A API deve retornar associados/fiadores ordenados por `DATA_CADASTRO DESC, NOME ASC`

### 8. Dashboard
**O que faz**: Fornecer KPIs e métricas sobre carteira inadimplente
**Por que é importante**: Permite monitoramento e tomada de decisões

**Requisitos funcionais:**
1. A API deve fornecer KPIs de vendas por responsável, inadimplência/clientes por empreendimento
2. A API deve fornecer status de repasse, blocos, unidades, usuários ativos
3. A API deve fornecer ocorrências por usuário, venda, dia, hora, dia/hora e listagem completa
4. A API deve fornecer próximas ações por dia, ações definidas, atendentes por próxima ação
5. A API deve fornecer aging, aging detalhes, parcelas inadimplentes, parcelas detalhes
6. A API deve fornecer score/saldo, score/saldo detalhes, saldo por mês de vencimento, perfil risco empreendimento
7. A API deve aplicar filtros `dataInicio` e `dataFim` juntos no formato `YYYY-MM-DD`
8. A API deve limitar `limit` a máximo 1000
9. A API deve usar whitelists/parsers para `faixa`, `qtd` e `score`, nunca SQL livre

### 9. Notificações
**O que faz**: Sistema de notificações em tempo real via SSE
**Por que é importante**: Permite comunicação assíncrona com usuários sobre eventos relevantes

**Requisitos funcionais:**
1. A API deve normalizar username para lowercase
2. A API deve criar notificação `VENDA_ATRIBUIDA` quando admin atribui venda a responsável
3. A API deve criar notificação `VENDA_ATRASADA` quando venda tem kanban `todo` com `PROXIMA_ACAO` anterior a hoje
4. A API deve implementar dedupe por `TIPO|USUARIO|NUM_VENDA|PROXIMA_ACAO_DIA`
5. A API deve persistir notificação antes de broadcast SSE
6. A API deve exigir notificação lida para exclusão, retornando 409 se não lida
7. A API deve considerar responsabilidade atual na listagem
8. A API deve manter SSE com snapshot inicial, heartbeat a cada 15s e eventos
9. A API deve suportar marcar uma notificação como lida
10. A API deve suportar marcar todas notificações como lidas
11. A API deve suportar exclusão lógica de notificações

### 10. Relatórios RM/TOTVS
**O que faz**: Geração de relatórios via integração Fluig/TOTVS RM
**Por que é importante**: Permite emissão de ficha financeira e outros relatórios corporativos

**Requisitos funcionais:**
1. A API deve buscar XML de parâmetros no Fluig via dataset `ds_paramsRel`
2. A API deve usar fallback em `ds_paiFilho_controleDeAcessoRMreportsFluig` se não encontrar
3. A API deve trocar parâmetros `COLIGADA` e `NUMVENDA` no XML
4. A API deve chamar `dsIntegraFacilRM` com `OPC=6`, `REPORT`, `COLIGADA`, `PARAMETER`, `FILE=Report.pdf`, `FILTRO=''`
5. A API deve retornar URL do PDF gerado

### 11. Serasa PEFIN
**O que faz**: Integração com Serasa Experian PEFIN para negativação
**Por que é importante**: Permite registrar dívidas em órgãos de proteção ao crédito

**Requisitos funcionais:**
1. A API deve fornecer preview de negativação por venda
2. A API deve solicitar negativação principal e garantidor
3. A API deve validar documentos UAT, valor mínimo `10.00`, data de vencimento e endereço
4. A API deve mascarar documentos em respostas e logs
5. A API deve persistir solicitações pendentes antes de chamar Serasa
6. A API deve marcar erro se principal falhar
7. A API deve continuar enviando garantidores sequencialmente mesmo se um falhar
8. A API deve processar webhooks de inclusão, avalista e baixa
9. A API deve exigir `uuid` em webhooks para idempotência
10. A API deve implementar cache de token com buffer de 60s
11. A API deve implementar retry uma vez em 401
12. A API deve suportar consulta de histórico, detalhes e acompanhamento

## Experiência do Usuário

**Personas:**
- **Desenvolvedores**: Equipe interna que mantém e evolui a API
- **Frontend jnc_inadimplencia**: Aplicação web que consome a API
- **Administradores**: Usuários que gerenciam carteira inadimplente

**Fluxos principais:**
1. **Consulta de carteira**: Frontend lista inadimplências com filtros, API retorna dados consistentes
2. **Registro de ocorrência**: Frontend envia dados, API valida e persiste, notifica responsáveis
3. **Atribuição de responsável**: Admin atribui venda, API valida permissões, cria notificação
4. **Notificações em tempo real**: Frontend conecta SSE, API envia eventos de mudanças
5. **Relatórios**: Frontend solicita relatório, API integra com Fluig/RM, retorna URL

**Considerações UI/UX:**
- Contrato REST deve ser mantido 100% compatível
- SSE deve continuar funcionando exatamente igual
- Respostas de erro padronizadas com `ProblemDetails`
- Swagger/OpenAPI deve documentar todos endpoints

## Restrições Técnicas de Alto Nível

**Integrações externas:**
- SQL Server DW/operacional para persistência de dados
- Fluig para autenticação e datasets
- TOTVS RM para relatórios
- Serasa Experian PEFIN para negativação

**Segurança e privacidade:**
- Dados sensíveis (CPF/CNPJ, tokens, secrets, cookies Fluig, payloads Serasa) devem ser mascarados em logs e respostas
- Segredos não devem ser commitados em `appsettings.json`, usar env vars/secret store
- Autenticação Serasa via Basic para obter bearer token

**Performance e escalabilidade:**
- Queries de dashboard podem ser pesadas, monitorar timeouts, índices e paginação
- Notificações não podem depender apenas de memória se API escalar horizontalmente
- Scanner de vencidos deve ser `BackgroundService` com trava de reentrada

**Requisitos não negociáveis:**
- Stack: .NET 8, ASP.NET Core, Clean Architecture, CQRS
- Banco: SQL Server (não será substituído)
- Arquitetura: Domain, Application, Infrastructure, API layers
- Docker: Multi-stage build, runtime non-root, healthcheck HTTP
- Contrato: 100% compatibilidade com frontend existente
- SSE: Manter exatamente igual para notificações

## Fora de Escopo

**Funcionalidades explicitamente excluídas:**
- Migração de banco de dados (schema SQL Server será mantido)
- Mudanças no schema de banco de dados
- Novas funcionalidades além das existentes no módulo Node.js
- Migração do frontend jnc_inadimplencia
- Implementação de CI/CD inicial (será apenas Git na branch master)
- Logging, monitoring e observability (podem ser sugeridos na implementação)
- Multi-tenancy (ambiente único)
- Período de operação paralela das duas APIs

**Considerações futuras:**
- Substituição de SSE por WebSockets ou outra tecnologia
- Implementação de Redis Pub/Sub para multi-instância SSE
- Implementação de outbox pattern para garantia de entrega
- CI/CD pipeline
- Logging estruturado e observability
- Testes de carga e performance

## Questões em Aberto

**Análise de endpoints não utilizados:**
- Quais endpoints do módulo Node.js não estão sendo utilizados pelo frontend?
- Como comprovar que um endpoint não é utilizado antes de descartá-lo?
- Endpoints de teste Serasa (`/serasa-pefin/testes`) devem ser bloqueados em produção na nova API?

**Definição de fases de migração:**
- Qual deve ser a primeira fase da migração? (domínios mais simples ou críticos?)
- Como definir a ordem de migração dos domínios?
- Como validar que cada fase está completa antes de sugerir a próxima?

**Integrações externas:**
- As credenciais e configurações das integrações (Fluig, RM, Serasa) estão documentadas?
- Há necessidade de ambientes de testes (UAT) para as integrações?

**Dados sensíveis:**
- Qual é a política de mascaramento de dados sensíveis em logs?
- Há requisitos de compliance específicos (LGPD, etc.)?

**Performance:**
- Qual é o tempo de resposta atual dos endpoints críticos?
- Há gargalos conhecidos que devem ser priorizados na migração?
