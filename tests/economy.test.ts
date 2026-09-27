import { describe, expect, it } from 'vitest';
import { makeEconomy } from './helpers';

describe('Economy', () => {
  it('starts with the free enclosure owned and one dino in it', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    const first = eco.park.enclosures[0];
    expect(s.enclosures[first.id]).toEqual({ level: 1, dinos: 1 });
    for (const def of eco.park.enclosures.slice(1)) expect(s.enclosures[def.id].level).toBe(0);
    expect(s.coins).toBe(eco.cfg.startingCoins);
  });

  it('upgrading spends coins and raises the ticket', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    const def = eco.park.enclosures[0];
    const before = eco.ticketFor(def, s.enclosures[def.id]);
    const cost = eco.upgradeCost(def, 1);
    const r = eco.upgrade(s, def.id);
    expect(r).toEqual({ ok: true, cost });
    expect(s.coins).toBe(eco.cfg.startingCoins - cost);
    expect(eco.ticketFor(def, s.enclosures[def.id])).toBeGreaterThan(before);
  });

  it('refuses purchases the player cannot afford without changing state', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    s.coins = 0;
    const snapshot = structuredClone(s);
    expect(eco.upgrade(s, eco.park.enclosures[0].id)).toEqual({ ok: false, reason: 'coins' });
    expect(eco.upgradeEntrance(s)).toEqual({ ok: false, reason: 'coins' });
    expect(s).toEqual(snapshot);
  });

  it('plots unlock strictly in order', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    s.coins = 1e12;
    const [, second, third] = eco.park.enclosures;
    expect(eco.unlock(s, third.id)).toEqual({ ok: false, reason: 'locked' });
    expect(eco.unlock(s, second.id).ok).toBe(true);
    expect(eco.unlock(s, second.id)).toEqual({ ok: false, reason: 'owned' });
    expect(eco.unlock(s, third.id).ok).toBe(true);
  });

  it('caps levels, dinos and entrance', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    s.coins = 1e30;
    const def = eco.park.enclosures[0];
    while (eco.upgrade(s, def.id).ok);
    expect(s.enclosures[def.id].level).toBe(eco.cfg.maxLevel);
    while (eco.hatch(s, def.id).ok);
    expect(s.enclosures[def.id].dinos).toBe(def.maxDinos);
    while (eco.upgradeEntrance(s).ok);
    expect(s.entranceLevel).toBe(eco.cfg.entranceMaxLevel);
    expect(eco.visitorRate(s.entranceLevel)).toBeLessThanOrEqual(eco.cfg.maxVisitorRate);
  });

  it('milestones multiply income every N levels', () => {
    const eco = makeEconomy();
    const def = eco.park.enclosures[0];
    const st = { level: 1, dinos: 1 };
    const n = eco.cfg.milestoneEvery;
    const justBefore = eco.ticketFor(def, st, n - 1);
    const at = eco.ticketFor(def, st, n);
    expect(at / justBefore).toBeGreaterThan(eco.cfg.milestoneMultiplier * 0.99);
  });

  it('boost doubles income only while active', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    const base = eco.incomePerSecond(s, 1000);
    s.boostUntil = 5000;
    expect(eco.incomePerSecond(s, 1000)).toBeCloseTo(base * eco.cfg.boostMultiplier);
    expect(eco.incomePerSecond(s, 6000)).toBeCloseTo(base);
  });

  it('offline earnings are capped and never negative', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    expect(eco.offlineEarnings(s, -1000)).toEqual({ seconds: 0, coins: 0 });
    const day = eco.offlineEarnings(s, 24 * 3600 * 1000);
    expect(day.seconds).toBe(eco.cfg.offlineCapSeconds);
    const rate = eco.visitorRate(1) * eco.incomePerVisitor(s);
    expect(day.coins).toBe(Math.floor(rate * eco.cfg.offlineCapSeconds * eco.cfg.offlineEfficiency));
  });

  it('costs grow monotonically', () => {
    const eco = makeEconomy();
    for (const def of eco.park.enclosures) {
      for (let l = 1; l < eco.cfg.maxLevel; l++) expect(eco.upgradeCost(def, l + 1)).toBeGreaterThanOrEqual(eco.upgradeCost(def, l));
    }
  });
});
