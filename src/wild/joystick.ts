// Floating virtual joystick: touch/drag anywhere on the game view to steer; the stick appears under the
// finger. Keyboard (WASD / arrows) also works for desktop testing.

import { el } from '../ui/dom';

const RADIUS = 56;

export class Joystick {
  /** Output in screen space: x right, y down; magnitude 0..1. */
  readonly vec = { x: 0, y: 0 };
  private pointerId: number | null = null;
  private ox = 0;
  private oy = 0;
  private base: HTMLElement;
  private knob: HTMLElement;
  private keys = new Set<string>();
  /** True while a finger is steering (used to suppress tap handling). */
  active = false;

  constructor(target: HTMLElement, layer: HTMLElement) {
    this.base = el('div', 'joy-base');
    this.knob = el('div', 'joy-knob');
    this.base.appendChild(this.knob);
    this.base.hidden = true;
    layer.appendChild(this.base);
    target.addEventListener('pointerdown', this.down);
    window.addEventListener('pointermove', this.move);
    window.addEventListener('pointerup', this.up);
    window.addEventListener('pointercancel', this.up);
    window.addEventListener('keydown', (e) => this.keys.add(e.key.toLowerCase()));
    window.addEventListener('keyup', (e) => this.keys.delete(e.key.toLowerCase()));
    window.addEventListener('blur', () => this.keys.clear());
  }

  /** Called each frame: uses the keyboard when no finger is steering. */
  poll(): void {
    if (this.pointerId !== null) return;
    const k = this.keys;
    const x = (k.has('d') || k.has('arrowright') ? 1 : 0) - (k.has('a') || k.has('arrowleft') ? 1 : 0);
    const y = (k.has('s') || k.has('arrowdown') ? 1 : 0) - (k.has('w') || k.has('arrowup') ? 1 : 0);
    const len = Math.hypot(x, y) || 1;
    this.vec.x = x / len;
    this.vec.y = y / len;
  }

  /** Stops steering (e.g. when a second finger starts a pinch). */
  cancel(): void {
    this.pointerId = null;
    this.active = false;
    this.vec.x = this.vec.y = 0;
    this.base.hidden = true;
  }

  private down = (e: PointerEvent) => {
    if (this.pointerId !== null) {
      this.cancel(); // second finger: let the camera pinch instead
      return;
    }
    this.pointerId = e.pointerId;
    this.ox = e.clientX;
    this.oy = e.clientY;
  };

  private move = (e: PointerEvent) => {
    if (e.pointerId !== this.pointerId) return;
    const dx = e.clientX - this.ox;
    const dy = e.clientY - this.oy;
    const d = Math.hypot(dx, dy);
    if (!this.active && d > 10) {
      this.active = true;
      this.base.hidden = false;
      this.base.style.transform = `translate(${this.ox - RADIUS}px, ${this.oy - RADIUS}px)`;
    }
    if (!this.active) return;
    // Drag the stick's origin along when the finger goes far, so direction changes stay responsive.
    if (d > RADIUS * 1.6) {
      this.ox = e.clientX - (dx / d) * RADIUS * 1.6;
      this.oy = e.clientY - (dy / d) * RADIUS * 1.6;
      this.base.style.transform = `translate(${this.ox - RADIUS}px, ${this.oy - RADIUS}px)`;
    }
    const kx = e.clientX - this.ox;
    const ky = e.clientY - this.oy;
    const kd = Math.min(RADIUS, Math.hypot(kx, ky));
    const a = Math.atan2(ky, kx);
    this.knob.style.transform = `translate(${Math.cos(a) * kd}px, ${Math.sin(a) * kd}px)`;
    const mag = Math.min(1, Math.hypot(kx, ky) / RADIUS);
    this.vec.x = Math.cos(a) * mag;
    this.vec.y = Math.sin(a) * mag;
  };

  private up = (e: PointerEvent) => {
    if (e.pointerId !== this.pointerId) return;
    this.cancel();
  };
}
