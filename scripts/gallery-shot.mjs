import { chromium } from 'playwright-core';
import { createServer } from 'vite';
import fs from 'node:fs';
fs.mkdirSync('screenshots', { recursive: true });
const server = await createServer({ server: { port: 5198 }, logLevel: 'error' });
await server.listen();
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1400, height: 520 } });
const errors = [];
page.on('pageerror', (e) => errors.push(String(e)));
for (const q of process.argv.slice(2)) {
  await page.goto(`http://localhost:5198/gallery.html?${q}`);
  await page.waitForFunction(() => window.ready, null, { timeout: 60000 });
  await page.waitForTimeout(1500);
  const name = q.replace(/[^a-z0-9]+/gi, '_');
  await page.screenshot({ path: `screenshots/gallery-${name}.png` });
  console.log('saved', name);
}
console.log('errors', errors);
await browser.close();
await server.close();
