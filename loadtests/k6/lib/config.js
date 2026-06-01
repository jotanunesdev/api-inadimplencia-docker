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
