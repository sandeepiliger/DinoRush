import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

/** Paints a flat vertex colour on a geometry (optionally with per-vertex brightness jitter). */
export function paint(geo: THREE.BufferGeometry, color: number, jitter = 0, rng: () => number = Math.random): THREE.BufferGeometry {
  const g = geo.index ? geo.toNonIndexed() : geo;
  for (const name of Object.keys(g.attributes)) if (name !== 'position' && name !== 'normal') g.deleteAttribute(name);
  const count = g.attributes.position.count;
  const colors = new Float32Array(count * 3);
  const c = new THREE.Color(color);
  const tmp = new THREE.Color();
  for (let i = 0; i < count; i += 3) {
    // Jitter per triangle so flat-shaded surfaces get a hand-painted look.
    const k = 1 + (rng() - 0.5) * jitter;
    tmp.copy(c).multiplyScalar(k);
    for (let v = 0; v < 3 && i + v < count; v++) {
      colors[(i + v) * 3] = tmp.r;
      colors[(i + v) * 3 + 1] = tmp.g;
      colors[(i + v) * 3 + 2] = tmp.b;
    }
  }
  g.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  return g;
}

export function at(geo: THREE.BufferGeometry, x: number, y: number, z: number, rx = 0, ry = 0, rz = 0, s: number | [number, number, number] = 1) {
  const m = new THREE.Matrix4();
  const scale = Array.isArray(s) ? new THREE.Vector3(...s) : new THREE.Vector3(s, s, s);
  m.compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, rz)), scale);
  geo.applyMatrix4(m);
  return geo;
}

export function merge(geos: THREE.BufferGeometry[]): THREE.BufferGeometry {
  const merged = mergeGeometries(geos, false);
  if (!merged) throw new Error('mergeGeometries failed (attribute mismatch)');
  geos.forEach((g) => g.dispose());
  return merged;
}

export const vertexColorMat = (opts: THREE.MeshStandardMaterialParameters = {}) =>
  new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.85, ...opts });

/** Scatters instances of a geometry at the given transforms. */
export function instanced(
  geo: THREE.BufferGeometry,
  material: THREE.Material,
  transforms: { x: number; y?: number; z: number; ry?: number; s?: number }[],
  castShadow = true,
): THREE.InstancedMesh {
  const im = new THREE.InstancedMesh(geo, material, Math.max(1, transforms.length));
  const m = new THREE.Matrix4();
  const q = new THREE.Quaternion();
  const e = new THREE.Euler();
  const v = new THREE.Vector3();
  const sc = new THREE.Vector3();
  transforms.forEach((t, i) => {
    e.set(0, t.ry ?? 0, 0);
    q.setFromEuler(e);
    v.set(t.x, t.y ?? 0, t.z);
    sc.setScalar(t.s ?? 1);
    m.compose(v, q, sc);
    im.setMatrixAt(i, m);
  });
  im.count = transforms.length;
  im.castShadow = castShadow;
  im.receiveShadow = true;
  return im;
}
