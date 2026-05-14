# API de Negativação - PEFIN

**Versão:** PEFIN – V8  
**Ano:** 2026  
**Produto:** ECS Collection  
**Fornecedor:** Serasa Experian  

---

## Sumário

1. [API de Negativação](#api-de-negativação)
2. [Acessos fora do Brasil](#acessos-fora-do-brasil)
3. [Autenticação](#autenticação)
4. [Bearer Token](#bearer-token)
5. [Requisições de Inclusão](#requisições-de-inclusão)
6. [Inclusão de Avalista](#inclusão-de-avalista)
7. [Tabela de Natureza da Dívida](#tabela-de-natureza-da-dívida)
8. [Massa de teste de homologação](#massa-de-teste-de-homologação)
9. [Requisições de Exclusão](#requisições-de-exclusão)
10. [Tabela de Motivo de Baixas](#tabela-de-motivo-de-baixas)
11. [Webhook Obrigatório](#webhook-obrigatório)
12. [Payload de Resposta - Inclusão](#payload-de-resposta---inclusão)
13. [Payload de Resposta - Exclusão](#payload-de-resposta---exclusão)
14. [Validação de Campos](#validação-de-campos)
15. [Glossário](#glossário)
16. [Observações Técnicas para Implementação](#observações-técnicas-para-implementação)

---

# API de Negativação

A API de Negativação é responsável por receber requisições por meio de endpoints REST e converter o payload de entrada em um comando compatível com o contrato esperado pelo mainframe da Serasa.

A API funciona de forma **assíncrona**.

Ao enviar uma requisição, o cliente recebe uma resposta com:

- `HTTP Status 200`;
- `transactionId`.

Esse retorno indica apenas que a requisição foi recebida e encaminhada para processamento pela Serasa. Ele **não representa sucesso ou erro da operação**.

Após a validação e processamento, o resultado final, seja sucesso ou erro, será informado por meio de um **webhook**, que enviará a resposta para uma API desenvolvida pelo cliente.

Em resumo:

- O `HTTP 200` inicial confirma somente o recebimento da requisição;
- O sucesso ou erro real da operação deve ser verificado pelo retorno do webhook;
- O cliente deve possuir uma API própria para receber os retornos da Serasa.

---

# Acessos fora do Brasil

## Liberação

A Serasa aceita apenas requisições originadas de IPs localizados no território brasileiro.

Qualquer requisição realizada fora do Brasil é bloqueada por padrão.

Caso a infraestrutura do parceiro esteja alocada fora do país, será necessário solicitar a liberação dos IPs.

As liberações ocorrem em janelas fixas e não flexíveis:

- Terças-feiras;
- Quintas-feiras;
- A partir das 22h.

O SLA é de até **10 dias corridos**.

Esse prazo é um requisito crítico para garantir qualidade e conformidade do serviço, portanto não é possível flexibilizá-lo.

Para solicitar a liberação, deve-se enviar um e-mail para:

```text
suporteapicollection@experian.com
```

Com as seguintes informações:

- CNPJ;
- Razão social;
- Lista de IPs de cada ambiente, teste e produção;
- Pessoa responsável pelo acompanhamento, opcional.

Poderá ser agendada uma reunião com o parceiro para que um representante realize o teste de conexão junto ao time de Redes da Serasa, validando o funcionamento correto.

Caso não seja possível a participação do colaborador do parceiro e ocorra alguma falha na liberação, a correção só será feita em uma nova janela, reiniciando o SLA de 10 dias corridos.

> Observação: não é possível liberar IPs dinâmicos.

---

# Autenticação

## Credenciais

A Serasa fornece um par de credenciais, enviadas por e-mail, que devem ser utilizadas para gerar o token de autenticação da API.

Caso o cliente ainda não possua essas credenciais, deve entrar em contato com o executivo de vendas ou com o responsável pelo contrato na Serasa para solicitá-las.

Serão emitidos dois pares de credenciais:

- Um para o ambiente de testes;
- Outro para o ambiente de produção, após a homologação ser concluída com sucesso.

> Atenção: utilize cada par de credenciais exclusivamente em seu respectivo ambiente.

Com o `clientID` e o `clientSecret`, deve-se gerar um token JWT contendo as informações de acesso necessárias para autenticação.

---

## Endpoints de Autenticação

### Homologação

```text
https://uat-api.serasaexperian.com.br/security/iam/v1/client-identities/login
```

### Produção

```text
https://api.serasaexperian.com.br/security/iam/v1/client-identities/login
```

---

## Basic Auth

Deve ser gerado, através das credenciais, um Basic OAuth e enviado como parâmetro `Authorization` no header da requisição.

### Headers

```http
Authorization: Basic {oAuth token}
content-type: application/json
```

---

## Response da Autenticação

```json
{
  "accessToken": "aaaa.bbbb.cccc-dddd-eeee-ffff-gggg",
  "tokenType": "Bearer",
  "expiresIn": "1742567784",
  "scope": [
    "READ",
    "WRITE"
  ]
}
```

---

# Bearer Token

O retorno do endpoint de autenticação será o `accessToken`, também chamado de Bearer Token.

Esse token JWT deverá ser passado em todas as requisições para a API.

Informações importantes:

- O token expira em 15 minutos;
- Pode ser estendido em até 1 hora;
- Ao decodificar o JWT, será possível visualizar o `logon` usado nas transações.

Exemplo de payload JWT decodificado:

```json
{
  "iat": 1699633956,
  "scope": [
    "READ",
    "WRITE"
  ],
  "logon": "HOM17687",
  "client_id": "650d8a82998efd6285af7b22",
  "app_id": "6399fb89cb453f530143cf8a",
  "service_id": "650d8a82e9fbef72fb3c6660",
  "business_unit_id": "63925481939ca5530df906eb",
  "organization_id": "6234bf03ab14624d4807d6a5",
  "authorities": [
    "ROLE_CLI-AUTH-BASIC",
    "ROLE_CLI-AUTH-IDENTIFIED",
    "ROLE_CLI-3RDPARTY"
  ],
  "exp": 1699637556,
  "sub": "650d8a82998efd6285af7b22"
}
```

---

# Requisições de Inclusão

## Inclusão de Dívida

A inclusão de dívida deve utilizar sempre o método `POST`.

### Endpoint de Homologação

```text
https://api.serasa.dev/collection/debt/
```

### Endpoint de Produção

```text
https://api.serasa.com.br/collection/debt/
```

---

## Headers

```http
Authorization: Bearer {token}
content-type: application/json
```

---

## Payload de Inclusão de Dívida

Para realizar requisições na API, é necessário preencher o payload individualmente para cada chamada, pois não são permitidas requisições em lote.

```json
{
  "value": "Double",
  "areaInformante": "String",
  "dueDate": "String: yyyy-MM-dd",
  "categoryId": "String",
  "debtor": {
    "documentNumber": "String",
    "name": "String",
    "address": {
      "zipCode": "String",
      "addressLine": "String",
      "complement": "String",
      "district": "String",
      "city": "String",
      "state": "String"
    }
  },
  "creditor": {
    "documentNumber": "String"
  },
  "contractNumber": "String",
  "debtType": "PEFIN",
  "bankSlip": {
    "bankName": "String",
    "bankCode": "String",
    "bankDigit": "String",
    "typeableLine": "String",
    "paymentSite": "String",
    "dueDate": "String",
    "beneficiary": "String",
    "creditorDocument": "String",
    "beneficiaryCode": "String",
    "documentDate": "String: yyyy-MM-dd",
    "documentNumber": "String",
    "documentType": "String",
    "accepted": "Boolean",
    "processingDate": "String: yyyy-MM-dd",
    "ourNumber": "String",
    "bankUse": "String",
    "wallet": "String",
    "currency": "String",
    "quantity": "String",
    "value": "String",
    "documentValue": "String",
    "discount": "String",
    "interest": "String",
    "fees": "String",
    "chargedValue": "String",
    "debtorName": "String",
    "debtorDocument": "String",
    "instructions": [
      "String"
    ]
  },
  "debtorDigitalContact": {
    "phoneAreaCode": "Integer",
    "phoneNumber": "Integer",
    "email": "String"
  }
}
```

---

## Observações sobre o Payload

Os campos obrigatórios devem ser enviados independentemente do cenário.

Os campos referentes a `bankSlip` são opcionais e devem ser enviados somente quando o parceiro tiver habilitado contratualmente a funcionalidade de envio de boleto no comunicado.

Caso os campos de boleto sejam enviados sem a devida habilitação contratual, eles serão ignorados.

Os campos referentes a `debtorDigitalContact` também são opcionais e devem ser utilizados apenas quando o parceiro possuir contratualmente a opção de esteira digital no envio de comunicados.

---

# Inclusão de Avalista

A inclusão de avalista é opcional.

Além de incluir uma dívida para o devedor principal, é possível também fazer a inclusão para o avalista, coobrigado ou fiador.

A inclusão de avalista deve utilizar sempre o método `POST`.

---

## Endpoint de Homologação

```text
https://api.serasa.dev/collection/debt/guarantor
```

## Endpoint de Produção

```text
https://api.serasa.com.br/collection/debt/guarantor
```

---

## Headers

```http
Authorization: Bearer {token}
content-type: application/json
```

---

## Payload de Inclusão de Avalista

Após a inclusão do principal, é necessário enviar o payload do avalista, referenciando sempre o mesmo `contractNumber` da dívida principal.

```json
{
  "categoryId": "String",
  "value": "Double",
  "dueDate": "String: yyyy-MM-dd",
  "debtorDocument": "String",
  "contractNumber": "String",
  "guarantor": {
    "documentNumber": "String",
    "name": "String",
    "address": {
      "zipCode": "String",
      "addressLine": "String",
      "complement": "String",
      "district": "String",
      "city": "String",
      "state": "String",
      "number": "String"
    }
  },
  "creditor": {
    "documentNumber": "String"
  },
  "debtType": "PEFIN"
}
```

---

## Response das Requisições de Inclusão

Para todas as requisições, o retorno será um HTTP Code `200` com um `transactionId`.

Esse retorno não significa que a dívida foi incluída. Ele significa apenas que a requisição será processada pela Serasa.

A validação de que a inclusão ocorreu com sucesso ou erro será recebida via webhook.

```json
{
  "transactionId": "f1d11b18-b459-4f11-97a8-8143a6c392e4"
}
```

---

# Tabela de Natureza da Dívida

| Código | Descrição |
|---|---|
| AD | ADIANT CONTA |
| AG | EMPRESTIMO |
| AL | ALUGUEL |
| AR | LEASING |
| C1 | CONS IMOVEIS |
| C2 | CONS VEI PES |
| C3 | CONS VEICULO |
| C4 | CONS MOTOS |
| C5 | CONS BENS |
| C6 | CONS AEREO |
| CA | OPER CAMBIO |
| CB | CDC BENS |
| CC | CONDOMINIO |
| CD | CREDIARIO |
| CL | CDC V LEV |
| CM | CDC MOTOS |
| CO | CONSORCIO CONTEMPLADO |
| CP | CRED PESSOAL |
| CR | IMPEDIDO BC |
| CT | CRED CARTAO |
| CV | CDC V PES |
| DA | DIVIDA ATIVA |
| DC | DIVIDAS CHEQ |
| DE | CHEQUE ELET |
| DP | DUPLICATA |
| EC | EMPRES CONTA |
| EE | ENERGIA ELET |
| EG | EMPR CONSIG |
| FG | FAT GAS |
| FI | FINANCIAMENTO |
| HO | HOSPITAIS |
| IE | INST ENSINO |
| IM | OPER IMOBILI |
| LL | LEAS VEICULO |
| LM | LEAS MOTOS |
| LV | LEAS VEI PES |
| ME | MENS ESCOLAR |
| NF | NOTA FISCAL |
| OA | OPER AGRIC |
| OJ | OPER AJUIZAD |
| OO | OUTRAS OPER |
| RE | REPASSES |
| RR | ARRECADADOR |
| SB | FAT AGUA |
| SF | SEGURO FIANCA LOCATICIA |
| SG | SEGURO GARANTIA |
| SQ | SEG QUEBRA |
| SR | SEGURO RISCO DECORRIDO |
| SS | SEGURO SAUDE |
| TC | CONFISS DIV |
| TD | TIT DESCONTA |
| TE | TELEF MO |
| TF | TELEF FX |
| TI | SERV DADOS |
| TM | TELEF MOVEL |
| TP | SERV TELEFON |
| TR | RENEG DIVIDA |
| TT | TELEF FIXA |
| VM | VENDA MERCAD |

---

# Massa de teste de homologação

No ambiente de testes, não é permitido utilizar CPFs ou CNPJs reais para efetuar uma negativação.

Para esse fim, a Serasa disponibiliza a seguinte lista de massas de teste:

| Documento | Nome |
|---|---|
| 000.012.095-23 | CLIENTE TESTE ABCB |
| 000.084.414-48 | BJRNRNSD OIOIE |
| 074.205.658-99 | TESTE CPF SEM POSITIVO |
| 042.367.984-84 | NCUH KLCOHKKHH ECAJAE NCGMLU |
| 43.557.445/0001-80 | ESFERA ARENA E NEGOCIOS SPE LTDA |
| 00.079.854/0001-05 | U F NXALWPULN ZK EWCQIXG |
| 168.816.700-52 | TST PEFIN |
| 115.724.678-86 | TST FLEX |

Qualquer tentativa de negativação utilizando números de documento diferentes dos listados acima resultará em erro.

---

# Requisições de Exclusão

As requisições de exclusão devem utilizar sempre o método `DELETE`.

Existem duas formas de realizar a exclusão:

- Exclusão por Chave CADUS;
- Exclusão por Contrato no Header.

---

## Exclusão por Chave CADUS

### Endpoint de Homologação

```text
https://api.serasa.dev/collection/debt/cadus/{cadus}
```

### Endpoint de Produção

```text
https://api.serasa.com.br/collection/debt/cadus/{cadus}
```

---

## Query Parameter

| Campo | Descrição |
|---|---|
| cadus | Chave CADUS |

---

## Headers

```http
creditor-document: {String}
debtor-document: {String}
reason: {Integer}
type: PEFIN
Authorization: Bearer {token}
```

---

## Exemplo cURL

```bash
curl --location --request DELETE 'https://api.serasa.dev/collection/debt/cadus/008080948A' \
--header 'creditor-document: 62173620000180' \
--header 'debtor-document: 12345678900' \
--header 'reason: 1' \
--header 'type: PEFIN' \
--header 'Authorization: Bearer {token}'
```

---

## Exclusão por Contrato no Header

### Endpoint de Homologação

```text
https://api.serasa.dev/collection/debt/contract
```

### Endpoint de Produção

```text
https://api.serasa.com.br/collection/debt/contract
```

---

## Headers

```http
creditor-document: {String}
debtor-document: {String}
contract-number: {String}
reason: {Integer}
type: PEFIN
Authorization: Bearer {token}
```

---

## Exemplo cURL

```bash
curl --location --request DELETE 'https://api.serasa.dev/collection/debt/contract' \
--header 'creditor-document: 62173620000180' \
--header 'debtor-document: 12345678900' \
--header 'contract-number: 123456789/00' \
--header 'reason: 1' \
--header 'type: PEFIN' \
--header 'Authorization: Bearer {token}'
```

---

## Observação sobre Baixa de Dívida com Avalistas

Ao realizar a baixa de uma dívida principal que possua avalistas, todas as dívidas vinculadas serão baixadas automaticamente.

Caso seja necessário efetuar a baixa apenas para um avalista específico, é preciso enviar os dados correspondentes a esse avalista.

---

## Response das Requisições de Exclusão

Para todas as requisições, o retorno será um HTTP Code `200` com um `transactionId`.

Esse retorno não significa que a baixa foi realizada. Ele significa apenas que a requisição será processada pela Serasa.

A validação de que a baixa ocorreu com sucesso ou erro será recebida via webhook.

```json
{
  "transactionId": "f1d11b18-b459-4f11-97a8-8143a6c392e4"
}
```

---

# Tabela de Motivo de Baixas

| Código | Descrição |
|---|---|
| 01 | PAGAMENTO DA DÍVIDA |
| 02 | RENEGOCIACAO DA DÍVIDA |
| 03 | POR SOLICITACAO DO CLIENTE |
| 04 | ORDEM JUDICIAL |
| 05 | CORRECAO DO ENDERECO |
| 06 | ATUALIZACAO DO VALOR - VALORIZACAO |
| 07 | ATUALIZACAO DO VALOR - PAGAMENTO PARCIAL |
| 08 | ATUALIZACAO DA DATA |
| 09 | CORRECAO DO NOME |
| 10 | CORRECAO DO NÚMERO DO CONTRATO |
| 11 | CORRECAO DE VARIOS VALORES (VALOR+DATA+ETC) |
| 12 | BAIXA POR PERDA DE CONTROLE DA BASE |
| 13 | MOTIVO NAO IDENTIFICADO |
| 14 | PONTUALIZACAO DA DÍVIDA |
| 15 | BAIXA POR CONCESSAO DE CRÉDITO |
| 16 | INCORPORACAO/MUDANCA DE TITULARIDADE |
| 17 | COMUNICADO DEVOLVIDO DO CORREIO |
| 18 | CORRECAO DE DADOS DO COOBRIGADO/AVALISTA |
| 19 | RENEGOCIACAO DA DIVIDA POR ACORDO |
| 20 | PAGAMENTO DA DIVIDA POR DEPOSITO BANCARIO |
| 21 | ANÁLISE DE DOCUMENTOS |
| 22 | CORRECAO DE DADOS PELA LOJA/FILIAL |
| 23 | PGTO DA DIVIDA POR EMISSAO DE NOTA PROMISSORIA |
| 24 | ANÁLISE DE DOCUMENTO PELO SEGURO |
| 25 | DEVOLUCAO OU TROCA DE BEM FINANCIADO |
| 40 | FRAUDE |
| 41 | CALAMIDADE PUBLICA |
| 42 | BAIXA COMPULSORIA |
| 43 | BAIXA POR NEGOCIACAO |
| 44 | FALECIMENTO |
| 45 | CONTESTACAO |

---

# Webhook Obrigatório

Em todas as requisições enviadas, o retorno inicial é um `transactionId`.

Como as chamadas são assíncronas, após o processamento da requisição, a Serasa dispara um webhook contendo o payload da resposta.

Para isso, é necessário que o parceiro desenvolva uma API capaz de receber chamadas HTTP para processar esse retorno.

---

## Formatos de Autenticação do Webhook

A API do parceiro pode implementar autenticação nos seguintes formatos:

### Basic

Nesse formato, o cliente envia usuário e senha codificados em Base64 no cabeçalho da requisição.

É simples de implementar, mas exige o uso de HTTPS para garantir segurança, já que as credenciais são estáticas e podem ser reutilizadas se interceptadas.

### OAuth

O OAuth utiliza tokens de acesso temporários no lugar de credenciais fixas.

Fornece um nível maior de segurança, pois o token pode expirar, ser renovado e possuir escopo limitado.

É indicado quando há necessidade de controle mais robusto sobre permissões de acesso.

### Basic com JWT

Nesse modelo, a autenticação mantém a simplicidade do Basic, mas o uso de JWT acrescenta segurança adicional.

O JWT contém informações assinadas digitalmente, permitindo validar a autenticidade e integridade do token enviado.

### Sem autenticação

Nesse caso, o endpoint do parceiro aceita chamadas sem qualquer tipo de verificação.

É um cenário menos seguro e geralmente recomendado apenas para ambientes controlados ou quando há outras camadas externas de proteção, como firewall ou VPN.

---

## Cadastro da API de Webhook junto à Serasa

O parceiro deve solicitar o cadastro da API junto à Serasa pelo e-mail:

```text
suporteapicollection@experian.com
```

Devem ser informadas as seguintes informações:

- CNPJ;
- Razão social;
- Evento e respectivo endpoint;
- Tipo de autenticação e credenciais;
- Se deseja realizar um teste assistido ou não.

O teste assistido permite validar junto à equipe técnica da Serasa se o webhook está funcionando corretamente.

Durante uma reunião, o parceiro poderá realizar testes no ambiente de homologação, garantindo que os retornos sejam recebidos no endpoint configurado para os eventos executados.

---

## Eventos possíveis para Webhook

É possível cadastrar até 6 endpoints para retorno via webhook, sendo 1 para cada evento:

- Inclusão de dívida com sucesso;
- Inclusão de dívida com erro;
- Inclusão de avalista com sucesso;
- Inclusão de avalista com erro;
- Exclusão de dívida com sucesso;
- Exclusão de dívida com erro.

---

## IPs da Serasa para Liberação

Caso seja necessário, deve-se realizar a liberação dos IPs da Serasa para permitir o recebimento dos retornos via webhook.

### Homologação

```text
34.193.175.234
52.20.7.153
35.175.32.186
```

### Produção

```text
52.1.15.220
34.231.117.105
```

---

# Payload de Resposta - Inclusão

A diferença entre os eventos de sucesso e erro está apenas na presença do objeto `error`.

Esse objeto é incluído somente quando ocorre um erro na operação.

Em casos de sucesso, ele é retornado como `null`.

O campo `uuid` corresponde ao `transactionId` gerado no momento do envio da requisição.

```json
{
  "uuid": "String",
  "debtorDocument": "String",
  "creditorDocument": "String",
  "requester": null,
  "contract": "String",
  "debtValue": "Double",
  "debtDate": "String: yyyy-MM-dd",
  "cadusKey": "String",
  "cadusSerie": "String",
  "debtType": "String",
  "creditorArea": "String",
  "categoryId": "String",
  "error": {
    "message": "String",
    "statusCode": "Integer"
  }
}
```

---

# Payload de Resposta - Exclusão

A diferença entre os eventos de sucesso e erro está apenas na presença do objeto `error`.

Esse objeto é incluído somente quando ocorre um erro na operação.

Em casos de sucesso, ele é retornado como `null`.

O campo `uuid` corresponde ao `transactionId` gerado no momento do envio da requisição.

```json
{
  "uuid": "String",
  "requester": null,
  "creditor": {
    "documentNumber": "String",
    "documentType": "String"
  },
  "debtor": {
    "documentNumber": "String",
    "documentType": "String"
  },
  "cadusKey": "String",
  "cadusSerie": "String",
  "contractNumber": "String",
  "creditorArea": "String",
  "categoryId": "String",
  "writeOff": {
    "reason": "String"
  },
  "error": {
    "statusCode": "String",
    "message": "String"
  }
}
```

---

# Validação de Campos

Ao realizar a criação da API, a Serasa solicita que sejam validados apenas os seguintes campos:

- `uuid`;
- `creditor` ou `creditorDocument`;
- `debtor` ou `debtorDocument`;
- `contractNumber`.

Os demais campos não devem ser validados de forma rígida, pois podem sofrer alterações futuras e devem ser tratados como opcionais ou dinâmicos.

---

# Glossário

| Campo | Qtd. máxima | Descrição |
|---|---:|---|
| areaInformante | 4 posições | Usado para separação entre áreas |
| value | 15 posições com 2 casas decimais | Valor no formato ##.##, mínimo 10,00 |
| dueDate | 10 posições | Data de vencimento no formato yyyy-MM-dd |
| categoryId | 2 posições | Natureza da Dívida |
| debtor.documentNumber | 15 posições | Documento do Devedor |
| debtor.name | 70 posições | Nome ou Razão Social do Devedor |
| debtor.address.zipCode | 8 posições | CEP |
| debtor.address.addressLine | 70 posições | Logradouro com número |
| debtor.address.complement | 20 posições | Complemento, não obrigatório |
| debtor.address.district | 20 posições | Bairro |
| debtor.address.city | 20 posições | Município |
| debtor.address.state | 2 posições | Estado |
| creditor.documentNumber | 14 posições | Documento do Credor |
| creditor.name | 70 posições | Razão Social |
| creditor.address.zipCode | 8 posições | CEP |
| creditor.address.addressLine | 70 posições | Logradouro com número |
| creditor.address.complement | 20 posições | Complemento, não obrigatório |
| creditor.address.district | 20 posições | Bairro |
| creditor.address.city | 20 posições | Município |
| creditor.address.state | 2 posições | Estado |
| phone.areaCode | 4 posições | DDD |
| phone.phoneNumber | 9 posições | Telefone do Devedor |
| participantDocument | 14 posições | Documento do Participante |
| contractNumber | 20 posições | Número do Contrato ou Título |
| debtType | PEFIN | Tipo de dívida |
| bankSlip.bankName | 15 posições | Nome do Banco |
| bankSlip.bankCode | 3 posições | Código do Banco |
| bankSlip.bankDigit | 1 posição | Dígito do código do Banco |
| bankSlip.typeableLine | 50 posições | Linha Digitável |
| bankSlip.paymentSite | 70 posições | Local de Pagamento |
| bankSlip.dueDate | 8 posições | Data de vencimento no formato yyyy-MM-dd |
| bankSlip.beneficiary | 40 posições | Nome do Cedente |
| bankSlip.creditorDocument | 15 posições | Documento do Cedente |
| bankSlip.beneficiaryCode | 25 posições | Código da Agência do Cedente |
| bankSlip.documentDate | 8 posições | Data do documento do Cedente |
| bankSlip.documentNumber | 25 posições | Número do Documento do Cedente |
| bankSlip.documentType | 1 posição | Tipo do Documento do Cedente |
| bankSlip.accepted | 3 posições | Aceite |
| bankSlip.processingDate | 8 posições | Data de Processamento |
| bankSlip.ourNumber | 25 posições | Nosso Número |
| bankSlip.bankUse | 20 posições | Uso do Banco |
| bankSlip.wallet | 5 posições | Carteira |
| bankSlip.currency | 3 posições | Espécie Moeda |
| bankSlip.quantity | 10 posições | Quantidade |
| bankSlip.value | 15 posições com 2 casas decimais | Valor no formato ##.## |
| bankSlip.documentValue | 15 posições com 2 casas decimais | Valor do Documento no formato ##.## |
| bankSlip.discount | 15 posições com 2 casas decimais | Desconto/Abatimento no formato ##.## |
| bankSlip.interest | 15 posições com 2 casas decimais | Outras deduções no formato ##.## |
| bankSlip.fees | 15 posições com 2 casas decimais | Outros acréscimos no formato ##.## |
| bankSlip.chargedValue | 15 posições com 2 casas decimais | Valor cobrado no formato ##.## |
| bankSlip.debtorName | 50 posições | Nome do Sacado |
| bankSlip.debtorDocument | 15 posições | Documento do Sacado |
| bankSlip.instructions | 8 blocos de 70 posições | Instruções |
| debtorDocument | 15 posições | Documento do Devedor |
| creditorDocument | 15 posições | Documento do Credor |

---

# Observações Técnicas para Implementação

## Responsabilidades do Backend

A integração com a API do Serasa deve ser implementada principalmente no backend, pois envolve:

- Armazenamento seguro de `clientID` e `clientSecret`;
- Geração e renovação de token;
- Montagem do payload de inclusão;
- Montagem dos headers de exclusão;
- Comunicação com endpoints de homologação e produção;
- Recebimento dos webhooks obrigatórios;
- Validação mínima dos retornos;
- Controle de status da negativação;
- Persistência do `transactionId`;
- Tratamento dos erros retornados pela Serasa.

---

## Responsabilidades do Frontend

O frontend deve atuar apenas como interface para o usuário:

- Solicitar negativação;
- Exibir dados do cliente/devedor;
- Permitir seleção da natureza da dívida;
- Permitir seleção do motivo de baixa;
- Exibir status da solicitação;
- Exibir mensagens de erro ou sucesso;
- Consultar histórico das negativações.

O frontend não deve chamar diretamente a API da Serasa, pois isso exporia credenciais e tokens sensíveis.

---

## Fluxo Recomendado de Inclusão

1. Usuário solicita a negativação no sistema.
2. Frontend envia a solicitação para o backend.
3. Backend valida os dados obrigatórios.
4. Backend gera ou reutiliza o Bearer Token válido.
5. Backend envia o payload para a API da Serasa.
6. Serasa retorna `HTTP 200` com `transactionId`.
7. Backend salva o `transactionId` com status `Aguardando retorno`.
8. Serasa processa a requisição de forma assíncrona.
9. Serasa chama o webhook do cliente.
10. Backend recebe o webhook.
11. Backend atualiza o status para sucesso ou erro.
12. Frontend exibe o status atualizado para o usuário.

---

## Fluxo Recomendado de Exclusão

1. Usuário solicita a baixa da negativação no sistema.
2. Frontend envia a solicitação para o backend.
3. Backend valida documento do credor, documento do devedor, contrato ou CADUS e motivo da baixa.
4. Backend gera ou reutiliza o Bearer Token válido.
5. Backend envia a requisição `DELETE` para a Serasa.
6. Serasa retorna `HTTP 200` com `transactionId`.
7. Backend salva o `transactionId` com status `Aguardando retorno de baixa`.
8. Serasa processa a solicitação de forma assíncrona.
9. Serasa chama o webhook de exclusão.
10. Backend atualiza o status da baixa.
11. Frontend exibe o resultado ao usuário.

---

## Status sugeridos para controle interno

| Status | Descrição |
|---|---|
| PENDENTE_ENVIO | Solicitação criada, mas ainda não enviada à Serasa |
| ENVIADO_SERASA | Requisição enviada e transactionId recebido |
| AGUARDANDO_RETORNO | Aguardando retorno via webhook |
| NEGATIVADO_SUCESSO | Inclusão processada com sucesso |
| NEGATIVADO_ERRO | Inclusão processada com erro |
| BAIXA_ENVIADA | Solicitação de baixa enviada |
| BAIXA_AGUARDANDO_RETORNO | Aguardando retorno da baixa via webhook |
| BAIXADO_SUCESSO | Baixa processada com sucesso |
| BAIXADO_ERRO | Baixa processada com erro |

---

## Recomendações de Segurança

- Nunca expor `clientID`, `clientSecret` ou Bearer Token no frontend;
- Utilizar HTTPS em todos os endpoints;
- Validar origem e autenticação dos webhooks;
- Registrar logs de envio e retorno;
- Armazenar `transactionId` para conciliação;
- Separar credenciais de homologação e produção;
- Nunca usar CPF ou CNPJ real em homologação;
- Liberar apenas IPs fixos quando necessário;
- Tratar os campos dinâmicos do webhook de forma flexível.

---

## Ambientes

| Ambiente | Autenticação | Inclusão | Exclusão |
|---|---|---|---|
| Homologação | `https://uat-api.serasaexperian.com.br/security/iam/v1/client-identities/login` | `https://api.serasa.dev/collection/debt/` | `https://api.serasa.dev/collection/debt/contract` |
| Produção | `https://api.serasaexperian.com.br/security/iam/v1/client-identities/login` | `https://api.serasa.com.br/collection/debt/` | `https://api.serasa.com.br/collection/debt/contract` |

---

## Conclusão

A API de Negativação PEFIN da Serasa exige uma integração backend bem estruturada, pois o fluxo é assíncrono e depende obrigatoriamente de webhooks para confirmação do resultado final.

O retorno inicial `HTTP 200` com `transactionId` deve ser tratado apenas como confirmação de recebimento da requisição, e não como confirmação de negativação ou baixa.

A implementação correta deve contemplar autenticação segura, envio individual das requisições, controle interno de status, armazenamento do `transactionId`, recebimento de webhooks e atualização posterior do resultado para consulta pelo usuário no sistema.
