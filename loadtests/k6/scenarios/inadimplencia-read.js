import http from 'k6/http';
import { url, config, tagged } from '../lib/config.js';
import { checkRead } from '../lib/checks.js';

// Carrega massa de dados uma vez por VU (k6 init context).
const testData = JSON.parse(open('../data/test-data.json'));

function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

// GETs autenticados de inadimplência. Requer K6_BEARER_TOKEN ou
// INAD_REQUIRE_AUTHENTICATED=false na API.
export function inadimplenciaRead() {
  // Lista geral
  const rList = http.get(url('/inadimplencia/'), {
    headers: config.defaultHeaders,
    ...tagged('GET /inadimplencia/'),
  });
  checkRead(rList, 'GET /inadimplencia/');

  // Por CPF
  const cpf = pick(testData.cpfs);
  const rCpf = http.get(url(`/inadimplencia/cpf/${cpf}`), {
    headers: config.defaultHeaders,
    ...tagged('GET /inadimplencia/cpf/{cpf}'),
  });
  checkRead(rCpf, 'GET /inadimplencia/cpf/{cpf}');

  // Por num-venda
  const numVenda = pick(testData.numVendas);
  const rVenda = http.get(url(`/inadimplencia/num-venda/${numVenda}`), {
    headers: config.defaultHeaders,
    ...tagged('GET /inadimplencia/num-venda/{numVenda}'),
  });
  checkRead(rVenda, 'GET /inadimplencia/num-venda/{numVenda}');

  // Por responsável
  const responsavel = encodeURIComponent(pick(testData.responsaveis));
  const rResp = http.get(url(`/inadimplencia/responsavel/${responsavel}`), {
    headers: config.defaultHeaders,
    ...tagged('GET /inadimplencia/responsavel/{nome}'),
  });
  checkRead(rResp, 'GET /inadimplencia/responsavel/{nome}');

  // Por cliente
  const cliente = encodeURIComponent(pick(testData.clientes));
  const rCli = http.get(url(`/inadimplencia/cliente/${cliente}`), {
    headers: config.defaultHeaders,
    ...tagged('GET /inadimplencia/cliente/{nomeCliente}'),
  });
  checkRead(rCli, 'GET /inadimplencia/cliente/{nomeCliente}');
}
