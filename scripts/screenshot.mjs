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
if (scenario === 'interact') {
  await page.addInitScript(() => {
    const now = Date.now();
    localStorage.setItem('dpt.save', JSON.stringify({
      saveVersion: 1, removeAds: false, tutorialDone: true, settings: { sfx: false, music: false, haptics: false },
      quest: { index: 3, count: 0 },
      park: { parkId: 'mumbai', coins: 900, entranceLevel: 4, boostUntil: 0, lastSeen: now, totalEarned: 0, totalVisitors: 0,
        enclosures: { 'raptor-pen': { level: 12, dinos: 3 }, 'trike-field': { level: 4, dinos: 2 } } },
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
  await page.evaluate(() => { window.game.wild.heroX = 0; window.game.wild.heroZ = -7; });
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
  await page.evaluate(() => { window.game.wild.heroX = -1; window.game.wild.heroZ = 7; }); // walk back to the raptors
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
if (scenario === 'interact') {
  await page.evaluate(() => {
    const g = window.game;
    for (const c of g.interactions.care.values()) c.food = 0.1;          // everyone hungry
    g.interactions.giftTimer = 0;                                         // pterodactyl now
    g.interactions.tipTimer = 0;                                          // tip now
    for (const [d] of g.interactions.poopTimers) g.interactions.poopTimers.set(d, 0); // poops now
    g.wild.heroX = 0; g.wild.heroZ = 5; g.cam.distance = 28;
  });
  await page.waitForTimeout(3500);
  await shot('interact-2-bubbles');
  const before = await page.evaluate(() => window.game.data.park.coins);
  await page.locator('.bubble-feed').first().click({ force: true });
  await page.waitForTimeout(400);
  await shot('interact-3-feeding');
  if (await page.locator('.bubble-tip').count()) await page.locator('.bubble-tip').click({ force: true });
  const poop = await page.evaluate(() => {
    const g = window.game; const p = g.effects.poops.find((x) => x.cleaning === 0);
    if (!p) return null;
    const v = p.mesh.position.clone().setY(0.25).project(g.cam.camera);
    return { x: (v.x * 0.5 + 0.5) * innerWidth, y: (-v.y * 0.5 + 0.5) * innerHeight };
  });
  if (poop) { await page.mouse.click(poop.x, poop.y); }
  await page.waitForTimeout(600);
  await page.waitForSelector('.bubble-gift:not([hidden])', { timeout: 90000 });
  await shot('interact-4-pterodactyl');
  await page.locator('.bubble-gift').click({ force: true });
  await page.waitForTimeout(500);
  await shot('interact-4b-rewards');
  await page.waitForTimeout(4000);
  await shot('interact-5-eating');
  const after = await page.evaluate(() => ({ coins: window.game.data.park.coins, quest: window.game.data.quest, poops: window.game.effects.poops.length }));
  console.log('coins', Math.round(before), '->', Math.round(after.coins), 'quest', JSON.stringify(after.quest), 'poops left', after.poops, 'poop tapped', !!poop);
  await page.locator('#quest').click({ force: true });
  await page.waitForTimeout(800);
  await shot('interact-6-claimed');
}
if (scenario === 'wild') {
  const tp = (x, z) => page.evaluate(([x, z]) => { const w = window.game.wild; w.heroX = x; w.heroZ = z; }, [x, z]);
  // 1) Into the jungle.
  await tp(0, -34);
  await page.waitForTimeout(2500);
  await shot('wild-1-jungle');
  // 2) Hunt: pull the nearest prey into bite range a few times.
  for (let i = 0; i < 6; i++) {
    await page.evaluate(() => {
      const w = window.game.wild; const p = w.prey.find((q) => q.state === 'wander' || q.state === 'flee');
      if (p) { p.x = w.heroX + 1.2; p.z = w.heroZ - 0.3; }
    });
    await page.waitForTimeout(700);
  }
  await page.waitForTimeout(6000);
  await shot('wild-2-hunt');
  const hunt = await page.evaluate(() => ({ meat: window.game.data.hero.meat, loose: window.game.wild.loose.length, quest: window.game.data.quest }));
  console.log('after hunting:', JSON.stringify(hunt));
  // 3) Build the nest with a full back.
  await page.evaluate(() => { window.game.data.hero.stats.cargo = 3; window.game.data.hero.meat = 20; });
  await page.waitForTimeout(600);
  await shot('wild-3-stack');
  await tp(0, -26);
  await page.waitForFunction(() => window.game.data.hero.built.includes('nest'), null, { timeout: 90000 });
  await page.waitForTimeout(2500);
  await shot('wild-4-nest-built');
  // 4) Hatch a pack member.
  await page.evaluate(() => { window.game.data.hero.eggReady = true; window.game.wild.refreshPads(); });
  await tp(0, -26.2);
  await page.waitForFunction(() => window.game.data.hero.pack >= 1, null, { timeout: 60000 });
  await page.waitForTimeout(4000);
  await tp(-3, -30);
  await page.waitForTimeout(8000);
  await shot('wild-5-pack');
  // 5) Sell at the market.
  await tp(6.5, 16.5);
  await page.waitForTimeout(4000);
  await shot('wild-6-market');
  // 6) DNA Lab.
  await tp(-6.5, 16.5);
  await page.waitForTimeout(1500);
  await shot('wild-7-lab');
  const end = await page.evaluate(() => ({ hero: window.game.data.hero, coins: Math.round(window.game.data.park.coins), sheet: window.game.sheet }));
  console.log('end:', JSON.stringify(end));
}
if (scenario === 'zoom') {
  await page.evaluate(() => { const g = window.game; g.wild.heroX = -2.4; g.wild.heroZ = 6; g.cam.distance = 12; });
  await page.waitForTimeout(1500);
  await shot('zoom-2-raptor');
}
const stats = await page.evaluate(() => ({ w: innerWidth, h: innerHeight }));
console.log('viewport', stats);
console.log('errors:', errors.length ? errors : 'none');
await browser.close();
await server.close();
