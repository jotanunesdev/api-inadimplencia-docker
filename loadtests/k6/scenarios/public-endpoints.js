import http from 'k6/http';
import { url, config, tagged } from '../lib/config.js';
import { checkHealth } from '../lib/checks.js';

// Endpoints públicos (sem auth). Servem como baseline de latência da pipeline HTTP.
// Nota: /health (root) depende de dependências externas (DB/Fluig/RM) e pode retornar 503
// em ambientes isolados. Usamos /inadimplencia/health que é puro (apenas Results.Ok).
export function publicEndpoints() {
  const r2 = http.get(url('/inadimplencia/health'), { headers: config.defaultHeaders, ...tagged('GET /inadimplencia/health') });
  checkHealth(r2, 'GET /inadimplencia/health');

  const r3 = http.get(url('/inadimplencia/contracts'), { headers: config.defaultHeaders, ...tagged('GET /inadimplencia/contracts') });
  checkHealth(r3, 'GET /inadimplencia/contracts');
}
