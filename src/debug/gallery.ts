// Dev-only character gallery: every species side by side, animated. Not part of the production build.
// Query params: ?view=side|front|top|three  &anim=walk|idle|roar  &species=trex
import * as THREE from 'three';
import { buildDino } from '../characters/dinoBuilder';
import { DinoAnimator } from '../characters/dinoAnimator';
import { mergeStaticChildren } from '../characters/mergeStatic';
import { SPECIES } from '../data/species';
import { mulberry32 } from '../sim/parkSim';

const params = new URLSearchParams(location.search);
const view = params.get('view') ?? 'three';
const anim = params.get('anim') ?? 'idle';
const only = params.get('species');
const canvas = document.getElementById('c') as HTMLCanvasElement;
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
renderer.setPixelRatio(Math.min(2, devicePixelRatio));
renderer.shadowMap.enabled = true;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
const scene = new THREE.Scene();
scene.background = new THREE.Color(0xcfe8b8);
scene.add(new THREE.HemisphereLight(0xdff3ff, 0x6b8f4a, 1.35));
const sun = new THREE.DirectionalLight(0xfff1d6, 2.4);
sun.position.set(6, 12, 8);
sun.castShadow = true;
sun.shadow.camera.left = sun.shadow.camera.bottom = -14;
sun.shadow.camera.right = sun.shadow.camera.top = 14;
scene.add(sun);
const ground = new THREE.Mesh(new THREE.PlaneGeometry(80, 80), new THREE.MeshStandardMaterial({ color: 0x8fbf5a }));
ground.rotation.x = -Math.PI / 2;
ground.receiveShadow = true;
scene.add(ground);

const list = Object.values(SPECIES).filter((s) => !only || s.id === only);
const rng = mulberry32(1);
const dinos: DinoAnimator[] = [];
list.forEach((sp, i) => {
  const rig = buildDino(sp, 0.5);
  mergeStaticChildren(rig.root);
  scene.add(rig.root);
  const x = (i - (list.length - 1) / 2) * 5.2;
  const a = new DinoAnimator(rig, { minX: x, maxX: x, minZ: 0, maxZ: 0 }, rng, () => {});
  a.x = x; a.z = 0; a.heading = 0;
  dinos.push(a);
});
const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 200);
const span = list.length * 5.2;
const d = span * 1.25 + 6;
if (view === 'side') camera.position.set(0, 2.2, d);
else if (view === 'front') camera.position.set(d, 2.2, 0.01);
else if (view === 'top') camera.position.set(0, d, 0.01);
else camera.position.set(d * 0.35, d * 0.45, d * 0.8);
camera.lookAt(0, 1.4, 0);
const resize = () => { renderer.setSize(innerWidth, innerHeight, false); camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); };
resize();
addEventListener('resize', resize);
const clock = new THREE.Clock();
let t = 0;
renderer.setAnimationLoop(() => {
  const dt = Math.min(0.05, clock.getDelta());
  t += dt;
  for (const a of dinos) {
    // Force the requested animation for inspection.
    const anyA = a as unknown as { mode: string; speedNow: number; timer: number; roarT: number };
    if (anim === 'walk') { anyA.mode = 'roar'; anyA.roarT = 99; anyA.speedNow = a.rig.species.walkSpeed; }
    if (anim === 'idle') { anyA.mode = 'idle'; anyA.timer = 99; }
    if (anim === 'roar' && anyA.mode !== 'roar') a.roar();
    a.update(dt, dinos);
    a.x = (dinos.indexOf(a) - (dinos.length - 1) / 2) * 5.2; a.z = 0; a.heading = 0;
  }
  renderer.render(scene, camera);
});
(window as unknown as { ready: boolean }).ready = true;
