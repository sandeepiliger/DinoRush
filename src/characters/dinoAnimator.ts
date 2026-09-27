// Procedural animation + wander behaviour for one dinosaur inside its enclosure.

import * as THREE from 'three';
import type { DinoRig } from './dinoBuilder';

export interface Bounds {
  minX: number;
  maxX: number;
  minZ: number;
  maxZ: number;
}

type Mode = 'hatching' | 'idle' | 'walk' | 'roar';

const TAU = Math.PI * 2;

export class DinoAnimator {
  x: number;
  z: number;
  heading: number;
  private mode: Mode = 'idle';
  private timer = 0;
  private targetX = 0;
  private targetZ = 0;
  private phase = 0;
  private time = Math.random() * 100;
  private speedNow = 0;
  private roarT = 0;
  private hatchT = 0;
  private lookYaw = 0;
  private lookTarget = 0;
  /** Blend 0..1 between idle pose and walk pose, smoothed to avoid pops. */
  private walkBlend = 0;

  constructor(
    readonly rig: DinoRig,
    private bounds: Bounds,
    private rng: () => number,
    private onRoar: (a: DinoAnimator) => void,
  ) {
    this.x = THREE.MathUtils.lerp(bounds.minX, bounds.maxX, 0.3 + rng() * 0.4);
    this.z = THREE.MathUtils.lerp(bounds.minZ, bounds.maxZ, 0.3 + rng() * 0.4);
    this.heading = rng() * TAU;
    this.timer = 0.5 + rng() * 2;
    this.apply(0);
  }

  /** Plays the egg-hatch entrance: pop in with a squash-and-stretch bounce, then roar. */
  hatch(): void {
    this.mode = 'hatching';
    this.hatchT = 0;
    this.rig.root.scale.setScalar(0.001);
  }

  roar(): void {
    if (this.mode === 'hatching') return;
    this.mode = 'roar';
    this.roarT = 0;
    this.onRoar(this);
  }

  get isHatching(): boolean {
    return this.mode === 'hatching';
  }

  update(dt: number, neighbours: DinoAnimator[]): void {
    this.time += dt;
    const sp = this.rig.species;
    switch (this.mode) {
      case 'hatching': {
        this.hatchT += dt;
        const t = Math.min(1, this.hatchT / 0.9);
        // Elastic overshoot.
        const s = t >= 1 ? 1 : 1 - Math.pow(2, -9 * t) * Math.cos(t * 10);
        this.rig.root.scale.setScalar(sp.scale * Math.max(0.001, s));
        if (this.hatchT > 1.0) {
          this.rig.root.scale.setScalar(sp.scale);
          this.mode = 'idle';
          this.timer = 0.2;
          this.roar();
        }
        this.speedNow = 0;
        break;
      }
      case 'idle': {
        this.speedNow = Math.max(0, this.speedNow - dt * 3);
        this.timer -= dt;
        if (this.rng() < dt * 0.4) this.lookTarget = (this.rng() - 0.5) * 1.2;
        if (this.timer <= 0) {
          if (this.rng() < 0.14) this.roar();
          else this.pickTarget();
        }
        break;
      }
      case 'walk': {
        const dx = this.targetX - this.x;
        const dz = this.targetZ - this.z;
        const dist = Math.hypot(dx, dz);
        const desired = Math.atan2(-dz, dx);
        const turn = wrapAngle(desired - this.heading);
        const turnRate = 2.2;
        this.heading += THREE.MathUtils.clamp(turn, -turnRate * dt, turnRate * dt);
        this.lookTarget = THREE.MathUtils.clamp(turn, -0.6, 0.6);
        // Slow down for sharp turns and on arrival so the feet never skate.
        const want = sp.walkSpeed * (Math.abs(turn) > 1.2 ? 0.35 : 1) * Math.min(1, dist / 1.2);
        this.speedNow += (want - this.speedNow) * Math.min(1, dt * 3);
        if (dist < 0.25) {
          this.mode = 'idle';
          this.timer = 1.5 + this.rng() * 3.5;
        }
        break;
      }
      case 'roar': {
        this.roarT += dt;
        this.speedNow = Math.max(0, this.speedNow - dt * 4);
        if (this.roarT > 1.6) {
          this.mode = 'idle';
          this.timer = 1 + this.rng() * 2;
        }
        break;
      }
    }

    // Move along heading.
    const move = this.speedNow * dt * sp.scale;
    this.x += Math.cos(this.heading) * move;
    this.z -= Math.sin(this.heading) * move;

    // Gentle separation so herd members don't walk through each other.
    for (const o of neighbours) {
      if (o === this) continue;
      const dx = this.x - o.x;
      const dz = this.z - o.z;
      const d2 = dx * dx + dz * dz;
      const minD = 1.6 * sp.scale;
      if (d2 > 1e-6 && d2 < minD * minD) {
        const d = Math.sqrt(d2);
        const push = (minD - d) * 0.5 * Math.min(1, dt * 4);
        this.x += (dx / d) * push;
        this.z += (dz / d) * push;
      }
    }
    const b = this.bounds;
    this.x = THREE.MathUtils.clamp(this.x, b.minX, b.maxX);
    this.z = THREE.MathUtils.clamp(this.z, b.minZ, b.maxZ);

    // Gait phase advances with distance travelled: one cycle per stride => no foot sliding.
    this.phase = (this.phase + (move / (this.rig.stride * sp.scale)) * TAU) % TAU;
    this.apply(dt);
  }

  private pickTarget(): void {
    const b = this.bounds;
    this.targetX = THREE.MathUtils.lerp(b.minX, b.maxX, this.rng());
    this.targetZ = THREE.MathUtils.lerp(b.minZ, b.maxZ, this.rng());
    this.mode = 'walk';
  }

  private apply(dt: number): void {
    const rig = this.rig;
    const sp = rig.species;
    const walkTarget = Math.min(1, this.speedNow / Math.max(0.01, sp.walkSpeed * 0.6));
    this.walkBlend += (walkTarget - this.walkBlend) * Math.min(1, dt * 6 || 1);
    const wb = this.walkBlend;
    const t = this.time;

    rig.root.position.set(this.x, 0, this.z);
    rig.root.rotation.y = this.heading;

    const biped = sp.gait === 'biped';
    const amp = (biped ? 0.5 : 0.36) * wb;
    for (const leg of rig.legs) {
      const ph = this.phase + leg.phase;
      const s = Math.sin(ph);
      const swing = Math.max(0, Math.cos(ph)); // leg moving forward => lift the foot
      const hipA = leg.spec.thighAngle + amp * s;
      const kneeA = -(leg.spec.thighAngle + leg.spec.shinAngle) - swing * 0.9 * wb;
      leg.hip.rotation.z = hipA;
      leg.knee.rotation.z = kneeA;
      // Keep the foot flat, with a little toe-off at the end of stance.
      leg.ankle.rotation.z = -(hipA + kneeA) + Math.max(0, -s) * 0.25 * wb;
    }

    // Body bob (twice per cycle), forward lean while moving, breathing when still.
    const bob = Math.abs(Math.cos(this.phase)) * (biped ? 0.06 : 0.035) * wb;
    const breathe = Math.sin(t * 2.1) * 0.012 * (1 - wb);
    rig.body.position.y = bob + breathe - 0.02 * wb;
    rig.body.rotation.z = -0.04 * wb;

    // Tail: travelling sine wave from hips to tip, stronger while walking.
    const tailAmp = 0.07 + 0.06 * wb;
    for (let i = rig.hipBone - 1, k = 0; i >= 0; i--, k++) {
      const bone = rig.bones[i];
      bone.rotation.y = Math.sin(t * (2 + wb * 2) - k * 0.7) * tailAmp;
      bone.rotation.z = Math.sin(t * 1.3 - k * 0.5) * 0.03 - 0.02 * wb;
    }

    // Neck and head: idle look-around, walk bob, roar pose.
    this.lookYaw += (this.lookTarget - this.lookYaw) * Math.min(1, dt * 2.5 || 1);
    const roarK = this.mode === 'roar' ? roarCurve(this.roarT) : 0;
    const neckCount = rig.bones.length - 1 - rig.hipBone;
    for (let i = rig.hipBone + 1, k = 0; i < rig.bones.length; i++, k++) {
      const bone = rig.bones[i];
      bone.rotation.y = (this.lookYaw / neckCount) * 0.8;
      bone.rotation.z = Math.sin(t * 1.7 + k * 0.4) * 0.025 + Math.cos(this.phase * 2) * 0.03 * wb + roarK * 0.12;
    }
    rig.headPivot.rotation.y = this.lookYaw * 0.3;
    rig.jaw.rotation.z = -roarK * 0.65 - (Math.sin(t * 0.9) > 0.97 ? 0.08 : 0);
    rig.body.rotation.z += roarK * 0.1;

    for (let i = 0; i < rig.arms.length; i++) {
      rig.arms[i].rotation.z = -0.6 + Math.sin(this.phase + i * Math.PI) * 0.25 * wb + roarK * 0.4;
    }
  }
}

function roarCurve(t: number): number {
  // Wind up, hold with a shake, release.
  if (t < 0.25) return t / 0.25;
  if (t < 1.2) return 1 + Math.sin(t * 40) * 0.04;
  return Math.max(0, 1 - (t - 1.2) / 0.4);
}

function wrapAngle(a: number): number {
  while (a > Math.PI) a -= TAU;
  while (a < -Math.PI) a += TAU;
  return a;
}
