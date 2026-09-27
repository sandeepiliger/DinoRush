// The quest line for the first park. Order matters: early quests teach one mechanic each.
// Adding a quest = adding a row; no code changes needed.

export type QuestKind = 'level' | 'dinos' | 'unlock' | 'entrance' | 'feed' | 'clean' | 'tip' | 'gift' | 'roar';

export interface QuestDef {
  kind: QuestKind;
  /** Enclosure id for level / dinos / unlock quests. */
  enclosure?: string;
  target: number;
  /** Fixed coin reward... */
  reward?: number;
  /** ...or a reward worth this many seconds of income at claim time (used by repeatable quests). */
  rewardSeconds?: number;
}

export const QUESTS: QuestDef[] = [
  { kind: 'level', enclosure: 'raptor-pen', target: 3, reward: 30 },
  { kind: 'tip', target: 1, reward: 40 },
  { kind: 'entrance', target: 3, reward: 60 },
  { kind: 'feed', target: 1, reward: 80 },
  { kind: 'level', enclosure: 'raptor-pen', target: 10, reward: 150 },
  { kind: 'clean', target: 3, reward: 150 },
  { kind: 'unlock', enclosure: 'trike-field', target: 1, reward: 200 },
  { kind: 'dinos', enclosure: 'raptor-pen', target: 2, reward: 300 },
  { kind: 'gift', target: 1, reward: 500 },
  { kind: 'level', enclosure: 'trike-field', target: 10, reward: 1_500 },
  { kind: 'tip', target: 10, reward: 1_500 },
  { kind: 'entrance', target: 10, reward: 3_000 },
  { kind: 'unlock', enclosure: 'stego-grove', target: 1, reward: 5_000 },
  { kind: 'feed', target: 10, reward: 6_000 },
  { kind: 'level', enclosure: 'stego-grove', target: 10, reward: 15_000 },
  { kind: 'dinos', enclosure: 'trike-field', target: 3, reward: 20_000 },
  { kind: 'roar', target: 5, reward: 20_000 },
  { kind: 'unlock', enclosure: 'brachio-heights', target: 1, reward: 60_000 },
  { kind: 'level', enclosure: 'raptor-pen', target: 40, reward: 100_000 },
  { kind: 'level', enclosure: 'brachio-heights', target: 10, reward: 300_000 },
  { kind: 'unlock', enclosure: 'rex-arena', target: 1, reward: 1_000_000 },
  { kind: 'level', enclosure: 'rex-arena', target: 10, reward: 3_000_000 },
];

/** After the scripted line, these repeat forever with growing targets. */
export const REPEATABLE: QuestDef[] = [
  { kind: 'tip', target: 15, rewardSeconds: 90 },
  { kind: 'feed', target: 8, rewardSeconds: 90 },
  { kind: 'clean', target: 10, rewardSeconds: 90 },
  { kind: 'gift', target: 2, rewardSeconds: 120 },
];
