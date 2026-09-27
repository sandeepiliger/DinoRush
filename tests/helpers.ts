import { Economy, type ParkState } from '../src/economy/economy';
import { ECONOMY, PARKS } from '../src/data/parks';

export const makeEconomy = () => new Economy(PARKS[0], ECONOMY);

type Action = { kind: 'unlock' | 'upgrade' | 'hatch' | 'entrance'; id?: string; cost: number; gain: number };

/** Every action currently purchasable-in-principle, with its income gain per second. */
export function candidateActions(eco: Economy, s: ParkState): Action[] {
  const out: Action[] = [];
  const base = eco.incomePerSecond(s, 0);
  const tryGain = (mutate: (c: ParkState) => void) => {
    const c = structuredClone(s);
    mutate(c);
    return eco.incomePerSecond(c, 0) - base;
  };
  for (const def of eco.park.enclosures) {
    const st = s.enclosures[def.id];
    if (st.level === 0) {
      if (eco.isPlotAvailable(s, def.id))
        out.push({ kind: 'unlock', id: def.id, cost: def.unlockCost, gain: tryGain((c) => (c.enclosures[def.id] = { level: 1, dinos: 1 })) });
      continue;
    }
    if (st.level < eco.cfg.maxLevel)
      out.push({ kind: 'upgrade', id: def.id, cost: eco.upgradeCost(def, st.level), gain: tryGain((c) => c.enclosures[def.id].level++) });
    if (st.dinos < def.maxDinos)
      out.push({ kind: 'hatch', id: def.id, cost: eco.eggCost(def, st.dinos), gain: tryGain((c) => c.enclosures[def.id].dinos++) });
  }
  if (s.entranceLevel < eco.cfg.entranceMaxLevel)
    out.push({ kind: 'entrance', cost: eco.entranceCost(s.entranceLevel), gain: tryGain((c) => c.entranceLevel++) });
  return out;
}

/**
 * Simulates a reasonably smart active player: always saves for the next plot when that pays back
 * faster than anything else, otherwise buys the best income-per-coin action. Returns the time (s)
 * each enclosure was unlocked.
 */
export function simulateGreedyPlayer(eco: Economy, maxSeconds: number, step = 1) {
  const s = eco.createInitialState(0);
  const unlockedAt: Record<string, number> = {};
  for (const def of eco.park.enclosures) if (s.enclosures[def.id].level > 0) unlockedAt[def.id] = 0;
  let t = 0;
  for (; t < maxSeconds; t += step) {
    eco.earn(s, eco.incomePerSecond(s, 0) * step);
    for (let guard = 0; guard < 50; guard++) {
      const acts = candidateActions(eco, s);
      if (acts.length === 0) break;
      const income = Math.max(1e-9, eco.incomePerSecond(s, 0));
      // Score = seconds to afford + seconds to pay back. Lower is better.
      let best = acts[0];
      let bestScore = Infinity;
      for (const a of acts) {
        if (a.gain <= 0) continue;
        const wait = Math.max(0, (a.cost - s.coins) / income);
        const score = wait + a.cost / a.gain;
        if (score < bestScore) { bestScore = score; best = a; }
      }
      if (best.cost > s.coins) break;
      const r = best.kind === 'unlock' ? eco.unlock(s, best.id!)
        : best.kind === 'upgrade' ? eco.upgrade(s, best.id!)
        : best.kind === 'hatch' ? eco.hatch(s, best.id!)
        : eco.upgradeEntrance(s);
      if (!r.ok) break;
      if (best.kind === 'unlock') unlockedAt[best.id!] = t;
    }
    if (Object.keys(unlockedAt).length === eco.park.enclosures.length) break;
  }
  return { unlockedAt, state: s, seconds: t };
}
