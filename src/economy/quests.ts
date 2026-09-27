// Quest progression. Pure: "state" quests read the park, "counter" quests count events.

import { QUESTS, REPEATABLE, type QuestDef, type QuestKind } from '../data/quests';
import type { ParkState } from './economy';

export interface QuestProgress {
  index: number;
  /** Events counted towards the current counter quest. */
  count: number;
}

const COUNTER_KINDS: ReadonlySet<QuestKind> = new Set(['feed', 'clean', 'tip', 'gift', 'roar', 'hunt', 'sell', 'hatchWild', 'upgradeStat']);

/** Extra state some quests read (the hero's built pads). */
export interface QuestContext {
  built?: readonly string[];
}

export const isCounterQuest = (q: QuestDef) => COUNTER_KINDS.has(q.kind);

export function questAt(index: number, list = QUESTS, repeat = REPEATABLE): QuestDef {
  if (index < list.length) return list[index];
  const k = index - list.length;
  const base = repeat[k % repeat.length];
  const round = Math.floor(k / repeat.length);
  return { ...base, target: Math.round(base.target * (1 + round * 0.5)) };
}

export function questValue(q: QuestDef, p: QuestProgress, s: ParkState, ctx: QuestContext = {}): number {
  switch (q.kind) {
    case 'build':
      return ctx.built?.includes(q.pad!) ? 1 : 0;
    case 'level':
      return s.enclosures[q.enclosure!]?.level ?? 0;
    case 'dinos':
      return s.enclosures[q.enclosure!]?.dinos ?? 0;
    case 'unlock':
      return (s.enclosures[q.enclosure!]?.level ?? 0) > 0 ? 1 : 0;
    case 'entrance':
      return s.entranceLevel;
    default:
      return p.count;
  }
}

export function questComplete(q: QuestDef, p: QuestProgress, s: ParkState, ctx: QuestContext = {}): boolean {
  return questValue(q, p, s, ctx) >= q.target;
}

/** Records a gameplay event; only counts if the current quest is waiting for that kind. */
export function recordQuestEvent(kind: QuestKind, p: QuestProgress, list = QUESTS, repeat = REPEATABLE): boolean {
  const q = questAt(p.index, list, repeat);
  if (q.kind !== kind || !isCounterQuest(q) || p.count >= q.target) return false;
  p.count++;
  return true;
}

export function questReward(q: QuestDef, incomePerSecond: number): number {
  if (q.reward !== undefined) return q.reward;
  return Math.max(10, Math.round(incomePerSecond * (q.rewardSeconds ?? 60)));
}

/** Claims the current quest if complete. Returns the coin reward, or 0 if not claimable. */
export function claimQuest(
  p: QuestProgress,
  s: ParkState,
  incomePerSecond: number,
  ctx: QuestContext = {},
  list = QUESTS,
  repeat = REPEATABLE,
): number {
  const q = questAt(p.index, list, repeat);
  if (!questComplete(q, p, s, ctx)) return 0;
  p.index++;
  p.count = 0;
  return questReward(q, incomePerSecond);
}
