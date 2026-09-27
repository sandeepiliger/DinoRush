import { describe, expect, it } from 'vitest';
import { WILD } from '../src/data/wild';
import {
  cargoCapacity, depositToPad, freshHero, hatchNestEgg, isBuilt, packCapacity, pickUpMeat, sellMeat, statCost,
} from '../src/economy/hero';

describe('hero', () => {
  it('starts with the free pads built and an empty back', () => {
    const h = freshHero(WILD);
    expect(h.meat).toBe(0);
    for (const p of WILD.pads) expect(isBuilt(h, p.id)).toBe(p.buildMeat === 0);
  });

  it('never carries more than capacity', () => {
    const h = freshHero(WILD);
    const cap = cargoCapacity(h, WILD);
    expect(pickUpMeat(h, cap + 5, WILD)).toBe(cap);
    expect(pickUpMeat(h, 1, WILD)).toBe(0);
    h.stats.cargo++;
    expect(pickUpMeat(h, 99, WILD)).toBe(WILD.stats.cargo.perLevel);
  });

  it('builds a pad after the required meat, one piece at a time', () => {
    const h = freshHero(WILD);
    const nest = WILD.pads.find((p) => p.kind === 'nest')!;
    h.stats.cargo = 10;
    pickUpMeat(h, nest.buildMeat, WILD);
    let built = false;
    for (let i = 0; i < nest.buildMeat; i++) built = depositToPad(h, nest.id, WILD).built;
    expect(built).toBe(true);
    expect(isBuilt(h, nest.id)).toBe(true);
    expect(depositToPad(h, nest.id, WILD).moved).toBe(false);
  });

  it('sells meat for income-scaled coins with a floor', () => {
    const h = freshHero(WILD);
    pickUpMeat(h, 2, WILD);
    expect(sellMeat(h, 0, WILD)).toBe(WILD.meatMinCoins);
    expect(sellMeat(h, 100, WILD)).toBe(100 * WILD.meatIncomeSeconds);
    expect(sellMeat(h, 100, WILD)).toBe(0);
  });

  it('only hatches a nest egg when the pack has room', () => {
    const h = freshHero(WILD);
    h.eggReady = true;
    h.pack = packCapacity(h, WILD);
    expect(hatchNestEgg(h, WILD)).toBe(false);
    h.pack = 0;
    expect(hatchNestEgg(h, WILD)).toBe(true);
    expect(h.eggReady).toBe(false);
  });

  it('upgrade costs grow', () => {
    const h = freshHero(WILD);
    const c0 = statCost(h, 'cargo', WILD);
    h.stats.cargo++;
    expect(statCost(h, 'cargo', WILD)).toBeGreaterThan(c0);
  });
});
