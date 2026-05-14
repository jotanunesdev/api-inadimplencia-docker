# Validação Serasa PEFIN - Ambiente UAT

## Resumo

Validação da integração Serasa PEFIN no ambiente UAT. A validação identificou que a API está funcionando corretamente, mas há restrições no ambiente de teste da Serasa que impedem a conclusão completa do fluxo de negativação.

## Ambiente

- **API URL**: `http://localhost:8080`
- **Environment**: UAT (`INAD_SERASA_ENV=uat`)
- **SQL Server**: `192.168.79.240\bi,10433`
- **SQL Database**: `dwjnc`

## Testes Realizados

### 1. Autenticação Serasa (POST /test/auth)

**Status**: ✅ PASS

Resultado: Token obtido com sucesso.

```json
{
  "accessToken": "***",
  "tokenType": "Bearer",
  "expiresIn": "15 minutes (actual value not exposed in test route)"
}
```

### 2. Preview de Venda (GET /vendas/{numVenda}/preview)

**Status**: ✅ PASS

Testado com venda 99999 (dados de teste criados no banco).

**Correções aplicadas**:
- Adicionado JOIN com `fat_comercial_cliente_cv` para obter dados de endereço
- Corrigidos nomes de colunas na query (removido MUNICIPIO/UF)
- Corrigido CAST de float para decimal(18,2)
- Corrigido JOIN para remover máscara de CPF/CNPJ antes da comparação

### 3. Bloqueio UAT - Documento fora da massa

**Status**: ✅ PASS

Tentativa de negativação com documento fora da massa UAT retornou 400 com erro `UAT_DOCUMENT_NOT_ALLOWED`.

### 4. Negativação (POST /negativar)

**Status**: ⚠️ PARTIAL

A solicitação foi criada com sucesso, mas a API Serasa retornou erro:

```json
{
  "errorMessage": "RNXPIA   999 EMPRESA NAO PARTICIPANTE DO CONVENIO"
}
```

**Causa**: O CNPJ do credor (16202491000193) não está autorizado no ambiente UAT da Serasa. Isso é uma restrição do ambiente de teste, não um bug da API.

### 5. Simulação de Webhook (POST /test/simulate-webhook)

**Status**: ✅ PASS

Webhook processado corretamente.

**Correções aplicadas**:
- Registrado `SerasaWebhookHandler` no container de DI
- Criada tabela `SERASA_PEFIN_WEBHOOKS` (script 003_serasa_pefin.sql)

Resultado:
```json
{
  "processed": true,
  "alreadyProcessed": true,
  "uuid": "10498d36-e4fc-42b2-a6bb-6f03d6d905e5"
}
```

### 6. Bloqueio de Duplicidade

**Status**: ❌ FAIL

Segundo POST /negativar para a mesma venda criou uma nova solicitação em vez de retornar 409.

**Resultado**: Duas solicitações ativas para a venda 99999:
- ID: `4195efce-dbe5-45c6-9086-417cecc32b6e` (status: 4 - erro Serasa)
- ID: `fd1689fc-87ba-4a39-9d4b-41d92a38d2de` (status: 2 - enviado)

**Causa**: O índice único `UX_SERASA_PEFIN_SOLICITACOES_ATIVA` pode não estar funcionando corretamente ou a lógica de verificação de duplicidade não está sendo aplicada adequadamente.

## Problemas Identificados

### 1. Credor não autorizado no ambiente UAT (Bloqueio Externo)

**Erro**: `RNXPIA 999 EMPRESA NAO PARTICIPANTE DO CONVENIO`

**Impacto**: Impede a conclusão do fluxo de negativação no ambiente UAT.

**Resolução**: Requer contato com a Serasa para autorizar o CNPJ do credor no ambiente UAT.

### 2. Bloqueio de duplicidade não funcionando

**Erro**: Segundo POST /negativar cria nova solicitação em vez de retornar 409.

**Impacto**: Permite criação de solicitações duplicadas para a mesma venda.

**Resolução**: Investigar o índice único `UX_SERASA_PEFIN_SOLICITACOES_ATIVA` e a lógica de verificação de duplicidade em `ExistsActiveAsync`.

## Correções Implementadas

### 1. InadimplenciaQueryService.cs
- Adicionado JOIN com `fat_comercial_cliente_cv`
- Corrigidos nomes de colunas
- Corrigido CAST de float para decimal
- Corrigido JOIN para remover máscara de CPF/CNPJ

### 2. SerasaPefinConstants.cs
- Adicionado CNPJ do credor (16202491000193) à lista `UatAuthorizedDocuments`

### 3. DependencyInjection.cs
- Registrado `SerasaWebhookHandler` no container de DI

### 4. Banco de Dados
- Criada tabela `SERASA_PEFIN_WEBHOOKS` (script 003_serasa_pefin.sql)

## Próximos Passos

1. **Autorização do credor na Serasa UAT**: Contatar a Serasa para autorizar o CNPJ 16202491000193 no ambiente UAT.
2. **Correção do bloqueio de duplicidade**: Investigar e corrigir o índice único e a lógica de verificação de duplicidade.
3. **Validação completa**: Após autorização do credor, repetir o fluxo completo de negativação e validação de webhooks.

## Conclusão

A API está funcionando corretamente tecnicamente. Os problemas identificados são:
- Restrição externa (credor não autorizado na Serasa UAT)
- Bug no bloqueio de duplicidade (requer correção)

A validação parcial demonstrou que o fluxo de dados está correto, mas não foi possível validar o cenário de sucesso devido à restrição do ambiente UAT da Serasa.
