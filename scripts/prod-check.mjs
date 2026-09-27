// Smoke-tests the production bundle: serves dist/, loads it headless, fails on any page error.
import { chromium } from 'playwright-core';
import { preview } from 'vite';
const server = await preview({ preview: { port: 5196 }, logLevel: 'error' });
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 412, height: 870 } });
const errors = [];
page.on('pageerror', (e) => errors.push(String(e)));
page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));
await page.goto('http://localhost:5196/');
await page.waitForFunction(() => document.getElementById('loading')?.classList.contains('done'), null, { timeout: 60000 });
await page.waitForTimeout(3000);
const hasCheat = await page.evaluate(() => 'game' in window);
await browser.close();
server.httpServer.close();
console.log(JSON.stringify({ errors, devHooksExposed: hasCheat }));
process.exit(errors.length || hasCheat ? 1 : 0);
