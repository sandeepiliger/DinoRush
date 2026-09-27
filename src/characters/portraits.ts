// Renders a studio portrait of each species once at startup, for the UI cards. Uses the real 3D model,
// so the UI always matches the park — no separate 2D art to maintain.

import * as THREE from 'three';
import type { SpeciesDef } from '../data/species';
import { buildDino } from './dinoBuilder';

export function renderPortraits(renderer: THREE.WebGLRenderer, species: SpeciesDef[], size = 192): Map<string, string> {
  const out = new Map<string, string>();
  const target = new THREE.WebGLRenderTarget(size, size, { samples: 4 });
  const scene = new THREE.Scene();
  scene.add(new THREE.HemisphereLight(0xffffff, 0x9bbf7a, 1.6));
  const key = new THREE.DirectionalLight(0xffffff, 2.2);
  key.position.set(2, 3, 4);
  scene.add(key);
  const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 50);
  const pixels = new Uint8Array(size * size * 4);
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  const ctx = canvas.getContext('2d')!;
  const prevTarget = renderer.getRenderTarget();
  const prevClear = renderer.getClearColor(new THREE.Color());
  const prevAlpha = renderer.getClearAlpha();

  for (const sp of species) {
    const rig = buildDino(sp, 0.5);
    rig.root.rotation.y = -0.55; // three-quarter view, facing the viewer
    scene.add(rig.root);
    rig.root.updateMatrixWorld(true);
    // Frame the head and upper body: the most characterful part.
    const box = new THREE.Box3().setFromObject(rig.root);
    const head = new THREE.Vector3();
    rig.headPivot.getWorldPosition(head);
    const center = box.getCenter(new THREE.Vector3()).lerp(head, 0.55);
    const radius = box.getSize(new THREE.Vector3()).length() * 0.36;
    camera.position.set(center.x + radius * 0.6, center.y + radius * 0.35, center.z + radius * 3.2);
    camera.lookAt(center);
    renderer.setRenderTarget(target);
    renderer.setClearColor(0x000000, 0);
    renderer.clear();
    renderer.render(scene, camera);
    renderer.readRenderTargetPixels(target, 0, 0, size, size, pixels);
    const img = ctx.createImageData(size, size);
    // WebGL rows are bottom-up; flip while copying.
    for (let y = 0; y < size; y++) {
      img.data.set(pixels.subarray((size - 1 - y) * size * 4, (size - y) * size * 4), y * size * 4);
    }
    ctx.putImageData(img, 0, 0);
    out.set(sp.id, canvas.toDataURL('image/png'));
    scene.remove(rig.root);
  }
  renderer.setRenderTarget(prevTarget);
  renderer.setClearColor(prevClear, prevAlpha);
  target.dispose();
  return out;
}
