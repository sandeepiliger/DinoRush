import { describe, expect, it } from 'vitest';
import { INTERACTION as C } from '../src/data/parks';
import { addPoop, careMultiplier, cleanPoop, feed, freshCare, isHungry, rewardFor, tickCare } from '../src/economy/care';

describe('care', () => {
  it('gets hungry only after enough play time', () => {
    const c = freshCare();
    tickCare(c, C.hungerSeconds * (1 - C.hungryBelow) - 1, C);
    expect(isHungry(c, C)).toBe(false);
    tickCare(c, 2, C);
    expect(isHungry(c, C)).toBe(true);
    expect(careMultiplier(c, 0, C)).toBeCloseTo(C.hungryMultiplier);
    tickCare(c, 1e6, C);
    expect(c.food).toBe(0);
  });

  it('feeding refills and makes the herd happy for a while', () => {
    const c = freshCare();
    c.food = 0;
    feed(c, 1000, C);
    expect(c.food).toBe(1);
    expect(careMultiplier(c, 1000, C)).toBeCloseTo(C.happyMultiplier);
    expect(careMultiplier(c, 1000 + C.happySeconds * 1000 + 1, C)).toBe(1);
  });

  it('poops cap out and make the enclosure dirty until cleaned', () => {
    const c = freshCare();
    for (let i = 0; i < C.maxPoops; i++) expect(addPoop(c, C)).toBe(true);
    expect(addPoop(c, C)).toBe(false);
    expect(careMultiplier(c, 0, C)).toBeCloseTo(C.dirtyMultiplier);
    expect(cleanPoop(c)).toBe(true);
    expect(careMultiplier(c, 0, C)).toBe(1);
    c.poops = 0;
    expect(cleanPoop(c)).toBe(false);
  });

  it('rewards scale with income and never drop below the floor', () => {
    expect(rewardFor(0, 10, C)).toBe(C.minReward);
    expect(rewardFor(100, 6, C)).toBe(600);
  });
});
