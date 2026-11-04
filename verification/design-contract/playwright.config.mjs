// Rendered design contract: Chromium and WebKit, run from the governed checkout against the candidate (CAUSA_APP_URL).
import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: '.',
  testMatch: /rendered\.spec\.mjs$/,
  timeout: 300000,
  workers: 1,
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
  ],
});
