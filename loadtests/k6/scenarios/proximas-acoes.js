import http from 'k6/http';
import { url, config, tagged } from '../lib/config.js';
import { checkRead } from '../lib/checks.js';

const testData = JSON.parse(open('../data/test-data.json'));

function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

export function proximasAcoes() {
  // Lista próximas ações
  const rList = http.get(url('/proximas-acoes/'), {
    headers: config.defaultHeaders,
    ...tagged('GET /proximas-acoes/'),
  });
  checkRead(rList, 'GET /proximas-acoes/');

  // Detalhe por num venda
  const numVenda = pick(testData.numVendas);
  const rOne = http.get(url(`/proximas-acoes/${numVenda}`), {
    headers: config.defaultHeaders,
    ...tagged('GET /proximas-acoes/{numVenda}'),
  });
  checkRead(rOne, 'GET /proximas-acoes/{numVenda}');

  // Solicitações de negativação (lista)
  const rSol = http.get(url('/negativacao/solicitacoes'), {
    headers: config.defaultHeaders,
    ...tagged('GET /negativacao/solicitacoes'),
  });
  checkRead(rSol, 'GET /negativacao/solicitacoes');

  // Dívidas elegíveis para uma venda
  const rDiv = http.get(url(`/negativacao/vendas/${numVenda}/dividas`), {
    headers: config.defaultHeaders,
    ...tagged('GET /negativacao/vendas/{numVenda}/dividas'),
  });
  checkRead(rDiv, 'GET /negativacao/vendas/{numVenda}/dividas');
}
