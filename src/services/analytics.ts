// Analytics abstraction. Game code calls analytics.track(); the provider decides where events go.
// A real backend (e.g. Firebase) plugs in later behind IAnalyticsProvider.

export type EventParams = Record<string, string | number | boolean>;

export interface IAnalyticsProvider {
  track(event: string, params?: EventParams): void;
}

class DebugAnalyticsProvider implements IAnalyticsProvider {
  track(event: string, params?: EventParams) {
    if (import.meta.env.DEV) console.debug('[analytics]', event, params ?? {});
  }
}

class AnalyticsManager {
  private provider: IAnalyticsProvider = new DebugAnalyticsProvider();

  setProvider(p: IAnalyticsProvider) {
    this.provider = p;
  }

  track(event: string, params?: EventParams) {
    try {
      this.provider.track(event, params);
    } catch {
      // Analytics must never break gameplay.
    }
  }
}

export const analytics = new AnalyticsManager();
