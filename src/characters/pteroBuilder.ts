// Procedural pterodactyl that carries gift crates across the park. Faces +x; wings flap about x.

import * as THREE from 'three';
import { loft, withRim } from './loft';

export interface PteroRig {
  root: THREE.Group;
  wings: { inner: THREE.Group; outer: THREE.Group; side: number }[];
  crate: THREE.Group;
  jaw: THREE.Group;
}

export function buildPterodactyl(): PteroRig {
  const root = new THREE.Group();
  const skin = withRim(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.55 }));
  const membrane = withRim(new THREE.MeshStandardMaterial({ color: 0xd9824a, roughness: 0.6, side: THREE.DoubleSide }));
  const crestMat = withRim(new THREE.MeshStandardMaterial({ color: 0xe8505b, roughness: 0.5 }));
  const base = new THREE.Color(0xc96a3a);
  const belly = new THREE.Color(0xf3d9b0);
  const paint = (up: number, _s: number, _t: number, _a: number, out: THREE.Color) =>
    out.copy(base).lerp(belly, THREE.MathUtils.smoothstep(-up, 0.2, 0.8));

  const body = new THREE.Mesh(
    loft(
      [
        { x: -0.9, y: 0, rw: 0.03, rh: 0.03 },
        { x: -0.5, y: 0.02, rw: 0.12, rh: 0.12 },
        { x: 0, y: 0.05, rw: 0.28, rh: 0.27 },
        { x: 0.45, y: 0.12, rw: 0.2, rh: 0.2 },
        { x: 0.7, y: 0.25, rw: 0.13, rh: 0.13 },
      ],
      { segments: 14, ringsPerSpan: 5, color: paint },
    ).geometry,
    skin,
  );
  body.castShadow = true;
  root.add(body);

  // Head: skull, long beak, back crest, jaw that can squawk.
  const head = new THREE.Group();
  head.position.set(0.78, 0.3, 0);
  root.add(head);
  const skull = new THREE.Mesh(new THREE.SphereGeometry(0.17, 14, 10), skin);
  skull.scale.set(1.2, 0.9, 0.85);
  head.add(skull);
  const beak = new THREE.Mesh(new THREE.ConeGeometry(0.09, 0.62, 10), new THREE.MeshStandardMaterial({ color: 0xf1c75b, roughness: 0.4 }));
  beak.rotation.z = -Math.PI / 2;
  beak.position.set(0.42, 0.01, 0);
  head.add(beak);
  const jaw = new THREE.Group();
  jaw.position.set(0.1, -0.05, 0);
  head.add(jaw);
  const lower = new THREE.Mesh(new THREE.ConeGeometry(0.06, 0.5, 8), beak.material);
  lower.rotation.z = -Math.PI / 2;
  lower.position.set(0.28, -0.02, 0);
  jaw.add(lower);
  const crest = new THREE.Mesh(new THREE.ConeGeometry(0.07, 0.55, 8), crestMat);
  crest.rotation.z = Math.PI / 2 + 0.35;
  crest.position.set(-0.3, 0.12, 0);
  crest.scale.set(1, 1, 0.4);
  head.add(crest);
  for (const sz of [-1, 1]) {
    const eye = new THREE.Mesh(new THREE.SphereGeometry(0.055, 10, 8), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.2 }));
    eye.position.set(0.05, 0.06, sz * 0.13);
    head.add(eye);
    const pupil = new THREE.Mesh(new THREE.SphereGeometry(0.034, 8, 6), new THREE.MeshStandardMaterial({ color: 0x111111 }));
    pupil.position.set(0.075, 0.065, sz * 0.165);
    head.add(pupil);
  }

  // Wings: inner and outer membrane panels, hinged so the flap has a whip to it.
  const panel = (w: number, d: number) => {
    const shape = new THREE.Shape();
    shape.moveTo(0, 0);
    shape.lineTo(w, 0.05);
    shape.quadraticCurveTo(w * 0.6, -d * 0.6, 0, -d);
    shape.lineTo(0, 0);
    const g = new THREE.ShapeGeometry(shape, 6);
    g.rotateX(Math.PI / 2); // lay flat in the xz plane
    g.rotateY(Math.PI / 2); // width along -z, trailing edge towards -x (backwards)
    return g;
  };
  const wings: PteroRig['wings'] = [];
  for (const side of [-1, 1]) {
    const inner = new THREE.Group();
    inner.position.set(0.15, 0.12, side * 0.18);
    root.add(inner);
    const innerMesh = new THREE.Mesh(panel(1.0, 0.7), membrane);
    innerMesh.scale.z = -side;
    innerMesh.castShadow = true;
    inner.add(innerMesh);
    const outer = new THREE.Group();
    outer.position.set(0, 0, side * 0.98);
    inner.add(outer);
    const outerMesh = new THREE.Mesh(panel(1.1, 0.5), membrane);
    outerMesh.scale.z = -side;
    outerMesh.castShadow = true;
    outer.add(outerMesh);
    wings.push({ inner, outer, side });
  }

  // Gift crate hanging from its feet.
  const crate = new THREE.Group();
  crate.position.set(-0.05, -0.75, 0);
  root.add(crate);
  const box = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.45, 0.55), withRim(new THREE.MeshStandardMaterial({ color: 0x3fa7d6, roughness: 0.5 })));
  box.castShadow = true;
  crate.add(box);
  const ribbonMat = new THREE.MeshStandardMaterial({ color: 0xffd23f, roughness: 0.4 });
  const r1 = new THREE.Mesh(new THREE.BoxGeometry(0.57, 0.47, 0.1), ribbonMat);
  const r2 = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.47, 0.57), ribbonMat);
  const bow = new THREE.Mesh(new THREE.TorusGeometry(0.09, 0.035, 6, 12), ribbonMat);
  bow.position.y = 0.28;
  crate.add(r1, r2, bow);
  const rope = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, 0.55, 4), new THREE.MeshStandardMaterial({ color: 0x6b4a2a }));
  rope.position.y = 0.48;
  crate.add(rope);

  root.scale.setScalar(1.3);
  return { root, wings, crate, jaw };
}

/** Flap pose for time t (seconds). */
export function flapPtero(rig: PteroRig, t: number): void {
  const flap = Math.sin(t * 7);
  for (const w of rig.wings) {
    w.inner.rotation.x = w.side * flap * 0.55;
    w.outer.rotation.x = w.side * Math.sin(t * 7 - 0.6) * 0.45;
  }
  rig.crate.rotation.z = Math.sin(t * 2.2) * 0.12;
  rig.crate.rotation.x = Math.sin(t * 1.7) * 0.08;
  rig.jaw.rotation.z = Math.sin(t * 0.8) > 0.93 ? -0.35 : 0;
}
