/** Tiny DOM helpers — enough for a game UI without pulling in a framework. */

export function el<K extends keyof HTMLElementTagNameMap>(tag: K, className = '', html = ''): HTMLElementTagNameMap[K] {
  const e = document.createElement(tag);
  if (className) e.className = className;
  if (html) e.innerHTML = html;
  return e;
}

export function $(id: string): HTMLElement {
  const e = document.getElementById(id);
  if (!e) throw new Error(`Missing #${id}`);
  return e;
}

/**
 * Calls `fn` on tap, and repeatedly (accelerating) while the button is held — the standard idle-game
 * "hold to upgrade" feel.
 */
export function holdToRepeat(button: HTMLElement, fn: () => boolean): void {
  let timer: number | null = null;
  let delay = 0;
  const stop = () => {
    if (timer !== null) clearTimeout(timer);
    timer = null;
  };
  const tick = () => {
    if (!fn()) return stop();
    delay = Math.max(45, delay * 0.8);
    timer = window.setTimeout(tick, delay);
  };
  button.addEventListener('pointerdown', (e) => {
    e.preventDefault();
    stop();
    if (!fn()) return;
    delay = 380;
    timer = window.setTimeout(tick, delay);
  });
  for (const ev of ['pointerup', 'pointerleave', 'pointercancel'] as const) button.addEventListener(ev, stop);
}
