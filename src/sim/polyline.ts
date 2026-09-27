export interface RouteSample {
  x: number;
  z: number;
  /** Unit tangent (direction of travel). */
  tx: number;
  tz: number;
  /** Unit normal (to the right of travel). */
  nx: number;
  nz: number;
}

/** A 2D polyline on the ground plane (x, z) with distance-based sampling. */
export class Polyline {
  readonly length: number;
  private cumulative: number[] = [0];
  private readonly sampleOut: RouteSample = { x: 0, z: 0, tx: 0, tz: 1, nx: 1, nz: 0 };

  constructor(readonly points: [number, number][]) {
    if (points.length < 2) throw new Error('Polyline needs at least two points');
    let total = 0;
    for (let i = 1; i < points.length; i++) {
      total += Math.hypot(points[i][0] - points[i - 1][0], points[i][1] - points[i - 1][1]);
      this.cumulative.push(total);
    }
    this.length = total;
  }

  segmentStart(i: number): number {
    return this.cumulative[i];
  }

  segmentLength(i: number): number {
    return this.cumulative[i + 1] - this.cumulative[i];
  }

  /** Distance along the route where segment `seg` crosses the given z (clamped to the segment). */
  distanceAtZ(z: number, seg: number): number {
    const [ax, az] = this.points[seg];
    const [bx, bz] = this.points[seg + 1];
    const t = az === bz ? 0.5 : Math.min(1, Math.max(0, (z - az) / (bz - az)));
    return this.cumulative[seg] + t * Math.hypot(bx - ax, bz - az);
  }

  /** Samples position and direction at distance d. Returns a shared object — copy it if you keep it. */
  sample(d: number): RouteSample {
    const dd = Math.min(this.length, Math.max(0, d));
    let i = 1;
    while (i < this.cumulative.length - 1 && this.cumulative[i] < dd) i++;
    const [ax, az] = this.points[i - 1];
    const [bx, bz] = this.points[i];
    const segLen = this.cumulative[i] - this.cumulative[i - 1] || 1;
    const t = (dd - this.cumulative[i - 1]) / segLen;
    const tx = (bx - ax) / segLen;
    const tz = (bz - az) / segLen;
    const out = this.sampleOut;
    out.x = ax + (bx - ax) * t;
    out.z = az + (bz - az) * t;
    out.tx = tx;
    out.tz = tz;
    // Right-hand normal when looking along +tangent in a y-up world.
    out.nx = -tz;
    out.nz = tx;
    return out;
  }
}
