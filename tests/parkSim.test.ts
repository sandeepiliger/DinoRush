import { describe, expect, it } from 'vitest';
import { ParkSim } from '../src/sim/parkSim';
import { Polyline } from '../src/sim/polyline';
import { makeEconomy } from './helpers';

describe('Polyline', () => {
  it('samples along length', () => {
    const p = new Polyline([[0, 0], [0, 10], [10, 10]]);
    expect(p.length).toBe(20);
    const s = p.sample(15);
    expect(s.x).toBeCloseTo(5);
    expect(s.z).toBeCloseTo(10);
    expect(p.distanceAtZ(4, 0)).toBeCloseTo(4);
  });
});

describe('ParkSim', () => {
  it('has one stop per enclosure, in route order', () => {
    const eco = makeEconomy();
    const sim = new ParkSim(eco);
    expect(sim.stops.length).toBe(eco.park.enclosures.length);
    for (let i = 1; i < sim.stops.length; i++) expect(sim.stops[i].d).toBeGreaterThan(sim.stops[i - 1].d);
  });

  it('live visitor income converges to the analytic income rate', () => {
    const eco = makeEconomy();
    const state = eco.createInitialState(0);
    state.coins = 1e12;
    for (const def of eco.park.enclosures) eco.unlock(state, def.id);
    for (let i = 0; i < 10; i++) eco.upgradeEntrance(state);
    const sim = new ParkSim(eco, 48, 7);
    let earned = 0;
    const dt = 1 / 30;
    const warmup = 60;
    const measure = 600;
    for (let t = 0; t < warmup + measure; t += dt) {
      sim.update(dt, state, 0, (e) => { if (t >= warmup) earned += e.amount; });
    }
    const expected = eco.incomePerSecond(state, 0) * measure;
    expect(earned / expected).toBeGreaterThan(0.93);
    expect(earned / expected).toBeLessThan(1.07);
  });

  it('never loses income when the visitor pool is exhausted', () => {
    const eco = makeEconomy();
    const state = eco.createInitialState(0);
    state.entranceLevel = eco.cfg.entranceMaxLevel;
    const sim = new ParkSim(eco, 2, 1);
    let virtual = 0;
    for (let i = 0; i < 3000; i++) sim.update(1 / 30, state, 0, (e) => { if (!e.visitor) virtual++; });
    expect(virtual).toBeGreaterThan(0);
    expect(sim.activeCount()).toBeLessThanOrEqual(2);
  });

  it('only charges for unlocked enclosures', () => {
    const eco = makeEconomy();
    const state = eco.createInitialState(0);
    const sim = new ParkSim(eco);
    const paidAt = new Set<string>();
    for (let i = 0; i < 30 * 120; i++) sim.update(1 / 30, state, 0, (e) => e.enclosureId && paidAt.add(e.enclosureId));
    expect([...paidAt]).toEqual([eco.park.enclosures[0].id]);
  });
});
