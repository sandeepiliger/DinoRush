// Launches the dev build in headless Chromium, plays a scripted session and saves screenshots.
// Usage: npm run screenshot  (starts its own Vite server)
import { chromium } from 'playwright-core';
import { createServer } from 'vite';
import fs from 'node:fs';

const exe = process.env.CHROMIUM_PATH || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
fs.mkdirSync('screenshots', { recursive: true });
const server = await createServer({ server: { port: 5199 }, logLevel: 'error' });
await server.listen();
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 412, height: 870 }, deviceScaleFactor: 2, hasTouch: true, isMobile: true });
const errors = [];
page.on('pageerror', (e) => errors.push(String(e)));
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
const shot = async (name) => { await page.screenshot({ path: `screenshots/${name}.png` }); console.log('saved', name); };
const scenario = process.argv[2] || 'basic';

if (scenario === 'rich') {
  // Pre-seed a progressed save so every species is on screen.
  await page.addInitScript(() => {
    const now = Date.now();
    localStorage.setItem('dpt.save', JSON.stringify({
      saveVersion: 1, removeAds: false, tutorialDone: true, settings: { sfx: false, music: false, haptics: false },
      park: { parkId: 'mumbai', coins: 2e7, entranceLevel: 12, boostUntil: 0, lastSeen: now, totalEarned: 0, totalVisitors: 0,
        enclosures: { 'raptor-pen': { level: 30, dinos: 4 }, 'trike-field': { level: 20, dinos: 3 }, 'stego-grove': { level: 15, dinos: 3 }, 'brachio-heights': { level: 8, dinos: 2 }, 'rex-arena': { level: 3, dinos: 2 } } },
    }));
  });
}
if (scenario === 'flow') {
  await page.addInitScript(() => {
    if (sessionStorage.getItem('seeded')) return;
    sessionStorage.setItem('seeded', '1');
    const now = Date.now();
    localStorage.setItem('dpt.save', JSON.stringify({
      saveVersion: 1, removeAds: false, tutorialDone: true, settings: { sfx: true, music: true, haptics: true },
      park: { parkId: 'mumbai', coins: 5000, entranceLevel: 3, boostUntil: 0, lastSeen: now - 3600e3, totalEarned: 0, totalVisitors: 0,
        enclosures: { 'raptor-pen': { level: 12, dinos: 1 } } },
    }));
  });
}
await page.goto('http://localhost:5199/');
await page.waitForFunction(() => document.getElementById('loading')?.classList.contains('done'), null, { timeout: 60000 });
await page.waitForTimeout(4000);
await shot(`${scenario}-1-start`);
if (scenario === 'basic') {
  await page.locator('.wlabel').first().click();
  await page.waitForTimeout(900);
  await shot('basic-2-sheet');
  for (let i = 0; i < 3; i++) { await page.locator('.sheet .actions .btn').first().click(); await page.waitForTimeout(150); }
  await page.waitForTimeout(600);
  await shot('basic-3-upgraded');
}
if (scenario === 'rich') {
  await page.mouse.move(206, 300); await page.mouse.down(); await page.mouse.move(206, 700, { steps: 10 }); await page.mouse.up();
  await page.waitForTimeout(2000);
  await shot('rich-2-north');
  await page.locator('.wlabel').nth(4).click();
  await page.waitForTimeout(1500);
  await shot('rich-3-rex-sheet');
}
if (scenario === 'flow') {
  await shot('flow-offline-modal');
  await page.locator('[data-a="collect"]').click();
  await page.waitForTimeout(500);
  await page.locator('.wlabel').nth(1).click();           // Triceratops plot
  await page.waitForTimeout(700);
  await page.locator('.sheet .actions .btn').first().click(); // unlock
  await page.waitForTimeout(600);
  await shot('flow-unlock-banner');
  await page.waitForTimeout(2500);
  await shot('flow-unlocked');
  await page.waitForTimeout(4000);
  await shot('flow-hatched');
  await page.locator('.sheet-close').click();
  await page.evaluate(() => window.game.cam.focus(-3.6, 8.5, 26)); // pan back to the raptors
  await page.waitForTimeout(2500);
  await page.locator('.wlabel').first().click();          // raptor
  await page.waitForTimeout(600);
  await page.locator('.sheet .actions .btn').nth(1).click(); // hatch egg
  await page.waitForTimeout(700);
  await shot('flow-egg');
  await page.waitForTimeout(2500);
  await page.locator('.sheet-close').click();
  await page.waitForTimeout(300);
  await page.locator('#hud-boost').click();
  await page.waitForTimeout(800);
  await shot('flow-mock-ad');
  await page.waitForTimeout(2600);
  await shot('flow-boost-active');
  await page.locator('#hud-settings').click();
  await page.waitForTimeout(400);
  await page.locator('[data-a="music"]').click();
  await shot('flow-settings');
  const saved = await page.evaluate(() => JSON.parse(localStorage.getItem('dpt.save')));
  await page.reload();
  await page.waitForFunction(() => document.getElementById('loading')?.classList.contains('done'), null, { timeout: 60000 });
  const reloaded = await page.evaluate(() => ({ trike: window.game.data.park.enclosures['trike-field'], raptor: window.game.data.park.enclosures['raptor-pen'], music: window.game.data.settings.music }));
  console.log('persisted:', JSON.stringify(reloaded), 'boostUntil>now:', saved.park.boostUntil > Date.now());
}
if (scenario === 'zoom') {
  await page.evaluate(() => { const g = window.game; g.cam.target.set(-6, 5); g.cam.distance = 12; });
  await page.waitForTimeout(1500);
  await shot('zoom-2-raptor');
}
const stats = await page.evaluate(() => ({ w: innerWidth, h: innerHeight }));
console.log('viewport', stats);
console.log('errors:', errors.length ? errors : 'none');
await browser.close();
await server.close();
