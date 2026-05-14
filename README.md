# API Inadimplência

API para gestão de inadimplência e integração com Serasa PEFIN.

## Arquitetura

- **.NET 8.0**
- **Clean Architecture** com separação em Domain, Application, Infrastructure e API
- **SQL Server** para persistência
- **RabbitMQ** com MassTransit para mensageria (outbox pattern)
- **Docker** para containerização

## Estrutura do Projeto

```
.
├── ApiInadimplencia.Domain/          # Entidades de domínio e value objects
├── ApiInadimplencia.Application/     # Lógica de aplicação (CQRS, handlers)
├── ApiInadimplencia.Infrastructure/ # Integrações (SQL, Serasa, RabbitMQ)
├── api-inadimplencia.Api/            # Endpoints REST
├── db/                              # Scripts SQL
├── documentos/                      # Documentação técnica
└── docker-compose.yml               # Orquestração de containers
```

## Setup

### Pré-requisitos

- Docker e Docker Compose
- .NET 8.0 SDK (para desenvolvimento local)

### Executar com Docker

```bash
docker-compose up -d
```

A API estará disponível em `http://localhost:8080`

### Scripts SQL

Execute os scripts em ordem:

1. `db/001_schema_inadimplencia.sql` - Schema principal
2. `db/002_masstransit_outbox.sql` - Tabelas MassTransit
3. `db/003_serasa_pefin.sql` - Tabelas Serasa PEFIN

### Variáveis de Ambiente

Configure no arquivo `.env`:

```bash
# SQL Server
INAD_SQL_SERVER=192.168.79.240\bi,10433
INAD_SQL_DATABASE=dwjnc
INAD_SQL_USER=fluig
INAD_SQL_PASSWORD=fluig@2019

# Serasa PEFIN
INAD_SERASA_ENV=uat|prod
INAD_SERASA_BASE_URL=https://api.serasa.com.br/pefin/v1
INAD_SERASA_CLIENT_ID=<client_id>
INAD_SERASA_CLIENT_SECRET=<client_secret>

# RabbitMQ
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
```

## Serasa PEFIN

A API integra com o serviço Serasa PEFIN para negativação de devedores.

### Endpoints Principais

- `POST /serasa-pefin/negativar` - Solicitar negativação
- `GET /serasa-pefin/vendas/{numVenda}/preview` - Preview de dados antes da negativação
- `GET /serasa-pefin/vendas/{numVenda}/negativacoes` - Histórico de solicitações
- `POST /serasa-pefin/webhook` - Webhook para receber notificações da Serasa

### Documentação

- [Runbook Serasa PEFIN](documentos/serasa-pefin-runbook.md) - Guia de operação
- [Validação UAT](documentos/serasa-pefin-validacao-uat.md) - Resultados de testes
- [Documentação Serasa v8](documentos/documentacao-serasa-pefin-v8.md) - Especificação da API Serasa

### Configuração UAT

No ambiente UAT, documentos autorizados devem ser configurados em `ApiInadimplencia.Domain/SerasaPefin/SerasaPefinConstants.cs`:

```csharp
public static readonly HashSet<string> UatAuthorizedDocuments = new()
{
    // Adicionar CPFs/CNPJs autorizados para testes UAT
};
```

## Desenvolvimento

### Build

```bash
dotnet build
```

### Testes

```bash
dotnet test
```

### Executar localmente

```bash
cd api-inadimplencia.Api
dotnet run
```

## Troubleshooting

### Timeout na conexão SQL

Verifique se o SQL Server está acessível e as credenciais no `.env` estão corretas.

### Erro de DI

Certifique-se de que todos os serviços estão registrados em `ApiInadimplencia.Infrastructure/DependencyInjection.cs`.

### Webhook Serasa não processando

Verifique se a tabela `SERASA_PEFIN_WEBHOOKS` existe (script 003_serasa_pefin.sql) e se o `SerasaWebhookHandler` está registrado no DI.

## Licença

Propriedade privada.
