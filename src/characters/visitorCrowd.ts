// Renders every park visitor with a handful of InstancedMeshes (one per body part), so a crowd of
// 50 costs ~6 draw calls instead of ~300.

import * as THREE from 'three';
import type { Visitor } from '../sim/parkSim';

const SHIRTS = [0xe8505b, 0xf9d56e, 0x3fa7d6, 0x59c3a0, 0xf38d68, 0xa68de0, 0xffffff, 0x2d3e50, 0xff9fb2];
const PANTS = [0x2d3e50, 0x4a5a6a, 0x6b4f3a, 0x1f4e79, 0xd8c9a3, 0x333333];
const SKIN = [0xf2d0b1, 0xe0ac86, 0xc68a62, 0x9c6644, 0x70462b];
const HAIR = [0x2b1d14, 0x4a2f1b, 0x111111, 0x8a5a2b, 0xd9b36a, 0x5b3b26];
const HATS = [0xf5c342, 0xe8505b, 0x3fa7d6, 0xffffff];

export class VisitorCrowd {
  readonly group = new THREE.Group();
  private torso: THREE.InstancedMesh;
  private head: THREE.InstancedMesh;
  private hair: THREE.InstancedMesh;
  private hat: THREE.InstancedMesh;
  private legs: THREE.InstancedMesh;
  private arms: THREE.InstancedMesh;
  private m = new THREE.Matrix4();
  private q = new THREE.Quaternion();
  private e = new THREE.Euler();
  private s = new THREE.Vector3(1, 1, 1);
  private v = new THREE.Vector3();
  private parent = new THREE.Matrix4();
  private local = new THREE.Matrix4();
  private c = new THREE.Color();

  constructor(private capacity: number) {
    const mat = new THREE.MeshStandardMaterial({ roughness: 0.7 });
    const make = (geo: THREE.BufferGeometry, count: number) => {
      const im = new THREE.InstancedMesh(geo, mat, count);
      im.castShadow = true;
      im.count = 0;
      im.frustumCulled = false;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      this.group.add(im);
      return im;
    };
    const torsoGeo = new THREE.CapsuleGeometry(0.16, 0.3, 4, 10);
    this.torso = make(torsoGeo, capacity);
    this.head = make(new THREE.SphereGeometry(0.15, 14, 10), capacity);
    const hairGeo = new THREE.SphereGeometry(0.158, 14, 8, 0, Math.PI * 2, 0, Math.PI * 0.55);
    this.hair = make(hairGeo, capacity);
    const hatGeo = new THREE.CylinderGeometry(0.1, 0.24, 0.07, 14);
    this.hat = make(hatGeo, capacity);
    const legGeo = new THREE.CapsuleGeometry(0.065, 0.3, 3, 8);
    legGeo.translate(0, -0.2, 0); // pivot at the hip
    this.legs = make(legGeo, capacity * 2);
    const armGeo = new THREE.CapsuleGeometry(0.05, 0.24, 3, 8);
    armGeo.translate(0, -0.15, 0); // pivot at the shoulder
    this.arms = make(armGeo, capacity * 2);
    // Instance colours must exist before first render.
    for (const im of [this.torso, this.head, this.hair, this.hat, this.legs, this.arms]) {
      im.setColorAt(0, new THREE.Color(1, 1, 1));
    }
  }

  update(visitors: Visitor[]): void {
    let n = 0;
    for (const v of visitors) {
      if (!v.active || n >= this.capacity) continue;
      this.write(n, v);
      n++;
    }
    for (const im of [this.torso, this.head, this.hair, this.hat]) {
      im.count = n;
      im.instanceMatrix.needsUpdate = true;
      if (im.instanceColor) im.instanceColor.needsUpdate = true;
    }
    for (const im of [this.legs, this.arms]) {
      im.count = n * 2;
      im.instanceMatrix.needsUpdate = true;
      if (im.instanceColor) im.instanceColor.needsUpdate = true;
    }
  }

  private write(i: number, v: Visitor) {
    const pick = <T,>(arr: T[], salt: number) => arr[Math.floor(frac(v.look * 97.13 + salt * 13.7) * arr.length)];
    const height = 0.88 + frac(v.look * 31.7) * 0.24;
    const walk = v.walking ? 1 : 0;
    const swing = Math.sin(v.phase) * 0.55 * walk;
    const bounce = Math.abs(Math.cos(v.phase)) * 0.04 * walk;
    // A little hop when paying — reads as "happy customer".
    const hop = v.paidFlash > 0 ? Math.sin(v.paidFlash * Math.PI) * 0.12 : 0;

    this.e.set(0, v.heading, 0);
    this.q.setFromEuler(this.e);
    this.s.set(height, height, height);
    this.v.set(v.x, bounce + hop, v.z);
    this.parent.compose(this.v, this.q, this.s);

    const place = (im: THREE.InstancedMesh, idx: number, x: number, y: number, z: number, rx = 0) => {
      this.e.set(rx, 0, 0);
      this.q.setFromEuler(this.e);
      this.v.set(x, y, z);
      this.s.set(1, 1, 1);
      this.local.compose(this.v, this.q, this.s);
      this.m.multiplyMatrices(this.parent, this.local);
      im.setMatrixAt(idx, this.m);
    };
    place(this.torso, i, 0, 0.72, 0);
    place(this.head, i, 0, 1.12, 0);
    place(this.hair, i, 0, 1.14, -0.01);
    const hasHat = frac(v.look * 7.3) > 0.72;
    place(this.hat, i, 0, hasHat ? 1.27 : -50, 0);
    place(this.legs, i * 2, 0.08, 0.5, 0, swing);
    place(this.legs, i * 2 + 1, -0.08, 0.5, 0, -swing);
    const wave = v.paidFlash > 0 ? -2.4 : 0;
    place(this.arms, i * 2, 0.21, 0.9, 0, -swing * 0.8 + wave);
    place(this.arms, i * 2 + 1, -0.21, 0.9, 0, swing * 0.8);

    // Colours are cheap to rewrite and instance slots get compacted every frame, so just set them.
    const c = this.c;
    const skin = pick(SKIN, 2);
    const pants = pick(PANTS, 5);
    this.torso.setColorAt(i, c.set(pick(SHIRTS, 1)));
    this.head.setColorAt(i, c.set(skin));
    this.hair.setColorAt(i, c.set(pick(HAIR, 3)));
    this.hat.setColorAt(i, c.set(pick(HATS, 4)));
    this.legs.setColorAt(i * 2, c.set(pants));
    this.legs.setColorAt(i * 2 + 1, c);
    this.arms.setColorAt(i * 2, c.set(skin));
    this.arms.setColorAt(i * 2 + 1, c);
  }
}

function frac(x: number) {
  return x - Math.floor(x);
}
