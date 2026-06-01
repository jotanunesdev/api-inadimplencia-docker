import { sleep, group } from 'k6';
import { thresholds, config } from './lib/config.js';
import { publicEndpoints } from './scenarios/public-endpoints.js';
import { inadimplenciaRead } from './scenarios/inadimplencia-read.js';
import { proximasAcoes } from './scenarios/proximas-acoes.js';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js';

// SPIKE TEST — simula picos abruptos (0→200 VUs em 30s).
// Avalia auto-scaling, recuperação e degradação graciosa.
export const options = {
  scenarios: {
    spike: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 10 },   // baseline
        { duration: '30s', target: 200 },  // SPIKE
        { duration: '1m', target: 200 },   // mantém pico
        { duration: '30s', target: 10 },   // recovery
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '15s',
    },
  },
  thresholds: thresholds.spike,
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export default function () {
  group('public', () => publicEndpoints());

  if (config.hasAuth || __ENV.K6_FORCE_AUTHED === 'true') {
    group('inadimplencia-read', () => inadimplenciaRead());
    group('proximas-acoes', () => proximasAcoes());
  }
  // sem think time — agressivo de propósito
}

export function handleSummary(data) {
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    [`loadtests/k6/results/spike-${ts}.json`]: JSON.stringify(data, null, 2),
    [`loadtests/k6/results/spike-${ts}.html`]: htmlReport(data),
  };
}
