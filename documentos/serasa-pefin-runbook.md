# Runbook - Integração Serasa PEFIN

## Overview

Este runbook descreve o processo de operação e manutenção da integração com a API Serasa PEFIN para negativação de devedores.

## Arquitetura

### Componentes

- **API**: `api-inadimplencia.Api` - Endpoints REST para operações Serasa PEFIN
- **Application**: `ApiInadimplencia.Application` - Lógica de negócio e handlers
- **Infrastructure**: `ApiInadimplencia.Infrastructure` - Integrações e persistência
- **Domain**: `ApiInadimplencia.Domain` - Entidades e constantes de domínio

### Tabelas do Banco de Dados

- `dbo.SERASA_PEFIN_SOLICITACOES` - Solicitações de negativação
- `dbo.SERASA_PEFIN_WEBHOOKS` - Webhooks recebidos da Serasa

## Configuração

### Variáveis de Ambiente

```bash
# Ambiente Serasa
INAD_SERASA_ENV=uat|prod
INAD_SERASA_BASE_URL=https://api.serasa.com.br/pefin/v1
INAD_SERASA_CLIENT_ID=<client_id>
INAD_SERASA_CLIENT_SECRET=<client_secret>

# Banco de Dados
INAD_SQL_SERVER=192.168.79.240\bi,10433
INAD_SQL_DATABASE=dwjnc
INAD_SQL_USER=fluig
INAD_SQL_PASSWORD=fluig@2019

# RabbitMQ (para MassTransit outbox)
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
```

### Configuração UAT

No ambiente UAT, adicione documentos autorizados em `SerasaPefinConstants.cs`:

```csharp
public static readonly HashSet<string> UatAuthorizedDocuments = new()
{
    // CPFs
    "00001209523",
    "00008441448",
    // ... outros CPFs autorizados

    // CNPJs
    "43557445000180",
    "00079854000105",
    "16202491000193", // CNPJ do credor
};
```

## Operações

### 1. Setup Inicial

#### Executar Scripts SQL

```bash
# Script 001: Schema principal
sqlcmd -S <server> -d <database> -U <user> -P <password> -i db/001_schema_inadimplencia.sql

# Script 002: MassTransit outbox
sqlcmd -S <server> -d <database> -U <user> -P <password> -i db/002_masstransit_outbox.sql

# Script 003: Tabelas Serasa PEFIN
sqlcmd -S <server> -d <database> -U <user> -P <password> -i db/003_serasa_pefin.sql
```

#### Iniciar a API

```bash
docker-compose up -d
```

### 2. Autenticação Serasa

A API gerencia automaticamente o token de acesso com cache de 15 minutos. Para forçar renovação:

```bash
curl -X POST http://localhost:8080/serasa-pefin/test/auth
```

### 3. Negativação de Devedor

#### Endpoint: POST /serasa-pefin/negativar

**Request**:
```json
{
  "numVenda": 12345
}
```

**Response (Sucesso)**:
```json
{
  "data": {
    "solicitacoes": [
      {
        "solicitacaoId": "uuid",
        "tipoRegistro": 0,
        "transactionId": "serasa-transaction-id",
        "status": 2,
        "errorMessage": null
      }
    ],
    "statusAgregado": 2
  }
}
```

**Status Codes**:
- 0: Pendente de envio
- 1: Enviado para Serasa
- 2: Aguardando retorno
- 3: Negativado com sucesso
- 4: Erro na negativação

### 4. Consulta de Preview

#### Endpoint: GET /serasa-pefin/vendas/{numVenda}/preview

Retorna dados da venda para preview antes da negativação.

**Response**:
```json
{
  "numVenda": 12345,
  "documentoDevedor": "12345678901",
  "valor": 1000.00,
  "dataVencimento": "2024-12-31",
  "endereco": {
    "cep": "12345678",
    "cidade": "São Paulo",
    "estado": "SP"
  },
  "fiadores": [...]
}
```

### 5. Consulta de Histórico

#### Endpoint: GET /serasa-pefin/vendas/{numVenda}/negativacoes

Retorna histórico de solicitações de negativação para uma venda.

### 6. Webhooks

A API Serasa envia webhooks para notificar o resultado da negativação. O endpoint `/serasa-pefin/webhook` recebe e processa esses eventos.

#### Tipos de Webhook

- `inclusao/sucesso` - Negativação realizada com sucesso
- `inclusao/erro` - Erro na negativação
- `avalista/sucesso` - Avalista negativado com sucesso
- `avalista/erro` - Erro na negativação do avalista
- `baixa/sucesso` - Baixa realizada com sucesso
- `baixa/erro` - Erro na baixa

#### Simulação de Webhook (UAT apenas)

```bash
curl -X POST http://localhost:8080/serasa-pefin/test/simulate-webhook \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "inclusao",
    "resultado": "sucesso",
    "payload": "{\"uuid\":\"transaction-id\",\"cadusKey\":\"key\",\"cadusSerie\":\"serie\"}"
  }'
```

## Troubleshooting

### Erro: UAT_DOCUMENT_NOT_ALLOWED

**Causa**: Documento não está na lista de documentos autorizados para UAT.

**Resolução**: Adicionar o documento à lista `UatAuthorizedDocuments` em `SerasaPefinConstants.cs` e rebuildar.

### Erro: RNXPIA 999 EMPRESA NAO PARTICIPANTE DO CONVENIO

**Causa**: CNPJ do credor não está autorizado na Serasa.

**Resolução**: Contatar a Serasa para autorizar o CNPJ no ambiente correspondente.

### Erro: SERASA_PEFIN_MISSING_REQUIRED_FIELDS

**Causa**: Campos obrigatórios faltando no payload (ex: endereço do devedor).

**Resolução**: Verificar se os dados da venda estão completos no DW, especialmente endereço.

### Timeout na Conexão SQL

**Causa**: SQL Server não acessível.

**Resolução**: 
1. Verificar se o SQL Server está rodando
2. Verificar conectividade de rede
3. Verificar credenciais no `.env`

### Bloqueio de Duplicidade Não Funcionando

**Causa**: Índice único `UX_SERASA_PEFIN_SOLICITACOES_ATIVA` pode não estar configurado corretamente.

**Resolução**: Verificar se o índice existe no banco de dados:
```sql
SELECT * FROM sys.indexes WHERE name = 'UX_SERASA_PEFIN_SOLICITACOES_ATIVA'
```

## Monitoramento

### Logs

Ver logs do container:
```bash
docker logs api-inadimplencia -f
```

### Health Checks

```bash
curl http://localhost:8080/health
```

### Métricas

A API expõe métricas através do endpoint `/metrics` (se configurado).

## Manutenção

### Atualização de Dependências

```bash
# Atualizar pacotes NuGet
dotnet add package <package-name>

# Rebuildar container
docker-compose up -d --build
```

### Backup do Banco de Dados

```bash
sqlcmd -S <server> -d <database> -U <user> -P <password> -Q "BACKUP DATABASE [dwjnc] TO DISK = N'/backup/dwjnc.bak'"
```

## Referências

- Documentação Serasa PEFIN: `documentos/documentacao-serasa-pefin-v8.md`
- Validação UAT: `documentos/serasa-pefin-validacao-uat.md`
- Scripts SQL: `db/`
