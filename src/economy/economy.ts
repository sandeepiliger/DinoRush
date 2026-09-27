// Pure economy rules. No rendering, no DOM, no timers — everything here is deterministic and unit tested.

import type { EconomyConfig, EnclosureDef, ParkDef } from '../data/parks';

export interface EnclosureState {
  /** 0 = plot not purchased yet. */
  level: number;
  dinos: number;
}

export interface ParkState {
  parkId: string;
  coins: number;
  entranceLevel: number;
  enclosures: Record<string, EnclosureState>;
  /** Epoch ms until which the rewarded income boost is active. */
  boostUntil: number;
  /** Epoch ms of the last time the park was simulated (for offline earnings). */
  lastSeen: number;
  totalEarned: number;
  totalVisitors: number;
}

export type ActionResult = { ok: true; cost: number } | { ok: false; reason: 'locked' | 'max' | 'coins' | 'owned' };

export class Economy {
  constructor(
    readonly park: ParkDef,
    readonly cfg: EconomyConfig,
  ) {}

  createInitialState(now: number): ParkState {
    const enclosures: Record<string, EnclosureState> = {};
    for (const def of this.park.enclosures) {
      const free = def.unlockCost === 0;
      enclosures[def.id] = { level: free ? 1 : 0, dinos: free ? 1 : 0 };
    }
    return {
      parkId: this.park.id,
      coins: this.cfg.startingCoins,
      entranceLevel: 1,
      enclosures,
      boostUntil: 0,
      lastSeen: now,
      totalEarned: 0,
      totalVisitors: 0,
    };
  }

  enclosure(id: string): EnclosureDef {
    const def = this.park.enclosures.find((e) => e.id === id);
    if (!def) throw new Error(`Unknown enclosure: ${id}`);
    return def;
  }

  // ---------- Income ----------

  ticketFor(def: EnclosureDef, st: EnclosureState, level = st.level, dinos = st.dinos): number {
    if (level <= 0 || dinos <= 0) return 0;
    const c = this.cfg;
    const linear = 1 + c.ticketPerLevel * (level - 1);
    const milestones = Math.pow(c.milestoneMultiplier, Math.floor(level / c.milestoneEvery));
    const herd = 1 + c.perDinoBonus * (dinos - 1);
    return def.baseTicket * linear * milestones * herd;
  }

  entryFee(level: number): number {
    return this.cfg.baseEntryFee * (1 + this.cfg.entryFeePerLevel * (level - 1));
  }

  visitorRate(level: number): number {
    const c = this.cfg;
    return Math.min(c.maxVisitorRate, c.baseVisitorRate * Math.pow(1 + c.visitorRateGrowth, level - 1));
  }

  /** `ticketMultiplier` lets live-only modifiers (dino care) apply without touching offline math. */
  incomePerVisitor(state: ParkState, ticketMultiplier?: (enclosureId: string) => number): number {
    let total = this.entryFee(state.entranceLevel);
    for (const def of this.park.enclosures) {
      total += this.ticketFor(def, state.enclosures[def.id]) * (ticketMultiplier ? ticketMultiplier(def.id) : 1);
    }
    return total;
  }

  boostActive(state: ParkState, now: number): boolean {
    return state.boostUntil > now;
  }

  boostMultiplier(state: ParkState, now: number): number {
    return this.boostActive(state, now) ? this.cfg.boostMultiplier : 1;
  }

  /** Steady-state income per second. The live visitor simulation converges to this value. */
  incomePerSecond(state: ParkState, now: number, ticketMultiplier?: (enclosureId: string) => number): number {
    return this.visitorRate(state.entranceLevel) * this.incomePerVisitor(state, ticketMultiplier) * this.boostMultiplier(state, now);
  }

  offlineEarnings(state: ParkState, now: number): { seconds: number; coins: number } {
    const seconds = Math.max(0, Math.min(this.cfg.offlineCapSeconds, (now - state.lastSeen) / 1000));
    const rate = this.visitorRate(state.entranceLevel) * this.incomePerVisitor(state);
    return { seconds, coins: Math.floor(rate * seconds * this.cfg.offlineEfficiency) };
  }

  // ---------- Costs ----------

  upgradeCost(def: EnclosureDef, level: number): number {
    return Math.round(def.upgradeBaseCost * Math.pow(this.cfg.upgradeCostGrowth, level - 1));
  }

  eggCost(def: EnclosureDef, dinos: number): number {
    return Math.round(def.eggBaseCost * Math.pow(this.cfg.eggCostGrowth, dinos - 1));
  }

  entranceCost(level: number): number {
    return Math.round(this.cfg.entranceBaseCost * Math.pow(this.cfg.entranceCostGrowth, level - 1));
  }

  // ---------- Actions ----------

  earn(state: ParkState, amount: number): void {
    if (!(amount > 0)) return;
    state.coins += amount;
    state.totalEarned += amount;
  }

  private spend(state: ParkState, cost: number): ActionResult {
    if (state.coins < cost) return { ok: false, reason: 'coins' };
    state.coins -= cost;
    return { ok: true, cost };
  }

  unlock(state: ParkState, id: string): ActionResult {
    const def = this.enclosure(id);
    const st = state.enclosures[id];
    if (st.level > 0) return { ok: false, reason: 'owned' };
    if (!this.isPlotAvailable(state, id)) return { ok: false, reason: 'locked' };
    const r = this.spend(state, def.unlockCost);
    if (r.ok) {
      st.level = 1;
      st.dinos = 1;
    }
    return r;
  }

  upgrade(state: ParkState, id: string): ActionResult {
    const def = this.enclosure(id);
    const st = state.enclosures[id];
    if (st.level <= 0) return { ok: false, reason: 'locked' };
    if (st.level >= this.cfg.maxLevel) return { ok: false, reason: 'max' };
    const r = this.spend(state, this.upgradeCost(def, st.level));
    if (r.ok) st.level += 1;
    return r;
  }

  hatch(state: ParkState, id: string): ActionResult {
    const def = this.enclosure(id);
    const st = state.enclosures[id];
    if (st.level <= 0) return { ok: false, reason: 'locked' };
    if (st.dinos >= def.maxDinos) return { ok: false, reason: 'max' };
    const r = this.spend(state, this.eggCost(def, st.dinos));
    if (r.ok) st.dinos += 1;
    return r;
  }

  upgradeEntrance(state: ParkState): ActionResult {
    if (state.entranceLevel >= this.cfg.entranceMaxLevel) return { ok: false, reason: 'max' };
    const r = this.spend(state, this.entranceCost(state.entranceLevel));
    if (r.ok) state.entranceLevel += 1;
    return r;
  }

  /** Plots open in order: a plot can be bought once the previous one is owned. */
  isPlotAvailable(state: ParkState, id: string): boolean {
    const idx = this.park.enclosures.findIndex((e) => e.id === id);
    if (idx <= 0) return true;
    return state.enclosures[this.park.enclosures[idx - 1].id].level > 0;
  }

  /** Levels at which the next income doubling happens, for the progress bar. */
  milestoneProgress(level: number): { from: number; to: number; ratio: number } {
    const every = this.cfg.milestoneEvery;
    const from = Math.floor(level / every) * every;
    const to = from + every;
    return { from, to, ratio: (level - from) / every };
  }
}
