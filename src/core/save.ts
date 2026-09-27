// Versioned, validated save data. Old or corrupted saves are migrated or repaired, never trusted blindly.

import type { ParkState } from '../economy/economy';
import type { Economy } from '../economy/economy';
import type { QuestProgress } from '../economy/quests';
import { WILD, type StatId } from '../data/wild';
import { WILD_QUESTS_INSERTED, WILD_QUESTS_INSERTED_AT } from '../data/quests';
import { freshHero, type HeroState } from '../economy/hero';

// v1: park, settings, quests.  v2: adds the hero (wild zone) and inserts the wild quests into the quest line.
export const SAVE_VERSION = 2;
const SAVE_KEY = 'dpt.save';
const BACKUP_KEY = 'dpt.save.bak';

export interface Settings {
  sfx: boolean;
  music: boolean;
  haptics: boolean;
}

export interface SaveData {
  saveVersion: number;
  park: ParkState;
  settings: Settings;
  removeAds: boolean;
  tutorialDone: boolean;
  quest: QuestProgress;
  hero: HeroState;
}

/** Minimal storage surface so tests can run without a browser. */
export interface KeyValueStore {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export class MemoryStore implements KeyValueStore {
  private map = new Map<string, string>();
  getItem(key: string) {
    return this.map.get(key) ?? null;
  }
  setItem(key: string, value: string) {
    this.map.set(key, value);
  }
  removeItem(key: string) {
    this.map.delete(key);
  }
}

export const DEFAULT_SETTINGS: Settings = { sfx: true, music: true, haptics: true };

const isNum = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v);
const nonNeg = (v: unknown, fallback: number) => (isNum(v) && v >= 0 ? v : fallback);
const int = (v: unknown, fallback: number, min: number, max: number) =>
  isNum(v) ? Math.min(max, Math.max(min, Math.floor(v))) : fallback;

export class SaveManager {
  constructor(
    private store: KeyValueStore,
    private economy: Economy,
  ) {}

  fresh(now: number): SaveData {
    return {
      saveVersion: SAVE_VERSION,
      park: this.economy.createInitialState(now),
      settings: { ...DEFAULT_SETTINGS },
      removeAds: false,
      tutorialDone: false,
      quest: { index: 0, count: 0 },
      hero: freshHero(WILD),
    };
  }

  load(now: number): { data: SaveData; recovered: boolean } {
    const primary = this.tryParse(this.store.getItem(SAVE_KEY), now);
    if (primary) return { data: primary, recovered: false };
    const backup = this.tryParse(this.store.getItem(BACKUP_KEY), now);
    if (backup) return { data: backup, recovered: true };
    const hadData = this.store.getItem(SAVE_KEY) !== null;
    return { data: this.fresh(now), recovered: hadData };
  }

  save(data: SaveData): void {
    try {
      const json = JSON.stringify(data);
      const previous = this.store.getItem(SAVE_KEY);
      if (previous) this.store.setItem(BACKUP_KEY, previous);
      this.store.setItem(SAVE_KEY, json);
    } catch {
      // Storage full or unavailable (private mode). The game keeps running; progress just isn't persisted.
    }
  }

  reset(): void {
    this.store.removeItem(SAVE_KEY);
    this.store.removeItem(BACKUP_KEY);
  }

  private tryParse(raw: string | null, now: number): SaveData | null {
    if (!raw) return null;
    let obj: unknown;
    try {
      obj = JSON.parse(raw);
    } catch {
      return null;
    }
    return this.validate(obj, now);
  }

  /** Rebuilds a save from untrusted JSON: unknown fields are dropped, bad values are clamped or defaulted. */
  validate(obj: unknown, now: number): SaveData | null {
    if (!obj || typeof obj !== 'object') return null;
    const o = obj as Record<string, unknown>;
    if (!isNum(o.saveVersion) || o.saveVersion < 1) return null;
    // Future versions: migrate step by step here (v1 -> v2 -> ...). A save from a newer build is rejected.
    if (o.saveVersion > SAVE_VERSION) return null;

    const base = this.fresh(now);
    const p = (o.park ?? {}) as Record<string, unknown>;
    if (typeof p !== 'object' || p.parkId !== base.park.parkId) return null;

    const cfg = this.economy.cfg;
    const park = base.park;
    park.coins = nonNeg(p.coins, park.coins);
    park.entranceLevel = int(p.entranceLevel, 1, 1, cfg.entranceMaxLevel);
    park.boostUntil = nonNeg(p.boostUntil, 0);
    // A lastSeen in the future (clock moved back, or tampering) earns nothing offline.
    park.lastSeen = isNum(p.lastSeen) ? Math.min(p.lastSeen, now) : now;
    park.totalEarned = nonNeg(p.totalEarned, 0);
    park.totalVisitors = nonNeg(p.totalVisitors, 0);
    const encl = (p.enclosures ?? {}) as Record<string, unknown>;
    for (const def of this.economy.park.enclosures) {
      const e = (encl[def.id] ?? {}) as Record<string, unknown>;
      const level = int(e.level, park.enclosures[def.id].level, 0, cfg.maxLevel);
      const dinos = level > 0 ? int(e.dinos, 1, 1, def.maxDinos) : 0;
      park.enclosures[def.id] = { level, dinos };
    }

    const s = (o.settings ?? {}) as Record<string, unknown>;
    const settings: Settings = {
      sfx: typeof s.sfx === 'boolean' ? s.sfx : DEFAULT_SETTINGS.sfx,
      music: typeof s.music === 'boolean' ? s.music : DEFAULT_SETTINGS.music,
      haptics: typeof s.haptics === 'boolean' ? s.haptics : DEFAULT_SETTINGS.haptics,
    };

    // `quest` was added after v1 shipped to testers; it is optional and defaults to the start.
    const q = (o.quest ?? {}) as Record<string, unknown>;
    const quest = { index: int(q.index, 0, 0, 1_000_000), count: int(q.count, 0, 0, 1_000_000) };
    // v1 -> v2: the wild quests were inserted mid-line; keep v1 players on the quest they were doing.
    if (o.saveVersion < 2 && quest.index >= WILD_QUESTS_INSERTED_AT) quest.index += WILD_QUESTS_INSERTED;

    const hero = freshHero(WILD);
    const hs = (o.hero ?? {}) as Record<string, unknown>;
    const st = (hs.stats ?? {}) as Record<string, unknown>;
    for (const id of Object.keys(hero.stats) as StatId[]) hero.stats[id] = int(st[id], 0, 0, WILD.stats[id].maxLevel);
    hero.meat = int(hs.meat, 0, 0, 10_000);
    hero.pack = int(hs.pack, 0, 0, WILD.stats.pack.base + WILD.stats.pack.perLevel * WILD.stats.pack.maxLevel);
    hero.eggReady = hs.eggReady === true;
    const padIds = new Set(WILD.pads.map((p) => p.id));
    if (Array.isArray(hs.built)) for (const id of hs.built) if (typeof id === 'string' && padIds.has(id) && !hero.built.includes(id)) hero.built.push(id);
    const prog = (hs.padProgress ?? {}) as Record<string, unknown>;
    for (const pad of WILD.pads) if (!hero.built.includes(pad.id) && isNum(prog[pad.id])) hero.padProgress[pad.id] = int(prog[pad.id], 0, 0, pad.buildMeat);

    return {
      saveVersion: SAVE_VERSION,
      park,
      settings,
      removeAds: o.removeAds === true,
      tutorialDone: o.tutorialDone === true,
      quest,
      hero,
    };
  }
}
