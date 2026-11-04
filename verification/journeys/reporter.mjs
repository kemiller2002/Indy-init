// Writes indy-init.journey-results/1: one result per journey and engine, with every bound assertion's outcome. The
// provider base URL is recorded so a harness double is never mistaken for real-provider verification.
import fs from 'node:fs';
import { providerBase, resultsPath } from './support.mjs';

export default class JourneyReporter {
  constructor() { this.results = []; }
  onTestEnd(test, result) {
    const journey = (test.title.match(/^(J[0-9]+)/) || [])[1] || test.title;
    const assertions = result.annotations.filter((a) => a.type === 'assertion').map((a) => JSON.parse(a.description));
    this.results.push({
      journey,
      engine: test.parent.project()?.name || 'unknown',
      status: result.status,
      error: result.error ? String(result.error.message || '').split('\n')[0] : null,
      durationMs: result.duration,
      assertions,
    });
  }
  onEnd() {
    const out = {
      schema: 'indy-init.journey-results/1',
      candidate: process.env.CAUSA_CANDIDATE || '',
      app: process.env.CAUSA_APP_URL || '',
      provider: { mode: process.env.CAUSA_PROVIDER || 'github', baseUrl: providerBase },
      results: this.results,
    };
    fs.writeFileSync(resultsPath(), JSON.stringify(out, null, 2) + '\n');
  }
}
