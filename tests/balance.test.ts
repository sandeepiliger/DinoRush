import { describe, expect, it } from 'vitest';
import { formatDuration } from '../src/core/format';
import { makeEconomy, simulateGreedyPlayer } from './helpers';

// Pacing guarantees for the first park. If a balance change breaks these, the change needs a
// deliberate decision, not a silent test edit.
describe('Balance (greedy active player)', () => {
  const eco = makeEconomy();
  const { unlockedAt } = simulateGreedyPlayer(eco, 4 * 3600);
  const [, trike, stego, brachio, rex] = eco.park.enclosures.map((e) => e.id);
  const env = (globalThis as { process?: { env: Record<string, string | undefined> } }).process?.env;
  if (env?.BALANCE_REPORT) {
    console.table(Object.fromEntries(Object.entries(unlockedAt).map(([id, s]) => [id, formatDuration(s)])));
  }

  it('makes a first purchase possible within seconds', () => {
    const s = eco.createInitialState(0);
    const firstCost = eco.upgradeCost(eco.park.enclosures[0], 1);
    const secondsToAfford = Math.max(0, firstCost - s.coins) / eco.incomePerSecond(s, 0);
    expect(secondsToAfford).toBeLessThan(10);
  });

  it('unlocks the second species within 1–5 minutes', () => {
    expect(unlockedAt[trike]).toBeGreaterThan(60);
    expect(unlockedAt[trike]).toBeLessThan(5 * 60);
  });

  it('unlocks species in a steadily widening rhythm', () => {
    expect(unlockedAt[stego]).toBeLessThan(20 * 60);
    expect(unlockedAt[brachio]).toBeLessThan(45 * 60);
    expect(unlockedAt[rex]).toBeGreaterThan(30 * 60);
    expect(unlockedAt[rex]).toBeLessThan(2 * 3600);
  });
});
