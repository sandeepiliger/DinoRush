// "Loft" = sweep a varying elliptical cross-section along a planar (x/y) curve. It's the core modelling
// primitive for organic shapes here: bodies, necks, tails, heads and jaws are all lofts.

import * as THREE from 'three';

export interface LoftKnot {
  x: number;
  y: number;
  /** Half-width (z). */
  rw: number;
  /** Half-height, above the spine line. */
  rh: number;
  /** Optional half-height below the spine line (defaults to rh) — lets bellies sag or jaws flatten. */
  rb?: number;
}

export interface LoftOptions {
  segments?: number;
  ringsPerSpan?: number;
  /** Colour per vertex: `up` is -1 (belly) .. 1 (back), `t` is 0..1 along the loft, `arc` metres. */
  color?: (up: number, side: number, t: number, arc: number, out: THREE.Color) => void;
  capStart?: boolean;
  capEnd?: boolean;
}

export interface LoftResult {
  geometry: THREE.BufferGeometry;
  /** For each vertex: index of the knot span it belongs to and the blend towards the next knot. */
  spanOf: Int32Array;
  spanT: Float32Array;
  curve: THREE.CatmullRomCurve3;
  radii: THREE.CatmullRomCurve3;
}

export function loft(knots: LoftKnot[], opts: LoftOptions = {}): LoftResult {
  const seg = opts.segments ?? 18;
  const perSpan = opts.ringsPerSpan ?? 7;
  const curve = new THREE.CatmullRomCurve3(knots.map((k) => new THREE.Vector3(k.x, k.y, 0)));
  const radii = new THREE.CatmullRomCurve3(knots.map((k) => new THREE.Vector3(k.rw, k.rh, k.rb ?? k.rh)));
  const spans = knots.length - 1;
  const rings = spans * perSpan + 1;
  const positions: number[] = [];
  const colors: number[] = [];
  const spanOf: number[] = [];
  const spanT: number[] = [];
  const indices: number[] = [];
  const p = new THREE.Vector3();
  const tan = new THREE.Vector3();
  const r = new THREE.Vector3();
  const c = new THREE.Color();
  const prev = new THREE.Vector3();
  let arc = 0;
  curve.getPoint(0, prev);

  for (let i = 0; i < rings; i++) {
    const t = i / (rings - 1);
    curve.getPoint(t, p);
    curve.getTangent(t, tan);
    radii.getPoint(t, r);
    arc += p.distanceTo(prev);
    prev.copy(p);
    const nx = -tan.y;
    const ny = tan.x;
    const kp = t * spans;
    const k0 = Math.min(spans - 1, Math.floor(kp));
    for (let j = 0; j < seg; j++) {
      const a = (j / seg) * Math.PI * 2;
      const up = Math.cos(a);
      const side = Math.sin(a);
      const h = up >= 0 ? r.y : r.z;
      // Slightly squarer than an ellipse (superellipse) — reads as muscle rather than sausage.
      const sq = (v: number) => Math.sign(v) * Math.pow(Math.abs(v), 0.85);
      const u = sq(up);
      const s = sq(side);
      positions.push(p.x + nx * h * u, p.y + ny * h * u, s * r.x);
      if (opts.color) {
        opts.color(up, side, t, arc, c);
        colors.push(c.r, c.g, c.b);
      }
      spanOf.push(k0);
      spanT.push(kp - k0);
    }
  }
  for (let i = 0; i < rings - 1; i++) {
    for (let j = 0; j < seg; j++) {
      const a = i * seg + j;
      const b = i * seg + ((j + 1) % seg);
      // Counter-clockwise when seen from outside (ring angle runs +y -> +z, rings run along +x).
      indices.push(a, b, a + seg, b, b + seg, a + seg);
    }
  }
  const cap = (ring: number, flip: boolean) => {
    const start = ring * seg;
    let cx = 0, cy = 0, cz = 0;
    for (let j = 0; j < seg; j++) {
      cx += positions[(start + j) * 3];
      cy += positions[(start + j) * 3 + 1];
      cz += positions[(start + j) * 3 + 2];
    }
    const center = positions.length / 3;
    positions.push(cx / seg, cy / seg, cz / seg);
    if (opts.color) colors.push(colors[start * 3], colors[start * 3 + 1], colors[start * 3 + 2]);
    spanOf.push(spanOf[start]);
    spanT.push(spanT[start]);
    for (let j = 0; j < seg; j++) {
      const a = start + j;
      const b = start + ((j + 1) % seg);
      if (flip) indices.push(center, b, a);
      else indices.push(center, a, b);
    }
  };
  if (opts.capStart !== false) cap(0, true);
  if (opts.capEnd !== false) cap(rings - 1, false);

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  if (opts.color) geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  return { geometry, spanOf: Int32Array.from(spanOf), spanT: Float32Array.from(spanT), curve, radii };
}

/**
 * Adds a soft rim light to a standard material — the classic trick that makes characters "pop" off the
 * background in top-down mobile games, at the cost of three shader instructions.
 */
export function withRim(material: THREE.MeshStandardMaterial, color = 0xfff4d6, strength = 0.35, power = 2.6) {
  material.onBeforeCompile = (shader) => {
    shader.uniforms.rimColor = { value: new THREE.Color(color).multiplyScalar(strength) };
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nuniform vec3 rimColor;')
      .replace(
        '#include <emissivemap_fragment>',
        `#include <emissivemap_fragment>
        float rimF = pow(1.0 - clamp(dot(normal, normalize(vViewPosition)), 0.0, 1.0), ${power.toFixed(2)});
        totalEmissiveRadiance += rimColor * rimF;`,
      );
  };
  material.customProgramCacheKey = () => `rim-${color}-${strength}-${power}`;
  return material;
}
