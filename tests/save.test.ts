import { describe, expect, it } from 'vitest';
import { MemoryStore, SaveManager, SAVE_VERSION } from '../src/core/save';
import { makeEconomy } from './helpers';

const setup = () => {
  const store = new MemoryStore();
  const eco = makeEconomy();
  return { store, eco, sm: new SaveManager(store, eco) };
};

describe('SaveManager', () => {
  it('round-trips a save', () => {
    const { sm, eco } = setup();
    const data = sm.fresh(1000);
    data.park.coins = 4321;
    eco.unlock(data.park, eco.park.enclosures[1].id);
    data.settings.music = false;
    data.quest = { index: 4, count: 1 };
    data.hero.meat = 3;
    data.hero.stats.bite = 2;
    sm.save(data);
    const { data: loaded, recovered } = sm.load(2000);
    expect(recovered).toBe(false);
    expect(loaded).toEqual(data);
  });

  it('starts fresh when nothing is saved', () => {
    const { sm } = setup();
    const { data, recovered } = sm.load(0);
    expect(recovered).toBe(false);
    expect(data.saveVersion).toBe(SAVE_VERSION);
  });

  it('falls back to the backup when the main save is corrupted', () => {
    const { sm, store } = setup();
    const a = sm.fresh(0);
    a.park.coins = 111;
    sm.save(a);
    const b = sm.fresh(0);
    b.park.coins = 222;
    sm.save(b);
    store.setItem('dpt.save', '{not json');
    const { data, recovered } = sm.load(0);
    expect(recovered).toBe(true);
    expect(data.park.coins).toBe(111);
  });

  it('recovers with a fresh save when everything is garbage', () => {
    const { sm, store } = setup();
    store.setItem('dpt.save', 'garbage');
    const { data, recovered } = sm.load(0);
    expect(recovered).toBe(true);
    expect(data.park.coins).toBeGreaterThan(0);
  });

  it('clamps tampered or invalid values', () => {
    const { sm, eco } = setup();
    const raptor = eco.park.enclosures[0];
    const v = sm.validate({
      saveVersion: 1,
      park: {
        parkId: eco.park.id, coins: -50, entranceLevel: 9999, lastSeen: 10_000_000, boostUntil: 'x',
        enclosures: { [raptor.id]: { level: 1e9, dinos: 99 }, bogus: { level: 3 } },
      },
      settings: { sfx: 'yes' },
      removeAds: 'true',
    }, 5000)!;
    expect(v.park.coins).toBe(eco.cfg.startingCoins);
    expect(v.park.entranceLevel).toBe(eco.cfg.entranceMaxLevel);
    expect(v.park.lastSeen).toBe(5000);
    expect(v.park.boostUntil).toBe(0);
    expect(v.park.enclosures[raptor.id]).toEqual({ level: eco.cfg.maxLevel, dinos: raptor.maxDinos });
    expect('bogus' in v.park.enclosures).toBe(false);
    expect(v.settings.sfx).toBe(true);
    expect(v.removeAds).toBe(false);
    expect(v.quest).toEqual({ index: 0, count: 0 });
  });

  it('migrates v1 saves: adds a fresh hero and shifts quests past the inserted wild quests', () => {
    const { sm, eco } = setup();
    const v1 = { saveVersion: 1, park: { parkId: eco.park.id, coins: 10 }, quest: { index: 7, count: 2 } };
    const v = sm.validate(v1, 0)!;
    expect(v.saveVersion).toBe(SAVE_VERSION);
    expect(v.quest.index).toBe(12);
    expect(v.hero.meat).toBe(0);
    const early = sm.validate({ ...v1, quest: { index: 2, count: 0 } }, 0)!;
    expect(early.quest.index).toBe(2);
  });

  it('clamps tampered hero data', () => {
    const { sm, eco } = setup();
    const v = sm.validate({ saveVersion: 2, park: { parkId: eco.park.id }, hero: { meat: -3, stats: { cargo: 999 }, built: ['nest', 'hack'], pack: 1e9 } }, 0)!;
    expect(v.hero.meat).toBe(0);
    expect(v.hero.stats.cargo).toBeLessThanOrEqual(20);
    expect(v.hero.built).toContain('nest');
    expect(v.hero.built).not.toContain('hack');
    expect(v.hero.pack).toBeLessThan(100);
  });

  it('rejects saves from a newer version or another park', () => {
    const { sm, eco } = setup();
    expect(sm.validate({ saveVersion: SAVE_VERSION + 1, park: { parkId: eco.park.id } }, 0)).toBeNull();
    expect(sm.validate({ saveVersion: 1, park: { parkId: 'elsewhere' } }, 0)).toBeNull();
    expect(sm.validate(null, 0)).toBeNull();
  });
});
