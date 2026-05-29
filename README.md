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

## Fluxo de Negativação Serasa

O sistema inclui um fluxo de solicitação e aprovação para negativações Serasa, garantindo dupla custódia e rastreabilidade completa.

### Fluxo de Trabalho

1. **Solicitação**: Operador de cobrança solicita negativação para dívidas elegíveis (>60 dias de atraso)
2. **Aprovação**: Aprovadores autorizados revisam e aprovam/rejeitam a solicitação com senha de transação
3. **Envio Serasa**: Após aprovação, o sistema envia a negativação para a Serasa
4. **Retorno**: Webhook da Serasa notifica o resultado final (sucesso ou erro)

### Endpoints do Fluxo

- `GET /negativacao/vendas/{numVenda}/dividas` - Consultar dívidas elegíveis para negativação
- `POST /negativacao/solicitacoes` - Criar solicitação de negativação
- `GET /negativacao/solicitacoes?status=AGUARDANDO_APROVACAO` - Listar solicitações pendentes
- `POST /negativacao/solicitacoes/{id}/decisao` - Aprovar ou rejeitar solicitação
- `GET /configuracoes/senha-transacao` - Verificar se usuário tem senha de transação
- `POST /configuracoes/senha-transacao` - Definir/atualizar senha de transação

### Configuração

Configure em `appsettings.json`:

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

### Segurança

- **Dupla custódia**: Solicitante não pode aprovar sua própria solicitação
- **Senha de transação**: Senha adicional separada da senha de login (hash PBKDF2)
- **Lockout**: 3 tentativas falhas em 5 minutos bloqueiam o usuário por 15 minutos
- **Aprovadores**: Lista de aprovadores autorizados (configurável via appsettings)
- **Mascaramento**: CPF/CNPJ e dados sensíveis mascarados em logs e respostas

### Documentação

- [Roteiro de Validação UAT](documentos/uat-fluxo-negativacao.md) - Guia completo de testes manuais
- [PRD Fluxo Negativação Serasa](tasks/prd-fluxo-negativacao-serasa/prd.md) - Requisitos funcionais
- [Tech Spec Fluxo Negativação Serasa](tasks/prd-fluxo-negativacao-serasa/techspec.md) - Especificação técnica

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
