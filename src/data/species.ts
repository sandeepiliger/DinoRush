// Species definitions: everything that makes one dinosaur look and behave differently from another.
// Adding a new dinosaur should mean adding an entry here (and to an enclosure in parks.ts), never
// touching the character builder or the economy code.

export type Rarity = 'common' | 'rare' | 'epic' | 'legendary';

/** One control point along the dinosaur's spine, from tail tip to neck end. Units are metres. */
export interface SpineKnot {
  x: number;
  y: number;
  /** Half-width of the body cross-section at this knot (sideways). */
  rw: number;
  /** Half-height of the body cross-section at this knot (vertical). */
  rh: number;
}

export interface LegSpec {
  /** x position of the hip joint, relative to the dino origin. */
  x: number;
  /** Thickness of the thigh; the shin and foot are derived from it. */
  thickness: number;
  /** Rest angle of the thigh (radians, positive = knee forward). */
  thighAngle: number;
  /** Rest angle of the shin (radians, positive = foot back). */
  shinAngle: number;
  /** Half the lateral distance between the left and right leg. */
  spread: number;
  /** Foot length multiplier. */
  foot: number;
}

export interface HeadSpec {
  size: number;
  /** Snout length relative to head size. */
  snout: number;
  /** Snout thickness relative to head size. */
  snoutHeight: number;
  /** Eye size relative to head size. Larger reads friendlier. */
  eye: number;
  teeth: boolean;
  /** Extra head features. */
  frill?: boolean;
  horns?: boolean;
  noseHorn?: boolean;
  crest?: boolean;
  beak?: boolean;
}

export interface SpeciesPalette {
  base: number;
  belly: number;
  accent: number;
  /** Stripe strength along the back, 0 = none. */
  stripes: number;
  extra: number;
}

export interface SpeciesDef {
  id: string;
  name: string;
  rarity: Rarity;
  description: string;
  spine: SpineKnot[];
  /** Index into `spine` of the hip knot — the root of the skeleton. */
  hipKnot: number;
  hipHeight: number;
  gait: 'biped' | 'quad';
  hindLegs: LegSpec;
  frontLegs?: LegSpec;
  arms?: { x: number; length: number; thickness: number };
  head: HeadSpec;
  palette: SpeciesPalette;
  plates?: { count: number; size: number };
  tailSpikes?: boolean;
  tailClub?: boolean;
  /** Metres per second while wandering. */
  walkSpeed: number;
  /** Overall display scale inside the park. */
  scale: number;
}

export const SPECIES: Record<string, SpeciesDef> = {
  raptor: {
    id: 'raptor',
    name: 'Velociraptor',
    rarity: 'common',
    description: 'Quick, curious and always looking for trouble.',
    spine: [
      { x: -1.55, y: 0.95, rw: 0.05, rh: 0.05 },
      { x: -1.05, y: 0.98, rw: 0.09, rh: 0.11 },
      { x: -0.55, y: 1.0, rw: 0.19, rh: 0.22 },
      { x: -0.05, y: 1.0, rw: 0.24, rh: 0.28 },
      { x: 0.4, y: 1.08, rw: 0.22, rh: 0.26 },
      { x: 0.7, y: 1.32, rw: 0.12, rh: 0.13 },
      { x: 0.85, y: 1.55, rw: 0.09, rh: 0.1 },
    ],
    hipKnot: 3,
    hipHeight: 0.9,
    gait: 'biped',
    hindLegs: { x: -0.05, thickness: 0.13, thighAngle: 0.55, shinAngle: 0.45, spread: 0.17, foot: 1.1 },
    arms: { x: 0.42, length: 0.32, thickness: 0.05 },
    head: { size: 0.2, snout: 1.25, snoutHeight: 0.55, eye: 0.3, teeth: true, crest: true },
    palette: { base: 0x5b8f3a, belly: 0xe8d9a8, accent: 0x2f5a24, stripes: 0.8, extra: 0xd9492b },
    walkSpeed: 1.6,
    scale: 1.3,
  },
  triceratops: {
    id: 'triceratops',
    name: 'Triceratops',
    rarity: 'common',
    description: 'Gentle giant with three horns and a big frilly collar.',
    spine: [
      { x: -1.7, y: 0.85, rw: 0.05, rh: 0.05 },
      { x: -1.2, y: 0.95, rw: 0.19, rh: 0.19 },
      { x: -0.6, y: 1.08, rw: 0.51, rh: 0.49 },
      { x: 0.0, y: 1.12, rw: 0.55, rh: 0.5 },
      { x: 0.6, y: 1.04, rw: 0.48, rh: 0.44 },
      { x: 1.0, y: 0.98, rw: 0.28, rh: 0.3 },
      { x: 1.2, y: 0.98, rw: 0.18, rh: 0.2 },
    ],
    hipKnot: 3,
    hipHeight: 0.8,
    gait: 'quad',
    hindLegs: { x: -0.45, thickness: 0.2, thighAngle: 0.15, shinAngle: 0.1, spread: 0.36, foot: 0.8 },
    frontLegs: { x: 0.65, thickness: 0.17, thighAngle: -0.05, shinAngle: 0.05, spread: 0.32, foot: 0.7 },
    head: {
      size: 0.36, snout: 0.95, snoutHeight: 0.7, eye: 0.2, teeth: false,
      frill: true, horns: true, noseHorn: true, beak: true,
    },
    palette: { base: 0x4f8fb3, belly: 0xdfe6c9, accent: 0x2b5b7a, stripes: 0.35, extra: 0xf2a33a },
    walkSpeed: 1.0,
    scale: 1.2,
  },
  stegosaurus: {
    id: 'stegosaurus',
    name: 'Stegosaurus',
    rarity: 'rare',
    description: 'Plates on its back, spikes on its tail, snacks on its mind.',
    spine: [
      { x: -2.0, y: 0.95, rw: 0.05, rh: 0.05 },
      { x: -1.4, y: 1.1, rw: 0.22, rh: 0.23 },
      { x: -0.7, y: 1.38, rw: 0.54, rh: 0.62 },
      { x: 0.0, y: 1.42, rw: 0.52, rh: 0.58 },
      { x: 0.65, y: 1.12, rw: 0.38, rh: 0.4 },
      { x: 1.1, y: 0.82, rw: 0.17, rh: 0.17 },
      { x: 1.35, y: 0.7, rw: 0.1, rh: 0.1 },
    ],
    hipKnot: 3,
    hipHeight: 1.0,
    gait: 'quad',
    hindLegs: { x: -0.3, thickness: 0.22, thighAngle: 0.12, shinAngle: 0.08, spread: 0.36, foot: 0.8 },
    frontLegs: { x: 0.7, thickness: 0.15, thighAngle: -0.05, shinAngle: 0.05, spread: 0.3, foot: 0.7 },
    head: { size: 0.19, snout: 1.2, snoutHeight: 0.6, eye: 0.3, teeth: false, beak: true },
    palette: { base: 0xa97a3c, belly: 0xefe0b5, accent: 0x6b4a22, stripes: 0.5, extra: 0xd8563a },
    plates: { count: 9, size: 0.45 },
    tailSpikes: true,
    walkSpeed: 0.9,
    scale: 1.15,
  },
  brachiosaurus: {
    id: 'brachiosaurus',
    name: 'Brachiosaurus',
    rarity: 'epic',
    description: 'So tall it eats from the treetops. Visitors love the view.',
    spine: [
      { x: -2.1, y: 1.1, rw: 0.05, rh: 0.05 },
      { x: -1.5, y: 1.35, rw: 0.24, rh: 0.24 },
      { x: -0.8, y: 1.7, rw: 0.68, rh: 0.68 },
      { x: 0.0, y: 1.9, rw: 0.62, rh: 0.62 },
      { x: 0.75, y: 2.2, rw: 0.44, rh: 0.46 },
      { x: 1.05, y: 2.9, rw: 0.22, rh: 0.24 },
      { x: 1.2, y: 3.7, rw: 0.16, rh: 0.17 },
      { x: 1.3, y: 4.35, rw: 0.13, rh: 0.14 },
    ],
    hipKnot: 3,
    hipHeight: 1.45,
    gait: 'quad',
    hindLegs: { x: -0.5, thickness: 0.24, thighAngle: 0.06, shinAngle: 0.04, spread: 0.42, foot: 0.7 },
    frontLegs: { x: 0.6, thickness: 0.24, thighAngle: -0.02, shinAngle: 0.02, spread: 0.4, foot: 0.7 },
    head: { size: 0.24, snout: 1.0, snoutHeight: 0.65, eye: 0.26, teeth: false, crest: true },
    palette: { base: 0x7d9a6a, belly: 0xe6e2c3, accent: 0x4e6b45, stripes: 0.25, extra: 0x9fc27a },
    walkSpeed: 0.8,
    scale: 1.0,
  },
  trex: {
    id: 'trex',
    name: 'Tyrannosaurus Rex',
    rarity: 'legendary',
    description: 'The king of the park. One roar and the whole crowd cheers.',
    spine: [
      { x: -2.2, y: 1.45, rw: 0.05, rh: 0.05 },
      { x: -1.5, y: 1.55, rw: 0.19, rh: 0.20 },
      { x: -0.8, y: 1.68, rw: 0.43, rh: 0.49 },
      { x: 0.0, y: 1.78, rw: 0.5, rh: 0.58 },
      { x: 0.65, y: 1.9, rw: 0.44, rh: 0.5 },
      { x: 1.05, y: 2.2, rw: 0.28, rh: 0.3 },
      { x: 1.25, y: 2.42, rw: 0.22, rh: 0.24 },
    ],
    hipKnot: 3,
    hipHeight: 1.45,
    gait: 'biped',
    hindLegs: { x: 0.0, thickness: 0.27, thighAngle: 0.5, shinAngle: 0.42, spread: 0.36, foot: 1.0 },
    arms: { x: 0.75, length: 0.3, thickness: 0.07 },
    head: { size: 0.46, snout: 1.15, snoutHeight: 0.72, eye: 0.17, teeth: true },
    palette: { base: 0x8a4b33, belly: 0xe9cfa4, accent: 0x4d2a1e, stripes: 0.7, extra: 0xf0c24a },
    walkSpeed: 1.1,
    scale: 1.05,
  },
};
