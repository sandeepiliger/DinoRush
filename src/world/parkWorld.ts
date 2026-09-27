// Builds the static park: terrain, paths, entrance, city skyline, enclosures and their scenery.

import * as THREE from 'three';
import type { EnclosureDef, ParkDef } from '../data/parks';
import { mulberry32 } from '../sim/parkSim';
import { ROUTE_POINTS } from '../sim/parkSim';
import { at, instanced, merge, paint, vertexColorMat } from './geometry';
import { benchGeometry, bushGeometry, fencePostGeometry, lampGeometry, palmGeometry, rockGeometry, roundTreeGeometry } from './props';

/** Ground colour per enclosure theme. */
const ENCLOSURE_GROUND: Record<string, number> = {
  raptor: 0x8fbf5a,
  triceratops: 0xa6c96a,
  stegosaurus: 0x9dbb63,
  brachiosaurus: 0x86b457,
  trex: 0xc9a46a,
};

export interface EnclosureView {
  def: EnclosureDef;
  group: THREE.Group;
  rails: THREE.Group;
  scenery: THREE.Group;
  lockedDecor: THREE.Group;
  ground: THREE.Mesh;
  /** World position of the food trough (dinos walk here to eat). */
  trough: THREE.Vector3;
  setUnlocked(unlocked: boolean): void;
}

export class ParkWorld {
  readonly scene = new THREE.Scene();
  readonly sun: THREE.DirectionalLight;
  readonly enclosures = new Map<string, EnclosureView>();
  readonly gatePosition = new THREE.Vector3(0, 0, 10.2);
  private vc = vertexColorMat();
  private vcFlat = vertexColorMat({ flatShading: true });

  constructor(private park: ParkDef) {
    const rng = mulberry32(2024);
    const scene = this.scene;
    const skyTop = new THREE.Color(0x8fd3ff);
    scene.background = skyTop;
    scene.fog = new THREE.Fog(0xbfe6ff, 55, 110);

    const hemi = new THREE.HemisphereLight(0xdff3ff, 0x6b8f4a, 1.35);
    scene.add(hemi);
    this.sun = new THREE.DirectionalLight(0xfff1d6, 2.4);
    this.sun.position.set(14, 26, 12);
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(2048, 2048);
    const sc = this.sun.shadow.camera;
    sc.left = -26;
    sc.right = 26;
    sc.top = 26;
    sc.bottom = -26;
    sc.near = 1;
    sc.far = 80;
    this.sun.shadow.bias = -0.0005;
    this.sun.shadow.normalBias = 0.03;
    this.sun.target.position.set(0, 0, -2);
    scene.add(this.sun, this.sun.target);

    this.buildGround(rng);
    this.buildPaths();
    this.buildEntrance();
    this.buildSkyline(rng);
    this.buildDecor(rng);
    for (const def of park.enclosures) this.buildEnclosure(def, rng);
  }

  private buildGround(rng: () => number) {
    const size = 120;
    const geo = new THREE.PlaneGeometry(size, size, 60, 60);
    geo.rotateX(-Math.PI / 2);
    const pos = geo.attributes.position;
    const colors = new Float32Array(pos.count * 3);
    const a = new THREE.Color(0x7cc257);
    const b = new THREE.Color(0x5fa843);
    const c = new THREE.Color();
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i);
      const z = pos.getZ(i);
      const n = Math.sin(x * 0.35) * Math.cos(z * 0.3) * 0.5 + 0.5;
      c.copy(a).lerp(b, n * 0.7 + rng() * 0.3);
      colors.set([c.r, c.g, c.b], i * 3);
      // Rolling hills far away, perfectly flat inside the park.
      const r = Math.hypot(x, z * 0.8);
      if (r > 30) pos.setY(i, Math.pow((r - 30) / 30, 2) * 6 * (0.6 + n * 0.8));
    }
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geo.computeVertexNormals();
    const ground = new THREE.Mesh(geo, this.vc);
    ground.receiveShadow = true;
    this.scene.add(ground);
  }

  private buildPaths() {
    const pathMat = new THREE.MeshStandardMaterial({ color: 0xf0d9a8, roughness: 0.95 });
    const edgeMat = new THREE.MeshStandardMaterial({ color: 0xd9bf8c, roughness: 0.95 });
    const xs = ROUTE_POINTS.map((p) => p[0]);
    const zs = ROUTE_POINTS.map((p) => p[1]);
    const minZ = Math.min(...zs) - 1.3;
    const maxZ = Math.max(...zs) + 6;
    const width = Math.max(...xs) - Math.min(...xs) + 2.2;
    const avenue = new THREE.Mesh(new THREE.BoxGeometry(width, 0.06, maxZ - minZ), pathMat);
    avenue.position.set(0, 0.03, (minZ + maxZ) / 2);
    avenue.receiveShadow = true;
    this.scene.add(avenue);
    for (const side of [-1, 1]) {
      const edge = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.1, maxZ - minZ), edgeMat);
      edge.position.set(side * (width / 2 + 0.08), 0.05, (minZ + maxZ) / 2);
      edge.receiveShadow = true;
      this.scene.add(edge);
    }
    // Round plazas at the gate and at the far end.
    for (const [z, r] of [[9.5, 3.2], [-9.1, 1.3], [-0.8, 2.3]] as const) {
      const plaza = new THREE.Mesh(new THREE.CylinderGeometry(r, r, 0.07, 40), pathMat);
      plaza.position.set(0, 0.035, z);
      plaza.receiveShadow = true;
      this.scene.add(plaza);
    }
    // Side spurs from the avenue to each enclosure's viewing deck.
    for (const def of this.park.enclosures) {
      if (Math.abs(def.x) < 1) continue;
      const len = Math.abs(def.x) - def.halfW - width / 2 + 0.4;
      const spur = new THREE.Mesh(new THREE.BoxGeometry(len, 0.055, 1.6), pathMat);
      spur.position.set(Math.sign(def.x) * (width / 2 + len / 2 - 0.2), 0.03, def.z);
      spur.receiveShadow = true;
      this.scene.add(spur);
    }
  }

  private buildEntrance() {
    const g = new THREE.Group();
    const stone = new THREE.MeshStandardMaterial({ color: 0x8c6a4a, roughness: 0.8 });
    const wood = new THREE.MeshStandardMaterial({ color: 0x6e4a2c, roughness: 0.8 });
    const leaf = new THREE.MeshStandardMaterial({ color: 0x3f8f3a, roughness: 0.8, flatShading: true });
    for (const side of [-1, 1]) {
      const pillar = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.5, 4.2, 8), stone);
      pillar.position.set(side * 2.4, 2.1, 0);
      pillar.castShadow = true;
      g.add(pillar);
      const cap = new THREE.Mesh(new THREE.IcosahedronGeometry(0.75, 0), leaf);
      cap.position.set(side * 2.4, 4.45, 0);
      cap.castShadow = true;
      g.add(cap);
    }
    const beam = new THREE.Mesh(new THREE.BoxGeometry(5.8, 0.5, 0.5), wood);
    beam.position.set(0, 3.85, 0);
    beam.castShadow = true;
    g.add(beam);
    const sign = new THREE.Mesh(new THREE.PlaneGeometry(4.4, 1.1), new THREE.MeshBasicMaterial({ map: signTexture(this.park.name.toUpperCase()), transparent: true }));
    sign.position.set(0, 3.3, 0.27);
    sign.rotation.x = -0.12;
    g.add(sign);
    // Ticket booth with a striped awning.
    const booth = new THREE.Group();
    const boothBody = new THREE.Mesh(new THREE.BoxGeometry(1.5, 1.6, 1.2), new THREE.MeshStandardMaterial({ color: 0xfff4e0, roughness: 0.8 }));
    boothBody.position.y = 0.8;
    boothBody.castShadow = true;
    booth.add(boothBody);
    for (let i = 0; i < 5; i++) {
      const stripe = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.08, 1.5), new THREE.MeshStandardMaterial({ color: i % 2 ? 0xffffff : 0xe8505b }));
      stripe.position.set(-0.6 + i * 0.3, 1.72, 0.1);
      stripe.rotation.x = 0.15;
      stripe.castShadow = true;
      booth.add(stripe);
    }
    booth.position.set(-3.8, 0, 0.5);
    g.add(booth);
    g.position.copy(this.gatePosition);
    this.scene.add(g);
  }

  private buildSkyline(rng: () => number) {
    // A stylised city around the park: every park sits inside a real city on the world map.
    const palette = [0xf4e1c1, 0xe9c6a5, 0xd7e3ea, 0xf1d3d3, 0xcfd8c4, 0xe6d7f0, 0xf6e8b1];
    const windowTex = windowTexture();
    const box = new THREE.BoxGeometry(1, 1, 1);
    box.translate(0, 0.5, 0);
    const transforms: { x: number; z: number; w: number; d: number; h: number; c: number }[] = [];
    const ring = (count: number, radius: number, hMin: number, hMax: number) => {
      for (let i = 0; i < count; i++) {
        const a = (i / count) * Math.PI * 2 + rng() * 0.1;
        const r = radius + rng() * 6;
        const x = Math.cos(a) * r * 1.1;
        const z = Math.sin(a) * r * 0.95 - 4;
        if (z > 18) continue; // keep the view behind the camera clear
        transforms.push({ x, z, w: 2 + rng() * 2.5, d: 2 + rng() * 2.5, h: hMin + rng() * (hMax - hMin), c: palette[Math.floor(rng() * palette.length)] });
      }
    };
    ring(46, 27, 3, 9);
    ring(40, 36, 7, 18);
    const mat = new THREE.MeshStandardMaterial({ map: windowTex, roughness: 0.8 });
    const im = new THREE.InstancedMesh(box, mat, transforms.length);
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion();
    transforms.forEach((t, i) => {
      q.setFromEuler(new THREE.Euler(0, Math.atan2(t.x, t.z), 0));
      m.compose(new THREE.Vector3(t.x, 0, t.z), q, new THREE.Vector3(t.w, t.h, t.d));
      im.setMatrixAt(i, m);
      im.setColorAt(i, new THREE.Color(t.c));
    });
    im.castShadow = false;
    im.receiveShadow = false;
    this.scene.add(im);
  }

  private buildDecor(rng: () => number) {
    const palms: { x: number; z: number; ry: number; s: number }[] = [];
    const trees: { x: number; z: number; ry: number; s: number }[] = [];
    const bushes: { x: number; z: number; ry: number; s: number }[] = [];
    const occupied = (x: number, z: number, pad: number) => {
      if (Math.abs(x) < 2.6 && z > -11 && z < 16) return true; // avenue
      if (Math.hypot(x, z - 10.2) < 4.5) return true; // gate plaza
      for (const e of this.park.enclosures) {
        if (Math.abs(x - e.x) < e.halfW + pad && Math.abs(z - e.z) < e.halfD + pad) return true;
        // viewing spur
        if (Math.abs(e.x) > 1 && Math.abs(z - e.z) < 1.4 && Math.sign(x) === Math.sign(e.x) && Math.abs(x) < Math.abs(e.x)) return true;
      }
      return false;
    };
    for (let i = 0; i < 260 && palms.length + trees.length < 70; i++) {
      const x = (rng() - 0.5) * 44;
      const z = -22 + rng() * 40;
      if (occupied(x, z, 0.9)) continue;
      (rng() < 0.55 ? palms : trees).push({ x, z, ry: rng() * Math.PI * 2, s: 0.8 + rng() * 0.45 });
    }
    // Hedge rows along the avenue.
    for (let z = 12.5; z > -8.5; z -= 2.1) {
      for (const side of [-1, 1]) {
        if (this.park.enclosures.some((e) => Math.abs(e.x) > 1 && Math.abs(z - e.z) < 1.3 && Math.sign(e.x) === side)) continue;
        if (Math.abs(z - 9.5) < 3.3 || Math.abs(z + 0.8) < 2.4) continue;
        bushes.push({ x: side * 2.2, z, ry: rng() * 6, s: 0.8 + rng() * 0.3 });
      }
    }
    for (let i = 0; i < 120 && bushes.length < 60; i++) {
      const x = (rng() - 0.5) * 40;
      const z = -20 + rng() * 36;
      if (occupied(x, z, 0.4)) continue;
      bushes.push({ x, z, ry: rng() * 6, s: 0.7 + rng() * 0.5 });
    }
    this.scene.add(instanced(palmGeometry(rng), this.vcFlat, palms));
    this.scene.add(instanced(roundTreeGeometry(rng), this.vcFlat, trees));
    this.scene.add(instanced(bushGeometry(rng), this.vcFlat, bushes));

    const lamps: { x: number; z: number }[] = [];
    const benches: { x: number; z: number; ry: number }[] = [];
    for (let z = 7; z > -8; z -= 4.5) {
      lamps.push({ x: 1.75, z }, { x: -1.75, z: z - 2.2 });
    }
    benches.push({ x: 2.0, z: -2.4, ry: -Math.PI / 2 }, { x: -2.0, z: -2.4, ry: Math.PI / 2 }, { x: 2.0, z: 0.8, ry: -Math.PI / 2 }, { x: -2.0, z: 0.8, ry: Math.PI / 2 });
    this.scene.add(instanced(lampGeometry(), this.vc, lamps));
    this.scene.add(instanced(benchGeometry(), this.vc, benches));
  }

  private buildEnclosure(def: EnclosureDef, rng: () => number) {
    const group = new THREE.Group();
    group.position.set(def.x, 0, def.z);
    this.scene.add(group);

    const groundColor = ENCLOSURE_GROUND[def.speciesId] ?? 0x8fbf5a;
    const groundMat = new THREE.MeshStandardMaterial({ color: groundColor, roughness: 0.95 });
    const ground = new THREE.Mesh(new THREE.BoxGeometry(def.halfW * 2, 0.08, def.halfD * 2), groundMat);
    ground.position.y = 0.04;
    ground.receiveShadow = true;
    ground.userData.enclosureId = def.id;
    group.add(ground);

    // Fence: posts are always there (they mark the plot); rails appear when the plot is bought.
    const posts: { x: number; z: number; ry?: number }[] = [];
    const railsGroup = new THREE.Group();
    const railMat = new THREE.MeshStandardMaterial({ color: 0xa8743f, roughness: 0.85 });
    const spacing = 1.2;
    const sides: [number, number, number, number][] = [
      [-def.halfW, -def.halfD, def.halfW, -def.halfD],
      [def.halfW, -def.halfD, def.halfW, def.halfD],
      [def.halfW, def.halfD, -def.halfW, def.halfD],
      [-def.halfW, def.halfD, -def.halfW, -def.halfD],
    ];
    for (const [x0, z0, x1, z1] of sides) {
      const len = Math.hypot(x1 - x0, z1 - z0);
      const n = Math.max(1, Math.round(len / spacing));
      for (let i = 0; i < n; i++) posts.push({ x: x0 + ((x1 - x0) * i) / n, z: z0 + ((z1 - z0) * i) / n });
      for (const y of [0.45, 0.85]) {
        const rail = new THREE.Mesh(new THREE.BoxGeometry(len, 0.09, 0.07), railMat);
        rail.position.set((x0 + x1) / 2, y, (z0 + z1) / 2);
        rail.rotation.y = -Math.atan2(z1 - z0, x1 - x0);
        rail.castShadow = true;
        railsGroup.add(rail);
      }
    }
    group.add(instanced(fencePostGeometry(), this.vc, posts));
    group.add(railsGroup);

    // Scenery inside the enclosure.
    const scenery = new THREE.Group();
    const rocks: { x: number; z: number; ry: number; s: number }[] = [];
    const inner: { x: number; z: number; ry: number; s: number }[] = [];
    const corner = (fx: number, fz: number) => ({ x: fx * (def.halfW - 0.8), z: fz * (def.halfD - 0.7) });
    for (const [fx, fz] of [[-1, -1], [1, -1], [1, 1]] as const) {
      const c = corner(fx, fz);
      rocks.push({ ...c, ry: rng() * 6, s: 0.9 + rng() * 0.8 });
      rocks.push({ x: c.x - fx * 0.7, z: c.z + 0.2, ry: rng() * 6, s: 0.5 + rng() * 0.4 });
    }
    scenery.add(instanced(rockGeometry(rng, def.speciesId === 'trex' ? 0x8c7a66 : 0x9a958c), this.vcFlat, rocks));
    const c = corner(-1, 1);
    inner.push({ ...c, ry: rng() * 6, s: def.speciesId === 'brachiosaurus' ? 1.5 : 1.0 });
    scenery.add(instanced(def.speciesId === 'trex' ? palmGeometry(rng) : roundTreeGeometry(rng, 0x5aa648), this.vcFlat, inner));
    if (def.speciesId === 'triceratops' || def.speciesId === 'brachiosaurus') {
      const pond = new THREE.Mesh(new THREE.CylinderGeometry(1.1, 1.1, 0.05, 28), new THREE.MeshStandardMaterial({ color: 0x4fc3f7, roughness: 0.1, metalness: 0.1 }));
      pond.scale.set(1.3, 1, 0.8);
      pond.position.set(def.halfW * 0.35, 0.1, -def.halfD * 0.4);
      scenery.add(pond);
    }
    // Hay/food trough near the viewing side.
    const trough = merge([
      paint(at(new THREE.BoxGeometry(1.2, 0.3, 0.5), 0, 0.15, 0), 0x7a5230),
      paint(at(new THREE.BoxGeometry(1.05, 0.12, 0.38), 0, 0.33, 0), def.speciesId === 'trex' || def.speciesId === 'raptor' ? 0xb8453a : 0xd9c25a, 0.3),
    ]);
    const troughMesh = new THREE.Mesh(trough, this.vc);
    troughMesh.castShadow = true;
    const viewSide = Math.abs(def.x) < 1 ? 1 : -Math.sign(def.x);
    if (Math.abs(def.x) < 1) troughMesh.position.set(def.halfW * 0.5, 0, def.halfD - 0.7);
    else troughMesh.position.set(viewSide * (def.halfW - 0.7), 0, def.halfD * 0.5);
    if (Math.abs(def.x) >= 1) troughMesh.rotation.y = Math.PI / 2;
    scenery.add(troughMesh);
    group.add(scenery);

    // Locked decor: "for sale" dirt patch and a construction cone pair.
    const lockedDecor = new THREE.Group();
    const dirt = new THREE.Mesh(new THREE.BoxGeometry(def.halfW * 2 - 0.2, 0.09, def.halfD * 2 - 0.2), new THREE.MeshStandardMaterial({ color: 0xc9b089, roughness: 1 }));
    dirt.position.y = 0.05;
    dirt.receiveShadow = true;
    dirt.userData.enclosureId = def.id;
    lockedDecor.add(dirt);
    const coneMat = new THREE.MeshStandardMaterial({ color: 0xff8a3d, roughness: 0.6 });
    for (const [x, z] of [[-1.2, 0.4], [1.3, -0.6], [0.2, 1.1]] as const) {
      const cone = new THREE.Mesh(new THREE.ConeGeometry(0.22, 0.6, 10), coneMat);
      cone.position.set(x, 0.38, z);
      cone.castShadow = true;
      lockedDecor.add(cone);
    }
    group.add(lockedDecor);

    const view: EnclosureView = {
      def, group, rails: railsGroup, scenery, lockedDecor, ground,
      trough: troughMesh.position.clone().add(group.position),
      setUnlocked(unlocked: boolean) {
        railsGroup.visible = unlocked;
        scenery.visible = unlocked;
        lockedDecor.visible = !unlocked;
      },
    };
    view.setUnlocked(false);
    this.enclosures.set(def.id, view);
  }

  /** Walkable area for dinos inside an enclosure (in world coordinates). */
  boundsFor(def: EnclosureDef, margin = 1.0) {
    return {
      minX: def.x - def.halfW + margin,
      maxX: def.x + def.halfW - margin,
      minZ: def.z - def.halfD + margin,
      maxZ: def.z + def.halfD - margin,
    };
  }
}

function signTexture(text: string): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 1024;
  canvas.height = 256;
  const ctx = canvas.getContext('2d')!;
  const r = 60;
  ctx.fillStyle = '#ffcf4a';
  roundRect(ctx, 8, 8, 1008, 240, r);
  ctx.fill();
  ctx.lineWidth = 14;
  ctx.strokeStyle = '#7a4a1f';
  ctx.stroke();
  ctx.fillStyle = '#5a3212';
  ctx.font = '900 96px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, 512, 134, 940);
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.anisotropy = 4;
  return tex;
}

function windowTexture(): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 64;
  canvas.height = 128;
  const ctx = canvas.getContext('2d')!;
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, 64, 128);
  ctx.fillStyle = '#9fb6c8';
  for (let y = 8; y < 124; y += 16) for (let x = 8; x < 60; x += 16) ctx.fillRect(x, y, 9, 10);
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
  return tex;
}

function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}
