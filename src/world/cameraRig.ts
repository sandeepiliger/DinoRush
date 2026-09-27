// Touch-first camera: one-finger drag pans (with inertia), pinch / wheel zooms, short tap selects.

import * as THREE from 'three';

export interface CameraLimits {
  minX: number;
  maxX: number;
  minZ: number;
  maxZ: number;
  minDist: number;
  maxDist: number;
}

export class CameraRig {
  readonly camera: THREE.PerspectiveCamera;
  target = new THREE.Vector2(0, 4);
  distance = 24;
  private pitch = THREE.MathUtils.degToRad(52);
  private vel = new THREE.Vector2();
  private pointers = new Map<number, { x: number; y: number; sx: number; sy: number; t: number }>();
  private pinchStart = 0;
  private pinchDist = 0;
  private dragging = false;
  private focusGoal: { x: number; z: number; d: number } | null = null;
  private shake = 0;
  onTap: (clientX: number, clientY: number) => void = () => {};

  constructor(
    private el: HTMLElement,
    private limits: CameraLimits,
  ) {
    this.camera = new THREE.PerspectiveCamera(36, 1, 0.5, 200);
    el.addEventListener('pointerdown', this.down);
    el.addEventListener('pointermove', this.move);
    el.addEventListener('pointerup', this.up);
    el.addEventListener('pointercancel', this.up);
    el.addEventListener('wheel', this.wheel, { passive: false });
    this.apply();
  }

  resize(w: number, h: number) {
    this.camera.aspect = w / h;
    // Portrait phones see less width; pull back so the park stays readable.
    this.camera.fov = w < h ? 50 : 36;
    this.camera.updateProjectionMatrix();
  }

  /** Smoothly glide to look at a point (used when unlocking or opening an enclosure). */
  focus(x: number, z: number, distance = this.distance) {
    this.focusGoal = { x, z, d: distance };
    this.vel.set(0, 0);
  }

  addShake(amount: number) {
    this.shake = Math.min(0.6, this.shake + amount);
  }

  update(dt: number) {
    if (this.focusGoal) {
      const k = 1 - Math.exp(-dt * 4);
      this.target.x += (this.focusGoal.x - this.target.x) * k;
      this.target.y += (this.focusGoal.z - this.target.y) * k;
      this.distance += (this.focusGoal.d - this.distance) * k;
      if (Math.abs(this.focusGoal.x - this.target.x) + Math.abs(this.focusGoal.z - this.target.y) < 0.02) this.focusGoal = null;
    } else if (!this.dragging) {
      this.target.addScaledVector(this.vel, dt);
      this.vel.multiplyScalar(Math.exp(-dt * 5));
    }
    this.clamp();
    this.shake = Math.max(0, this.shake - dt * 1.5);
    this.apply();
  }

  private apply() {
    const cam = this.camera;
    const y = Math.sin(this.pitch) * this.distance;
    const back = Math.cos(this.pitch) * this.distance;
    const sx = this.shake > 0 ? (Math.random() - 0.5) * this.shake * 0.4 : 0;
    const sy = this.shake > 0 ? (Math.random() - 0.5) * this.shake * 0.4 : 0;
    cam.position.set(this.target.x + sx, y + sy, this.target.y + back);
    cam.lookAt(this.target.x + sx, 0, this.target.y);
  }

  private clamp() {
    const l = this.limits;
    this.target.x = THREE.MathUtils.clamp(this.target.x, l.minX, l.maxX);
    this.target.y = THREE.MathUtils.clamp(this.target.y, l.minZ, l.maxZ);
    this.distance = THREE.MathUtils.clamp(this.distance, l.minDist, l.maxDist);
  }

  /** World units per screen pixel at the target, for 1:1 drag feel. */
  private unitsPerPixel() {
    const h = this.el.clientHeight || 1;
    return (2 * this.distance * Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2))) / h;
  }

  private down = (e: PointerEvent) => {
    this.el.setPointerCapture(e.pointerId);
    this.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY, sx: e.clientX, sy: e.clientY, t: performance.now() });
    this.focusGoal = null;
    this.vel.set(0, 0);
    if (this.pointers.size === 2) {
      const [a, b] = [...this.pointers.values()];
      this.pinchStart = Math.hypot(a.x - b.x, a.y - b.y);
      this.pinchDist = this.distance;
    }
  };

  private move = (e: PointerEvent) => {
    const p = this.pointers.get(e.pointerId);
    if (!p) return;
    const dx = e.clientX - p.x;
    const dy = e.clientY - p.y;
    p.x = e.clientX;
    p.y = e.clientY;
    if (this.pointers.size === 2) {
      const [a, b] = [...this.pointers.values()];
      const d = Math.hypot(a.x - b.x, a.y - b.y);
      if (this.pinchStart > 0) this.distance = this.pinchDist * (this.pinchStart / Math.max(1, d));
      this.dragging = true;
      return;
    }
    if (!this.dragging && Math.hypot(e.clientX - p.sx, e.clientY - p.sy) > 8) this.dragging = true;
    if (this.dragging) {
      const u = this.unitsPerPixel();
      // Screen-up drag moves the view "into" the park, like dragging a map.
      const mx = -dx * u;
      const mz = (-dy * u) / Math.sin(this.pitch);
      this.target.x += mx;
      this.target.y += mz;
      const dt = Math.max(1, e.timeStamp - (p.t || e.timeStamp)) / 1000;
      p.t = e.timeStamp;
      this.vel.set(mx / dt, mz / dt).clampLength(0, 40);
    }
  };

  private up = (e: PointerEvent) => {
    const p = this.pointers.get(e.pointerId);
    this.pointers.delete(e.pointerId);
    if (p && !this.dragging && this.pointers.size === 0) this.onTap(e.clientX, e.clientY);
    if (this.pointers.size === 0) this.dragging = false;
    if (this.pointers.size < 2) this.pinchStart = 0;
  };

  private wheel = (e: WheelEvent) => {
    e.preventDefault();
    this.distance *= Math.exp(e.deltaY * 0.0012);
    this.focusGoal = null;
  };
}
