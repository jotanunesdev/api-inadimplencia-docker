import { check } from 'k6';
import http from 'k6/http';
import { Rate } from 'k6/metrics';
import { config, url } from '../lib/config.js';

const testData = JSON.parse(open('../data/test-data.json'));
const endpointErrors = new Rate('endpoint_errors');
const batchSize = Number(__ENV.K6_ENDPOINT_BATCH_SIZE || 8);
const supportedMethods = ['get', 'post', 'put', 'patch', 'delete'];

export function discoverApiOperations() {
  const response = http.get(url('/swagger/v1/swagger.json'), {
    headers: config.defaultHeaders,
    tags: {
      name: 'GET /swagger/v1/swagger.json',
      endpoint: '/swagger/v1/swagger.json',
      method: 'GET',
      execution_mode: 'discovery',
    },
  });

  if (response.status !== 200) {
    throw new Error(`Swagger discovery failed with HTTP ${response.status}`);
  }

  const document = response.json();
  const operations = [];

  for (const [path, pathItem] of Object.entries(document.paths || {})) {
    for (const method of supportedMethods) {
      if (!pathItem[method]) continue;

      const operation = pathItem[method];
      const parameters = [...(pathItem.parameters || []), ...(operation.parameters || [])];
      const executionMode = shouldDryRun(method, path) ? 'dry-run' : 'real';
      operations.push({
        method: method.toUpperCase(),
        path,
        requestPath: buildRequestPath(path, parameters),
        executionMode,
      });
    }
  }

  operations.sort((left, right) =>
    `${left.path}:${left.method}`.localeCompare(`${right.path}:${right.method}`),
  );
  return operations;
}

export function executeApiBatch(operations) {
  if (!operations || operations.length === 0) return;

  const startIndex = ((__ITER * batchSize) + ((__VU - 1) * batchSize)) % operations.length;
  for (let offset = 0; offset < Math.min(batchSize, operations.length); offset += 1) {
    executeOperation(operations[(startIndex + offset) % operations.length]);
  }
}

function executeOperation(operation) {
  const name = `${operation.method} ${operation.path}`;
  const tags = {
    name,
    endpoint: operation.path,
    method: operation.method,
    execution_mode: operation.executionMode,
  };
  const headers = {
    ...config.defaultHeaders,
    ...(operation.executionMode === 'dry-run' ? { 'X-Load-Test-Dry-Run': 'true' } : {}),
  };
  const body = operation.method === 'GET' ? null : JSON.stringify({});
  const response = http.request(operation.method, url(operation.requestPath), body, {
    headers,
    tags,
    responseCallback: http.expectedStatuses({ min: 200, max: 499 }),
    redirects: 0,
    timeout: '30s',
  });

  const failed =
    response.status === 0 ||
    response.status >= 500 ||
    response.status === 401 ||
    response.status === 403;
  endpointErrors.add(failed, tags);

  check(response, {
    [`${name}: route reached`]: (result) =>
      result.status > 0 &&
      result.status < 500 &&
      result.status !== 401 &&
      result.status !== 403,
  });
}

function shouldDryRun(method, path) {
  if (method !== 'get') return true;

  const normalized = path.toLowerCase();
  return (
    normalized.includes('/notifications/stream') ||
    normalized.includes('/session/') ||
    normalized.endsWith('/session') ||
    normalized.includes('/serasa-pefin/test/')
  );
}

function buildRequestPath(path, parameters) {
  let resolvedPath = path.replace(/\{([^}]+)\}/g, (_, parameterName) =>
    encodeURIComponent(valueFor(parameterName)),
  );
  const query = new URLSearchParams();

  for (const parameter of parameters) {
    if (parameter.in !== 'query') continue;
    if (!parameter.required && !isUsefulOptionalQuery(parameter.name)) continue;
    query.set(parameter.name, valueFor(parameter.name));
  }

  const queryString = query.toString();
  if (queryString) resolvedPath += `?${queryString}`;
  return resolvedPath;
}

function isUsefulOptionalQuery(name) {
  return ['page', 'pageSize', 'limit', 'periodDays'].includes(name);
}

function valueFor(name) {
  const normalized = String(name).toLowerCase();
  if (normalized.includes('numvenda')) return String(testData.numVendas?.[0] || 99999);
  if (normalized === 'cpf') return String(testData.cpfs?.[0] || '00001209523');
  if (normalized.includes('cliente')) return String(testData.clientes?.[0] || 'CLIENTE TESTE');
  if (normalized.includes('responsavel') || normalized === 'nome' || normalized.includes('usuario')) {
    return String(testData.responsaveis?.[0] || 'marcela freire');
  }
  if (normalized.includes('idlan')) return String(testData.idLans?.[0] || 12345);
  if (normalized.includes('protocolo')) return String(testData.protocolos?.[0] || '0000000000000');
  if (normalized.includes('transaction')) return 'managed-load-test';
  if (normalized === 'metric') return 'proximas-acoes-por-dia';
  if (normalized === 'status') return 'AGUARDANDO_APROVACAO';
  if (normalized === 'page') return '1';
  if (normalized === 'pagesize') return '50';
  if (normalized === 'limit') return '25';
  if (normalized === 'perioddays') return '7';
  if (normalized.includes('data')) return new Date().toISOString().slice(0, 10);
  if (normalized.includes('lida')) return 'false';
  if (normalized.includes('id') || normalized.includes('run')) {
    return '00000000-0000-0000-0000-000000000000';
  }
  return 'load-test';
}
