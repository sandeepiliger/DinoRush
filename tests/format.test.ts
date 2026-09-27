import { describe, expect, it } from 'vitest';
import { formatDuration, formatNumber } from '../src/core/format';

describe('formatNumber', () => {
  it.each([
    [0, '0'], [7, '7'], [2.5, '2.5'], [999, '999'], [1000, '1.00K'], [1234, '1.23K'],
    [99_999, '99.9K'], [999_999, '999K'], [1_500_000, '1.50M'], [2.5e9, '2.50B'], [-1500, '-1.50K'],
    [NaN, '0'], [Infinity, '0'],
  ])('%s -> %s', (v, s) => expect(formatNumber(v)).toBe(s));
});

describe('formatDuration', () => {
  it.each([[5, '5s'], [65, '1m 5s'], [3700, '1h 1m'], [-3, '0s']])('%s -> %s', (v, s) => expect(formatDuration(v)).toBe(s));
});
