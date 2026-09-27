// Pooled juice: coins popping from visitors, confetti bursts, hatching eggs. Nothing is allocated per frame.

import * as THREE from 'three';

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
