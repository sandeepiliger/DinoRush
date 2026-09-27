// UI text lives here, keyed, so localisation is a matter of adding tables — not editing UI code.

const en = {
  perSec: '/sec',
  upgrade: 'Upgrade',
  hatch: 'Hatch egg',
  unlock: 'Unlock',
  maxed: 'MAX',
  level: 'Lv {n}',
  ticket: 'Ticket',
  dinos: 'Dinos',
  visitors: 'Visitors',
  entryFee: 'Entry fee',
  entrance: 'Entrance',
  entranceSub: 'More visitors, higher entry fee',
  nextMilestone: 'Next milestone: Lv {n}',
  milestoneReward: 'x{m} income',
  unlockFirst: 'Unlock {name} first',
  notEnough: 'Not enough coins',
  newDino: 'New dinosaur!',
  hatched: 'A new {name} hatched!',
  milestone: 'Milestone!',
  boost: '2x Income',
  boostWatch: 'Watch ad',
  boostActive: 'Boost active',
  welcomeBack: 'Welcome back!',
  offlineEarned: 'Your park earned this while you were away ({t})',
  collect: 'Collect',
  collectDouble: 'Collect x2',
  settings: 'Settings',
  sound: 'Sound effects',
  music: 'Music',
  haptics: 'Vibration',
  resetProgress: 'Reset progress',
  resetConfirm: 'Reset all progress? This cannot be undone.',
  tutorialUpgrade: 'Tap to upgrade!',
  close: 'Close',
  boostStarted: 'Income doubled for {t}!',
  adFailed: 'Ad not available right now — try again soon',
  saveRecovered: 'Your save was repaired',
};

export type StringKey = keyof typeof en;

export function t(key: StringKey, params: Record<string, string | number> = {}): string {
  return en[key].replace(/\{(\w+)\}/g, (_, k: string) => String(params[k] ?? ''));
}
