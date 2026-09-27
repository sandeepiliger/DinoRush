// Park and balancing data. All economy numbers live here (or in a future remote-config override),
// never inside gameplay code. See tests/balance.test.ts for the pacing guarantees these values must meet.

export interface EnclosureDef {
  id: string;
  speciesId: string;
  /** Centre of the enclosure in park coordinates (x, z). */
  x: number;
  z: number;
  /** Enclosure half-size along x and z. */
  halfW: number;
  halfD: number;
  /** Cost to buy the plot (the first dino hatches for free on purchase). */
  unlockCost: number;
  /** Coins each visitor pays when viewing this enclosure at level 1 with one dino. */
  baseTicket: number;
  /** Cost of the first upgrade (level 1 -> 2). */
  upgradeBaseCost: number;
  /** Cost of hatching the second dino; later eggs scale by `eggCostGrowth`. */
  eggBaseCost: number;
  maxDinos: number;
}

export interface EconomyConfig {
  startingCoins: number;
  /** Ticket income multiplier added per enclosure level (linear part). */
  ticketPerLevel: number;
  /** Every `milestoneEvery` levels the enclosure's income doubles. */
  milestoneEvery: number;
  milestoneMultiplier: number;
  maxLevel: number;
  upgradeCostGrowth: number;
  eggCostGrowth: number;
  /** Income bonus each additional dino adds to its enclosure (0.6 = +60%). */
  perDinoBonus: number;
  /** Visitors arriving per second at entrance level 1, and growth per level. */
  baseVisitorRate: number;
  visitorRateGrowth: number;
  maxVisitorRate: number;
  entranceBaseCost: number;
  entranceCostGrowth: number;
  entranceMaxLevel: number;
  /** Entry fee paid at the gate. */
  baseEntryFee: number;
  entryFeePerLevel: number;
  /** Offline earnings stop accruing after this many seconds away. */
  offlineCapSeconds: number;
  /** Fraction of live income earned while away (the rewarded ad doubles it). */
  offlineEfficiency: number;
  boostMultiplier: number;
  boostSeconds: number;
}

export interface ParkDef {
  id: string;
  city: string;
  name: string;
  enclosures: EnclosureDef[];
}

export const ECONOMY: EconomyConfig = {
  startingCoins: 60,
  ticketPerLevel: 0.25,
  milestoneEvery: 10,
  milestoneMultiplier: 1.5,
  maxLevel: 60,
  upgradeCostGrowth: 1.2,
  eggCostGrowth: 3.2,
  perDinoBonus: 0.6,
  baseVisitorRate: 0.5,
  visitorRateGrowth: 0.07,
  maxVisitorRate: 1.6,
  entranceBaseCost: 40,
  entranceCostGrowth: 1.32,
  entranceMaxLevel: 25,
  baseEntryFee: 1,
  entryFeePerLevel: 0.2,
  offlineCapSeconds: 2 * 60 * 60,
  offlineEfficiency: 0.5,
  boostMultiplier: 2,
  boostSeconds: 120,
};

/** Cities on the world map, in unlock order. Only the first park is built in this milestone. */
export const CITIES = ['Mumbai', 'Dubai', 'Tokyo', 'Paris', 'New York'] as const;

export const PARKS: ParkDef[] = [
  {
    id: 'mumbai',
    city: 'Mumbai',
    name: 'Mumbai Dino Park',
    enclosures: [
      { id: 'raptor-pen', speciesId: 'raptor', x: -6, z: 3.5, halfW: 3.2, halfD: 3,
        unlockCost: 0, baseTicket: 2, upgradeBaseCost: 10, eggBaseCost: 120, maxDinos: 4 },
      { id: 'trike-field', speciesId: 'triceratops', x: 6, z: 3.5, halfW: 3.3, halfD: 3,
        unlockCost: 350, baseTicket: 8, upgradeBaseCost: 150, eggBaseCost: 2_500, maxDinos: 3 },
      { id: 'stego-grove', speciesId: 'stegosaurus', x: -6, z: -5.5, halfW: 3.3, halfD: 3.2,
        unlockCost: 15_000, baseTicket: 40, upgradeBaseCost: 1_800, eggBaseCost: 40_000, maxDinos: 3 },
      { id: 'brachio-heights', speciesId: 'brachiosaurus', x: 6, z: -5.5, halfW: 3.3, halfD: 3.2,
        unlockCost: 450_000, baseTicket: 260, upgradeBaseCost: 25_000, eggBaseCost: 600_000, maxDinos: 2 },
      { id: 'rex-arena', speciesId: 'trex', x: 0, z: -13.6, halfW: 4.8, halfD: 3.2,
        unlockCost: 15_000_000, baseTicket: 1_800, upgradeBaseCost: 400_000, eggBaseCost: 12_000_000, maxDinos: 2 },
    ],
  },
];
