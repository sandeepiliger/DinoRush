import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

/**
 * Reduces draw calls: under every node, leaf meshes that share a material are merged into one mesh
 * (their local transforms are baked in). Animated joints are Groups/Bones and are left untouched, so
 * the rig still works. A procedurally built dino drops from ~70 draw calls to ~25.
 */
export function mergeStaticChildren(root: THREE.Object3D): void {
  const parents: THREE.Object3D[] = [];
  root.traverse((o) => parents.push(o));
  for (const parent of parents) {
    const groups = new Map<THREE.Material, THREE.Mesh[]>();
    for (const child of parent.children) {
      const m = child as THREE.Mesh;
      if (!m.isMesh || (m as unknown as THREE.SkinnedMesh).isSkinnedMesh || m.children.length > 0) continue;
      if (Array.isArray(m.material)) continue;
      const list = groups.get(m.material) ?? [];
      list.push(m);
      groups.set(m.material, list);
    }
    for (const [material, meshes] of groups) {
      if (meshes.length < 2) continue;
      const geos = meshes.map((m) => {
        m.updateMatrix();
        const g = m.geometry.index ? m.geometry.toNonIndexed() : m.geometry.clone();
        for (const name of Object.keys(g.attributes)) if (name !== 'position' && name !== 'normal') g.deleteAttribute(name);
        g.applyMatrix4(m.matrix);
        return g;
      });
      const merged = mergeGeometries(geos, false);
      geos.forEach((g) => g.dispose());
      if (!merged) continue;
      const mesh = new THREE.Mesh(merged, material);
      mesh.castShadow = meshes.some((m) => m.castShadow);
      mesh.receiveShadow = meshes.some((m) => m.receiveShadow);
      for (const m of meshes) {
        parent.remove(m);
        m.geometry.dispose();
      }
      parent.add(mesh);
    }
  }
}
