import { describe, expect, it } from 'vitest';
import { QUESTS, REPEATABLE } from '../src/data/quests';
import { claimQuest, questAt, questComplete, recordQuestEvent, type QuestProgress } from '../src/economy/quests';
import { makeEconomy } from './helpers';

describe('quests', () => {
  it('every scripted quest references a real enclosure', () => {
    const eco = makeEconomy();
    const ids = new Set(eco.park.enclosures.map((e) => e.id));
    for (const q of QUESTS) if (q.enclosure) expect(ids.has(q.enclosure)).toBe(true);
  });

  it('state quests complete from the park, then claim once', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    const p: QuestProgress = { index: 0, count: 0 };
    const q = questAt(0);
    expect(questComplete(q, p, s)).toBe(false);
    expect(claimQuest(p, s, 10)).toBe(0);
    s.enclosures[q.enclosure!].level = q.target;
    expect(claimQuest(p, s, 10)).toBe(q.reward);
    expect(p).toEqual({ index: 1, count: 0 });
  });

  it('counter quests only count matching events, capped at the target', () => {
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    const p: QuestProgress = { index: 1, count: 0 }; // "tip 1"
    expect(recordQuestEvent('feed', p)).toBe(false);
    expect(recordQuestEvent('tip', p)).toBe(true);
    expect(recordQuestEvent('tip', p)).toBe(false);
    expect(claimQuest(p, s, 0)).toBe(QUESTS[1].reward);
  });

  it('repeatable quests continue forever with growing targets and income-based rewards', () => {
    const first = questAt(QUESTS.length);
    const later = questAt(QUESTS.length + REPEATABLE.length);
    expect(first.kind).toBe(later.kind);
    expect(later.target).toBeGreaterThan(first.target);
    const eco = makeEconomy();
    const s = eco.createInitialState(0);
    const p: QuestProgress = { index: QUESTS.length, count: first.target };
    expect(claimQuest(p, s, 100)).toBe(100 * first.rewardSeconds!);
  });
});
