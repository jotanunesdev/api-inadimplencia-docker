import { sleep, group } from 'k6';
import { thresholds, config } from './lib/config.js';
import { publicEndpoints } from './scenarios/public-endpoints.js';
import { inadimplenciaRead } from './scenarios/inadimplencia-read.js';
import { proximasAcoes } from './scenarios/proximas-acoes.js';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

// SMOKE TEST — valida que a API responde sem erros sob carga mínima.
// Use antes de qualquer teste maior. Roda em ~1 minuto.
export const options = {
  vus: Number(__ENV.K6_VUS || 1),
  duration: __ENV.K6_DURATION || '1m',
  thresholds: thresholds.smoke,
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export default function () {
  group('public', () => publicEndpoints());

  if (config.hasAuth || __ENV.K6_FORCE_AUTHED === 'true') {
    group('inadimplencia-read', () => inadimplenciaRead());
    group('proximas-acoes', () => proximasAcoes());
  }

  sleep(1);
}

export function handleSummary(data) {
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    [`loadtests/k6/results/smoke-${ts}.json`]: JSON.stringify(data, null, 2),
    [`loadtests/k6/results/smoke-${ts}.html`]: htmlReport(data),
  };
}
