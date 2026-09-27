const SUFFIXES = ['', 'K', 'M', 'B', 'T', 'aa', 'ab', 'ac', 'ad', 'ae'];

/** Short, idle-game style number formatting: 950, 1.2K, 34.5M, 1.00B. */
export function formatNumber(value: number): string {
  if (!Number.isFinite(value)) return '0';
  const sign = value < 0 ? '-' : '';
  let v = Math.abs(value);
  if (v < 1000) return sign + (v < 10 && v % 1 !== 0 ? v.toFixed(1) : Math.floor(v).toString());
  let tier = 0;
  while (v >= 1000 && tier < SUFFIXES.length - 1) {
    v /= 1000;
    tier++;
  }
  // Truncate rather than round so "999.95K" never displays as "1000K".
  const digits = v >= 100 ? 0 : v >= 10 ? 1 : 2;
  const factor = Math.pow(10, digits);
  const truncated = Math.floor(v * factor) / factor;
  return sign + truncated.toFixed(digits) + SUFFIXES[tier];
}

export function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m ${sec}s`;
  return `${sec}s`;
}
