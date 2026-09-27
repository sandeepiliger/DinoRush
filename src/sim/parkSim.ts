// Visitor simulation. Pure logic (no Three.js) so it can be tested headless; the renderer only reads
// visitor positions from here. Income from visitors converges to Economy.incomePerSecond.

import type { Economy, ParkState } from '../economy/economy';
import { Polyline } from './polyline';

export interface Stop {
  /** Distance along the route where visitors pause. */
  d: number;
  enclosureId: string;
  /** Point the visitor looks at while paused. */
  lookX: number;
  lookZ: number;
}

export interface Visitor {
  active: boolean;
  id: number;
  d: number;
  speed: number;
  offset: number;
  pause: number;
  nextStop: number;
  paidEntry: boolean;
  x: number;
  z: number;
  heading: number;
  walking: boolean;
  /** 0..1, randomised per visitor for outfit colours and height. */
  look: number;
  /** Walk-cycle phase, advanced by distance so feet match speed. */
  phase: number;
  /** Set when the visitor just paid; the renderer pops a coin and clears it. */
  paidFlash: number;
}

export type PayKind = 'entry' | 'ticket';
export interface PayEvent {
  visitor: Visitor | null;
  amount: number;
  kind: PayKind;
  enclosureId?: string;
}

export const ROUTE_POINTS: [number, number][] = [
  [0.55, 14],
  [0.55, 9],
  [0.65, -9.1],
  [-0.65, -9.1],
  [-0.55, 9],
  [-0.55, 14],
];
/** Where the gate is along the route: visitors pay the entry fee here. */
const GATE_Z = 9.5;
const PAUSE_SECONDS = 1.4;

export class ParkSim {
  readonly route = new Polyline(ROUTE_POINTS);
  readonly visitors: Visitor[] = [];
  readonly stops: Stop[];
  private spawnAcc = 0;
  private nextId = 1;
  private rng: () => number;
  private gateD: number;

  constructor(
    private economy: Economy,
    poolSize = 48,
    seed = 12345,
  ) {
    this.rng = mulberry32(seed);
    for (let i = 0; i < poolSize; i++) this.visitors.push(blankVisitor());
    this.gateD = this.route.distanceAtZ(GATE_Z, 0);
    this.stops = this.buildStops();
  }

  private buildStops(): Stop[] {
    const stops: Stop[] = [];
    for (const def of this.economy.park.enclosures) {
      // Right-hand enclosures are viewed on the way down (segment 1), left-hand ones on the way back
      // up (segment 3), and the centre arena from the turn at the far end (segment 2).
      let d: number;
      if (Math.abs(def.x) < 1) d = this.route.segmentStart(2) + this.route.segmentLength(2) / 2;
      else d = this.route.distanceAtZ(def.z, def.x > 0 ? 1 : 3);
      stops.push({ d, enclosureId: def.id, lookX: def.x, lookZ: def.z });
    }
    return stops.sort((a, b) => a.d - b.d);
  }

  update(dt: number, state: ParkState, now: number, onPay: (e: PayEvent) => void): void {
    const eco = this.economy;
    const rate = eco.visitorRate(state.entranceLevel);
    const boost = eco.boostMultiplier(state, now);
    this.spawnAcc += dt * rate;
    while (this.spawnAcc >= 1) {
      this.spawnAcc -= 1;
      state.totalVisitors += 1;
      if (!this.spawn()) {
        // Pool exhausted: a "virtual" visitor pays everything at once so income stays correct.
        onPay({ visitor: null, amount: eco.incomePerVisitor(state) * boost, kind: 'entry' });
      }
    }

    const total = this.route.length;
    for (const v of this.visitors) {
      if (!v.active) continue;
      if (v.paidFlash > 0) v.paidFlash = Math.max(0, v.paidFlash - dt * 2.5);
      if (v.pause > 0) {
        v.pause -= dt;
        v.walking = false;
      } else {
        v.walking = true;
        v.d += v.speed * dt;
        v.phase += v.speed * dt * 2.6;
        if (!v.paidEntry && v.d >= this.gateD) {
          v.paidEntry = true;
          v.paidFlash = 1;
          onPay({ visitor: v, amount: eco.entryFee(state.entranceLevel) * boost, kind: 'entry' });
        }
        while (v.nextStop < this.stops.length && v.d >= this.stops[v.nextStop].d) {
          const stop = this.stops[v.nextStop++];
          const def = eco.enclosure(stop.enclosureId);
          const ticket = eco.ticketFor(def, state.enclosures[def.id]);
          if (ticket > 0) {
            v.d = stop.d;
            v.pause = PAUSE_SECONDS * (0.8 + this.rng() * 0.5);
            v.walking = false;
            v.paidFlash = 1;
            v.heading = Math.atan2(stop.lookX - v.x, stop.lookZ - v.z);
            onPay({ visitor: v, amount: ticket * boost, kind: 'ticket', enclosureId: def.id });
            break;
          }
        }
        if (v.d >= total) {
          v.active = false;
          continue;
        }
        const p = this.route.sample(v.d);
        v.x = p.x + p.nx * v.offset;
        v.z = p.z + p.nz * v.offset;
        v.heading = Math.atan2(p.tx, p.tz);
      }
    }
  }

  activeCount(): number {
    let n = 0;
    for (const v of this.visitors) if (v.active) n++;
    return n;
  }

  private spawn(): boolean {
    const v = this.visitors.find((x) => !x.active);
    if (!v) return false;
    Object.assign(v, blankVisitor());
    v.active = true;
    v.id = this.nextId++;
    v.speed = 1.5 + this.rng() * 0.5;
    v.offset = (this.rng() - 0.5) * 0.5;
    v.look = this.rng();
    v.phase = this.rng() * Math.PI * 2;
    const p = this.route.sample(0);
    v.x = p.x + p.nx * v.offset;
    v.z = p.z;
    return true;
  }
}

function blankVisitor(): Visitor {
  return {
    active: false, id: 0, d: 0, speed: 1.6, offset: 0, pause: 0, nextStop: 0, paidEntry: false,
    x: 0, z: 0, heading: 0, walking: true, look: 0, phase: 0, paidFlash: 0,
  };
}

/** Small, fast, seedable PRNG — deterministic runs make simulation bugs reproducible. */
export function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
