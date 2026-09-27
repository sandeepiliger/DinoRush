// Pooled juice: coins popping from visitors, confetti bursts, hatching eggs. Nothing is allocated per frame.

import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

interface Coin {
  active: boolean;
  t: number;
  x: number;
  y: number;
  z: number;
  vy: number;
}

interface Particle {
  active: boolean;
  t: number;
  life: number;
  p: THREE.Vector3;
  v: THREE.Vector3;
  spin: number;
  size: number;
}

interface Egg {
  mesh: THREE.Group;
  t: number;
  onHatch: () => void;
  hatched: boolean;
}

interface Heart {
  active: boolean;
  t: number;
  p: THREE.Vector3;
  drift: number;
}

export interface Poop {
  mesh: THREE.Mesh;
  enclosureId: string;
  /** >0 while being cleaned (squash-out animation). */
  cleaning: number;
  born: number;
}

interface Meat {
  mesh: THREE.Mesh;
  t: number;
  y0: number;
}

const CONFETTI_COLORS = [0xffd23f, 0xff6b8a, 0x3fa7d6, 0x59c3a0, 0xffffff, 0xf38d68];

export class Effects {
  readonly group = new THREE.Group();
  private coins: Coin[] = [];
  private coinMesh: THREE.InstancedMesh;
  private particles: Particle[] = [];
  private particleMesh: THREE.InstancedMesh;
  private eggs: Egg[] = [];
  private m = new THREE.Matrix4();
  private q = new THREE.Quaternion();
  private e = new THREE.Euler();
  private s = new THREE.Vector3();
  private v = new THREE.Vector3();
  private eggGeo = new THREE.SphereGeometry(0.42, 20, 16);
  private eggMats = new Map<number, THREE.MeshStandardMaterial>();
  private hearts: Heart[] = [];
  private heartMesh: THREE.InstancedMesh;
  readonly poops: Poop[] = [];
  private poopGeo: THREE.BufferGeometry;
  private poopMat = new THREE.MeshStandardMaterial({ color: 0x7a4a26, roughness: 0.45 });
  private meats: Meat[] = [];
  private meatGeo: THREE.BufferGeometry;
  private meatMat = new THREE.MeshStandardMaterial({ color: 0xd9534f, roughness: 0.5 });

  constructor() {
    const coinGeo = new THREE.CylinderGeometry(0.2, 0.2, 0.05, 18);
    coinGeo.rotateX(Math.PI / 2);
    const coinMat = new THREE.MeshStandardMaterial({ color: 0xffc83d, metalness: 0.6, roughness: 0.25, emissive: 0x6b4a00, emissiveIntensity: 0.35 });
    this.coinMesh = new THREE.InstancedMesh(coinGeo, coinMat, 64);
    this.coinMesh.count = 0;
    this.coinMesh.frustumCulled = false;
    this.group.add(this.coinMesh);
    for (let i = 0; i < 64; i++) this.coins.push({ active: false, t: 0, x: 0, y: 0, z: 0, vy: 0 });

    const partGeo = new THREE.PlaneGeometry(0.14, 0.09);
    const partMat = new THREE.MeshBasicMaterial({ side: THREE.DoubleSide });
    this.particleMesh = new THREE.InstancedMesh(partGeo, partMat, 300);
    this.particleMesh.count = 0;
    this.particleMesh.frustumCulled = false;
    const c = new THREE.Color();
    for (let i = 0; i < 300; i++) {
      this.particleMesh.setColorAt(i, c.set(CONFETTI_COLORS[i % CONFETTI_COLORS.length]));
      this.particles.push({ active: false, t: 0, life: 1, p: new THREE.Vector3(), v: new THREE.Vector3(), spin: 0, size: 1 });
    }
    this.group.add(this.particleMesh);

    // Heart: two spheres + a cone, merged.
    const lobe = new THREE.SphereGeometry(0.1, 10, 8);
    const l1 = lobe.clone().translate(-0.07, 0.05, 0);
    const l2 = lobe.clone().translate(0.07, 0.05, 0);
    const tip = new THREE.ConeGeometry(0.135, 0.2, 12).rotateZ(Math.PI).translate(0, -0.08, 0);
    const heartGeo = mergeGeometries([l1.toNonIndexed(), l2.toNonIndexed(), tip.toNonIndexed()])!;
    this.heartMesh = new THREE.InstancedMesh(heartGeo, new THREE.MeshBasicMaterial({ color: 0xff5c8a }), 60);
    this.heartMesh.count = 0;
    this.heartMesh.frustumCulled = false;
    this.group.add(this.heartMesh);
    for (let i = 0; i < 60; i++) this.hearts.push({ active: false, t: 0, p: new THREE.Vector3(), drift: 0 });

    // Cartoon poop swirl: stacked tori with a curl on top.
    const parts: THREE.BufferGeometry[] = [];
    [[0.2, 0.07, 0.06], [0.15, 0.06, 0.16], [0.09, 0.05, 0.25]].forEach(([r, tube, y]) => {
      parts.push(new THREE.TorusGeometry(r, tube, 8, 16).rotateX(Math.PI / 2).translate(0, y, 0).toNonIndexed());
    });
    parts.push(new THREE.ConeGeometry(0.06, 0.14, 8).rotateZ(-0.5).translate(0.02, 0.33, 0).toNonIndexed());
    parts.push(new THREE.CylinderGeometry(0.18, 0.2, 0.08, 14).translate(0, 0.04, 0).toNonIndexed());
    this.poopGeo = mergeGeometries(parts)!;
    this.meatGeo = mergeGeometries([
      new THREE.CapsuleGeometry(0.1, 0.18, 4, 8).rotateZ(Math.PI / 2).toNonIndexed(),
      new THREE.CylinderGeometry(0.03, 0.03, 0.2, 6).rotateZ(Math.PI / 2).translate(0.2, 0, 0).toNonIndexed(),
    ])!;
  }

  hearts3d(x: number, y: number, z: number, count = 6): void {
    let n = 0;
    for (const h of this.hearts) {
      if (h.active) continue;
      h.active = true;
      h.t = -Math.random() * 0.6;
      h.p.set(x + (Math.random() - 0.5) * 1.6, y + Math.random() * 0.4, z + (Math.random() - 0.5) * 1.2);
      h.drift = (Math.random() - 0.5) * 0.6;
      if (++n >= count) break;
    }
  }

  spawnPoop(x: number, z: number, enclosureId: string): Poop {
    const mesh = new THREE.Mesh(this.poopGeo, this.poopMat);
    mesh.position.set(x, 0.08, z);
    mesh.rotation.y = Math.random() * Math.PI * 2;
    mesh.castShadow = true;
    mesh.scale.setScalar(0.001);
    this.group.add(mesh);
    const p: Poop = { mesh, enclosureId, cleaning: 0, born: 0 };
    this.poops.push(p);
    return p;
  }

  /** Starts the clean animation; the poop is removed when it finishes. */
  cleanPoop(p: Poop): void {
    if (p.cleaning > 0) return;
    p.cleaning = 0.001;
    this.confetti(p.mesh.position.x, 0.5, p.mesh.position.z, 10, 0.4);
  }

  dropMeat(x: number, z: number): void {
    for (let i = 0; i < 3; i++) {
      const mesh = new THREE.Mesh(this.meatGeo, this.meatMat);
      mesh.position.set(x + (i - 1) * 0.3, 4 + i * 0.6, z + (Math.random() - 0.5) * 0.3);
      mesh.rotation.set(Math.random(), Math.random() * 6, Math.random());
      mesh.castShadow = true;
      this.group.add(mesh);
      this.meats.push({ mesh, t: -i * 0.12, y0: mesh.position.y });
    }
  }

  coinPop(x: number, y: number, z: number): void {
    const c = this.coins.find((k) => !k.active);
    if (!c) return;
    c.active = true;
    c.t = 0;
    c.x = x;
    c.y = y;
    c.z = z;
    c.vy = 3.2;
  }

  confetti(x: number, y: number, z: number, count = 60, power = 1): void {
    let spawned = 0;
    for (const p of this.particles) {
      if (p.active) continue;
      p.active = true;
      p.t = 0;
      p.life = 1.2 + Math.random() * 0.8;
      p.p.set(x, y, z);
      const a = Math.random() * Math.PI * 2;
      const up = 4 + Math.random() * 4;
      const out = (1 + Math.random() * 2.5) * power;
      p.v.set(Math.cos(a) * out, up * power, Math.sin(a) * out);
      p.spin = (Math.random() - 0.5) * 20;
      p.size = 0.7 + Math.random() * 0.8;
      if (++spawned >= count) break;
    }
  }

  /** Drops a speckled egg that wobbles, cracks, and calls `onHatch` at the moment it bursts. */
  spawnEgg(x: number, z: number, color: number, onHatch: () => void): void {
    const g = new THREE.Group();
    let mat = this.eggMats.get(color);
    if (!mat) {
      mat = new THREE.MeshStandardMaterial({ color: 0xfff6e0, roughness: 0.5 });
      this.eggMats.set(color, mat);
    }
    const shell = new THREE.Mesh(this.eggGeo, mat);
    shell.scale.set(1, 1.3, 1);
    shell.position.y = 0.55;
    shell.castShadow = true;
    g.add(shell);
    const spotMat = new THREE.MeshStandardMaterial({ color, roughness: 0.6 });
    for (let i = 0; i < 7; i++) {
      const spot = new THREE.Mesh(new THREE.SphereGeometry(0.07 + Math.random() * 0.05, 8, 6), spotMat);
      const a = Math.random() * Math.PI * 2;
      const h = (Math.random() - 0.4) * 0.8;
      const r = 0.42 * Math.sqrt(1 - Math.min(0.9, (h / 1.3) ** 2));
      spot.position.set(Math.cos(a) * r * 0.97, 0.55 + h * 0.42, Math.sin(a) * r * 0.97);
      spot.scale.set(1, 1, 0.35);
      spot.lookAt(new THREE.Vector3(0, spot.position.y, 0));
      g.add(spot);
    }
    g.position.set(x, 6, z);
    this.group.add(g);
    this.eggs.push({ mesh: g, t: 0, onHatch, hatched: false });
  }

  update(dt: number): void {
    // Coins: pop up, spin, shrink away.
    let n = 0;
    for (const c of this.coins) {
      if (!c.active) continue;
      c.t += dt;
      c.vy -= 9 * dt;
      c.y += c.vy * dt;
      const life = 0.75;
      if (c.t > life) {
        c.active = false;
        continue;
      }
      const k = c.t / life;
      const scale = k < 0.15 ? k / 0.15 : 1 - Math.max(0, (k - 0.7) / 0.3);
      this.e.set(0, c.t * 12, 0);
      this.q.setFromEuler(this.e);
      this.s.setScalar(Math.max(0.001, scale));
      this.v.set(c.x, c.y, c.z);
      this.m.compose(this.v, this.q, this.s);
      this.coinMesh.setMatrixAt(n++, this.m);
    }
    this.coinMesh.count = n;
    this.coinMesh.instanceMatrix.needsUpdate = true;

    n = 0;
    for (let i = 0; i < this.particles.length; i++) {
      const p = this.particles[i];
      if (!p.active) continue;
      p.t += dt;
      if (p.t > p.life || p.p.y < 0) {
        p.active = false;
        continue;
      }
      p.v.y -= 9.5 * dt;
      p.v.multiplyScalar(1 - dt * 1.2); // air drag makes confetti flutter
      p.p.addScaledVector(p.v, dt);
      this.e.set(p.t * p.spin, p.t * p.spin * 0.7, 0);
      this.q.setFromEuler(this.e);
      this.s.setScalar(p.size * (1 - Math.max(0, (p.t / p.life - 0.8) / 0.2)));
      this.m.compose(p.p, this.q, this.s);
      this.particleMesh.setMatrixAt(n, this.m);
      // Keep each slot's colour stable by writing into its own index order.
      n++;
    }
    this.particleMesh.count = n;
    this.particleMesh.instanceMatrix.needsUpdate = true;

    n = 0;
    for (const h of this.hearts) {
      if (!h.active) continue;
      h.t += dt;
      if (h.t < 0) continue;
      if (h.t > 1.6) {
        h.active = false;
        continue;
      }
      h.p.y += dt * 1.1;
      h.p.x += Math.sin(h.t * 5) * dt * h.drift;
      const k = h.t / 1.6;
      this.q.identity();
      this.s.setScalar((k < 0.15 ? k / 0.15 : 1 - Math.max(0, (k - 0.7) / 0.3)) * 1.4 + 0.001);
      this.m.compose(h.p, this.q, this.s);
      this.heartMesh.setMatrixAt(n++, this.m);
    }
    this.heartMesh.count = n;
    this.heartMesh.instanceMatrix.needsUpdate = true;

    for (let i = this.poops.length - 1; i >= 0; i--) {
      const p = this.poops[i];
      if (p.cleaning > 0) {
        p.cleaning += dt;
        const k = Math.min(1, p.cleaning / 0.25);
        p.mesh.scale.set(1 + k * 0.6, Math.max(0.001, 1 - k), 1 + k * 0.6);
        if (k >= 1) {
          this.group.remove(p.mesh);
          this.poops.splice(i, 1);
        }
      } else if (p.born < 1) {
        // Plop in with a squash.
        p.born = Math.min(1, p.born + dt * 3);
        const e = 1 - Math.pow(2, -8 * p.born) * Math.cos(p.born * 10);
        p.mesh.scale.set(e, Math.max(0.001, e * (1 + Math.sin(p.born * Math.PI) * 0.3)), e);
      }
    }

    for (let i = this.meats.length - 1; i >= 0; i--) {
      const m = this.meats[i];
      m.t += dt;
      if (m.t < 0) continue;
      const fall = Math.min(1, m.t / 0.45);
      m.mesh.position.y = Math.max(0.35, m.y0 * (1 - fall * fall));
      m.mesh.rotation.x += dt * 6 * (1 - fall);
      if (m.t > 3.6) {
        // Eaten: shrink away.
        m.mesh.scale.setScalar(Math.max(0.001, 1 - (m.t - 3.6) * 3));
        if (m.t > 3.95) {
          this.group.remove(m.mesh);
          this.meats.splice(i, 1);
        }
      }
    }

    for (let i = this.eggs.length - 1; i >= 0; i--) {
      const egg = this.eggs[i];
      egg.t += dt;
      const g = egg.mesh;
      if (egg.t < 0.45) {
        // Drop with a bounce.
        const k = egg.t / 0.45;
        g.position.y = 6 * (1 - k * k);
      } else if (egg.t < 1.6) {
        g.position.y = 0;
        const w = egg.t - 0.45;
        const land = Math.max(0, 1 - w * 6);
        g.scale.set(1 + land * 0.25, 1 - land * 0.25, 1 + land * 0.25);
        g.rotation.z = Math.sin(w * (18 + w * 20)) * 0.25 * Math.min(1, w * 2);
      } else if (!egg.hatched) {
        egg.hatched = true;
        this.confetti(g.position.x, 0.8, g.position.z, 40, 0.7);
        egg.onHatch();
        this.group.remove(g);
        g.traverse((o) => {
          const mesh = o as THREE.Mesh;
          if (mesh.isMesh && mesh.geometry !== this.eggGeo) mesh.geometry.dispose();
        });
        this.eggs.splice(i, 1);
      }
    }
  }
}
