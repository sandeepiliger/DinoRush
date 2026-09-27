// Hero progression and cargo rules. Pure and unit tested.

import type { StatId, WildConfig } from '../data/wild';

export interface HeroState {
  stats: Record<StatId, number>; // upgrade levels, starting at 0
  /** Meat currently carried on the hero's back. */
  meat: number;
  /** Pack members owned (hatched). */
  pack: number;
  /** Meat delivered so far to each unbuilt pad. */
  padProgress: Record<string, number>;
  /** Pads that are built. */
  built: string[];
  /** Egg waiting in the nest. */
  eggReady: boolean;
}

export const freshHero = (cfg: WildConfig): HeroState => ({
  stats: { cargo: 0, pack: 0, bite: 0, speed: 0 },
  meat: 0,
  pack: 0,
  padProgress: {},
  built: cfg.pads.filter((p) => p.buildMeat === 0).map((p) => p.id),
  eggReady: false,
});

export function statValue(h: HeroState, id: StatId, cfg: WildConfig): number {
  const d = cfg.stats[id];
  return d.base + d.perLevel * h.stats[id];
}

export function statCost(h: HeroState, id: StatId, cfg: WildConfig): number {
  const d = cfg.stats[id];
  return Math.round(d.costBase * Math.pow(d.costGrowth, h.stats[id]));
}

export function statMaxed(h: HeroState, id: StatId, cfg: WildConfig): boolean {
  return h.stats[id] >= cfg.stats[id].maxLevel;
}

export const cargoCapacity = (h: HeroState, cfg: WildConfig) => Math.floor(statValue(h, 'cargo', cfg));
export const packCapacity = (h: HeroState, cfg: WildConfig) => Math.floor(statValue(h, 'pack', cfg));

/** Adds carried meat up to capacity; returns how much was actually picked up. */
export function pickUpMeat(h: HeroState, amount: number, cfg: WildConfig): number {
  const room = Math.max(0, cargoCapacity(h, cfg) - h.meat);
  const taken = Math.min(room, amount);
  h.meat += taken;
  return taken;
}

export function isBuilt(h: HeroState, padId: string): boolean {
  return h.built.includes(padId);
}

/** Moves one meat from the hero into an unbuilt pad. Returns true when that completes the build. */
export function depositToPad(h: HeroState, padId: string, cfg: WildConfig): { moved: boolean; built: boolean } {
  const pad = cfg.pads.find((p) => p.id === padId);
  if (!pad || isBuilt(h, padId) || h.meat <= 0) return { moved: false, built: false };
  h.meat--;
  const progress = (h.padProgress[padId] ?? 0) + 1;
  h.padProgress[padId] = progress;
  if (progress >= pad.buildMeat) {
    h.built.push(padId);
    delete h.padProgress[padId];
    return { moved: true, built: true };
  }
  return { moved: true, built: false };
}

/** Sells one meat. Returns the coins earned (0 if nothing to sell). */
export function sellMeat(h: HeroState, incomePerSecond: number, cfg: WildConfig): number {
  if (h.meat <= 0) return 0;
  h.meat--;
  return Math.max(cfg.meatMinCoins, Math.round(incomePerSecond * cfg.meatIncomeSeconds));
}

/** Hatching the nest egg adds a pack member if there is room. */
export function hatchNestEgg(h: HeroState, cfg: WildConfig): boolean {
  if (!h.eggReady || h.pack >= packCapacity(h, cfg)) return false;
  h.eggReady = false;
  h.pack++;
  return true;
}
