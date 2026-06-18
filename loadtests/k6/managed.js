import { group, sleep } from 'k6';
import { thresholds, config, resolveManagedProfile } from './lib/config.js';
import { publicEndpoints } from './scenarios/public-endpoints.js';
import { inadimplenciaRead } from './scenarios/inadimplencia-read.js';
import { proximasAcoes } from './scenarios/proximas-acoes.js';

const profile = resolveManagedProfile(__ENV.K6_PROFILE_KEY || 'baseline');

export const options = {
  scenarios: {
    [profile.key]: profile.scenario,
  },
  thresholds: thresholds[profile.thresholdKey],
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export default function () {
  group('public', () => publicEndpoints());

  if (config.hasAuth || __ENV.K6_FORCE_AUTHED === 'true') {
    group('inadimplencia-read', () => inadimplenciaRead());
    group('proximas-acoes', () => proximasAcoes());
  }

  if (profile.sleepMs > 0) {
    sleep(profile.sleepMs / 1000);
  }
}
