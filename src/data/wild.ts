// Wild-zone content and balancing: the hero, pack, prey, build pads and upgrades.
// All numbers live here so the arcade loop can be tuned without touching gameplay code.

export type StatId = 'cargo' | 'pack' | 'bite' | 'speed';

export interface StatDef {
  id: StatId;
  base: number;
  perLevel: number;
  maxLevel: number;
  costBase: number;
  costGrowth: number;
}

export interface PreyDef {
  speciesId: string;
  hp: number;
  meat: number;
  /** Flee speed in m/s (the hero is faster). */
  speed: number;
  /** How many of this prey live in the jungle at once. */
  population: number;
}

export type PadKind = 'market' | 'nest' | 'lab';

export interface PadDef {
  id: string;
  kind: PadKind;
  x: number;
  z: number;
  /** Meat needed to build it; 0 = already built from the start. */
  buildMeat: number;
}

export interface WildConfig {
  /** Playable ground (everything outside rises into hills). */
  bounds: { minX: number; maxX: number; minZ: number; maxZ: number };
  /** Where prey roam. */
  jungle: { minX: number; maxX: number; minZ: number; maxZ: number };
  heroStart: { x: number; z: number };
  stats: Record<StatId, StatDef>;
  prey: PreyDef[];
  pads: PadDef[];
  /** Seconds between bites for the hero and each pack member. */
  biteInterval: number;
  biteRange: number;
  /** Distance at which prey notice the hero and run. */
  fleeRadius: number;
  /** Meat pickups within this radius fly onto the hero's back. */
  magnetRadius: number;
  /** Coins per delivered meat, as seconds of current park income (never below `meatMinCoins`). */
  meatIncomeSeconds: number;
  meatMinCoins: number;
  /** Seconds for the nest to lay an egg. */
  eggSeconds: number;
  /** Seconds between each item flying off the stack onto a pad. */
  depositInterval: number;
  respawnSeconds: number;
}

export const WILD: WildConfig = {
  bounds: { minX: -30, maxX: 30, minZ: -62, maxZ: 22 },
  jungle: { minX: -26, maxX: 26, minZ: -58, maxZ: -24 },
  heroStart: { x: 0, z: 6.5 },
  stats: {
    cargo: { id: 'cargo', base: 8, perLevel: 4, maxLevel: 20, costBase: 60, costGrowth: 1.55 },
    pack: { id: 'pack', base: 2, perLevel: 1, maxLevel: 8, costBase: 250, costGrowth: 2.4 },
    bite: { id: 'bite', base: 1, perLevel: 0.5, maxLevel: 20, costBase: 80, costGrowth: 1.6 },
    speed: { id: 'speed', base: 5.2, perLevel: 0.25, maxLevel: 10, costBase: 100, costGrowth: 1.7 },
  },
  prey: [
    { speciesId: 'compy', hp: 2, meat: 1, speed: 3.6, population: 9 },
    { speciesId: 'protoceratops', hp: 5, meat: 3, speed: 2.6, population: 5 },
  ],
  pads: [
    { id: 'market', kind: 'market', x: 6.5, z: 16.5, buildMeat: 0 },
    { id: 'lab', kind: 'lab', x: -6.5, z: 16.5, buildMeat: 0 },
    { id: 'nest', kind: 'nest', x: 0, z: -26, buildMeat: 15 },
  ],
  biteInterval: 0.45,
  biteRange: 1.9,
  fleeRadius: 6,
  magnetRadius: 2.2,
  meatIncomeSeconds: 2,
  meatMinCoins: 6,
  eggSeconds: 40,
  depositInterval: 0.09,
  respawnSeconds: 6,
};
