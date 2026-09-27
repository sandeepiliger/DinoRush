/** Light haptic feedback where the platform supports it (Android Chrome / WebView). */
export const haptics = {
  enabled: true,
  tap(ms = 12) {
    if (!this.enabled) return;
    try {
      navigator.vibrate?.(ms);
    } catch {
      /* unsupported */
    }
  },
};
