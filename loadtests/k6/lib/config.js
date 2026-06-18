// Configuração compartilhada entre scripts k6.
// Lê variáveis de ambiente via __ENV.

const BASE_URL = (__ENV.K6_BASE_URL || 'http://localhost:8080').replace(/\/$/, '');
const BEARER_TOKEN = __ENV.K6_BEARER_TOKEN || '';

export const config = {
  baseUrl: BASE_URL,
  hasAuth: BEARER_TOKEN.length > 0,
  defaultHeaders: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
    ...(BEARER_TOKEN ? { 'Authorization': `Bearer ${BEARER_TOKEN}` } : {}),
  },
};

export function url(path) {
  if (!path.startsWith('/')) path = '/' + path;
  return config.baseUrl + path;
}

// Thresholds compartilhados — ajuste conforme SLO.
export const thresholds = {
  smoke: {
    'http_req_failed': ['rate<0.01'],
    'http_req_duration': ['p(95)<500', 'p(99)<1000'],
    'checks': ['rate>0.99'],
  },
  load: {
    'http_req_failed': ['rate<0.01'],
    'http_req_duration': ['p(95)<800', 'p(99)<1500'],
    'checks': ['rate>0.98'],
  },
  stress: {
    'http_req_failed': ['rate<0.05'],
    'http_req_duration': ['p(95)<2000', 'p(99)<4000'],
    'checks': ['rate>0.95'],
  },
  spike: {
    'http_req_failed': ['rate<0.10'],
    'http_req_duration': ['p(95)<3000'],
  },
};

// Tags HTTP padrão p/ facilitar filtros no Grafana.
export function tagged(name, extra = {}) {
  return { tags: { name, ...extra } };
}

const managedProfiles = {
  baseline: {
    key: 'baseline',
    thresholdKey: 'smoke',
    sleepMs: 900,
    scenario: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '15s', target: 4 },
        { duration: '45s', target: 8 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '15s',
    },
  },
  intenso: {
    key: 'intenso',
    thresholdKey: 'load',
    sleepMs: 500,
    scenario: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 12 },
        { duration: '2m', target: 24 },
        { duration: '2m', target: 32 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '20s',
    },
  },
  'estresse-maximo': {
    key: 'estresse-maximo',
    thresholdKey: 'spike',
    sleepMs: 0,
    scenario: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 20 },
        { duration: '40s', target: 120 },
        { duration: '90s', target: 180 },
        { duration: '60s', target: 180 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '20s',
    },
  },
};

export function resolveManagedProfile(key) {
  const profile = managedProfiles[key];
  if (!profile) {
    throw new Error(`Unknown K6_PROFILE_KEY: ${key}`);
  }

  return profile;
}
