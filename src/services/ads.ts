// Ad abstraction. Gameplay only ever calls AdManager; the provider behind it is swappable.
// Real provider (AdMob via a Capacitor plugin) is a later milestone — until then MockAdProvider
// simulates ads so every rewarded flow can be built and tested. Never put ad unit IDs in code.

import { analytics } from './analytics';

export type RewardType = 'income_boost' | 'offline_double' | 'free_upgrade';

export interface IAdProvider {
  readonly name: string;
  init(): Promise<void>;
  isRewardedReady(): boolean;
  /** Resolves true only if the user watched to the end and earned the reward. */
  showRewarded(): Promise<boolean>;
  showInterstitial(): Promise<void>;
}

/** Development stand-in: shows a clearly labelled fake ad overlay for 2 seconds. */
export class MockAdProvider implements IAdProvider {
  readonly name = 'mock';
  async init() {}
  isRewardedReady() {
    return true;
  }
  showRewarded(): Promise<boolean> {
    return new Promise((resolve) => {
      const el = document.createElement('div');
      el.className = 'mock-ad';
      el.innerHTML = '<div class="mock-ad-box"><div class="mock-ad-tag">TEST AD</div><div class="mock-ad-count">2</div><div class="mock-ad-note">Development placeholder — no real ad is shown</div></div>';
      document.body.appendChild(el);
      let n = 2;
      const count = el.querySelector('.mock-ad-count')!;
      const timer = setInterval(() => {
        n--;
        count.textContent = String(n);
        if (n <= 0) {
          clearInterval(timer);
          el.remove();
          resolve(true);
        }
      }, 1000);
    });
  }
  async showInterstitial() {}
}

export class AdManager {
  private busy = false;
  private lastRewardedAt = 0;
  removeAds = false;

  constructor(private provider: IAdProvider) {}

  async init(): Promise<void> {
    try {
      await this.provider.init();
    } catch {
      // Ads failing to initialise must never block the game.
    }
  }

  canShowRewarded(): boolean {
    return !this.busy && this.provider.isRewardedReady();
  }

  async showRewarded(reward: RewardType): Promise<boolean> {
    if (this.busy) return false;
    this.busy = true;
    analytics.track('rewarded_ad_offered', { reward });
    try {
      const ok = await this.provider.showRewarded();
      if (ok) {
        this.lastRewardedAt = Date.now();
        analytics.track('rewarded_ad_completed', { reward });
      }
      return ok;
    } catch {
      return false;
    } finally {
      this.busy = false;
    }
  }

  /** Interstitials respect Remove Ads and never follow right after a rewarded ad. */
  async maybeShowInterstitial(): Promise<void> {
    if (this.removeAds || this.busy || Date.now() - this.lastRewardedAt < 90_000) return;
    try {
      await this.provider.showInterstitial();
      analytics.track('interstitial_shown');
    } catch {
      /* ignore */
    }
  }
}
