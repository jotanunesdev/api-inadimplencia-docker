import { sleep, group } from 'k6';
import { thresholds, config } from './lib/config.js';
import { publicEndpoints } from './scenarios/public-endpoints.js';
import { inadimplenciaRead } from './scenarios/inadimplencia-read.js';
import { proximasAcoes } from './scenarios/proximas-acoes.js';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

// LOAD TEST — simula carga normal de produção (~20 VUs, 5 min).
// Mede performance sustentada sob uso típico.
export const options = {
  scenarios: {
    normal_load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: Number(__ENV.K6_VUS || 10) },
        { duration: '3m', target: Number(__ENV.K6_VUS || 20) },
        { duration: '1m', target: Number(__ENV.K6_VUS || 20) },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: thresholds.load,
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export default function () {
  group('public', () => publicEndpoints());

  if (config.hasAuth || __ENV.K6_FORCE_AUTHED === 'true') {
    group('inadimplencia-read', () => inadimplenciaRead());
    group('proximas-acoes', () => proximasAcoes());
  }

  sleep(Math.random() * 2 + 1); // think time 1-3s
}

export function handleSummary(data) {
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    [`loadtests/k6/results/load-${ts}.json`]: JSON.stringify(data, null, 2),
    [`loadtests/k6/results/load-${ts}.html`]: htmlReport(data),
  };
}
