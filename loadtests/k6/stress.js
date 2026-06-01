import { sleep, group } from 'k6';
import { thresholds, config } from './lib/config.js';
import { publicEndpoints } from './scenarios/public-endpoints.js';
import { inadimplenciaRead } from './scenarios/inadimplencia-read.js';
import { proximasAcoes } from './scenarios/proximas-acoes.js';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

// STRESS TEST — empurra o sistema até o ponto de quebra (~100 VUs, 10 min).
// Identifica gargalos: DB connections, thread pool, memória.
export const options = {
  scenarios: {
    stress: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 25 },   // warm-up
        { duration: '2m', target: 50 },
        { duration: '2m', target: 75 },
        { duration: '2m', target: 100 },
        { duration: '2m', target: 100 },  // sustenta pico
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: thresholds.stress,
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export default function () {
  group('public', () => publicEndpoints());

  if (config.hasAuth || __ENV.K6_FORCE_AUTHED === 'true') {
    group('inadimplencia-read', () => inadimplenciaRead());
    group('proximas-acoes', () => proximasAcoes());
  }

  sleep(Math.random() + 0.5); // think time 0.5-1.5s
}

export function handleSummary(data) {
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    [`loadtests/k6/results/stress-${ts}.json`]: JSON.stringify(data, null, 2),
    [`loadtests/k6/results/stress-${ts}.html`]: htmlReport(data),
  };
}
