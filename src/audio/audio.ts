// Every sound in the game is synthesised at runtime with the Web Audio API — no audio files to license,
// download or compress. Sounds are short and layered so they feel "juicy" rather than beepy.

type Voice =
  | 'coin' | 'upgrade' | 'unlock' | 'hatch' | 'roar' | 'click' | 'error' | 'reward' | 'milestone'
  | 'munch' | 'squish' | 'tip' | 'squawk' | 'claim' | 'whoosh' | 'bite' | 'faint';

export class AudioManager {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private sfxBus: GainNode | null = null;
  private musicBus: GainNode | null = null;
  private noise: AudioBuffer | null = null;
  private lastCoin = 0;
  private coinStep = 0;
  private musicTimer: number | null = null;
  sfxEnabled = true;
  musicEnabled = true;

  /** Browsers only allow audio after a user gesture; call this from the first tap. */
  unlock(): void {
    if (this.ctx) {
      if (this.ctx.state === 'suspended') void this.ctx.resume();
      return;
    }
    try {
      const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.ctx = new Ctx();
    } catch {
      return; // no audio support: the game stays silent, never crashes
    }
    const ctx = this.ctx;
    this.master = ctx.createGain();
    this.master.gain.value = 0.8;
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14;
    comp.ratio.value = 4;
    this.master.connect(comp).connect(ctx.destination);
    this.sfxBus = ctx.createGain();
    this.sfxBus.connect(this.master);
    this.musicBus = ctx.createGain();
    this.musicBus.gain.value = 0.22;
    this.musicBus.connect(this.master);
    const len = ctx.sampleRate;
    this.noise = ctx.createBuffer(1, len, ctx.sampleRate);
    const data = this.noise.getChannelData(0);
    for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    this.setMusic(this.musicEnabled);
  }

  setSfx(on: boolean) {
    this.sfxEnabled = on;
  }

  setMusic(on: boolean) {
    this.musicEnabled = on;
    if (!this.ctx) return;
    if (on && this.musicTimer === null) this.startMusic();
    if (!on && this.musicTimer !== null) {
      clearInterval(this.musicTimer);
      this.musicTimer = null;
    }
  }

  play(voice: Voice, intensity = 1): void {
    const ctx = this.ctx;
    if (!ctx || !this.sfxBus || !this.sfxEnabled) return;
    const t = ctx.currentTime;
    switch (voice) {
      case 'coin': {
        // Rate-limit and walk up a pentatonic scale so a stream of coins sounds musical, not noisy.
        if (t - this.lastCoin < 0.07) return;
        this.coinStep = t - this.lastCoin < 0.5 ? (this.coinStep + 1) % 5 : 0;
        this.lastCoin = t;
        const scale = [0, 2, 4, 7, 9];
        const f = 1318 * Math.pow(2, scale[this.coinStep] / 12);
        this.tone('square', f, t, 0.05, 0.05 * intensity);
        this.tone('sine', f * 1.5, t + 0.045, 0.12, 0.07 * intensity);
        break;
      }
      case 'click':
        this.tone('triangle', 660, t, 0.05, 0.12);
        this.tone('sine', 990, t + 0.01, 0.04, 0.06);
        break;
      case 'error':
        this.tone('square', 196, t, 0.1, 0.06, 150);
        this.tone('square', 147, t + 0.09, 0.14, 0.06, 110);
        break;
      case 'upgrade': {
        const notes = [523, 659, 784, 1047];
        notes.forEach((f, i) => this.tone('triangle', f, t + i * 0.05, 0.16, 0.12));
        this.sparkle(t + 0.15, 0.3);
        break;
      }
      case 'milestone':
      case 'unlock': {
        const notes = [392, 523, 659, 784, 1047, 1319];
        notes.forEach((f, i) => {
          this.tone('triangle', f, t + i * 0.07, 0.3, 0.12);
          this.tone('sine', f / 2, t + i * 0.07, 0.3, 0.08);
        });
        this.sparkle(t + 0.3, 0.8);
        this.thump(t, 0.5);
        break;
      }
      case 'reward': {
        [659, 880, 1175, 1568].forEach((f, i) => this.tone('sine', f, t + i * 0.08, 0.35, 0.12));
        this.sparkle(t, 0.9);
        break;
      }
      case 'hatch':
        this.noiseBurst(t, 0.12, 2500, 0.35, 'highpass');
        this.tone('sine', 880, t + 0.05, 0.12, 0.12, 1400);
        this.sparkle(t + 0.08, 0.5);
        break;
      case 'roar':
        this.roar(t, intensity);
        break;
      case 'munch':
        // Three crunchy chomps.
        for (let i = 0; i < 3; i++) {
          this.noiseBurst(t + i * 0.16, 0.07, 1400, 0.3, 'bandpass');
          this.tone('square', 110, t + i * 0.16, 0.06, 0.05, 70);
        }
        break;
      case 'squish':
        this.noiseBurst(t, 0.12, 500, 0.35, 'lowpass');
        this.tone('sine', 300, t, 0.14, 0.12, 90);
        this.tone('sine', 1400, t + 0.12, 0.09, 0.05, 2200);
        break;
      case 'tip':
        this.tone('sine', 1568, t, 0.08, 0.1);
        this.tone('sine', 2093, t + 0.07, 0.18, 0.1);
        this.sparkle(t + 0.05, 0.25);
        break;
      case 'squawk':
        this.tone('sawtooth', 900, t, 0.12, 0.06, 1500);
        this.tone('sawtooth', 1300, t + 0.12, 0.18, 0.05, 700);
        break;
      case 'bite':
        this.noiseBurst(t, 0.05, 2200, 0.25, 'bandpass');
        this.tone('square', 180, t, 0.07, 0.06, 90);
        break;
      case 'faint':
        this.tone('triangle', 660, t, 0.28, 0.1, 180);
        this.noiseBurst(t + 0.2, 0.15, 400, 0.2, 'lowpass');
        break;
      case 'whoosh':
        this.noiseBurst(t, 0.4, 700, 0.25, 'bandpass');
        break;
      case 'claim': {
        [523, 659, 784, 1047, 1319, 1568].forEach((f, i) => this.tone('triangle', f, t + i * 0.06, 0.25, 0.1));
        this.thump(t, 0.4);
        this.sparkle(t + 0.2, 0.7);
        break;
      }
    }
  }

  /** A roar: detuned sawtooth growl with falling pitch through a sweeping low-pass, plus breathy noise. */
  private roar(t: number, size: number) {
    const ctx = this.ctx!;
    const base = 120 / Math.max(0.6, size);
    const dur = 1.1;
    const out = ctx.createGain();
    out.gain.setValueAtTime(0.0001, t);
    out.gain.exponentialRampToValueAtTime(0.35, t + 0.08);
    out.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    const lp = ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.setValueAtTime(600, t);
    lp.frequency.linearRampToValueAtTime(1800, t + 0.25);
    lp.frequency.exponentialRampToValueAtTime(300, t + dur);
    lp.Q.value = 6;
    lp.connect(out).connect(this.sfxBus!);
    for (const detune of [-18, 0, 13]) {
      const o = ctx.createOscillator();
      o.type = 'sawtooth';
      o.frequency.setValueAtTime(base * 1.4, t);
      o.frequency.exponentialRampToValueAtTime(base, t + 0.3);
      o.frequency.exponentialRampToValueAtTime(base * 0.6, t + dur);
      o.detune.value = detune;
      // Growl: fast wobble on pitch.
      const lfo = ctx.createOscillator();
      const lfoGain = ctx.createGain();
      lfo.frequency.value = 28;
      lfoGain.gain.value = base * 0.08;
      lfo.connect(lfoGain).connect(o.frequency);
      o.connect(lp);
      o.start(t);
      lfo.start(t);
      o.stop(t + dur);
      lfo.stop(t + dur);
    }
    this.noiseBurst(t, dur * 0.9, 900, 0.18, 'bandpass');
  }

  private tone(type: OscillatorType, freq: number, t: number, dur: number, vol: number, endFreq?: number) {
    const ctx = this.ctx!;
    const o = ctx.createOscillator();
    const g = ctx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    if (endFreq) o.frequency.exponentialRampToValueAtTime(endFreq, t + dur);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + 0.008);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(this.sfxBus!);
    o.start(t);
    o.stop(t + dur + 0.02);
  }

  private sparkle(t: number, dur: number) {
    for (let i = 0; i < 6; i++) {
      this.tone('sine', 2000 + Math.random() * 2500, t + (i / 6) * dur * 0.6, 0.08, 0.035);
    }
  }

  private thump(t: number, vol: number) {
    this.tone('sine', 140, t, 0.25, vol, 50);
  }

  private noiseBurst(t: number, dur: number, freq: number, vol: number, type: BiquadFilterType) {
    const ctx = this.ctx!;
    const src = ctx.createBufferSource();
    src.buffer = this.noise;
    const f = ctx.createBiquadFilter();
    f.type = type;
    f.frequency.value = freq;
    const g = ctx.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(this.sfxBus!);
    src.start(t, Math.random() * 0.5);
    src.stop(t + dur);
  }

  /**
   * Light, looping marimba-style background music, generated on the fly: a I–vi–IV–V progression with
   * a gentle arpeggio. Scheduled a bar ahead so it never stutters when the main thread is busy.
   */
  private startMusic() {
    const ctx = this.ctx!;
    const bpm = 96;
    const beat = 60 / bpm;
    const chords = [
      [60, 64, 67], [57, 60, 64], [53, 57, 60], [55, 59, 62],
    ];
    const pattern = [0, 1, 2, 1, 0, 2, 1, 2];
    let bar = 0;
    let next = ctx.currentTime + 0.1;
    const midi = (n: number) => 440 * Math.pow(2, (n - 69) / 12);
    const scheduleBar = () => {
      const chord = chords[bar % chords.length];
      for (let i = 0; i < 8; i++) {
        const t = next + i * beat * 0.5;
        const note = chord[pattern[i]] + 12;
        this.musicNote(midi(note), t, beat * 0.9, 0.16);
      }
      this.musicNote(midi(chord[0] - 12), next, beat * 3.5, 0.22, 'sine');
      this.musicNote(midi(chord[0] - 12), next + beat * 2, beat * 1.5, 0.16, 'sine');
      next += beat * 4;
      bar++;
    };
    scheduleBar();
    this.musicTimer = window.setInterval(() => {
      if (!this.ctx) return;
      while (next < this.ctx.currentTime + beat * 4) scheduleBar();
    }, 500);
  }

  private musicNote(freq: number, t: number, dur: number, vol: number, type: OscillatorType = 'triangle') {
    const ctx = this.ctx!;
    const o = ctx.createOscillator();
    const g = ctx.createGain();
    o.type = type;
    o.frequency.value = freq;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(this.musicBus!);
    o.start(t);
    o.stop(t + dur + 0.05);
  }
}
