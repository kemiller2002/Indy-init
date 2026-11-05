// PROPOSAL prototype: Layer B screen-behaviour check of the fixture-backed investigation-list screen. Chromium only in
// this sandbox (no WebKit build here); Layer C runs both engines against api.github.com and is not replaced by this.
import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: '.',
  testMatch: /render\.spec\.mjs$/,
  reporter: [['list'], ['json', { outputFile: 'test-results/render-results.json' }]],
  use: { baseURL: 'http://127.0.0.1:4631' },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
  webServer: {
    command: 'dotnet run --project ../host/Causa.Ui.Prototype.Host.fsproj --urls http://127.0.0.1:4631',
    url: 'http://127.0.0.1:4631/',
    reuseExistingServer: false,
    timeout: 180000,
  },
});
