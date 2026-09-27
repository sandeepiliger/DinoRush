// Low-poly prop geometries, each merged into one vertex-coloured BufferGeometry so it can be instanced.

import * as THREE from 'three';
import { at, merge, paint } from './geometry';

export function palmGeometry(rng: () => number): THREE.BufferGeometry {
  const parts: THREE.BufferGeometry[] = [];
  // Curved trunk from stacked, slightly offset segments.
  const segs = 6;
  let x = 0;
  let y = 0;
  for (let i = 0; i < segs; i++) {
    const r0 = 0.16 - i * 0.012;
    const seg = new THREE.CylinderGeometry(r0 - 0.012, r0, 0.62, 7);
    parts.push(paint(at(seg, x, y + 0.31, 0, 0, 0, -0.06 * i), i % 2 ? 0x8a6a45 : 0x9b7a50, 0.1, rng));
    x += 0.04 * i;
    y += 0.6;
  }
  // Fronds: long drooping leaves.
  const fronds = 7;
  for (let i = 0; i < fronds; i++) {
    const a = (i / fronds) * Math.PI * 2 + rng() * 0.3;
    const leaf = new THREE.ConeGeometry(0.22, 1.9, 4, 1);
    leaf.scale(1, 1, 0.25);
    leaf.translate(0, 0.95, 0); // base of the leaf at the origin
    leaf.applyMatrix4(new THREE.Matrix4().makeRotationY(a).multiply(new THREE.Matrix4().makeRotationZ(-1.2 - rng() * 0.3)));
    leaf.translate(x, y, 0);
    parts.push(paint(leaf, i % 2 ? 0x3f9b3a : 0x4fb043, 0.15, rng));
  }
  // Coconuts.
  for (let i = 0; i < 3; i++) {
    const a = (i / 3) * Math.PI * 2;
    parts.push(paint(at(new THREE.SphereGeometry(0.1, 6, 5), x + Math.cos(a) * 0.14, y - 0.1, Math.sin(a) * 0.14), 0x6b4a2a, 0.1, rng));
  }
  return merge(parts);
}

export function roundTreeGeometry(rng: () => number, tint = 0x4f9d45): THREE.BufferGeometry {
  const parts: THREE.BufferGeometry[] = [];
  parts.push(paint(at(new THREE.CylinderGeometry(0.12, 0.18, 1.2, 7), 0, 0.6, 0), 0x7a5a3a, 0.1, rng));
  const blobs = [
    [0, 1.7, 0, 0.85],
    [0.45, 1.45, 0.2, 0.55],
    [-0.4, 1.5, -0.15, 0.6],
    [0.05, 2.25, 0.05, 0.55],
  ];
  const c = new THREE.Color(tint);
  for (const [bx, by, bz, r] of blobs) {
    const blob = new THREE.IcosahedronGeometry(r, 1);
    const col = c.clone().offsetHSL((rng() - 0.5) * 0.03, 0, (rng() - 0.5) * 0.08);
    parts.push(paint(at(blob, bx, by, bz), col.getHex(), 0.18, rng));
  }
  return merge(parts);
}

export function rockGeometry(rng: () => number, color = 0x9a958c): THREE.BufferGeometry {
  const g = new THREE.DodecahedronGeometry(0.5, 0);
  const pos = g.attributes.position;
  for (let i = 0; i < pos.count; i++) {
    pos.setXYZ(i, pos.getX(i) * (0.8 + rng() * 0.4), pos.getY(i) * (0.55 + rng() * 0.3), pos.getZ(i) * (0.8 + rng() * 0.4));
  }
  g.computeVertexNormals();
  return paint(at(g, 0, 0.18, 0), color, 0.2, rng);
}

export function bushGeometry(rng: () => number): THREE.BufferGeometry {
  const parts: THREE.BufferGeometry[] = [];
  for (let i = 0; i < 3; i++) {
    const r = 0.32 + rng() * 0.15;
    parts.push(paint(at(new THREE.IcosahedronGeometry(r, 1), (i - 1) * 0.3, r * 0.8, (rng() - 0.5) * 0.2), i === 1 ? 0x3e8f3a : 0x4aa047, 0.2, rng));
  }
  // A few flowers make the park feel cared for.
  const flowerColors = [0xff6b8a, 0xffd23f, 0xffffff];
  for (let i = 0; i < 4; i++) {
    parts.push(paint(at(new THREE.IcosahedronGeometry(0.07, 0), (rng() - 0.5) * 0.8, 0.55 + rng() * 0.2, (rng() - 0.5) * 0.4), flowerColors[i % 3], 0, rng));
  }
  return merge(parts);
}

export function lampGeometry(): THREE.BufferGeometry {
  return merge([
    paint(at(new THREE.CylinderGeometry(0.05, 0.07, 2.2, 6), 0, 1.1, 0), 0x2f3b45),
    paint(at(new THREE.CylinderGeometry(0.12, 0.12, 0.08, 8), 0, 0.04, 0), 0x2f3b45),
    paint(at(new THREE.SphereGeometry(0.17, 10, 8), 0, 2.3, 0), 0xfff2c4),
  ]);
}

export function benchGeometry(): THREE.BufferGeometry {
  return merge([
    paint(at(new THREE.BoxGeometry(1.1, 0.06, 0.34), 0, 0.42, 0), 0xa0703f),
    paint(at(new THREE.BoxGeometry(1.1, 0.26, 0.05), 0, 0.62, -0.16, -0.15), 0xa0703f),
    paint(at(new THREE.BoxGeometry(0.06, 0.42, 0.3), -0.48, 0.21, 0), 0x333a40),
    paint(at(new THREE.BoxGeometry(0.06, 0.42, 0.3), 0.48, 0.21, 0), 0x333a40),
  ]);
}

export function fencePostGeometry(): THREE.BufferGeometry {
  return merge([
    paint(at(new THREE.CylinderGeometry(0.07, 0.08, 1.1, 6), 0, 0.55, 0), 0x8b5e34, 0.1),
    paint(at(new THREE.ConeGeometry(0.085, 0.14, 6), 0, 1.17, 0), 0x6e4726),
  ]);
}
