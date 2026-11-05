// Practice only (FENCE.md). Browser journeys against the harness REST double (component behavior, NOT real persistence).
// WebKit is not installed in this environment: the iPhone project emulates the iPhone viewport, touch and user agent in
// Chromium, and is reported as such.
import { defineConfig, devices } from '@playwright/test';
const iphone = { ...devices['iPhone 13'] };
delete iphone.defaultBrowserType;
export default defineConfig({
  testDir: './e2e',
  timeout: 120000,
  workers: 1,
  reporter: [['list'], ['json', { outputFile: 'evidence/playwright-results.json' }]],
  use: { baseURL: 'http://127.0.0.1:4631', trace: 'off' },
  webServer: [
    { command: 'node ../../verification/harness/serve.mjs dist 4631', port: 4631, reuseExistingServer: false },
    { command: 'node ../../verification/harness/github-double.mjs 4632', port: 4632, reuseExistingServer: false },
  ],
  projects: [
    { name: 'desktop-chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'pixel-7', use: { ...devices['Pixel 7'] } },
    { name: 'iphone-13-viewport-in-chromium', use: { ...iphone, browserName: 'chromium' } },
  ],
});
