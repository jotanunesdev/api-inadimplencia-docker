import { check } from 'k6';

// Checks padrão para respostas de leitura (GET).
// Aceita 200 ou 404 (CPF não encontrado etc.) como respostas funcionais.
export function checkRead(res, name) {
  return check(res, {
    [`${name}: status ok`]: (r) => r.status === 200 || r.status === 404,
    [`${name}: tem corpo`]: (r) => r.body && r.body.length > 0,
    [`${name}: content-type json`]: (r) =>
      (r.headers['Content-Type'] || '').includes('application/json'),
  });
}

// Checks para health endpoints — devem ser sempre 200 e rápidos.
export function checkHealth(res, name) {
  return check(res, {
    [`${name}: status 200`]: (r) => r.status === 200,
    [`${name}: < 200ms`]: (r) => r.timings.duration < 200,
  });
}
