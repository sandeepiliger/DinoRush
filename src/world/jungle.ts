// The wild jungle north of the park: dense tree walls around the edge, clusters and clearings inside,
// rocks and ferns. Returns circle colliders so the hero and prey walk around trunks and boulders.

import * as THREE from 'three';
import type { PadDef, WildConfig } from '../data/wild';
import { at, instanced, merge, paint, vertexColorMat } from './geometry';
import { bushGeometry, palmGeometry, rockGeometry, roundTreeGeometry } from './props';

export interface CircleCollider {
  x: number;
  z: number;
  r: number;
}

function treeFernGeometry(rng: () => number): THREE.BufferGeometry {
  const parts: THREE.BufferGeometry[] = [paint(at(new THREE.CylinderGeometry(0.12, 0.2, 1.8, 7), 0, 0.9, 0), 0x5a4029, 0.1, rng)];
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * Math.PI * 2;
    const leaf = new THREE.ConeGeometry(0.2, 1.6, 4);
    leaf.scale(1, 1, 0.2);
    leaf.translate(0, 0.8, 0);
    leaf.applyMatrix4(new THREE.Matrix4().makeRotationY(a).multiply(new THREE.Matrix4().makeRotationZ(-1.05)));
    leaf.translate(0, 1.8, 0);
    parts.push(paint(leaf, i % 2 ? 0x2f7a2c : 0x3c8f34, 0.2, rng));
  }
  return merge(parts);
}

export function buildJungle(scene: THREE.Scene, cfg: WildConfig, rng: () => number): CircleCollider[] {
  const J = cfg.jungle;
  const colliders: CircleCollider[] = [];
  const bigTrees: { x: number; z: number; ry: number; s: number }[] = [];
  const palms: { x: number; z: number; ry: number; s: number }[] = [];
  const ferns: { x: number; z: number; ry: number; s: number }[] = [];
  const bushes: { x: number; z: number; ry: number; s: number }[] = [];
  const rocks: { x: number; z: number; ry: number; s: number }[] = [];

  const clearOf = (x: number, z: number, pads: PadDef[], pad = 4) => {
    for (const p of pads) if (Math.hypot(x - p.x, z - p.z) < pad) return false;
    // Keep a trail from the park to the nest clearing.
    if (Math.abs(x) < 3.5 && z > -30 && z < -17) return false;
    for (const c of colliders) if (Math.hypot(x - c.x, z - c.z) < c.r + 0.8) return false;
    return true;
  };

  // Tree wall along the jungle's west, east and north edges (the playable boundary).
  for (let z = J.maxZ + 4; z > cfg.bounds.minZ; z -= 2.6) {
    for (const side of [-1, 1]) {
      const x = side * (cfg.bounds.maxX - 1 - rng() * 2.5);
      bigTrees.push({ x, z: z + rng(), ry: rng() * 6, s: 1.3 + rng() * 0.6 });
      colliders.push({ x, z, r: 1.0 });
    }
  }
  for (let x = cfg.bounds.minX; x < cfg.bounds.maxX; x += 2.6) {
    const z = cfg.bounds.minZ + 1 + rng() * 2.5;
    bigTrees.push({ x: x + rng(), z, ry: rng() * 6, s: 1.3 + rng() * 0.6 });
    colliders.push({ x, z, r: 1.0 });
  }

  // Clusters inside, leaving open clearings where prey graze.
  const clusters = 10;
  for (let c = 0; c < clusters; c++) {
    const cx = THREE.MathUtils.lerp(J.minX + 3, J.maxX - 3, rng());
    const cz = THREE.MathUtils.lerp(J.minZ + 3, J.maxZ - 2, rng());
    const n = 2 + Math.floor(rng() * 4);
    for (let i = 0; i < n; i++) {
      const x = cx + (rng() - 0.5) * 6;
      const z = cz + (rng() - 0.5) * 6;
      if (!clearOf(x, z, cfg.pads)) continue;
      const roll = rng();
      if (roll < 0.4) {
        bigTrees.push({ x, z, ry: rng() * 6, s: 1.1 + rng() * 0.6 });
        colliders.push({ x, z, r: 0.7 });
      } else if (roll < 0.65) {
        palms.push({ x, z, ry: rng() * 6, s: 1 + rng() * 0.5 });
        colliders.push({ x, z, r: 0.4 });
      } else {
        ferns.push({ x, z, ry: rng() * 6, s: 0.9 + rng() * 0.5 });
        colliders.push({ x, z, r: 0.35 });
      }
    }
  }
  for (let i = 0; i < 26; i++) {
    const x = THREE.MathUtils.lerp(J.minX, J.maxX, rng());
    const z = THREE.MathUtils.lerp(J.minZ, J.maxZ + 3, rng());
    if (!clearOf(x, z, cfg.pads, 3.5)) continue;
    const s = 0.8 + rng() * 1.4;
    rocks.push({ x, z, ry: rng() * 6, s });
    colliders.push({ x, z, r: 0.45 * s });
  }
  // Undergrowth is decorative only (walk-through).
  for (let i = 0; i < 90; i++) {
    const x = THREE.MathUtils.lerp(J.minX, J.maxX, rng());
    const z = THREE.MathUtils.lerp(J.minZ, J.maxZ + 4, rng());
    if (!clearOf(x, z, cfg.pads, 3)) continue;
    bushes.push({ x, z, ry: rng() * 6, s: 0.8 + rng() * 0.7 });
  }

  const flat = vertexColorMat({ flatShading: true });
  scene.add(instanced(roundTreeGeometry(rng, 0x3d8a38), flat, bigTrees));
  scene.add(instanced(palmGeometry(rng), flat, palms));
  scene.add(instanced(treeFernGeometry(rng), flat, ferns));
  scene.add(instanced(bushGeometry(rng), flat, bushes, false));
  scene.add(instanced(rockGeometry(rng, 0x8f8a80), flat, rocks));
  return colliders;
}
