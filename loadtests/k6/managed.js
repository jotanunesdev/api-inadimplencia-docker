import { group, sleep } from 'k6';
import { thresholds, resolveManagedProfile } from './lib/config.js';
import { discoverApiOperations, executeApiBatch } from './scenarios/full-api.js';

const profile = resolveManagedProfile(__ENV.K6_PROFILE_KEY || 'baseline');

export const options = {
  scenarios: {
    [profile.key]: profile.scenario,
  },
  thresholds: thresholds[profile.thresholdKey],
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export function setup() {
  return { operations: discoverApiOperations() };
}

export default function (data) {
  group('full-api', () => executeApiBatch(data.operations, profile.batchSize));
  if (profile.sleepMs > 0) {
    sleep(profile.sleepMs / 1000);
  }
}
