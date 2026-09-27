// Build pads: a glowing ring on the ground you walk onto. Unbuilt pads fill up as meat flies in; built
// pads show their building (market stall, DNA lab, nest). Labels are DOM, tracked to the pad.

import * as THREE from 'three';
import type { PadDef } from '../data/wild';
import { el } from '../ui/dom';
import { icons } from '../ui/icons';
import { at, merge, paint, vertexColorMat } from '../world/geometry';

export const PAD_RADIUS = 1.6;

export interface PadView {
  def: PadDef;
  group: THREE.Group;
  ring: THREE.Mesh;
  fill: THREE.Mesh;
  building: THREE.Group;
  ghost: THREE.Group;
  egg: THREE.Mesh | null;
  label: HTMLElement;
  /** World position of the building's front/centre, where delivered items fly to. */
  drop: THREE.Vector3;
  colliders: { x: number; z: number; r: number }[];
}

const RING_COLORS: Record<PadDef['kind'], number> = { market: 0xffc83d, lab: 0x3fd0c9, nest: 0x8fe07a };

export function buildPad(def: PadDef, scene: THREE.Scene, layer: HTMLElement): PadView {
  const group = new THREE.Group();
  group.position.set(def.x, 0, def.z);
  scene.add(group);
  const color = RING_COLORS[def.kind];

  const ring = new THREE.Mesh(
    new THREE.RingGeometry(PAD_RADIUS - 0.18, PAD_RADIUS, 48),
    new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.95 }),
  );
  ring.rotation.x = -Math.PI / 2;
  ring.position.y = 0.06;
  group.add(ring);
  const disc = new THREE.Mesh(new THREE.CircleGeometry(PAD_RADIUS - 0.18, 48), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.22 }));
  disc.rotation.x = -Math.PI / 2;
  disc.position.y = 0.05;
  group.add(disc);
  // Build progress: a pie slice that grows (geometry rebuilt only when progress changes).
  const fill = new THREE.Mesh(new THREE.CircleGeometry(PAD_RADIUS - 0.2, 48, Math.PI / 2, 0.0001), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.55 }));
  fill.rotation.x = -Math.PI / 2;
  fill.position.y = 0.055;
  group.add(fill);

  const building = new THREE.Group();
  const ghost = new THREE.Group();
  group.add(building, ghost);
  const vc = vertexColorMat();
  let egg: THREE.Mesh | null = null;
  const colliders: PadView['colliders'] = [];
  const drop = new THREE.Vector3(def.x, 1.2, def.z);

  if (def.kind === 'market') {
    // Market stall behind the pad (towards +z, outside the park).
    const stall = merge([
      paint(at(new THREE.BoxGeometry(2.6, 1.0, 1.2), 0, 0.5, 0), 0x9b6a3c, 0.1),
      paint(at(new THREE.BoxGeometry(2.4, 0.12, 1.0), 0, 1.06, 0), 0xe0c48a),
      paint(at(new THREE.CylinderGeometry(0.06, 0.06, 1.6, 6), -1.2, 1.8, -0.5), 0x6e4a2c),
      paint(at(new THREE.CylinderGeometry(0.06, 0.06, 1.6, 6), 1.2, 1.8, -0.5), 0x6e4a2c),
      paint(at(new THREE.CylinderGeometry(0.06, 0.06, 2.2, 6), -1.2, 1.1, 0.55), 0x6e4a2c),
      paint(at(new THREE.CylinderGeometry(0.06, 0.06, 2.2, 6), 1.2, 1.1, 0.55), 0x6e4a2c),
      ...[0, 1, 2, 3, 4].map((i) => paint(at(new THREE.BoxGeometry(0.54, 0.06, 1.5), -1.08 + i * 0.54, 2.35, 0, 0.3), i % 2 ? 0xffffff : 0xe8505b)),
      ...[-0.7, 0, 0.7].map((x) => paint(at(new THREE.CapsuleGeometry(0.12, 0.25, 4, 8), x, 1.25, 0, 0, 0, Math.PI / 2), 0xd9534f)),
    ]);
    const mesh = new THREE.Mesh(stall, vc);
    mesh.castShadow = true;
    mesh.position.set(0, 0, 2.3);
    building.add(mesh);
    colliders.push({ x: def.x, z: def.z + 2.3, r: 1.4 });
    drop.set(def.x, 1.3, def.z + 2.1);
  } else if (def.kind === 'lab') {
    const lab = merge([
      paint(at(new THREE.CylinderGeometry(1.3, 1.4, 1.2, 20), 0, 0.6, 0), 0xf2f4f5),
      paint(at(new THREE.SphereGeometry(1.25, 20, 10, 0, Math.PI * 2, 0, Math.PI / 2), 0, 1.2, 0), 0xbfeff0),
      paint(at(new THREE.BoxGeometry(0.8, 0.9, 0.2), 0, 0.45, -1.3), 0x2f8f8a),
      paint(at(new THREE.CylinderGeometry(0.05, 0.05, 1.2, 6), 0, 2.9, 0), 0x9aa5ab),
    ]);
    const mesh = new THREE.Mesh(lab, vc);
    mesh.castShadow = true;
    mesh.position.set(0, 0, 2.5);
    building.add(mesh);
    // Spinning DNA helix on the roof.
    const helix = new THREE.Group();
    helix.name = 'helix';
    const ballA = new THREE.MeshStandardMaterial({ color: 0x3fd0c9, emissive: 0x0f5c58, roughness: 0.3 });
    const ballB = new THREE.MeshStandardMaterial({ color: 0xff5c8a, emissive: 0x5c1026, roughness: 0.3 });
    for (let i = 0; i < 8; i++) {
      const a = i * 0.8;
      for (const [sign, m] of [[1, ballA], [-1, ballB]] as const) {
        const b = new THREE.Mesh(new THREE.SphereGeometry(0.1, 8, 6), m);
        b.position.set(Math.cos(a) * 0.3 * sign, i * 0.14, Math.sin(a) * 0.3 * sign);
        helix.add(b);
      }
    }
    helix.position.set(0, 3.4, 2.5);
    building.add(helix);
    colliders.push({ x: def.x, z: def.z + 2.5, r: 1.5 });
    drop.set(def.x, 1.3, def.z + 2);
  } else {
    // Nest: straw ring with sticks, sitting in the pad itself.
    const nest = merge([
      paint(new THREE.TorusGeometry(0.9, 0.3, 8, 20).rotateX(Math.PI / 2).translate(0, 0.25, 0), 0xb8894a, 0.25),
      paint(new THREE.CylinderGeometry(0.85, 0.85, 0.15, 20).translate(0, 0.12, 0), 0x8a6232),
      ...[0, 1, 2, 3, 4, 5].map((i) => paint(at(new THREE.CylinderGeometry(0.04, 0.04, 1.1, 5), Math.cos(i) * 0.9, 0.35, Math.sin(i) * 0.9, 0.4, i, 1.2), 0x6e4a2c)),
    ]);
    const mesh = new THREE.Mesh(nest, vc);
    mesh.castShadow = true;
    building.add(mesh);
    egg = new THREE.Mesh(new THREE.SphereGeometry(0.34, 16, 12), new THREE.MeshStandardMaterial({ color: 0xbfe8ff, emissive: 0x1f5c7a, emissiveIntensity: 0.5, roughness: 0.3 }));
    egg.scale.set(1, 1.3, 1);
    egg.position.y = 0.6;
    egg.visible = false;
    building.add(egg);
    // Construction ghost: a translucent outline of what will be built.
    const ghostMesh = new THREE.Mesh(nest.clone(), new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.25, depthWrite: false }));
    ghost.add(ghostMesh);
    drop.set(def.x, 0.6, def.z);
  }

  const label = el('div', `pad-label pad-${def.kind}`);
  layer.appendChild(label);
  return { def, group, ring, fill, building, ghost, egg, label, drop, colliders };
}

/** Updates the pie-slice fill to a 0..1 progress. */
export function setPadProgress(view: PadView, ratio: number) {
  const r = Math.max(0.0001, Math.min(1, ratio));
  const key = Math.round(r * 60);
  if (view.fill.userData.key === key) return;
  view.fill.userData.key = key;
  view.fill.geometry.dispose();
  view.fill.geometry = new THREE.CircleGeometry(PAD_RADIUS - 0.2, 48, Math.PI / 2, (key / 60) * Math.PI * 2 || 0.0001);
}

export const padIcons: Record<PadDef['kind'], string> = { market: icons.coin, lab: icons.dna, nest: icons.egg };
