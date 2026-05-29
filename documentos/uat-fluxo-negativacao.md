# Roteiro de Validação UAT - Fluxo de Negativação Serasa

## Visão Geral

Este documento descreve o roteiro de validação manual em ambiente UAT para o fluxo de solicitação e aprovação de negativação Serasa. Este roteiro complementa a suíte de testes E2E automatizados e deve ser executado antes do deploy em produção.

## Pré-requisitos

### Ambiente UAT
- API .NET rodando em ambiente UAT
- SQL Server `dwjnc` acessível
- Serasa PEFIN UAT configurado (credentials disponíveis)
- Base de dados com migrations aplicadas (scripts 001-006)

### Usuários de Teste
Os seguintes usuários devem existir na tabela `dbo.USUARIO`:

| Username | Perfil | Senha de Transação |
|----------|--------|-------------------|
| op1 | operador | 123abc |
| aracy.mendoca | admin | xyz789 |
| adriano.oliveira | admin | xyz789 |
| usuario.comum | operador | test123 |

**Nota**: Aprovadores devem estar configurados em `appsettings.json`:
```json
{
  "Negativacao": {
    "UsuariosAprovadores": ["aracy.mendoca", "adriano.oliveira"],
    "QuorumAprovacao": 1,
    "DiasAtrasoMinimo": 60,
    "MaxTentativasSenha": 3,
    "LockoutMinutos": 15,
    "JanelaTentativasMinutos": 5
  }
}
```

### Vendas de Teste
Vendas elegíveis para negativação (com parcelas >60 dias de atraso):
- Venda 295 (cliente de teste com CPF da massa UAT)
- Venda 300 (cliente de teste alternativo)

### Massa de Teste Serasa UAT
Documentos autorizados para ambiente UAT (já configurados em `SerasaPefinConstants.UatAuthorizedDocuments`):
- 000.012.095-23 (CLIENTE TESTE ABCB)
- 000.084.414-48 (BJRNRNSD OIOIE)
- 074.205.658-99 (TESTE CPF SEM POSITIVO)
- 042.367.984-84 (NCUH KLCOHKKHH ECAJAE NCGMLU)
- 43.557.445/0001-80 (ESFERA ARENA E NEGOCIOS SPE LTDA)
- 00.079.854/0001-05 (U F NXALWPULN ZK EWCQIXG)
- 168.816.700-52 (TST PEFIN)
- 115.724.678-86 (TST FLEX)

## Cenários de Validação

### Cenário A - Happy Path Completo

**Objetivo**: Validar o fluxo completo de solicitação → aprovação → envio Serasa → retorno.

**Passos**:

1. **Configurar senha de transação para solicitante**
   ```bash
   curl -X POST http://uat-api:8080/configuracoes/senha-transacao \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{"senhaAtual": null, "novaSenha": "123abc"}'
   ```
   **Esperado**: 204 No Content

2. **Configurar senha de transação para aprovador**
   ```bash
   curl -X POST http://uat-api:8080/configuracoes/senha-transacao \
     -H "Content-Type: application/json" \
     -H "X-Username: aracy.mendoca" \
     -d '{"senhaAtual": null, "novaSenha": "xyz789"}'
   ```
   **Esperado**: 204 No Content

3. **Consultar dívidas elegíveis**
   ```bash
   curl -X GET http://uat-api:8080/negativacao/vendas/295/dividas \
     -H "X-Username: op1"
   ```
   **Esperado**: 200 OK com `clientePodeNegativar: true` e lista de parcelas

4. **Criar solicitação de negativação**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{
       "numVenda": 295,
       "parcelaIds": [1],
       "incluirFiadores": false,
       "senhaTransacao": "123abc"
     }'
   ```
   **Esperado**: 201 Created com `solicitacaoId`

5. **Verificar notificação para aprovadores**
   ```sql
   SELECT * FROM dbo.INAD_NOTIFICACOES 
   WHERE USERNAME IN ('aracy.mendoca', 'adriano.oliveira')
   ORDER BY DT_CRIACAO DESC
   ```
   **Esperado**: Notificação do tipo `SolicitacaoNegativacao` para venda 295

6. **Verificar ocorrência criada**
   ```sql
   SELECT * FROM dbo.OCORRENCIAS 
   WHERE NUM_VENDA_FK = 295 
   AND STATUS_OCORRENCIA = 'Solicitação de negativação'
   ORDER BY DT_OCORRENCIA DESC
   ```
   **Esperado**: Ocorrência com mensagem padrão de solicitação

7. **Aprovar solicitação**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes/{solicitacaoId}/decisao \
     -H "Content-Type: application/json" \
     -H "X-Username: aracy.mendoca" \
     -d '{
       "decisao": "APROVAR",
       "senhaTransacao": "xyz789"
     }'
   ```
   **Esperado**: 200 OK

8. **Verificar status da solicitação**
   ```sql
   SELECT ID, STATUS, TRANSACTION_ID, SOLICITANTE_USERNAME, APROVADOR_USERNAME 
   FROM dbo.SERASA_PEFIN_SOLICITACOES 
   WHERE ID = '{solicitacaoId}'
   ```
   **Esperado**: `STATUS = 'ENVIADO_SERASA'` com `TRANSACTION_ID` preenchido

9. **Verificar ocorrência de aprovação**
   ```sql
   SELECT * FROM dbo.OCORRENCIAS 
   WHERE NUM_VENDA_FK = 295 
   AND STATUS_OCORRENCIA = 'Aprovação Negativação Serasa'
   ORDER BY DT_OCORRENCIA DESC
   ```
   **Esperado**: Ocorrência com mensagem de aprovação

10. **Verificar notificação para solicitante e aprovador**
    ```sql
    SELECT * FROM dbo.INAD_NOTIFICACOES 
    WHERE USERNAME IN ('op1', 'aracy.mendoca')
    AND TIPO = 'AprovacaoNegativacao'
    ORDER BY DT_CRIACAO DESC
    ```
    **Esperado**: Notificação de aprovação para ambos

11. **Simular webhook de sucesso Serasa**
    ```bash
    curl -X POST http://uat-api:8080/inadimplencia/serasa-pefin/webhooks/inclusao/sucesso \
      -H "Content-Type: application/json" \
      -d '{
        "uuid": "test-uuid-123",
        "debtorDocument": "000.012.095-23",
        "creditorDocument": "62173620000180",
        "contract": "295/00",
        "debtValue": 1000.00,
        "debtDate": "2024-01-01",
        "cadusKey": "008080948A",
        "cadusSerie": "001",
        "debtType": "PEFIN",
        "creditorArea": "0001",
        "categoryId": "FI",
        "error": null
      }'
    ```
    **Esperado**: 200 OK

12. **Verificar status final**
    ```sql
    SELECT STATUS FROM dbo.SERASA_PEFIN_SOLICITACOES 
    WHERE ID = '{solicitacaoId}'
    ```
    **Esperado**: `STATUS = 'NEGATIVADO_SUCESSO'`

**Critérios de Aceite**:
- ✅ Solicitação criada com status `AGUARDANDO_APROVACAO`
- ✅ Ocorrência de solicitação registrada
- ✅ Notificações enviadas aos aprovadores
- ✅ Aprovação registrada com ocorrência
- ✅ Serasa chamada com sucesso (transactionId gerado)
- ✅ Status atualizado para `ENVIADO_SERASA`
- ✅ Notificações enviadas ao solicitante e aprovador
- ✅ Webhook processado corretamente
- ✅ Status final `NEGATIVADO_SUCESSO`

---

### Cenário B - Rejeição

**Objetivo**: Validar que rejeição não chama Serasa e notifica apenas o solicitante.

**Passos**:

1-6. Mesmos passos do Cenário A (criar solicitação)

7. **Rejeitar solicitação**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes/{solicitacaoId}/decisao \
     -H "Content-Type: application/json" \
     -H "X-Username: aracy.mendoca" \
     -d '{
       "decisao": "REJEITAR",
       "senhaTransacao": "xyz789",
       "justificativa": "Dados insuficientes para negativação"
     }'
   ```
   **Esperado**: 200 OK

8. **Verificar status da solicitação**
   ```sql
   SELECT STATUS, JUSTIFICATIVA FROM dbo.SERASA_PEFIN_SOLICITACOES 
   WHERE ID = '{solicitacaoId}'
   ```
   **Esperado**: `STATUS = 'REJEITADA'` com `JUSTIFICATIVA` preenchida

9. **Verificar que Serasa NÃO foi chamada**
   - Verificar logs da API - não deve haver chamada HTTP para Serasa
   - `TRANSACTION_ID` deve ser NULL

10. **Verificar notificação apenas para solicitante**
    ```sql
    SELECT * FROM dbo.INAD_NOTIFICACOES 
    WHERE USERNAME = 'op1'
    AND TIPO = 'RejeicaoNegativacao'
    ORDER BY DT_CRIACAO DESC
    ```
    **Esperado**: Notificação de rejeição para `op1`, mas NÃO para aprovadores

**Critérios de Aceite**:
- ✅ Status `REJEITADA` com justificativa
- ✅ Serasa NÃO chamada (transactionId NULL)
- ✅ Ocorrência de rejeição registrada
- ✅ Notificação enviada apenas ao solicitante
- ✅ Aprovadores NÃO notificados da rejeição

---

### Cenário C - Auto-aprovação Bloqueada

**Objetivo**: Validar que solicitante não pode aprovar sua própria solicitação.

**Passos**:

1-2. Configurar senha para `aracy.mendoca` (que também é aprovador)

3. **Criar solicitação como aprovador**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes \
     -H "Content-Type: application/json" \
     -H "X-Username: aracy.mendoca" \
     -d '{
       "numVenda": 295,
       "parcelaIds": [1],
       "incluirFiadores": false,
       "senhaTransacao": "xyz789"
     }'
   ```
   **Esperado**: 201 Created

4. **Tentar aprovar própria solicitação**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes/{solicitacaoId}/decisao \
     -H "Content-Type: application/json" \
     -H "X-Username: aracy.mendoca" \
     -d '{
       "decisao": "APROVAR",
       "senhaTransacao": "xyz789"
     }'
   ```
   **Esperado**: 400 Bad Request ou 403 Forbidden com erro `SOLICITANTE_NAO_PODE_APROVAR`

**Critérios de Aceite**:
- ✅ Erro retornado com código `SOLICITANTE_NAO_PODE_APROVAR`
- ✅ Solicitação permanece em `AGUARDANDO_APROVACAO`
- ✅ Serasa NÃO chamada

---

### Cenário D - Não-aprovador Bloqueado

**Objetivo**: Validar que usuário comum não pode aprovar solicitações.

**Passos**:

1-2. Configurar senha para `op1` (solicitante) e `usuario.comum` (não-aprovador)

3-6. Criar solicitação como `op1`

7. **Tentar aprovar como usuário comum**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes/{solicitacaoId}/decisao \
     -H "Content-Type: application/json" \
     -H "X-Username: usuario.comum" \
     -d '{
       "decisao": "APROVAR",
       "senhaTransacao": "test123"
     }'
   ```
   **Esperado**: 401 Unauthorized ou 403 Forbidden com erro `NAO_AUTORIZADO`

**Critérios de Aceite**:
- ✅ Erro retornado com código `NAO_AUTORIZADO`
- ✅ Solicitação permanece em `AGUARDANDO_APROVACAO`
- ✅ Serasa NÃO chamada

---

### Cenário E - Lockout de Senha de Transação

**Objetivo**: Validar lockout após 3 tentativas falhas em 5 minutos.

**Passos**:

1. **Configurar senha para usuário**
   ```bash
   curl -X POST http://uat-api:8080/configuracoes/senha-transacao \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{"senhaAtual": null, "novaSenha": "123abc"}'
   ```

2. **Tentar criar solicitação com senha errada (1ª tentativa)**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{
       "numVenda": 295,
       "parcelaIds": [1],
       "incluirFiadores": false,
       "senhaTransacao": "wrongpass"
     }'
   ```
   **Esperado**: 401 Unauthorized com `SENHA_INVALIDA`

3. **Repetir 2ª e 3ª tentativas com senha errada**
   - Mesmo comando da 2ª tentativa
   **Esperado**: 401 Unauthorized com `SENHA_INVALIDA`

4. **4ª tentativa (deve estar bloqueado)**
   - Mesmo comando
   **Esperado**: 401 Unauthorized com `SENHA_BLOQUEADA`

5. **Verificar lockout no banco**
   ```sql
   SELECT USERNAME, TENTATIVAS_FALHAS, BLOQUEADO_ATE 
   FROM dbo.USUARIO_SENHA_TRANSACAO 
   WHERE USERNAME = 'op1'
   ```
   **Esperado**: `TENTATIVAS_FALHAS = 3`, `BLOQUEADO_ATE` preenchido (data atual + 15 min)

6. **Tentar com senha correta (ainda deve estar bloqueado)**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{
       "numVenda": 295,
       "parcelaIds": [1],
       "incluirFiadores": false,
       "senhaTransacao": "123abc"
     }'
   ```
   **Esperado**: 401 Unauthorized com `SENHA_BLOQUEADA`

**Critérios de Aceite**:
- ✅ 3 primeiras tentativas retornam `SENHA_INVALIDA`
- ✅ 4ª tentativa retorna `SENHA_BLOQUEADA`
- ✅ Contador de tentativas incrementado corretamente
- ✅ Data de bloqueio definida (atual + 15 min)
- ✅ Senha correta também rejeitada durante lockout

---

### Cenário F - Concorrência

**Objetivo**: Validar que duas solicitações paralelas para mesma venda resultam em conflito.

**Passos**:

1. **Enviar duas solicitações simultaneamente**
   ```bash
   # Terminal 1
   curl -X POST http://uat-api:8080/negativacao/solicitacoes \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{
       "numVenda": 295,
       "parcelaIds": [1],
       "incluirFiadores": false,
       "senhaTransacao": "123abc"
     }' &

   # Terminal 2 (executar imediatamente após)
   curl -X POST http://uat-api:8080/negativacao/solicitacoes \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{
       "numVenda": 295,
       "parcelaIds": [1],
       "incluirFiadores": false,
       "senhaTransacao": "123abc"
     }' &
   ```

**Esperado**: Uma requisição retorna 201 Created, outra retorna 409 Conflict

2. **Verificar que apenas uma solicitação ativa existe**
   ```sql
   SELECT COUNT(*) FROM dbo.SERASA_PEFIN_SOLICITACOES 
   WHERE NUM_VENDA_FK = 295 
   AND STATUS IN ('AGUARDANDO_APROVACAO', 'APROVADA', 'PENDENTE_ENVIO')
   ```
   **Esperado**: COUNT = 1

**Critérios de Aceite**:
- ✅ Uma requisição 201 Created, outra 409 Conflict
- ✅ Apenas uma solicitação ativa no banco
- ✅ Índice único filtrado `UX_SERASA_PEFIN_SOLICITACOES_ATIVA` funcionando

---

### Cenário G - Falha Síncrona Serasa

**Objetivo**: Validar comportamento quando Serasa retorna erro síncrono.

**Pré-requisito**: Configurar Serasa para retornar erro (ou bloquear temporariamente o endpoint)

**Passos**:

1-6. Mesmos passos do Cenário A (criar e aprovar solicitação)

7. **Aprovar solicitação (Serasa deve falhar)**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes/{solicitacaoId}/decisao \
     -H "Content-Type: application/json" \
     -H "X-Username: aracy.mendoca" \
     -d '{
       "decisao": "APROVAR",
       "senhaTransacao": "xyz789"
     }'
   ```
   **Esperado**: 200 OK (decisão registrada, mas falha no envio)

8. **Verificar status**
   ```sql
   SELECT STATUS, ERROR_MESSAGE FROM dbo.SERASA_PEFIN_SOLICITACOES 
   WHERE ID = '{solicitacaoId}'
   ```
   **Esperado**: `STATUS = 'APROVADA_FALHA_ENVIO'` com `ERROR_MESSAGE` preenchido

9. **Verificar notificação de falha**
   ```sql
   SELECT * FROM dbo.INAD_NOTIFICACOES 
   WHERE TIPO = 'AprovacaoNegativacao'
   AND MENSAGEM LIKE '%falha%'
   ORDER BY DT_CRIACAO DESC
   ```
   **Esperado**: Notificação contendo mensagem de falha

**Critérios de Aceite**:
- ✅ Status `APROVADA_FALHA_ENVIO`
- ✅ Erro registrado em `ERROR_MESSAGE`
- ✅ Ocorrência de aprovação criada
- ✅ Notificação enviada ao solicitante e aprovador sobre a falha
- ✅ Possibilidade de reenvio manual (via novo endpoint ou correção)

---

### Cenário H - Webhook Reentrante

**Objetivo**: Validar idempotência de webhook (segunda chamada não duplica).

**Passos**:

1. **Enviar webhook de sucesso pela primeira vez**
   ```bash
   curl -X POST http://uat-api:8080/inadimplencia/serasa-pefin/webhooks/inclusao/sucesso \
     -H "Content-Type: application/json" \
     -d '{
       "uuid": "test-idempotent-123",
       "debtorDocument": "000.012.095-23",
       "creditorDocument": "62173620000180",
       "contract": "295/00",
       "debtValue": 1000.00,
       "debtDate": "2024-01-01",
       "cadusKey": "008080948A",
       "cadusSerie": "001",
       "debtType": "PEFIN",
       "creditorArea": "0001",
       "categoryId": "FI",
       "error": null
     }'
   ```
   **Esperado**: 200 OK

2. **Verificar webhook registrado**
   ```sql
   SELECT * FROM dbo.SERASA_PEFIN_WEBHOOKS 
   WHERE UUID = 'test-idempotent-123'
   ```
   **Esperado**: 1 registro com `PROCESSADO = 1`

3. **Enviar mesmo webhook novamente**
   - Mesmo comando
   **Esperado**: 200 OK

4. **Verificar que não duplicou**
   ```sql
   SELECT COUNT(*) FROM dbo.SERASA_PEFIN_WEBHOOKS 
   WHERE UUID = 'test-idempotent-123'
   ```
   **Esperado**: COUNT = 1 (não duplicou)

**Critérios de Aceite**:
- ✅ Primeira chamada processada com sucesso
- ✅ Segunda chamada retorna 200 OK (não 409)
- ✅ Apenas 1 registro na tabela de webhooks
- ✅ Solicitação associada atualizada apenas uma vez

---

### Cenário I - SSE em Tempo Real

**Objetivo**: Validar que notificações são entregues via SSE em ≤2 segundos.

**Passos**:

1. **Conectar ao stream SSE**
   ```bash
   curl -N http://uat-api:8080/notifications/stream?username=aracy.mendoca
   ```

2. **Em outro terminal, criar solicitação**
   ```bash
   curl -X POST http://uat-api:8080/negativacao/solicitacoes \
     -H "Content-Type: application/json" \
     -H "X-Username: op1" \
     -d '{
       "numVenda": 295,
       "parcelaIds": [1],
       "incluirFiadores": false,
       "senhaTransacao": "123abc"
     }'
   ```

3. **Verificar que evento chegou no stream**
   **Esperado**: Evento SSE recebido em ≤2 segundos após criação da solicitação

4. **Verificar formato do evento**
   ```
   data: {"tipo":"SolicitacaoNegativacao","numVenda":295,"mensagem":"Nova solicitação..."}
   
   ```
   **Esperado**: Formato SSE válido com `data:` prefixo

**Critérios de Aceite**:
- ✅ Conexão SSE estabelecida com sucesso
- ✅ Evento recebido em ≤2 segundos
- ✅ Formato do evento válido (JSON em `data:`)
- ✅ Heartbeat mantém conexão ativa

---

## Critérios de Aceite Globais

Para considerar a validação UAT bem-sucedida:

- ✅ Todos os 9 cenários acima passaram
- ✅ Logs não expõem dados sensíveis (CPF, senhas, tokens)
- ✅ Não há exceções não tratadas nos logs da aplicação
- ✅ Performance: endpoints respondem em <500ms (exceto chamada Serasa)
- ✅ SSE: latência ≤2 segundos para entrega de notificações
- ✅ Idempotência de webhooks funcionando corretamente
- ✅ Lockout de senha funcionando conforme configurado
- ✅ Índice único filtrado prevenindo duplicatas

## Evidências

Anexar ao final da validação:

1. **Logs da aplicação** (trechos relevantes de cada cenário)
2. **Prints** das respostas HTTP (curl output)
3. **Screenshots** do SSE stream recebendo eventos
4. **Prints** das consultas SQL mostrando estados esperados

## Assinatura

Validado por: _________________  Data: ___/___/_____

Observações:
_________________________________________________________________________
_________________________________________________________________________
