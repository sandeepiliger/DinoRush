// Procedural dinosaur builder.
//
// Every dinosaur is generated from its SpeciesDef — no external art assets:
//  * body: one skinned loft from tail tip through hips to the neck, bound to a bone chain so the tail
//    swings and the neck bends;
//  * head: a sculpted skull loft plus a hinged jaw loft, glossy eyes with catch-lights, brows, teeth;
//  * legs: muscular thigh, tapered shin, and feet with toes/claws (theropod) or pads/nails (quadruped);
//  * species features: frill, horns, beak, crest, back plates, tail spikes.

import * as THREE from 'three';
import type { LegSpec, SpeciesDef } from '../data/species';
import { loft, withRim, type LoftKnot } from './loft';

export interface LegRig {
  hip: THREE.Group;
  knee: THREE.Group;
  ankle: THREE.Group;
  spec: LegSpec;
  /** Phase offset in the gait cycle (radians). */
  phase: number;
  length: number;
}

export interface DinoRig {
  root: THREE.Group;
  /** Moves up/down for the walk bob and breathing; everything visual hangs off it. */
  body: THREE.Group;
  bones: THREE.Bone[];
  hipBone: number;
  headPivot: THREE.Group;
  jaw: THREE.Group;
  legs: LegRig[];
  arms: THREE.Group[];
  species: SpeciesDef;
  /** Stride length in metres for one full gait cycle; used to lock foot speed to ground speed. */
  stride: number;
}

// Materials are shared per species so a herd costs one material each.
const materialCache = new Map<string, THREE.MeshStandardMaterial>();
function mat(key: string, make: () => THREE.MeshStandardMaterial) {
  let m = materialCache.get(key);
  if (!m) {
    m = make();
    materialCache.set(key, m);
  }
  return m;
}

const EYE_WHITE = new THREE.MeshStandardMaterial({ color: 0xfdfbf2, roughness: 0.2 });
const EYE_IRIS = new THREE.MeshStandardMaterial({ color: 0x1a120c, roughness: 0.1 });
const EYE_SHINE = new THREE.MeshBasicMaterial({ color: 0xffffff });
const TOOTH = new THREE.MeshStandardMaterial({ color: 0xfff8e6, roughness: 0.35 });
const CLAW = new THREE.MeshStandardMaterial({ color: 0x3a302a, roughness: 0.4 });
const NAIL = new THREE.MeshStandardMaterial({ color: 0xe8dcc0, roughness: 0.45 });
const MOUTH = new THREE.MeshStandardMaterial({ color: 0x8a2f35, roughness: 0.6 });
const TONGUE = new THREE.MeshStandardMaterial({ color: 0xd9646e, roughness: 0.5 });

export function buildDino(species: SpeciesDef, variant = 0.5): DinoRig {
  const root = new THREE.Group();
  root.name = `dino:${species.id}`;
  const body = new THREE.Group();
  root.add(body);

  const pal = species.palette;
  // Slight per-individual colour variation keeps a herd from looking copy-pasted.
  const vk = Math.round(variant * 4) / 4;
  const base = new THREE.Color(pal.base).offsetHSL((vk - 0.5) * 0.05, 0, (vk - 0.5) * 0.07);
  const belly = new THREE.Color(pal.belly);
  const accent = new THREE.Color(pal.accent);
  const skin = mat(`${species.id}:skin:${vk}`, () =>
    withRim(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.55 })),
  );
  const limbMat = mat(`${species.id}:limb:${vk}`, () =>
    withRim(new THREE.MeshStandardMaterial({ color: base.clone().multiplyScalar(0.95), roughness: 0.55 })),
  );
  const accentMat = mat(`${species.id}:accent`, () => withRim(new THREE.MeshStandardMaterial({ color: pal.accent, roughness: 0.55 })));
  const extraMat = mat(`${species.id}:extra`, () => withRim(new THREE.MeshStandardMaterial({ color: pal.extra, roughness: 0.5 })));
  const hornMat = mat('horn', () => withRim(new THREE.MeshStandardMaterial({ color: 0xf3ead2, roughness: 0.4 }), 0xffffff, 0.2));

  // Shared skin painter: dark dorsal band, stripes, subtle belly — used by body, head and jaw lofts.
  const paintSkin = (stripeFreq: number, stripeStrength: number) =>
    (up: number, side: number, _t: number, arc: number, out: THREE.Color) => {
      out.copy(base);
      const bellyK = THREE.MathUtils.smoothstep(-up, 0.3, 0.85);
      out.lerp(belly, bellyK * 0.9);
      const back = THREE.MathUtils.smoothstep(up, 0.45, 0.95);
      const stripe = Math.pow(Math.max(0, Math.sin(arc * stripeFreq)), 2.5) * stripeStrength * THREE.MathUtils.smoothstep(up, -0.1, 0.5);
      out.lerp(accent, Math.min(0.9, back * 0.45 + stripe));
      // Tiny mottling breaks up flat colour.
      const mottle = (Math.sin(arc * 23.1 + side * 9.7) * Math.sin(arc * 11.3 - up * 7.1)) * 0.035;
      out.offsetHSL(0, 0, mottle);
    };

  // ---------- Body loft + skeleton ----------
  const knots = species.spine;
  // Bodies sag a little below the spine line around the hips/chest: heavier, and it hides the thigh tops.
  const bodyKnots = knots.map((k, i) => (i > 0 && i < knots.length - 2 ? { ...k, rb: k.rh * 1.15 } : k));
  const bodyLoft = loft(bodyKnots, { segments: 20, ringsPerSpan: 8, color: paintSkin(7, pal.stripes) });
  const geo = bodyLoft.geometry;
  const n = geo.attributes.position.count;
  const skinIndex = new Uint16Array(n * 4);
  const skinWeight = new Float32Array(n * 4);
  for (let i = 0; i < n; i++) {
    skinIndex[i * 4] = bodyLoft.spanOf[i];
    skinIndex[i * 4 + 1] = bodyLoft.spanOf[i] + 1;
    skinWeight[i * 4] = 1 - bodyLoft.spanT[i];
    skinWeight[i * 4 + 1] = bodyLoft.spanT[i];
  }
  geo.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(skinIndex, 4));
  geo.setAttribute('skinWeight', new THREE.Float32BufferAttribute(skinWeight, 4));
  const bodyMesh = new THREE.SkinnedMesh(geo, skin);
  bodyMesh.castShadow = true;

  // One bone per knot. The hip knot is the root; chains run from it to the tail tip and to the neck.
  const bones = knots.map(() => new THREE.Bone());
  const hip = species.hipKnot;
  bones[hip].position.set(knots[hip].x, knots[hip].y, 0);
  for (let i = hip - 1; i >= 0; i--) {
    bones[i + 1].add(bones[i]);
    bones[i].position.set(knots[i].x - knots[i + 1].x, knots[i].y - knots[i + 1].y, 0);
  }
  for (let i = hip + 1; i < knots.length; i++) {
    bones[i - 1].add(bones[i]);
    bones[i].position.set(knots[i].x - knots[i - 1].x, knots[i].y - knots[i - 1].y, 0);
  }
  bodyMesh.add(bones[hip]);
  bodyMesh.updateMatrixWorld(true);
  bodyMesh.bind(new THREE.Skeleton(bones));
  // Cull off-screen bodies: bind-pose bounds, padded for tail swing and neck bend.
  bodyMesh.computeBoundingSphere();
  if (bodyMesh.boundingSphere) bodyMesh.boundingSphere.radius *= 1.5;
  body.add(bodyMesh);

  // ---------- Head ----------
  const hs = species.head.size;
  const H = species.head;
  const L = hs * H.snout; // snout length
  const sh = H.snoutHeight / 0.65; // snout thickness factor
  const headPivot = new THREE.Group();
  bones[knots.length - 1].add(headPivot);
  const head = new THREE.Group();
  headPivot.add(head);
  const neckEnd = knots[knots.length - 1];
  // Long-necked species hold the head level at the top of a vertical neck; others continue the neck line.
  head.position.set(hs * 0.12, hs * 0.15, 0);
  headPivot.rotation.z = neckEnd.y > 3 ? -0.2 : 0;

  const skullKnots: LoftKnot[] = [
    { x: -hs * 0.62, y: hs * 0.08, rw: hs * 0.42, rh: hs * 0.45, rb: hs * 0.4 },
    { x: -hs * 0.2, y: hs * 0.14, rw: hs * 0.8, rh: hs * 0.72, rb: hs * 0.55 },
    { x: hs * 0.35, y: hs * 0.12, rw: hs * 0.74, rh: hs * 0.66, rb: hs * 0.45 },
    { x: hs * 0.35 + L * 0.5, y: hs * 0.02, rw: hs * 0.55 * sh, rh: hs * 0.46 * sh, rb: hs * 0.3 * sh },
    { x: hs * 0.35 + L, y: -hs * 0.04, rw: hs * 0.4 * sh, rh: hs * 0.34 * sh, rb: hs * 0.22 * sh },
    { x: hs * 0.5 + L, y: -hs * 0.08, rw: hs * 0.14, rh: hs * 0.12, rb: hs * 0.08 },
  ];
  const skull = new THREE.Mesh(loft(skullKnots, { segments: 18, ringsPerSpan: 6, color: paintSkin(12, pal.stripes * 0.6) }).geometry, skin);
  skull.castShadow = true;
  head.add(skull);

  // Mouth interior sits under the upper jaw so an open mouth shows red, not a hollow shell.
  const mouth = new THREE.Mesh(new THREE.SphereGeometry(1, 16, 10), MOUTH);
  // Predators get a toothy grin; herbivores keep a small, closed smile.
  mouth.scale.set(L * 0.6 + hs * 0.3, hs * (H.teeth ? 0.14 : 0.08), hs * (H.teeth ? 0.5 : 0.36) * sh);
  mouth.position.set(hs * 0.2 + L * 0.45, -hs * (H.teeth ? 0.28 : 0.22) * sh, 0);
  head.add(mouth);

  // Jaw hinges below the back of the skull; rotating it negative about z opens the mouth.
  const jaw = new THREE.Group();
  jaw.position.set(-hs * 0.3, -hs * 0.22, 0);
  head.add(jaw);
  const jawKnots: LoftKnot[] = [
    { x: 0, y: 0, rw: hs * 0.5, rh: hs * 0.2, rb: hs * 0.3 },
    { x: hs * 0.6, y: -hs * 0.08 * sh, rw: hs * 0.55 * sh, rh: hs * 0.14, rb: hs * 0.26 * sh },
    { x: hs * 0.6 + L * 0.85, y: -hs * 0.04 * sh, rw: hs * 0.34 * sh, rh: hs * 0.1, rb: hs * 0.15 * sh },
    { x: hs * 0.72 + L * 0.9, y: -hs * 0.02, rw: hs * 0.12, rh: hs * 0.06, rb: hs * 0.07 },
  ];
  const jawMesh = new THREE.Mesh(loft(jawKnots, { segments: 14, ringsPerSpan: 5, color: paintSkin(12, 0) }).geometry, skin);
  jawMesh.castShadow = true;
  jaw.add(jawMesh);
  const tongue = new THREE.Mesh(new THREE.SphereGeometry(1, 12, 8), TONGUE);
  tongue.scale.set(L * 0.45 + hs * 0.2, hs * 0.07, hs * 0.28 * sh);
  tongue.position.set(hs * 0.45 + L * 0.35, hs * 0.08, 0);
  jaw.add(tongue);

  if (H.teeth) {
    const toothGeo = new THREE.ConeGeometry(hs * 0.055, hs * 0.18, 5);
    toothGeo.rotateX(Math.PI);
    const count = 5;
    for (const sz of [-1, 1]) {
      for (let i = 0; i < count; i++) {
        const k = i / (count - 1);
        const x = hs * 0.4 + k * L * 0.95;
        const w = THREE.MathUtils.lerp(hs * 0.62 * sh, hs * 0.34 * sh, k);
        const tooth = new THREE.Mesh(toothGeo, TOOTH);
        tooth.position.set(x, -hs * (0.2 + 0.1 * sh) + k * hs * 0.04, sz * w * 0.78);
        tooth.scale.setScalar(1 - k * 0.3);
        head.add(tooth);
      }
    }
  }
  if (H.beak) {
    const beak = new THREE.Mesh(new THREE.ConeGeometry(hs * 0.26 * sh, hs * 0.55, 12), hornMat);
    beak.rotation.z = -Math.PI / 2 - 0.25;
    beak.position.set(hs * 0.5 + L, -hs * 0.12, 0);
    beak.scale.set(1, 1, 0.75);
    head.add(beak);
  }

  // Eyes: big and glossy with a catch-light — eyes carry most of a character's appeal.
  const eyeR = hs * H.eye;
  for (const sz of [-1, 1]) {
    const eye = new THREE.Group();
    eye.position.set(hs * 0.1, hs * 0.32, sz * hs * 0.64);
    head.add(eye);
    const white = new THREE.Mesh(new THREE.SphereGeometry(eyeR, 16, 12), EYE_WHITE);
    eye.add(white);
    const iris = new THREE.Mesh(new THREE.SphereGeometry(eyeR * 0.66, 14, 10), EYE_IRIS);
    iris.position.set(eyeR * 0.28, eyeR * 0.04, sz * eyeR * 0.5);
    eye.add(iris);
    const shine = new THREE.Mesh(new THREE.SphereGeometry(eyeR * 0.22, 8, 6), EYE_SHINE);
    shine.position.set(eyeR * 0.5, eyeR * 0.4, sz * eyeR * 0.82);
    eye.add(shine);
    // Heavy brow ridge: expression, and it reads from the top-down camera.
    const brow = new THREE.Mesh(new THREE.CapsuleGeometry(eyeR * 0.36, eyeR * 1.3, 4, 8), accentMat);
    brow.rotation.set(sz * 0.25, 0, Math.PI / 2 + (H.teeth ? 0.3 : 0.1));
    brow.position.set(eyeR * 0.1, eyeR * 0.82, -sz * eyeR * 0.05);
    eye.add(brow);
  }
  // Nostrils.
  const nostrilGeo = new THREE.SphereGeometry(hs * 0.05, 6, 4);
  for (const sz of [-1, 1]) {
    const nostril = new THREE.Mesh(nostrilGeo, EYE_IRIS);
    nostril.position.set(hs * 0.3 + L * 0.95, hs * 0.14 * sh, sz * hs * 0.18 * sh);
    head.add(nostril);
  }

  if (H.crest) {
    if (species.id === 'brachiosaurus') {
      const dome = new THREE.Mesh(new THREE.SphereGeometry(hs * 0.45, 14, 10), limbMat);
      dome.scale.set(1.1, 0.8, 0.8);
      dome.position.set(hs * 0.1, hs * 0.7, 0);
      head.add(dome);
    } else {
      // Feather-like crest spikes.
      const crestGeo = new THREE.ConeGeometry(hs * 0.1, hs * 0.55, 6);
      for (let i = 0; i < 5; i++) {
        const spike = new THREE.Mesh(crestGeo, extraMat);
        spike.position.set(-hs * 0.05 - i * hs * 0.22, hs * 0.78 - i * hs * 0.06, 0);
        spike.rotation.z = 0.7 + i * 0.1;
        spike.scale.set(1, 1 - i * 0.12, 0.5);
        head.add(spike);
      }
    }
  }
  if (H.frill) {
    const frillGroup = new THREE.Group();
    frillGroup.position.set(-hs * 0.55, hs * 0.45, 0);
    frillGroup.rotation.z = 0.75;
    head.add(frillGroup);
    // A full shield disc; its lower half is hidden behind the head and neck.
    const frill = new THREE.Mesh(new THREE.CylinderGeometry(hs * 1.3, hs * 1.15, hs * 0.12, 32), extraMat);
    frill.rotation.z = Math.PI / 2;
    frill.position.y = hs * 0.35;
    frill.castShadow = true;
    frillGroup.add(frill);
    const inner = new THREE.Mesh(new THREE.CylinderGeometry(hs * 0.8, hs * 0.7, hs * 0.14, 28), accentMat);
    inner.rotation.z = Math.PI / 2;
    inner.position.set(hs * 0.02, hs * 0.35, 0);
    frillGroup.add(inner);
    const knobGeo = new THREE.SphereGeometry(hs * 0.15, 8, 6);
    for (let i = 0; i <= 10; i++) {
      const a = -Math.PI * 0.6 + (i / 10) * Math.PI * 1.2;
      const knob = new THREE.Mesh(knobGeo, accentMat);
      knob.position.set(0, hs * 0.35 + Math.cos(a) * hs * 1.3, Math.sin(a) * hs * 1.3);
      frillGroup.add(knob);
    }
  }
  if (H.horns) {
    const hornGeo = new THREE.ConeGeometry(hs * 0.12, hs * 1.1, 10);
    hornGeo.translate(0, hs * 0.55, 0);
    for (const sz of [-1, 1]) {
      const horn = new THREE.Mesh(hornGeo, hornMat);
      horn.position.set(hs * 0.15, hs * 0.72, sz * hs * 0.38);
      horn.rotation.set(sz * 0.15, 0, -1.0);
      horn.castShadow = true;
      head.add(horn);
    }
  }
  if (H.noseHorn) {
    const horn = new THREE.Mesh(new THREE.ConeGeometry(hs * 0.11, hs * 0.45, 10), hornMat);
    horn.position.set(hs * 0.35 + L * 0.75, hs * 0.38 * sh, 0);
    horn.rotation.z = -0.3;
    head.add(horn);
  }

  // ---------- Back plates / tail features ----------
  const spans = knots.length - 1;
  if (species.plates) {
    const shape = new THREE.Shape();
    shape.moveTo(-0.5, 0);
    shape.quadraticCurveTo(-0.5, 0.55, 0, 1);
    shape.quadraticCurveTo(0.5, 0.55, 0.5, 0);
    shape.lineTo(-0.5, 0);
    const plateGeo = new THREE.ExtrudeGeometry(shape, { depth: 0.04, bevelEnabled: true, bevelSize: 0.03, bevelThickness: 0.03, bevelSegments: 2 });
    plateGeo.translate(0, 0, -0.02);
    const { count, size } = species.plates;
    const p = new THREE.Vector3();
    const r = new THREE.Vector3();
    for (let i = 0; i < count; i++) {
      const t = 0.14 + (i / (count - 1)) * 0.66;
      bodyLoft.curve.getPoint(t, p);
      bodyLoft.radii.getPoint(t, r);
      const bump = Math.sin(((i + 0.5) / count) * Math.PI);
      const plate = new THREE.Mesh(plateGeo, i % 2 ? extraMat : accentMat);
      const s = size * (0.45 + bump * 0.75);
      plate.scale.set(s, s, 1);
      const knotIdx = Math.round(t * spans);
      const k = knots[knotIdx];
      plate.position.set(p.x - k.x, p.y - k.y + r.y * 0.8, (i % 2 ? 1 : -1) * r.x * 0.12);
      plate.rotation.y = (i % 2 ? 1 : -1) * 0.1;
      plate.castShadow = true;
      bones[knotIdx].add(plate);
    }
  }
  if (species.tailSpikes) {
    const spikeGeo = new THREE.ConeGeometry(0.05, 0.55, 8);
    spikeGeo.translate(0, 0.27, 0);
    const tailBone = bones[1];
    for (const [dx, sz] of [[0.05, 1], [0.05, -1], [0.3, 1], [0.3, -1]] as const) {
      const spike = new THREE.Mesh(spikeGeo, hornMat);
      spike.position.set(dx - 0.15, 0.06, sz * 0.08);
      spike.rotation.set(sz * 1.05, 0, 0.45);
      tailBone.add(spike);
    }
  }

  // ---------- Legs ----------
  const legs: LegRig[] = [];
  const addLegPair = (spec: LegSpec, phaseBase: number, front: boolean) => {
    // The hip joint sits inside the body (not under it), so the thigh grows out of the flank.
    const at = spineAt(knots, spec.x);
    const hipY = Math.max(species.hipHeight, at.y - at.rh * 0.3);
    const length = hipY / (Math.cos(spec.thighAngle) + Math.cos(spec.shinAngle));
    const th = spec.thickness;
    const theropod = species.gait === 'biped';
    for (const sz of [-1, 1]) {
      const hipG = new THREE.Group();
      hipG.position.set(spec.x, hipY, sz * spec.spread);
      body.add(hipG);
      // Thigh: a tapered "drumstick" (wide at the hip, narrow at the knee) whose top is buried in the
      // flank, so the leg reads as growing out of the body rather than bolted on.
      const w = th * (front ? 1.25 : 1.6);
      const thighGeo = new THREE.LatheGeometry([
        new THREE.Vector2(0.001, length * 0.32),
        new THREE.Vector2(w * 0.8, length * 0.22),
        new THREE.Vector2(w, 0),
        new THREE.Vector2(w * 0.92, -length * 0.35),
        new THREE.Vector2(th * 0.72, -length * 0.82),
        new THREE.Vector2(th * 0.62, -length),
        new THREE.Vector2(0.001, -length * 1.02),
      ], 16);
      thighGeo.scale(1, 1, 0.78);
      const thigh = new THREE.Mesh(thighGeo, limbMat);
      thigh.castShadow = true;
      hipG.add(thigh);
      const knee = new THREE.Group();
      knee.position.y = -length;
      hipG.add(knee);
      const shin = new THREE.Mesh(new THREE.CylinderGeometry(th * 0.68, th * 0.48, length, 12), limbMat);
      shin.position.y = -length / 2;
      shin.castShadow = true;
      knee.add(shin);
      const kneeCap = new THREE.Mesh(new THREE.SphereGeometry(th * 0.7, 12, 8), limbMat);
      knee.add(kneeCap);
      const ankle = new THREE.Group();
      ankle.position.y = -length;
      knee.add(ankle);
      if (theropod) {
        // Three forward toes with dark claws.
        const toeLen = th * 1.7 * spec.foot;
        const toeGeo = new THREE.CapsuleGeometry(th * 0.26, toeLen, 4, 8);
        toeGeo.rotateZ(-Math.PI / 2);
        toeGeo.translate(toeLen * 0.5, 0, 0);
        const clawGeo = new THREE.ConeGeometry(th * 0.18, th * 0.55, 6);
        clawGeo.rotateZ(-Math.PI / 2 - 0.5);
        for (const [a, len] of [[-0.35, 0.85], [0, 1], [0.35, 0.85]] as const) {
          const toe = new THREE.Mesh(toeGeo, limbMat);
          toe.scale.set(len, 1, 1);
          toe.rotation.y = a;
          toe.position.y = -th * 0.1;
          toe.castShadow = true;
          ankle.add(toe);
          const claw = new THREE.Mesh(clawGeo, CLAW);
          const reach = toeLen * len + th * 0.3;
          claw.position.set(Math.cos(a) * reach, -th * 0.05, -Math.sin(a) * reach);
          ankle.add(claw);
        }
        const heel = new THREE.Mesh(new THREE.SphereGeometry(th * 0.5, 10, 8), limbMat);
        heel.position.y = -th * 0.05;
        ankle.add(heel);
      } else {
        // Round, elephant-like foot pad with toenails.
        const pad = new THREE.Mesh(new THREE.CylinderGeometry(th * 0.62, th * 0.75, th * 0.55, 14), limbMat);
        pad.position.set(th * 0.12, -th * 0.12, 0);
        pad.castShadow = true;
        ankle.add(pad);
        const nailGeo = new THREE.SphereGeometry(th * 0.17, 8, 6);
        for (const a of [-0.6, 0, 0.6]) {
          const nail = new THREE.Mesh(nailGeo, NAIL);
          nail.scale.set(1, 0.8, 1);
          nail.position.set(th * 0.12 + Math.cos(a) * th * 0.72, -th * 0.22, Math.sin(a) * th * 0.72);
          ankle.add(nail);
        }
      }
      legs.push({ hip: hipG, knee, ankle, spec, length, phase: phaseBase + (sz > 0 ? Math.PI : 0) });
    }
  };
  addLegPair(species.hindLegs, 0, false);
  // Quadrupeds use a diagonal gait: front-left moves with hind-right.
  if (species.frontLegs) addLegPair(species.frontLegs, Math.PI * 0.5, true);

  // ---------- Arms (bipeds) ----------
  const arms: THREE.Group[] = [];
  if (species.arms) {
    const a = species.arms;
    const chestKnot = Math.min(knots.length - 1, hip + 1);
    const k = knots[chestKnot];
    for (const sz of [-1, 1]) {
      const shoulder = new THREE.Group();
      shoulder.position.set(a.x - k.x, -k.rh * 0.3, sz * k.rw * 0.8);
      shoulder.rotation.z = -0.6;
      bones[chestKnot].add(shoulder);
      const upper = new THREE.Mesh(new THREE.CapsuleGeometry(a.thickness, a.length * 0.5, 4, 8), limbMat);
      upper.position.y = -a.length * 0.3;
      shoulder.add(upper);
      const fore = new THREE.Mesh(new THREE.CapsuleGeometry(a.thickness * 0.8, a.length * 0.42, 4, 8), limbMat);
      fore.position.set(a.length * 0.22, -a.length * 0.66, 0);
      fore.rotation.z = 1.2;
      shoulder.add(fore);
      const clawGeo = new THREE.ConeGeometry(a.thickness * 0.4, a.thickness * 1.4, 5);
      for (const cz of [-0.5, 0.5]) {
        const claw = new THREE.Mesh(clawGeo, CLAW);
        claw.position.set(a.length * 0.5, -a.length * 0.78, cz * a.thickness);
        claw.rotation.z = Math.PI - 0.4;
        shoulder.add(claw);
      }
      arms.push(shoulder);
    }
  }

  const avgLen = legs.reduce((s, l) => s + l.length, 0) / legs.length;
  const amp = species.gait === 'biped' ? 0.5 : 0.36;
  const stride = 4 * Math.sin(amp) * avgLen;

  root.scale.setScalar(species.scale);
  root.traverse((o) => {
    if ((o as THREE.Mesh).isMesh) (o as THREE.Mesh).castShadow = true;
  });

  return { root, body, bones, hipBone: hip, headPivot, jaw, legs, arms, species, stride };
}

/** Linear sample of the spine's height and half-height at a given x (used to seat the legs). */
function spineAt(knots: SpeciesDef['spine'], x: number): { y: number; rh: number } {
  for (let i = 0; i < knots.length - 1; i++) {
    const a = knots[i];
    const b = knots[i + 1];
    if ((x >= a.x && x <= b.x) || (x <= a.x && x >= b.x)) {
      const t = b.x === a.x ? 0 : (x - a.x) / (b.x - a.x);
      return { y: a.y + (b.y - a.y) * t, rh: a.rh + (b.rh - a.rh) * t };
    }
  }
  const last = x < knots[0].x ? knots[0] : knots[knots.length - 1];
  return { y: last.y, rh: last.rh };
}
