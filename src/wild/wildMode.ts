// The arcade loop around the park: drive the hero, hunt with your pack, carry meat on your back, and
// deliver it to build pads (sell at the market, build the nest, hatch pack members, upgrade at the lab).
// Rules are pure (economy/hero.ts, data/wild.ts); this file runs movement, AI and presentation.

import * as THREE from 'three';
import type { AudioManager } from '../audio/audio';
import { buildDino } from '../characters/dinoBuilder';
import { DinoAnimator } from '../characters/dinoAnimator';
import { mergeStaticChildren } from '../characters/mergeStatic';
import type { EnclosureDef } from '../data/parks';
import type { QuestKind } from '../data/quests';
import { CREATURES } from '../data/species';
import { WILD, type PreyDef } from '../data/wild';
import { formatDuration } from '../core/format';
import {
  cargoCapacity, depositToPad, hatchNestEgg, isBuilt, packCapacity, pickUpMeat, sellMeat, statValue, type HeroState,
} from '../economy/hero';
import { haptics } from '../services/haptics';
import { mulberry32 } from '../sim/parkSim';
import { icons } from '../ui/icons';
import { t } from '../ui/strings';
import type { Effects } from '../world/effects';
import type { CircleCollider } from '../world/jungle';
import { buildPad, PAD_RADIUS, padIcons, setPadProgress, type PadView } from './pads';
import { Joystick } from './joystick';

export interface WildHost {
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  effects: Effects;
  audio: AudioManager;
  canvas: HTMLElement;
  layer: HTMLElement;
  app: HTMLElement;
  enclosures: EnclosureDef[];
  colliders: CircleCollider[];
  hero(): HeroState;
  income(): number;
  earn(amount: number): void;
  /** Shows a coin gain at a screen position (already earned). */
  showGain(amount: number, x: number, y: number): void;
  questEvent(kind: QuestKind): void;
  openLab(): void;
  closeLab(): void;
  toast(html: string): void;
  persist(): void;
}

interface Prey {
  def: PreyDef;
  anim: DinoAnimator;
  x: number;
  z: number;
  heading: number;
  hp: number;
  state: 'wander' | 'flee' | 'dead' | 'gone';
  tx: number;
  tz: number;
  timer: number;
  flash: number;
}

interface Member {
  anim: DinoAnimator;
  x: number;
  z: number;
  heading: number;
  biteCd: number;
}

interface Loose {
  x: number;
  y: number;
  z: number;
  /** 'ground' pickup, flying to the hero's back, or flying from the back to a pad. */
  mode: 'pop' | 'ground' | 'toHero' | 'toPad';
  t: number;
  from: THREE.Vector3;
  to: THREE.Vector3;
  onArrive?: () => void;
  spin: number;
}

const HERO_RADIUS = 0.6;
const MAX_LOOSE = 120;
const MAX_STACK_VISUAL = 60;

export class WildMode {
  readonly joystick: Joystick;
  heroX = WILD.heroStart.x;
  heroZ = WILD.heroStart.z;
  private heroHeading = Math.PI / 2;
  private hero: DinoAnimator;
  private heroBiteCd = 0;
  private marker: THREE.Mesh;
  private arrow: THREE.Mesh;
  private guide: THREE.Vector3 | null = null;
  private pack: Member[] = [];
  private prey: Prey[] = [];
  private loose: Loose[] = [];
  private looseMesh: THREE.InstancedMesh;
  private stackMesh: THREE.InstancedMesh;
  private incoming = 0;
  private pads: PadView[] = [];
  private onPad: PadView | null = null;
  private depositTimer = 0;
  private saleTotal = 0;
  private eggTimer = WILD.eggSeconds;
  private packFullWarned = false;
  private rng = mulberry32(777);
  private time = 0;
  private m = new THREE.Matrix4();
  private q = new THREE.Quaternion();
  private e = new THREE.Euler();
  private s = new THREE.Vector3(1, 1, 1);
  private v = new THREE.Vector3();
  private rects: { minX: number; maxX: number; minZ: number; maxZ: number }[];

  constructor(private host: WildHost) {
    this.joystick = new Joystick(host.canvas, host.app);
    // Hero.
    const rig = buildDino(CREATURES.hero, 0.5);
    mergeStaticChildren(rig.root);
    host.scene.add(rig.root);
    this.hero = new DinoAnimator(rig, { minX: -1e3, maxX: 1e3, minZ: -1e3, maxZ: 1e3 }, this.rng, () => {});
    this.hero.x = this.heroX;
    this.hero.z = this.heroZ;
    this.marker = new THREE.Mesh(new THREE.RingGeometry(0.75, 0.95, 36), new THREE.MeshBasicMaterial({ color: 0xffd23f, transparent: true, opacity: 0.85 }));
    this.marker.rotation.x = -Math.PI / 2;
    this.marker.position.y = 0.07;
    host.scene.add(this.marker);
    const arrowShape = new THREE.Shape();
    arrowShape.moveTo(0, 0.5);
    arrowShape.lineTo(0.42, -0.1);
    arrowShape.lineTo(0.15, -0.1);
    arrowShape.lineTo(0.15, -0.5);
    arrowShape.lineTo(-0.15, -0.5);
    arrowShape.lineTo(-0.15, -0.1);
    arrowShape.lineTo(-0.42, -0.1);
    arrowShape.closePath();
    const arrowGeo = new THREE.ShapeGeometry(arrowShape);
    arrowGeo.rotateX(-Math.PI / 2);
    this.arrow = new THREE.Mesh(arrowGeo, new THREE.MeshBasicMaterial({ color: 0xffd23f, transparent: true, opacity: 0.9, depthWrite: false }));
    this.arrow.position.y = 0.1;
    this.arrow.visible = false;
    host.scene.add(this.arrow);

    // Meat visuals: one instanced mesh for the stack on the hero's back, one for loose/flying pieces.
    const meatGeo = new THREE.CapsuleGeometry(0.13, 0.2, 4, 8).rotateZ(Math.PI / 2);
    const meatMat = new THREE.MeshStandardMaterial({ color: 0xd9534f, roughness: 0.45, emissive: 0x3a0a0a });
    this.stackMesh = new THREE.InstancedMesh(meatGeo, meatMat, MAX_STACK_VISUAL);
    this.stackMesh.count = 0;
    this.stackMesh.castShadow = true;
    this.stackMesh.frustumCulled = false;
    this.looseMesh = new THREE.InstancedMesh(meatGeo, meatMat, MAX_LOOSE);
    this.looseMesh.count = 0;
    this.looseMesh.castShadow = true;
    this.looseMesh.frustumCulled = false;
    host.scene.add(this.stackMesh, this.looseMesh);

    // Pads.
    for (const def of WILD.pads) {
      const view = buildPad(def, host.scene, host.layer);
      this.pads.push(view);
      host.colliders.push(...view.colliders);
    }
    // Park colliders: fenced enclosures (the hero walks around them).
    this.rects = host.enclosures.map((e) => ({ minX: e.x - e.halfW - 0.3, maxX: e.x + e.halfW + 0.3, minZ: e.z - e.halfD - 0.3, maxZ: e.z + e.halfD + 0.3 }));
    host.colliders.push({ x: -2.4, z: 10.2, r: 0.6 }, { x: 2.4, z: 10.2, r: 0.6 }, { x: -3.8, z: 10.7, r: 1.0 });

    // Prey population.
    for (const def of WILD.prey) for (let i = 0; i < def.population; i++) this.spawnPrey(def);
    // Existing pack members.
    for (let i = 0; i < host.hero().pack; i++) this.addMember(false);
    this.refreshPads();
  }

  get heroAnimator(): DinoAnimator {
    return this.hero;
  }

  /** Where the quest guide arrow should point (null hides it). */
  setGuide(target: THREE.Vector3 | null) {
    this.guide = target;
  }

  nestPosition(): THREE.Vector3 {
    const p = WILD.pads.find((d) => d.kind === 'nest')!;
    return new THREE.Vector3(p.x, 0, p.z);
  }

  padPosition(kind: 'market' | 'lab' | 'nest'): THREE.Vector3 {
    const p = WILD.pads.find((d) => d.kind === kind)!;
    return new THREE.Vector3(p.x, 0, p.z);
  }

  nearestPrey(): THREE.Vector3 | null {
    let best: Prey | null = null;
    let bd = Infinity;
    for (const p of this.prey) {
      if (p.state === 'dead' || p.state === 'gone') continue;
      const d = Math.hypot(p.x - this.heroX, p.z - this.heroZ);
      if (d < bd) {
        bd = d;
        best = p;
      }
    }
    return best ? new THREE.Vector3(best.x, 0, best.z) : null;
  }

  update(dt: number): void {
    this.time += dt;
    const h = this.host.hero();
    this.updateHero(dt, h);
    this.updatePack(dt);
    this.updatePrey(dt);
    this.updateLoose(dt, h);
    this.updatePads(dt, h);
    this.updateStack(h);
    this.updateGuide();
  }

  // ---------------------------------------------------------------- hero

  private updateHero(dt: number, h: HeroState) {
    const js = this.joystick;
    js.poll();
    const mag = Math.min(1, Math.hypot(js.vec.x, js.vec.y));
    const speed = statValue(h, 'speed', WILD);
    if (mag > 0.05) {
      // The camera looks north with no yaw: screen right = +x, screen down = +z.
      const dx = js.vec.x;
      const dz = js.vec.y;
      const len = Math.hypot(dx, dz);
      const nx = this.heroX + (dx / len) * speed * mag * dt;
      const nz = this.heroZ + (dz / len) * speed * mag * dt;
      const r = this.resolve(nx, nz, HERO_RADIUS);
      this.heroX = r.x;
      this.heroZ = r.z;
      const desired = Math.atan2(-dz, dx);
      this.heroHeading += wrap(desired - this.heroHeading) * Math.min(1, dt * 14);
    }
    this.hero.drive(dt, this.heroX, this.heroZ, this.heroHeading);

    // Auto-bite the nearest prey in range.
    this.heroBiteCd -= dt;
    const range = WILD.biteRange * CREATURES.hero.scale * 0.85;
    const target = this.closestPrey(this.heroX, this.heroZ, range);
    if (target && this.heroBiteCd <= 0) {
      this.heroBiteCd = WILD.biteInterval;
      this.heroHeading = Math.atan2(-(target.z - this.heroZ), target.x - this.heroX);
      this.hero.bite();
      this.damage(target, statValue(h, 'bite', WILD));
    }
    this.marker.position.set(this.heroX, 0.07, this.heroZ);
    (this.marker.material as THREE.MeshBasicMaterial).opacity = 0.6 + Math.sin(this.time * 4) * 0.25;
  }

  /** Pushes a circle out of trees, rocks, buildings and enclosures, and keeps it in the world. */
  private resolve(x: number, z: number, r: number): { x: number; z: number } {
    for (const c of this.host.colliders) {
      const dx = x - c.x;
      const dz = z - c.z;
      const min = c.r + r;
      const d2 = dx * dx + dz * dz;
      if (d2 < min * min && d2 > 1e-8) {
        const d = Math.sqrt(d2);
        x = c.x + (dx / d) * min;
        z = c.z + (dz / d) * min;
      }
    }
    for (const b of this.rects) {
      if (x > b.minX - r && x < b.maxX + r && z > b.minZ - r && z < b.maxZ + r) {
        // Exit along the shallowest side.
        const left = x - (b.minX - r);
        const right = b.maxX + r - x;
        const top = z - (b.minZ - r);
        const bottom = b.maxZ + r - z;
        const m = Math.min(left, right, top, bottom);
        if (m === left) x = b.minX - r;
        else if (m === right) x = b.maxX + r;
        else if (m === top) z = b.minZ - r;
        else z = b.maxZ + r;
      }
    }
    const B = WILD.bounds;
    return { x: THREE.MathUtils.clamp(x, B.minX + 1, B.maxX - 1), z: THREE.MathUtils.clamp(z, B.minZ + 1, B.maxZ - 1) };
  }

  // ---------------------------------------------------------------- pack

  addMember(hatch: boolean, at?: THREE.Vector3) {
    const rig = buildDino(CREATURES.packRaptor, this.rng());
    mergeStaticChildren(rig.root);
    this.host.scene.add(rig.root);
    const anim = new DinoAnimator(rig, { minX: -1e3, maxX: 1e3, minZ: -1e3, maxZ: 1e3 }, this.rng, () => {});
    const x = at ? at.x : this.heroX + (this.rng() - 0.5) * 3;
    const z = at ? at.z : this.heroZ + 2 + this.rng();
    anim.x = x;
    anim.z = z;
    if (hatch) anim.hatch();
    this.pack.push({ anim, x, z, heading: this.heroHeading, biteCd: 0 });
  }

  private updatePack(dt: number) {
    const heroSpeed = statValue(this.host.hero(), 'speed', WILD);
    const bite = statValue(this.host.hero(), 'bite', WILD);
    // Is there something to hunt near the hero?
    const huntRadius = 8;
    this.pack.forEach((m, i) => {
      let tx: number;
      let tz: number;
      const prey = this.closestPrey(this.heroX, this.heroZ, huntRadius);
      const mine = prey ? this.closestPrey(m.x, m.z, huntRadius) : null;
      if (mine) {
        tx = mine.x;
        tz = mine.z;
      } else {
        // Formation behind the hero: pairs in rows.
        const row = Math.floor(i / 2);
        const side = i % 2 === 0 ? -1 : 1;
        const back = 2.2 + row * 1.8;
        const lateral = side * (1.1 + row * 0.2);
        const ch = Math.cos(this.heroHeading);
        const sh = Math.sin(this.heroHeading);
        // Hero forward = (cos h, -sin h); right = (sin h, cos h).
        tx = this.heroX - ch * back + sh * lateral;
        tz = this.heroZ + sh * back + ch * lateral;
      }
      const dx = tx - m.x;
      const dz = tz - m.z;
      const d = Math.hypot(dx, dz);
      const stopAt = mine ? WILD.biteRange * 0.7 : 0.3;
      if (d > stopAt) {
        const sp = Math.min(heroSpeed * 1.2, (d - stopAt) * 4 + 0.5);
        const step = Math.min(d - stopAt, sp * dt);
        const r = this.resolve(m.x + (dx / d) * step, m.z + (dz / d) * step, 0.45);
        m.x = r.x;
        m.z = r.z;
        m.heading += wrap(Math.atan2(-dz, dx) - m.heading) * Math.min(1, dt * 10);
      }
      // Separation from other members and the hero.
      for (const o of this.pack) {
        if (o === m) continue;
        const sx = m.x - o.x;
        const sz = m.z - o.z;
        const sd = Math.hypot(sx, sz);
        if (sd > 1e-4 && sd < 1.1) {
          m.x += (sx / sd) * (1.1 - sd) * 0.5;
          m.z += (sz / sd) * (1.1 - sd) * 0.5;
        }
      }
      m.biteCd -= dt;
      if (mine && m.biteCd <= 0 && Math.hypot(mine.x - m.x, mine.z - m.z) < WILD.biteRange) {
        m.biteCd = WILD.biteInterval * 1.2;
        m.heading = Math.atan2(-(mine.z - m.z), mine.x - m.x);
        m.anim.bite();
        this.damage(mine, bite * 0.7);
      }
      m.anim.drive(dt, m.x, m.z, m.heading);
    });
  }

  // ---------------------------------------------------------------- prey

  private spawnPrey(def: PreyDef, existing?: Prey) {
    const J = WILD.jungle;
    let x = 0;
    let z = 0;
    for (let tries = 0; tries < 30; tries++) {
      x = THREE.MathUtils.lerp(J.minX + 2, J.maxX - 2, this.rng());
      z = THREE.MathUtils.lerp(J.minZ + 2, J.maxZ, this.rng());
      if (Math.hypot(x - this.heroX, z - this.heroZ) < 10) continue;
      if (this.host.colliders.some((c) => Math.hypot(x - c.x, z - c.z) < c.r + 1)) continue;
      break;
    }
    if (existing) {
      existing.x = x;
      existing.z = z;
      existing.hp = def.hp;
      existing.state = 'wander';
      existing.timer = 0;
      existing.anim.rig.root.visible = true;
      existing.anim.rig.root.rotation.x = 0;
      existing.anim.rig.root.position.y = 0;
      existing.anim.x = x;
      existing.anim.z = z;
      existing.anim.hatch();
      return;
    }
    const rig = buildDino(CREATURES[def.speciesId], this.rng());
    mergeStaticChildren(rig.root);
    this.host.scene.add(rig.root);
    const anim = new DinoAnimator(rig, { minX: -1e3, maxX: 1e3, minZ: -1e3, maxZ: 1e3 }, this.rng, () => {});
    anim.x = x;
    anim.z = z;
    this.prey.push({ def, anim, x, z, heading: this.rng() * Math.PI * 2, hp: def.hp, state: 'wander', tx: x, tz: z, timer: 0, flash: 0 });
  }

  private closestPrey(x: number, z: number, radius: number): Prey | null {
    let best: Prey | null = null;
    let bd = radius;
    for (const p of this.prey) {
      if (p.state === 'dead' || p.state === 'gone' || p.anim.isHatching) continue;
      const d = Math.hypot(p.x - x, p.z - z);
      if (d < bd) {
        bd = d;
        best = p;
      }
    }
    return best;
  }

  private damage(p: Prey, amount: number) {
    p.hp -= amount;
    p.flash = 1;
    p.state = 'flee';
    this.host.audio.play('bite');
    this.host.effects.confetti(p.x, 0.7, p.z, 4, 0.3);
    if (p.hp <= 0) this.kill(p);
  }

  private kill(p: Prey) {
    p.state = 'dead';
    p.timer = 0;
    this.host.audio.play('faint');
    haptics.tap(20);
    this.host.questEvent('hunt');
    // Meat spills out around the body.
    for (let i = 0; i < p.def.meat; i++) {
      if (this.loose.length >= MAX_LOOSE) break;
      const a = this.rng() * Math.PI * 2;
      const r = 0.4 + this.rng() * 0.6;
      this.loose.push({
        x: p.x, y: 0.6, z: p.z, mode: 'pop', t: 0, spin: this.rng() * 6,
        from: new THREE.Vector3(p.x, 0.6, p.z),
        to: new THREE.Vector3(p.x + Math.cos(a) * r, 0.18, p.z + Math.sin(a) * r),
      });
    }
  }

  private updatePrey(dt: number) {
    const threats: { x: number; z: number }[] = [{ x: this.heroX, z: this.heroZ }, ...this.pack];
    for (const p of this.prey) {
      const root = p.anim.rig.root;
      if (p.state === 'gone') {
        p.timer -= dt;
        if (p.timer <= 0) this.spawnPrey(p.def, p);
        continue;
      }
      if (p.state === 'dead') {
        p.timer += dt;
        // Keel over, then sink away.
        root.rotation.x = Math.min(Math.PI / 2, p.timer * 6);
        if (p.timer > 1.2) root.position.y = -(p.timer - 1.2) * 1.2;
        if (p.timer > 1.8) {
          root.visible = false;
          p.state = 'gone';
          p.timer = WILD.respawnSeconds;
        }
        continue;
      }
      // Nearest threat.
      let tx = 0;
      let tz = 0;
      let td = Infinity;
      for (const th of threats) {
        const d = Math.hypot(p.x - th.x, p.z - th.z);
        if (d < td) {
          td = d;
          tx = th.x;
          tz = th.z;
        }
      }
      let speed = 0;
      let dirX = 0;
      let dirZ = 0;
      if (td < WILD.fleeRadius) {
        p.state = 'flee';
        const ax = p.x - tx;
        const az = p.z - tz;
        const l = Math.hypot(ax, az) || 1;
        // A little zig-zag makes chases fun.
        const wig = Math.sin(this.time * 5 + p.x) * 0.5;
        dirX = ax / l - (az / l) * wig;
        dirZ = az / l + (ax / l) * wig;
        speed = p.def.speed;
      } else {
        p.state = 'wander';
        p.timer -= dt;
        if (p.timer <= 0) {
          const J = WILD.jungle;
          p.tx = THREE.MathUtils.clamp(p.x + (this.rng() - 0.5) * 10, J.minX, J.maxX);
          p.tz = THREE.MathUtils.clamp(p.z + (this.rng() - 0.5) * 10, J.minZ, J.maxZ);
          p.timer = 2 + this.rng() * 4;
        }
        dirX = p.tx - p.x;
        dirZ = p.tz - p.z;
        if (Math.hypot(dirX, dirZ) > 0.4) speed = p.def.speed * 0.3;
      }
      if (speed > 0 && !p.anim.isHatching) {
        const l = Math.hypot(dirX, dirZ) || 1;
        const J = WILD.jungle;
        let nx = p.x + (dirX / l) * speed * dt;
        let nz = p.z + (dirZ / l) * speed * dt;
        // Prey stay in the jungle (a little slack past the southern edge).
        nx = THREE.MathUtils.clamp(nx, J.minX, J.maxX);
        nz = THREE.MathUtils.clamp(nz, J.minZ, J.maxZ + 3);
        const r = this.resolve(nx, nz, 0.35);
        p.heading += wrap(Math.atan2(-(r.z - p.z), r.x - p.x) - p.heading) * Math.min(1, dt * 8);
        p.x = r.x;
        p.z = r.z;
      }
      p.anim.drive(dt, p.x, p.z, p.heading);
      if (p.flash > 0) {
        p.flash = Math.max(0, p.flash - dt * 5);
        const k = p.flash;
        root.scale.set(p.anim.rig.species.scale * (1 + k * 0.25), p.anim.rig.species.scale * (1 - k * 0.2), p.anim.rig.species.scale * (1 + k * 0.25));
      }
    }
  }

  // ---------------------------------------------------------------- meat

  private stackTop(h: HeroState, index = h.meat): THREE.Vector3 {
    const back = this.v.set(-Math.cos(this.heroHeading) * 0.35, 0, Math.sin(this.heroHeading) * 0.35);
    return new THREE.Vector3(this.heroX + back.x, 1.75 + Math.min(index, MAX_STACK_VISUAL) * 0.24, this.heroZ + back.z);
  }

  private updateLoose(dt: number, h: HeroState) {
    let n = 0;
    for (let i = this.loose.length - 1; i >= 0; i--) {
      const l = this.loose[i];
      l.t += dt;
      if (l.mode === 'pop') {
        const k = Math.min(1, l.t / 0.35);
        l.x = THREE.MathUtils.lerp(l.from.x, l.to.x, k);
        l.z = THREE.MathUtils.lerp(l.from.z, l.to.z, k);
        l.y = THREE.MathUtils.lerp(l.from.y, l.to.y, k) + Math.sin(k * Math.PI) * 0.8;
        if (k >= 1) {
          l.mode = 'ground';
          l.t = 0;
        }
      } else if (l.mode === 'ground') {
        l.y = 0.2 + Math.abs(Math.sin(this.time * 3 + l.spin)) * 0.12;
        // Magnet onto the hero's back when there's room.
        if (Math.hypot(l.x - this.heroX, l.z - this.heroZ) < WILD.magnetRadius && pickUpMeat(h, 1, WILD) === 1) {
          this.incoming++;
          l.mode = 'toHero';
          l.t = 0;
          l.from.set(l.x, l.y, l.z);
          this.host.audio.play('coin', 0.5);
        }
      } else if (l.mode === 'toHero') {
        const k = Math.min(1, l.t / 0.3);
        const top = this.stackTop(h, h.meat - this.incoming);
        l.x = THREE.MathUtils.lerp(l.from.x, top.x, k);
        l.z = THREE.MathUtils.lerp(l.from.z, top.z, k);
        l.y = THREE.MathUtils.lerp(l.from.y, top.y, k) + Math.sin(k * Math.PI) * 1.2;
        if (k >= 1) {
          this.incoming = Math.max(0, this.incoming - 1);
          this.loose.splice(i, 1);
          continue;
        }
      } else {
        const k = Math.min(1, l.t / 0.32);
        l.x = THREE.MathUtils.lerp(l.from.x, l.to.x, k);
        l.z = THREE.MathUtils.lerp(l.from.z, l.to.z, k);
        l.y = THREE.MathUtils.lerp(l.from.y, l.to.y, k) + Math.sin(k * Math.PI) * 1.4;
        if (k >= 1) {
          l.onArrive?.();
          this.loose.splice(i, 1);
          continue;
        }
      }
      if (n < MAX_LOOSE) {
        this.e.set(0, this.time * 2 + l.spin, 0.3);
        this.q.setFromEuler(this.e);
        this.v.set(l.x, l.y, l.z);
        this.m.compose(this.v, this.q, this.s.set(1, 1, 1));
        this.looseMesh.setMatrixAt(n++, this.m);
      }
    }
    this.looseMesh.count = n;
    this.looseMesh.instanceMatrix.needsUpdate = true;
  }

  private updateStack(h: HeroState) {
    const count = Math.min(MAX_STACK_VISUAL, Math.max(0, h.meat - this.incoming));
    const moving = Math.min(1, Math.hypot(this.joystick.vec.x, this.joystick.vec.y));
    for (let i = 0; i < count; i++) {
      // Lean backwards and sway more the taller the stack is.
      const lean = (i / 10) * 0.25 * moving;
      const sway = Math.sin(this.time * 6 - i * 0.3) * 0.03 * i * moving;
      const bx = -Math.cos(this.heroHeading) * (0.35 + lean);
      const bz = Math.sin(this.heroHeading) * (0.35 + lean);
      this.v.set(this.heroX + bx + Math.sin(this.heroHeading) * sway, 1.75 + i * 0.24, this.heroZ + bz + Math.cos(this.heroHeading) * sway);
      this.e.set(0, this.heroHeading + (i % 2 ? 0.25 : -0.25), 0);
      this.q.setFromEuler(this.e);
      this.m.compose(this.v, this.q, this.s.set(1, 1, 1));
      this.stackMesh.setMatrixAt(i, this.m);
    }
    this.stackMesh.count = count;
    this.stackMesh.instanceMatrix.needsUpdate = true;
  }

  // ---------------------------------------------------------------- pads

  private updatePads(dt: number, h: HeroState) {
    const w = window.innerWidth;
    const hh = window.innerHeight;
    let standing: PadView | null = null;
    for (const pad of this.pads) {
      if (Math.hypot(this.heroX - pad.def.x, this.heroZ - pad.def.z) < PAD_RADIUS) standing = pad;
      const helix = pad.building.getObjectByName('helix');
      if (helix) helix.rotation.y += dt * 1.5;
      // Label.
      const p = this.project(this.v.set(pad.def.x, pad.def.kind === 'nest' ? 2.2 : 4.1, pad.def.z + (pad.def.kind === 'nest' ? 0 : 1.5)), w, hh);
      pad.label.hidden = !p;
      if (p) pad.label.style.transform = `translate3d(${Math.min(w - 80, Math.max(80, p.x)).toFixed(1)}px, ${p.y.toFixed(1)}px, 0) translate(-50%, -100%)`;
    }

    // Nest egg timer.
    const nest = this.pads.find((p) => p.def.kind === 'nest')!;
    if (isBuilt(h, nest.def.id) && !h.eggReady) {
      this.eggTimer -= dt;
      if (this.eggTimer <= 0) {
        h.eggReady = true;
        this.eggTimer = WILD.eggSeconds;
        this.refreshPads();
      }
    }
    if (nest.egg && nest.egg.visible) {
      nest.egg.position.y = 0.6 + Math.abs(Math.sin(this.time * 3)) * 0.12;
      nest.egg.rotation.z = Math.sin(this.time * 9) * 0.12;
    }

    if (standing !== this.onPad) {
      // Leaving a pad: settle the sale summary.
      if (this.onPad?.def.kind === 'market' && this.saleTotal > 0) this.flushSale();
      if (this.onPad?.def.kind === 'lab') this.host.closeLab();
      this.onPad = standing;
      this.depositTimer = 0.25;
      this.packFullWarned = false;
      if (standing?.def.kind === 'lab') {
        this.host.audio.play('click');
        this.host.openLab();
      }
    }
    if (!standing) {
      this.refreshPadLabels(h);
      return;
    }

    const kind = standing.def.kind;
    if (kind === 'nest' && isBuilt(h, standing.def.id) && h.eggReady) {
      if (hatchNestEgg(h, WILD)) {
        this.addMember(true, new THREE.Vector3(standing.def.x + 1.2, 0, standing.def.z));
        this.host.audio.play('hatch');
        this.host.effects.confetti(standing.def.x, 1, standing.def.z, 60, 0.9);
        this.host.toast(`${icons.egg} ${t('packJoined')}`);
        this.host.questEvent('hatchWild');
        this.refreshPads();
        this.host.persist();
      } else if (!this.packFullWarned) {
        this.packFullWarned = true;
        this.host.toast(t('packFull'));
      }
    }

    const canDeposit = h.meat - this.incoming > 0 && (kind === 'market' || (kind === 'nest' && !isBuilt(h, standing.def.id)));
    if (canDeposit) {
      this.depositTimer -= dt;
      while (this.depositTimer <= 0 && h.meat - this.incoming > 0) {
        this.depositTimer += WILD.depositInterval;
        this.depositOne(standing, h);
        if (kind === 'nest' && isBuilt(h, standing.def.id)) break;
      }
    } else if (kind === 'market' && this.saleTotal > 0 && h.meat - this.incoming <= 0) {
      this.flushSale();
    }
    this.refreshPadLabels(h);
  }

  private depositOne(pad: PadView, h: HeroState) {
    const from = this.stackTop(h, h.meat - this.incoming - 1);
    let onArrive: (() => void) | undefined;
    if (pad.def.kind === 'market') {
      const coins = sellMeat(h, this.host.income(), WILD);
      if (coins <= 0) return;
      this.host.earn(coins);
      this.saleTotal += coins;
      this.host.questEvent('sell');
      onArrive = () => {
        this.host.effects.coinPop(pad.drop.x, pad.drop.y + 0.4, pad.drop.z);
        this.host.audio.play('coin', 0.8);
      };
    } else {
      const r = depositToPad(h, pad.def.id, WILD);
      if (!r.moved) return;
      const built = r.built;
      onArrive = () => {
        this.host.audio.play('click');
        this.refreshPads();
        if (built) {
          this.host.audio.play('unlock');
          this.host.effects.confetti(pad.def.x, 1.2, pad.def.z, 120, 1.1);
          pad.building.scale.setScalar(0.01);
          const t0 = performance.now();
          const grow = () => {
            const k = Math.min(1, (performance.now() - t0) / 500);
            pad.building.scale.setScalar(Math.max(0.01, 1 - Math.pow(2, -8 * k) * Math.cos(k * 9)));
            if (k < 1) requestAnimationFrame(grow);
          };
          grow();
          this.host.toast(`${icons.egg} ${t('nestBuilt')}`);
          this.host.persist();
        }
      };
    }
    if (this.loose.length < MAX_LOOSE) {
      this.loose.push({ x: from.x, y: from.y, z: from.z, mode: 'toPad', t: 0, spin: this.rng() * 6, from, to: pad.drop.clone(), onArrive });
    } else onArrive?.();
    haptics.tap(6);
  }

  private flushSale() {
    const p = this.project(this.padPosition('market').setY(3), window.innerWidth, window.innerHeight);
    this.host.showGain(this.saleTotal, p?.x ?? window.innerWidth / 2, p?.y ?? window.innerHeight / 2);
    this.saleTotal = 0;
    this.host.persist();
  }

  /** Visual state of pads (built vs ghost, egg visible, fill). */
  private refreshPads() {
    const h = this.host.hero();
    for (const pad of this.pads) {
      const built = isBuilt(h, pad.def.id);
      pad.building.visible = built;
      pad.ghost.visible = !built;
      pad.fill.visible = !built;
      if (!built) setPadProgress(pad, (h.padProgress[pad.def.id] ?? 0) / pad.def.buildMeat);
      if (pad.egg) pad.egg.visible = built && h.eggReady;
    }
  }

  private refreshPadLabels(h: HeroState) {
    for (const pad of this.pads) {
      const built = isBuilt(h, pad.def.id);
      let title = '';
      let sub = '';
      if (pad.def.kind === 'market') {
        title = t('padMarket');
        sub = t('padMarketSub');
      } else if (pad.def.kind === 'lab') {
        title = t('padLab');
        sub = t('padLabSub');
      } else if (!built) {
        title = t('padBuildNest');
        sub = `${icons.meat} ${h.padProgress[pad.def.id] ?? 0} / ${pad.def.buildMeat}`;
      } else if (h.eggReady) {
        title = t('padEggReady');
        sub = h.pack >= packCapacity(h, WILD) ? t('packFullShort') : t('padStepIn');
      } else {
        title = t('padNest');
        sub = `${t('padNextEgg')} ${formatDuration(this.eggTimer)}`;
      }
      const html = `<div class="pad-icon">${padIcons[pad.def.kind]}</div><div><div class="pad-title">${title}</div><div class="pad-sub">${sub}</div></div>`;
      if (pad.label.dataset.html !== html) {
        pad.label.dataset.html = html;
        pad.label.innerHTML = html;
      }
    }
  }

  // ---------------------------------------------------------------- guide arrow

  private updateGuide() {
    const g = this.guide;
    if (!g) {
      this.arrow.visible = false;
      return;
    }
    const dx = g.x - this.heroX;
    const dz = g.z - this.heroZ;
    const d = Math.hypot(dx, dz);
    this.arrow.visible = d > 3;
    if (!this.arrow.visible) return;
    const r = 1.9 + Math.sin(this.time * 5) * 0.15;
    this.arrow.position.set(this.heroX + (dx / d) * r, 0.1, this.heroZ + (dz / d) * r);
    // Arrow shape points to -z after rotateX; rotate so it points along (dx, dz).
    this.arrow.rotation.y = Math.atan2(-dx, -dz);
  }

  /** Summary for the HUD. */
  cargoText(): string {
    const h = this.host.hero();
    return `${Math.max(0, h.meat)}/${cargoCapacity(h, WILD)}`;
  }

  packText(): string {
    const h = this.host.hero();
    return `${h.pack}/${packCapacity(h, WILD)}`;
  }

  private project(p: THREE.Vector3, w: number, h: number): { x: number; y: number } | null {
    const v = p.project(this.host.camera);
    if (v.z > 1 || Math.abs(v.x) > 1.1 || Math.abs(v.y) > 1.1) return null;
    return { x: (v.x * 0.5 + 0.5) * w, y: (-v.y * 0.5 + 0.5) * h };
  }
}

function wrap(a: number): number {
  while (a > Math.PI) a -= Math.PI * 2;
  while (a < -Math.PI) a += Math.PI * 2;
  return a;
}
