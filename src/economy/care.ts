// Dino care rules (hunger, happiness, cleanliness). Pure and deterministic — the game feeds it time,
// the renderer shows it. Care only changes while the game is open; offline earnings ignore it.

import type { InteractionConfig } from '../data/parks';

export interface CareState {
  /** 1 = just fed, 0 = starving. */
  food: number;
  /** Epoch ms until which the herd is happy (income bonus). */
  happyUntil: number;
  poops: number;
}

export const freshCare = (): CareState => ({ food: 1, happyUntil: 0, poops: 0 });

export function tickCare(c: CareState, dt: number, cfg: InteractionConfig): void {
  c.food = Math.max(0, c.food - dt / cfg.hungerSeconds);
}

export const isHungry = (c: CareState, cfg: InteractionConfig) => c.food < cfg.hungryBelow;
export const isHappy = (c: CareState, now: number) => c.happyUntil > now;
export const isDirty = (c: CareState, cfg: InteractionConfig) => c.poops >= cfg.maxPoops;

/** Ticket multiplier from care. Happy overrides hunger (they were just fed). */
export function careMultiplier(c: CareState, now: number, cfg: InteractionConfig): number {
  let m = 1;
  if (isHappy(c, now)) m *= cfg.happyMultiplier;
  else if (isHungry(c, cfg)) m *= cfg.hungryMultiplier;
  if (isDirty(c, cfg)) m *= cfg.dirtyMultiplier;
  return m;
}

/** Feeding is always allowed; it refills food and starts (or refreshes) the happy bonus. */
export function feed(c: CareState, now: number, cfg: InteractionConfig): void {
  c.food = 1;
  c.happyUntil = now + cfg.happySeconds * 1000;
}

export function addPoop(c: CareState, cfg: InteractionConfig): boolean {
  if (c.poops >= cfg.maxPoops) return false;
  c.poops++;
  return true;
}

export function cleanPoop(c: CareState): boolean {
  if (c.poops <= 0) return false;
  c.poops--;
  return true;
}

/** Interaction reward: N seconds of current income, never below the floor. */
export function rewardFor(incomePerSecond: number, seconds: number, cfg: InteractionConfig): number {
  return Math.max(cfg.minReward, Math.round(incomePerSecond * seconds));
}
