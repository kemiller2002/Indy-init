// Governed journeys: Chromium and WebKit (iPad Safari engine). Run from the governed Indy-init checkout against the
// candidate (CAUSA_APP_URL); results are written to CAUSA_JOURNEY_RESULTS by reporter.mjs, bound to CAUSA_CANDIDATE.
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: /j[0-9]-.*\.spec\.mjs$/,
  timeout: 180000,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['./reporter.mjs']],
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'webkit', use: { ...devices['iPad (gen 7)'], viewport: { width: 820, height: 1180 } } },
  ],
});
