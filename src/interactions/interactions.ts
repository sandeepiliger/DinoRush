// Hands-on interactions that keep the player busy between upgrades: feeding hungry dinos, cleaning up
// after them, collecting visitor tips, and catching pterodactyl gift crates. Every tap pays out.
// Rules live in economy/care.ts and data/parks.ts (INTERACTION); this file only runs and presents them.

import * as THREE from 'three';
import type { AudioManager } from '../audio/audio';
import type { DinoAnimator } from '../characters/dinoAnimator';
import { buildPterodactyl, flapPtero, type PteroRig } from '../characters/pteroBuilder';
import { formatDuration } from '../core/format';
import { INTERACTION as CFG } from '../data/parks';
import type { QuestKind } from '../data/quests';
import {
  addPoop, careMultiplier, cleanPoop, feed, freshCare, isDirty, isHappy, isHungry, rewardFor, tickCare,
  type CareState,
} from '../economy/care';
import type { Economy, ParkState } from '../economy/economy';
import { haptics } from '../services/haptics';
import type { Visitor } from '../sim/parkSim';
import { el } from '../ui/dom';
import { icons } from '../ui/icons';
import { t } from '../ui/strings';
import type { Effects, Poop } from '../world/effects';
import type { ParkWorld } from '../world/parkWorld';

export interface InteractionHost {
  economy: Economy;
  world: ParkWorld;
  effects: Effects;
  audio: AudioManager;
  camera: THREE.PerspectiveCamera;
  herds: Map<string, DinoAnimator[]>;
  visitors: Visitor[];
  layer: HTMLElement;
  state(): ParkState;
  /** Live income per second, including care and boost. */
  income(): number;
  /** Grants coins with a coin-burst from a screen position. */
  reward(amount: number, screenX: number, screenY: number): void;
  questEvent(kind: QuestKind): void;
}

interface Tip {
  visitor: Visitor;
  visitorId: number;
  el: HTMLElement;
  t: number;
}

interface Gift {
  rig: PteroRig;
  el: HTMLElement;
  t: number;
  from: THREE.Vector3;
  to: THREE.Vector3;
  duration: number;
  caught: boolean;
}

const TIP_ICONS = [icons.coin, icons.camera, icons.heart];

export class Interactions {
  readonly care = new Map<string, CareState>();
  private feedBubbles = new Map<string, HTMLElement>();
  private poopTimers = new Map<DinoAnimator, number>();
  private tip: Tip | null = null;
  private tipTimer = 4;
  private gift: Gift | null = null;
  private giftTimer: number;
  private giftRig: PteroRig | null = null;
  private v = new THREE.Vector3();
  private crateV = new THREE.Vector3();
  private time = 0;

  constructor(private host: InteractionHost) {
    this.giftTimer = 35; // first gift comes early so players meet it in their first session
    for (const def of host.economy.park.enclosures) {
      const c = freshCare();
      // Stagger hunger so enclosures don't all get hungry at the same moment.
      c.food = 0.65 + Math.random() * 0.35;
      this.care.set(def.id, c);
      const bubble = el('button', 'bubble bubble-feed', `${icons.meat}<span>${t('feed')}</span>`);
      bubble.addEventListener('click', (e) => {
        e.stopPropagation();
        this.feed(def.id, bubble);
      });
      bubble.hidden = true;
      host.layer.appendChild(bubble);
      this.feedBubbles.set(def.id, bubble);
    }
  }

  /** Ticket multiplier for the visitor simulation (live play only). */
  ticketMultiplier = (id: string): number => careMultiplier(this.care.get(id)!, Date.now(), CFG);

  isHungry(id: string) {
    return isHungry(this.care.get(id)!, CFG);
  }

  /** Small status chip for the enclosure label: happy timer or dirty warning. */
  chip(id: string, now: number): string {
    const c = this.care.get(id)!;
    if (isHappy(c, now)) return `<span class="chip happy">${icons.heart} x${CFG.happyMultiplier} ${formatDuration((c.happyUntil - now) / 1000)}</span>`;
    if (isDirty(c, CFG)) return `<span class="chip dirty">${t('dirty')}</span>`;
    return '';
  }

  update(dt: number): void {
    this.time += dt;
    const state = this.host.state();
    const w = window.innerWidth;
    const h = window.innerHeight;

    // ---- Care: hunger, bubbles, poops.
    for (const def of this.host.economy.park.enclosures) {
      const owned = state.enclosures[def.id].level > 0;
      const c = this.care.get(def.id)!;
      const bubble = this.feedBubbles.get(def.id)!;
      if (!owned) {
        bubble.hidden = true;
        continue;
      }
      tickCare(c, dt, CFG);
      const show = isHungry(c, CFG);
      const trough = this.host.world.enclosures.get(def.id)!.trough;
      const p = this.project(this.v.set(trough.x, 2.4, trough.z), w, h);
      bubble.hidden = !show || !p;
      if (show && p) bubble.style.transform = `translate3d(${p.x.toFixed(1)}px, ${p.y.toFixed(1)}px, 0) translate(-50%, -100%)`;

      for (const dino of this.host.herds.get(def.id) ?? []) {
        if (dino.isHatching) continue;
        let timer = this.poopTimers.get(dino);
        if (timer === undefined) timer = CFG.poopMinSeconds * (0.3 + Math.random() * 0.7);
        timer -= dt;
        if (timer <= 0) {
          timer = THREE.MathUtils.lerp(CFG.poopMinSeconds, CFG.poopMaxSeconds, Math.random());
          if (addPoop(c, CFG)) {
            const back = 1.2 * dino.rig.species.scale;
            const b = this.host.world.boundsFor(def, 0.5);
            const x = THREE.MathUtils.clamp(dino.x - Math.cos(dino.heading) * back, b.minX, b.maxX);
            const z = THREE.MathUtils.clamp(dino.z + Math.sin(dino.heading) * back, b.minZ, b.maxZ);
            this.host.effects.spawnPoop(x, z, def.id);
          }
        }
        this.poopTimers.set(dino, timer);
      }
    }

    this.updateTip(dt, w, h);
    this.updateGift(dt, w, h);
  }

  /** Returns true if the tap was used (poop cleaned). */
  tap(clientX: number, clientY: number): boolean {
    const w = window.innerWidth;
    const h = window.innerHeight;
    let best: Poop | null = null;
    let bestD = 46; // generous finger-sized radius in CSS px
    for (const p of this.host.effects.poops) {
      if (p.cleaning > 0) continue;
      const s = this.project(this.v.copy(p.mesh.position).setY(0.25), w, h);
      if (!s) continue;
      const d = Math.hypot(s.x - clientX, s.y - clientY);
      if (d < bestD) {
        bestD = d;
        best = p;
      }
    }
    if (!best) return false;
    this.host.effects.cleanPoop(best);
    cleanPoop(this.care.get(best.enclosureId)!);
    this.host.audio.play('squish');
    haptics.tap(15);
    this.host.reward(rewardFor(this.host.income(), CFG.poopRewardSeconds, CFG), clientX, clientY);
    this.host.questEvent('clean');
    return true;
  }

  feed(id: string, from?: HTMLElement): void {
    const c = this.care.get(id)!;
    const view = this.host.world.enclosures.get(id)!;
    feed(c, Date.now(), CFG);
    this.host.effects.dropMeat(view.trough.x, view.trough.z);
    for (const d of this.host.herds.get(id) ?? []) d.goEat(view.trough.x + (Math.random() - 0.5), view.trough.z + (Math.random() - 0.5));
    this.host.audio.play('munch');
    setTimeout(() => {
      this.host.effects.hearts3d(view.trough.x, 2, view.trough.z, 8);
      this.host.audio.play('reward');
    }, 1400);
    haptics.tap(25);
    const r = from?.getBoundingClientRect();
    this.host.reward(rewardFor(this.host.income(), CFG.feedRewardSeconds, CFG), r ? r.left + r.width / 2 : window.innerWidth / 2, r ? r.top : window.innerHeight / 2);
    this.host.questEvent('feed');
  }

  /** Where to point the camera for "go do X" quest guidance. */
  hungryEnclosure(): string | null {
    for (const [id, c] of this.care) if (this.host.state().enclosures[id].level > 0 && isHungry(c, CFG)) return id;
    return null;
  }

  // ---------------------------------------------------------------- tips

  private updateTip(dt: number, w: number, h: number) {
    if (this.tip) {
      const tip = this.tip;
      tip.t += dt;
      const v = tip.visitor;
      const gone = !v.active || v.id !== tip.visitorId || tip.t > CFG.tipLifetime;
      const p = gone ? null : this.project(this.v.set(v.x, 2.05, v.z), w, h);
      if (gone || !p) {
        tip.el.remove();
        this.tip = null;
        this.tipTimer = THREE.MathUtils.lerp(CFG.tipMinSeconds, CFG.tipMaxSeconds, Math.random());
        return;
      }
      tip.el.style.transform = `translate3d(${p.x.toFixed(1)}px, ${p.y.toFixed(1)}px, 0) translate(-50%, -100%)`;
      tip.el.classList.toggle('fading', tip.t > CFG.tipLifetime - 1.2);
      return;
    }
    this.tipTimer -= dt;
    if (this.tipTimer > 0) return;
    // Pick an on-screen visitor.
    const candidates = this.host.visitors.filter((v) => v.active && this.project(this.v.set(v.x, 2, v.z), w, h, 0.8));
    if (candidates.length === 0) {
      this.tipTimer = 2;
      return;
    }
    const visitor = candidates[Math.floor(Math.random() * candidates.length)];
    const tipEl = el('button', 'bubble bubble-tip', TIP_ICONS[Math.floor(Math.random() * TIP_ICONS.length)]);
    const tip: Tip = { visitor, visitorId: visitor.id, el: tipEl, t: 0 };
    tipEl.addEventListener('click', (e) => {
      e.stopPropagation();
      if (this.tip !== tip) return;
      const r = tipEl.getBoundingClientRect();
      visitor.paidFlash = 1; // happy hop
      this.host.audio.play('tip');
      haptics.tap(15);
      this.host.reward(rewardFor(this.host.income(), CFG.tipRewardSeconds, CFG), r.left + r.width / 2, r.top + r.height / 2);
      this.host.questEvent('tip');
      tipEl.remove();
      this.tip = null;
      this.tipTimer = THREE.MathUtils.lerp(CFG.tipMinSeconds, CFG.tipMaxSeconds, Math.random());
    });
    this.host.layer.appendChild(tipEl);
    this.tip = tip;
  }

  // ---------------------------------------------------------------- pterodactyl gifts

  private updateGift(dt: number, w: number, h: number) {
    if (!this.gift) {
      this.giftTimer -= dt;
      if (this.giftTimer <= 0) this.launchGift();
      return;
    }
    const g = this.gift;
    g.t += dt * (g.caught ? 2.2 : 1);
    const k = g.t / g.duration;
    if (k >= 1) {
      this.host.world.scene.remove(g.rig.root);
      g.el.remove();
      this.gift = null;
      this.giftTimer = THREE.MathUtils.lerp(CFG.giftMinSeconds, CFG.giftMaxSeconds, Math.random());
      return;
    }
    const pos = this.v.copy(g.from).lerp(g.to, k);
    pos.y += Math.sin(this.time * 1.6) * 0.4 + (g.caught ? (g.t - g.duration * 0.5) * 2 : 0);
    g.rig.root.position.copy(pos);
    flapPtero(g.rig, this.time * (g.caught ? 1.6 : 1));
    if (!g.caught) {
      const p = this.project(g.rig.crate.getWorldPosition(this.crateV), w, h);
      g.el.hidden = !p;
      // Keep the button fully tappable even while the crate is entering from the screen edge.
      if (p) g.el.style.transform = `translate3d(${Math.min(w - 64, Math.max(64, p.x)).toFixed(1)}px, ${p.y.toFixed(1)}px, 0) translate(-50%, -50%)`;
    }
  }

  private launchGift() {
    if (!this.giftRig) this.giftRig = buildPterodactyl();
    const rig = this.giftRig;
    rig.crate.visible = true;
    // Fly across whatever the player is looking at.
    const target = new THREE.Vector3();
    this.host.camera.getWorldDirection(target);
    // Aim at the point the camera looks at, and fly well above the dinos and the gate.
    const center = this.host.camera.position.clone().addScaledVector(target, 26);
    center.y = 8;
    const dir = Math.random() < 0.5 ? 1 : -1;
    const from = center.clone().add(new THREE.Vector3(-16 * dir, 0, (Math.random() - 0.5) * 4));
    const to = center.clone().add(new THREE.Vector3(16 * dir, 0, (Math.random() - 0.5) * 4));
    rig.root.rotation.y = dir > 0 ? 0 : Math.PI;
    this.host.world.scene.add(rig.root);
    const giftEl = el('button', 'bubble bubble-gift', `${icons.gift}<span>${t('catch')}</span>`);
    const gift: Gift = { rig, el: giftEl, t: 0, from, to, duration: 11, caught: false };
    giftEl.addEventListener('click', (e) => {
      e.stopPropagation();
      if (gift.caught) return;
      gift.caught = true;
      const r = giftEl.getBoundingClientRect();
      giftEl.remove();
      rig.crate.visible = false;
      const crate = rig.crate.getWorldPosition(new THREE.Vector3());
      this.host.effects.confetti(crate.x, crate.y, crate.z, 90, 1);
      this.host.audio.play('squawk');
      this.host.audio.play('claim');
      haptics.tap(40);
      this.host.reward(rewardFor(this.host.income(), CFG.giftRewardSeconds, CFG), r.left + r.width / 2, r.top + r.height / 2);
      this.host.questEvent('gift');
    });
    this.host.layer.appendChild(giftEl);
    this.gift = gift;
    this.host.audio.play('squawk');
  }

  // ---------------------------------------------------------------- helpers

  /** World -> CSS px. Returns null when behind the camera or outside the screen (with margin). */
  private project(p: THREE.Vector3, w: number, h: number, limit = 1.05): { x: number; y: number } | null {
    const v = p.project(this.host.camera);
    if (v.z > 1 || Math.abs(v.x) > limit || Math.abs(v.y) > limit) return null;
    return { x: (v.x * 0.5 + 0.5) * w, y: (-v.y * 0.5 + 0.5) * h };
  }
}
