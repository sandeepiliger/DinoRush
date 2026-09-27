// Game orchestrator: wires economy, simulation, world, characters, UI and services together.
// Each subsystem stays independent; this file is the only place that knows about all of them.

import * as THREE from 'three';
import { AudioManager } from './audio/audio';
import { buildDino } from './characters/dinoBuilder';
import { DinoAnimator } from './characters/dinoAnimator';
import { mergeStaticChildren } from './characters/mergeStatic';
import { renderPortraits } from './characters/portraits';
import { VisitorCrowd } from './characters/visitorCrowd';
import { formatDuration, formatNumber } from './core/format';
import { SaveManager, type SaveData } from './core/save';
import { ECONOMY, PARKS, type EnclosureDef } from './data/parks';
import { SPECIES } from './data/species';
import { Economy } from './economy/economy';
import { claimQuest, questAt, questComplete, questReward, questValue, recordQuestEvent } from './economy/quests';
import type { QuestDef, QuestKind } from './data/quests';
import { Interactions } from './interactions/interactions';
import { AdManager, MockAdProvider } from './services/ads';
import { analytics } from './services/analytics';
import { haptics } from './services/haptics';
import { ParkSim, mulberry32, type PayEvent } from './sim/parkSim';
import { $, el, holdToRepeat } from './ui/dom';
import { icons } from './ui/icons';
import { t } from './ui/strings';
import { CameraRig } from './world/cameraRig';
import { Effects } from './world/effects';
import { ParkWorld } from './world/parkWorld';

type SheetState = { kind: 'enclosure'; id: string } | { kind: 'entrance' } | null;

interface Label {
  root: HTMLElement;
  /** Cached on content change — reading offsetWidth every frame would force a layout per label. */
  width: number;
  anchor: THREE.Vector3;
  refresh(): void;
}

const AUTOSAVE_SECONDS = 5;
const OFFLINE_MIN_SECONDS = 60;

export class Game {
  private economy = new Economy(PARKS[0], ECONOMY);
  private saveMgr = new SaveManager(safeStorage(), this.economy);
  private data!: SaveData;
  private renderer!: THREE.WebGLRenderer;
  private world!: ParkWorld;
  private cam!: CameraRig;
  private sim!: ParkSim;
  private crowd!: VisitorCrowd;
  private effects = new Effects();
  private audio = new AudioManager();
  private ads = new AdManager(new MockAdProvider());
  private herds = new Map<string, DinoAnimator[]>();
  private portraits = new Map<string, string>();
  private labels: Label[] = [];
  private sheet: SheetState = null;
  private sheetRefresh: (() => void) | null = null;
  private rng = mulberry32(Date.now() & 0xffff);
  private clock = new THREE.Clock();
  private shownCoins = 0;
  private hudCoins!: HTMLElement;
  private hudIncome!: HTMLElement;
  private boostBtn!: HTMLElement;
  private saveTimer = 0;
  private uiTimer = 0;
  private raycaster = new THREE.Raycaster();
  private tmpV = new THREE.Vector3();
  private tutorial: { hand: HTMLElement; text: HTMLElement } | null = null;
  private frameTimes: number[] = [];
  private pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  private hiddenAt = 0;
  private interactions!: Interactions;
  private questEl!: HTMLElement;

  async start(): Promise<void> {
    const now = Date.now();
    const { data, recovered } = this.saveMgr.load(now);
    this.data = data;
    this.ads.removeAds = data.removeAds;
    this.audio.setSfx(data.settings.sfx);
    this.audio.musicEnabled = data.settings.music;
    haptics.enabled = data.settings.haptics;
    await this.ads.init();
    analytics.track('session_started');

    this.initRenderer();
    this.world = new ParkWorld(this.economy.park);
    this.world.scene.add(this.effects.group);
    this.sim = new ParkSim(this.economy, 48, now & 0xffff);
    this.crowd = new VisitorCrowd(48);
    this.world.scene.add(this.crowd.group);
    // Pre-warm: simulate half a minute of visitors (earning nothing) so the park is busy on the first frame.
    const visitorsBefore = this.data.park.totalVisitors;
    for (let i = 0; i < 600; i++) this.sim.update(0.05, this.data.park, now, () => {});
    this.data.park.totalVisitors = visitorsBefore;

    this.cam = new CameraRig(this.renderer.domElement, { minX: -7, maxX: 7, minZ: -13, maxZ: 10, minDist: 14, maxDist: 46 });
    this.cam.target.set(0, 1.5);
    this.cam.distance = 34;
    this.cam.onTap = (x, y) => this.onWorldTap(x, y);

    for (const def of this.economy.park.enclosures) {
      const st = this.data.park.enclosures[def.id];
      this.world.enclosures.get(def.id)!.setUnlocked(st.level > 0);
      this.herds.set(def.id, []);
      for (let i = 0; i < st.dinos; i++) this.spawnDino(def, false);
    }

    this.portraits = renderPortraits(this.renderer, Object.values(SPECIES));
    this.buildHud();
    this.buildLabels();
    this.interactions = new Interactions({
      economy: this.economy,
      world: this.world,
      effects: this.effects,
      audio: this.audio,
      camera: this.cam.camera,
      herds: this.herds,
      visitors: this.sim.visitors,
      layer: $('labels'),
      state: () => this.data.park,
      income: () => this.liveIncome(Date.now()),
      reward: (amount, x, y) => this.reward(amount, x, y),
      questEvent: (kind) => this.questEvent(kind),
    });
    this.buildQuestBar();
    this.onResize();
    window.addEventListener('resize', () => this.onResize());
    document.addEventListener('visibilitychange', () => this.onVisibility());
    // Audio may only start after a user gesture.
    const unlockAudio = () => {
      this.audio.unlock();
      window.removeEventListener('pointerdown', unlockAudio);
    };
    window.addEventListener('pointerdown', unlockAudio);

    this.shownCoins = this.data.park.coins;
    $('loading').classList.add('done');
    if (recovered) this.toast(t('saveRecovered'));
    this.checkOffline(now);
    if (!this.data.tutorialDone) this.startTutorial();
    this.renderer.setAnimationLoop(() => this.frame());
  }

  // ---------------------------------------------------------------- setup

  private initRenderer() {
    const canvas = $('scene') as HTMLCanvasElement;
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(this.pixelRatio);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.05;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
  }

  private onResize() {
    const w = window.innerWidth;
    const h = window.innerHeight;
    this.renderer.setSize(w, h, false);
    this.cam.resize(w, h);
  }

  private spawnDino(def: EnclosureDef, hatch: boolean, at?: { x: number; z: number }) {
    const species = SPECIES[def.speciesId];
    const herd = this.herds.get(def.id)!;
    const rig = buildDino(species, this.rng());
    mergeStaticChildren(rig.root);
    this.world.scene.add(rig.root);
    const big = species.id === 'trex' || species.id === 'brachiosaurus';
    const bounds = this.world.boundsFor(def, big ? 1.9 : 1.2);
    const anim = new DinoAnimator(rig, bounds, this.rng, (a) => this.onRoar(a));
    if (at) {
      anim.x = at.x;
      anim.z = at.z;
    }
    herd.push(anim);
    if (hatch) anim.hatch();
    return anim;
  }

  private onRoar(a: DinoAnimator) {
    // Only roar audibly when the dino is on screen — otherwise the park becomes a wall of noise.
    const dx = a.x - this.cam.target.x;
    const dz = a.z - this.cam.target.y;
    if (Math.hypot(dx, dz) > this.cam.distance * 0.7) return;
    const size = a.rig.species.hipHeight;
    this.audio.play('roar', size);
    if (size > 1.2) this.cam.addShake(0.25);
  }

  // ---------------------------------------------------------------- frame loop

  private frame() {
    const dt = Math.min(0.05, this.clock.getDelta());
    const now = Date.now();
    const state = this.data.park;

    this.sim.update(dt, state, now, (e) => this.onPay(e), this.interactions.ticketMultiplier);
    this.interactions.update(dt);
    for (const herd of this.herds.values()) for (const d of herd) d.update(dt, herd);
    this.crowd.update(this.sim.visitors);
    this.effects.update(dt);
    this.cam.update(dt);
    this.updateLabelPositions();
    this.updateTutorial();

    this.uiTimer += dt;
    if (this.uiTimer > 0.2) {
      this.uiTimer = 0;
      for (const l of this.labels) l.refresh();
      this.sheetRefresh?.();
      this.updateBoost(now);
      this.refreshQuest();
    }
    this.updateCoins(dt);

    this.saveTimer += dt;
    if (this.saveTimer > AUTOSAVE_SECONDS) {
      this.saveTimer = 0;
      this.persist();
    }

    this.renderer.render(this.world.scene, this.cam.camera);
    this.adaptQuality(dt);
  }

  /** Drops resolution on slow devices instead of stuttering. Profile-driven, not guessed. */
  private adaptQuality(dt: number) {
    this.frameTimes.push(dt);
    if (this.frameTimes.length < 90) return;
    const avg = this.frameTimes.reduce((a, b) => a + b, 0) / this.frameTimes.length;
    this.frameTimes.length = 0;
    if (avg > 1 / 40 && this.pixelRatio > 1) {
      this.pixelRatio = Math.max(1, this.pixelRatio - 0.25);
      this.renderer.setPixelRatio(this.pixelRatio);
      this.onResize();
    }
  }

  private onPay(e: PayEvent) {
    this.economy.earn(this.data.park, e.amount);
    if (!e.visitor) return;
    const v = e.visitor;
    const dx = v.x - this.cam.target.x;
    const dz = v.z - this.cam.target.y;
    if (Math.hypot(dx, dz) < this.cam.distance * 0.8) {
      this.effects.coinPop(v.x, 1.9, v.z);
      this.audio.play('coin', e.kind === 'ticket' ? 1 : 0.6);
    }
  }

  private persist() {
    this.data.park.lastSeen = Date.now();
    this.saveMgr.save(this.data);
  }

  private onVisibility() {
    if (document.hidden) {
      this.hiddenAt = Date.now();
      this.persist();
      analytics.track('session_ended');
    } else if (this.hiddenAt) {
      this.checkOffline(Date.now());
      this.clock.getDelta(); // don't simulate the gap as one giant frame
    }
  }

  private checkOffline(now: number) {
    const { seconds, coins } = this.economy.offlineEarnings(this.data.park, now);
    this.data.park.lastSeen = now;
    if (seconds < OFFLINE_MIN_SECONDS || coins <= 0) return;
    const modal = this.modal(`
      <h2>${t('welcomeBack')}</h2>
      <p>${t('offlineEarned', { t: formatDuration(seconds) })}</p>
      <div class="big-coins">${icons.coin}<span>${formatNumber(coins)}</span></div>
      <div class="actions">
        <button class="btn blue" data-a="collect"><span class="btn-price">${t('collect')}</span></button>
        <button class="btn purple" data-a="double"><span class="btn-label">${icons.play} ${t('boostWatch')}</span><span class="btn-price">${t('collectDouble')}</span></button>
      </div>`, false);
    const finish = (mult: number) => {
      modal.close();
      this.economy.earn(this.data.park, coins * mult);
      this.effects.confetti(this.cam.target.x, 2, this.cam.target.y, 70);
      this.audio.play('reward');
      this.bumpCoins();
      this.persist();
    };
    modal.on('collect', () => finish(1));
    modal.on('double', async () => {
      const ok = await this.ads.showRewarded('offline_double');
      if (ok) finish(2);
      else this.toast(t('adFailed'));
    });
  }

  // ---------------------------------------------------------------- HUD

  private buildHud() {
    const hud = $('hud');
    hud.innerHTML = `
      <div class="hud-money">
        <div class="pill pill-coins" id="hud-coins">${icons.coin}<span>0</span></div>
        <div class="pill pill-income" id="hud-income"></div>
      </div>
      <div class="hud-spacer"></div>
      <div class="hud-btns">
        <button class="round-btn" id="hud-settings" aria-label="${t('settings')}">${icons.gear}</button>
        <button class="boost-btn" id="hud-boost">${icons.bolt}<span></span></button>
      </div>`;
    this.hudCoins = $('hud-coins');
    this.hudIncome = $('hud-income');
    this.boostBtn = $('hud-boost');
    $('hud-settings').addEventListener('click', () => this.openSettings());
    this.boostBtn.addEventListener('click', () => this.onBoost());
    this.updateBoost(Date.now());
  }

  private updateCoins(dt: number) {
    const target = this.data.park.coins;
    // Count up/down smoothly so income feels alive.
    const diff = target - this.shownCoins;
    this.shownCoins = Math.abs(diff) < 1 ? target : this.shownCoins + diff * Math.min(1, dt * 8);
    this.hudCoins.querySelector('span')!.textContent = formatNumber(Math.floor(this.shownCoins));
  }

  private bumpCoins() {
    this.hudCoins.classList.remove('bump');
    void this.hudCoins.offsetWidth;
    this.hudCoins.classList.add('bump');
  }

  private updateBoost(now: number) {
    const state = this.data.park;
    const income = this.liveIncome(now);
    this.hudIncome.innerHTML = `<b>+${formatNumber(income)}</b>${t('perSec')}`;
    const active = this.economy.boostActive(state, now);
    this.boostBtn.classList.toggle('active', active);
    const label = this.boostBtn.querySelector('span')!;
    label.innerHTML = active
      ? `${t('boost')}<small>${formatDuration((state.boostUntil - now) / 1000)}</small>`
      : `${t('boost')}<small>${t('boostWatch')}</small>`;
  }

  private async onBoost() {
    this.audio.play('click');
    const ok = await this.ads.showRewarded('income_boost');
    if (!ok) {
      this.toast(t('adFailed'));
      return;
    }
    const now = Date.now();
    const state = this.data.park;
    const cap = now + ECONOMY.boostSeconds * 1000 * 4;
    state.boostUntil = Math.min(cap, Math.max(now, state.boostUntil) + ECONOMY.boostSeconds * 1000);
    this.audio.play('reward');
    this.toast(`${icons.bolt} ${t('boostStarted', { t: formatDuration(ECONOMY.boostSeconds) })}`);
    this.updateBoost(now);
    this.persist();
  }

  // ---------------------------------------------------------------- world labels

  private buildLabels() {
    const layer = $('labels');
    for (const def of this.economy.park.enclosures) {
      const species = SPECIES[def.speciesId];
      const root = el('div', 'wlabel');
      layer.appendChild(root);
      const anchorY = def.speciesId === 'brachiosaurus' ? 5.2 : def.speciesId === 'trex' ? 3.6 : 2.6;
      const label: Label = {
        root,
        width: 120,
        anchor: new THREE.Vector3(def.x, anchorY, def.z - def.halfD * 0.2),
        refresh: () => {
          const st = this.data.park.enclosures[def.id];
          const coins = this.data.park.coins;
          let html: string;
          if (st.level === 0) {
            const available = this.economy.isPlotAvailable(this.data.park, def.id);
            if (!available) {
              html = `<div class="wlabel-buy blocked">${icons.lock} ${species.name}</div>`;
            } else {
              html = `<div class="wlabel-name">${species.name}</div><div class="wlabel-buy ${coins >= def.unlockCost ? 'can' : ''}">${icons.lock} ${icons.coin} ${formatNumber(def.unlockCost)}</div>`;
            }
          } else {
            const canUp = st.level < ECONOMY.maxLevel && coins >= this.economy.upgradeCost(def, st.level);
            const canHatch = st.dinos < def.maxDinos && coins >= this.economy.eggCost(def, st.dinos);
            const chip = this.interactions?.chip(def.id, Date.now()) ?? '';
            html = `${canUp || canHatch ? `<div class="wlabel-up">${icons.up}</div>` : ''}<div class="wlabel-name">${species.name} <span class="lv">${t('level', { n: st.level })}</span></div>${chip}`;
          }
          if (root.dataset.html !== html) {
            root.dataset.html = html;
            root.innerHTML = html;
            label.width = root.offsetWidth;
          }
        },
      };
      root.addEventListener('click', () => this.openSheet({ kind: 'enclosure', id: def.id }));
      this.labels.push(label);
    }
    const gate = el('div', 'wlabel');
    layer.appendChild(gate);
    const gateLabel: Label = {
      root: gate,
      width: 120,
      anchor: this.world.gatePosition.clone().setY(5.3),
      refresh: () => {
        const lvl = this.data.park.entranceLevel;
        const can = lvl < ECONOMY.entranceMaxLevel && this.data.park.coins >= this.economy.entranceCost(lvl);
        const html = `${can ? `<div class="wlabel-up">${icons.up}</div>` : ''}<div class="wlabel-name">${icons.ticket} ${t('entrance')} <span class="lv">${t('level', { n: lvl })}</span></div>`;
        if (gate.dataset.html !== html) {
          gate.dataset.html = html;
          gate.innerHTML = html;
          gateLabel.width = gate.offsetWidth;
        }
      },
    };
    this.labels.push(gateLabel);
    gate.addEventListener('click', () => this.openSheet({ kind: 'entrance' }));
    for (const l of this.labels) l.refresh();
  }

  private updateLabelPositions() {
    const w = window.innerWidth;
    const h = window.innerHeight;
    for (const l of this.labels) {
      this.tmpV.copy(l.anchor).project(this.cam.camera);
      const off = this.tmpV.z > 1 || Math.abs(this.tmpV.x) > 1.2 || Math.abs(this.tmpV.y) > 1.2;
      l.root.classList.toggle('far', off);
      if (off) continue;
      // Keep labels fully on screen near the edges.
      const half = l.width / 2 + 6;
      const x = Math.min(w - half, Math.max(half, (this.tmpV.x * 0.5 + 0.5) * w));
      const y = (-this.tmpV.y * 0.5 + 0.5) * h;
      l.root.style.transform = `translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, 0) translate(-50%, -100%)`;
    }
  }

  private onWorldTap(clientX: number, clientY: number) {
    if (this.interactions.tap(clientX, clientY)) return;
    const ndc = new THREE.Vector2((clientX / window.innerWidth) * 2 - 1, -(clientY / window.innerHeight) * 2 + 1);
    this.raycaster.setFromCamera(ndc, this.cam.camera);
    const targets: THREE.Object3D[] = [];
    for (const v of this.world.enclosures.values()) targets.push(v.ground, ...v.lockedDecor.children);
    const hit = this.raycaster.intersectObjects(targets, false)[0];
    const id = hit?.object.userData.enclosureId as string | undefined;
    if (id) {
      this.openSheet({ kind: 'enclosure', id });
      return;
    }
    // Tap on a dino: make it roar (pure fun, and it teaches players the park is alive).
    for (const herd of this.herds.values()) {
      for (const d of herd) {
        const sphere = new THREE.Sphere(new THREE.Vector3(d.x, d.rig.species.hipHeight, d.z), d.rig.species.hipHeight * 0.9);
        if (this.raycaster.ray.intersectsSphere(sphere)) {
          d.roar();
          haptics.tap(20);
          this.questEvent('roar');
          return;
        }
      }
    }
    const ground = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    const p = this.raycaster.ray.intersectPlane(ground, this.tmpV);
    if (p && p.distanceTo(this.world.gatePosition) < 3.5) {
      this.openSheet({ kind: 'entrance' });
      return;
    }
    if (this.sheet) this.closeSheet();
  }

  // ---------------------------------------------------------------- bottom sheet

  private openSheet(s: NonNullable<SheetState>) {
    this.audio.play('click');
    haptics.tap();
    this.sheet = s;
    const sheet = $('sheet');
    sheet.innerHTML = '';
    const card = el('div', 'sheet-card');
    sheet.appendChild(card);
    const close = el('button', 'round-btn sheet-close', icons.close);
    close.addEventListener('click', () => this.closeSheet());
    card.appendChild(close);
    if (s.kind === 'enclosure') this.buildEnclosureSheet(card, s.id);
    else this.buildEntranceSheet(card);
    sheet.classList.add('open');
    this.questEl?.classList.add('hidden');
    if (s.kind === 'enclosure') {
      const def = this.economy.enclosure(s.id);
      // Keep the enclosure visible above the sheet.
      this.cam.focus(def.x * 0.6, def.z + 5, 26);
    } else {
      this.cam.focus(0, this.world.gatePosition.z + 2, 24);
    }
  }

  private closeSheet() {
    this.sheet = null;
    this.sheetRefresh = null;
    $('sheet').classList.remove('open');
    this.questEl?.classList.remove('hidden');
  }

  private buildEnclosureSheet(card: HTMLElement, id: string) {
    const def = this.economy.enclosure(id);
    const species = SPECIES[def.speciesId];
    const state = this.data.park;
    const st = state.enclosures[id];
    const portrait = this.portraits.get(species.id);
    card.insertAdjacentHTML('beforeend', `
      <div class="sheet-head">
        <div class="portrait">${portrait ? `<img src="${portrait}" alt="">` : ''}</div>
        <div>
          <div class="sheet-title">${species.name}<span class="rarity ${species.rarity}">${species.rarity}</span></div>
          <div class="sheet-sub">${species.description}</div>
        </div>
      </div>`);

    if (st.level === 0) {
      const available = this.economy.isPlotAvailable(state, id);
      const btn = el('button', 'btn');
      const wrap = el('div', 'actions single');
      wrap.appendChild(btn);
      card.appendChild(wrap);
      const refresh = () => {
        const prev = this.economy.park.enclosures[this.economy.park.enclosures.indexOf(def) - 1];
        if (!this.economy.isPlotAvailable(state, id) && prev) {
          btn.className = 'btn off';
          btn.innerHTML = `<span class="btn-price">${icons.lock} ${t('unlockFirst', { name: SPECIES[prev.speciesId].name })}</span>`;
          return;
        }
        btn.className = `btn ${state.coins >= def.unlockCost ? 'yellow' : 'off'}`;
        btn.innerHTML = `<span class="btn-label">${icons.lock} ${t('unlock')}</span><span class="btn-price">${icons.coin} ${formatNumber(def.unlockCost)}</span>`;
      };
      btn.addEventListener('click', () => {
        const r = this.economy.unlock(state, id);
        if (!r.ok) {
          this.fail(r.reason === 'coins' ? t('notEnough') : undefined);
          return;
        }
        this.onUnlocked(def);
      });
      void available;
      refresh();
      this.sheetRefresh = refresh;
      return;
    }

    const stats = el('div', 'stats');
    stats.innerHTML = `
      <div class="stat"><div class="stat-k">${icons.ticket} ${t('ticket')}</div><div class="stat-v" data-k="ticket"></div></div>
      <div class="stat"><div class="stat-k">${icons.egg} ${t('dinos')}</div><div class="stat-v" data-k="dinos"></div></div>`;
    card.appendChild(stats);
    const progress = el('div', 'progress');
    progress.innerHTML = `<div class="progress-top"><span data-k="ms"></span><span data-k="msr"></span></div><div class="progress-bar"><div class="progress-fill"></div></div>`;
    card.appendChild(progress);
    const actions = el('div', 'actions');
    const upBtn = el('button', 'btn');
    const eggBtn = el('button', 'btn blue');
    actions.append(upBtn, eggBtn);
    card.appendChild(actions);

    const q = (k: string) => card.querySelector(`[data-k="${k}"]`) as HTMLElement;
    const refresh = () => {
      const s = state.enclosures[id];
      const ticket = this.economy.ticketFor(def, s);
      const maxed = s.level >= ECONOMY.maxLevel;
      const next = maxed ? ticket : this.economy.ticketFor(def, s, s.level + 1);
      q('ticket').innerHTML = `${icons.coin} ${formatNumber(ticket)}${maxed ? '' : ` <span class="delta">+${formatNumber(next - ticket)}</span>`}`;
      q('dinos').innerHTML = `${s.dinos} / ${def.maxDinos}`;
      const ms = this.economy.milestoneProgress(s.level);
      q('ms').textContent = `${t('level', { n: s.level })} · ${t('nextMilestone', { n: ms.to })}`;
      q('msr').textContent = t('milestoneReward', { m: ECONOMY.milestoneMultiplier });
      (progress.querySelector('.progress-fill') as HTMLElement).style.width = `${Math.round(ms.ratio * 100)}%`;

      if (maxed) {
        upBtn.className = 'btn maxed';
        upBtn.innerHTML = `<span class="btn-label">${t('upgrade')}</span><span class="btn-price">${icons.star} ${t('maxed')}</span>`;
      } else {
        const cost = this.economy.upgradeCost(def, s.level);
        upBtn.className = `btn ${state.coins >= cost ? '' : 'off'}`;
        upBtn.innerHTML = `<span class="btn-label">${icons.up} ${t('upgrade')}</span><span class="btn-price">${icons.coin} ${formatNumber(cost)}</span>`;
      }
      if (s.dinos >= def.maxDinos) {
        eggBtn.className = 'btn maxed';
        eggBtn.innerHTML = `<span class="btn-label">${t('hatch')}</span><span class="btn-price">${icons.star} ${t('maxed')}</span>`;
      } else {
        const cost = this.economy.eggCost(def, s.dinos);
        eggBtn.className = `btn blue ${state.coins >= cost ? '' : 'off'}`;
        eggBtn.innerHTML = `<span class="btn-label">${icons.egg} ${t('hatch')}</span><span class="btn-price">${icons.coin} ${formatNumber(cost)}</span>`;
      }
    };
    holdToRepeat(upBtn, () => {
      const r = this.economy.upgrade(state, id);
      if (!r.ok) {
        if (r.reason === 'coins') this.fail(t('notEnough'));
        return false;
      }
      this.onUpgraded(def);
      refresh();
      return true;
    });
    eggBtn.addEventListener('click', () => {
      const r = this.economy.hatch(state, id);
      if (!r.ok) {
        if (r.reason === 'coins') this.fail(t('notEnough'));
        return;
      }
      this.onHatchBought(def);
      refresh();
    });
    refresh();
    this.sheetRefresh = refresh;
  }

  private buildEntranceSheet(card: HTMLElement) {
    const state = this.data.park;
    card.insertAdjacentHTML('beforeend', `
      <div class="sheet-head">
        <div class="portrait" style="font-size:36px;color:#e8505b">${icons.ticket}</div>
        <div><div class="sheet-title">${t('entrance')}</div><div class="sheet-sub">${t('entranceSub')}</div></div>
      </div>
      <div class="stats">
        <div class="stat"><div class="stat-k">${icons.people} ${t('visitors')}</div><div class="stat-v" data-k="rate"></div></div>
        <div class="stat"><div class="stat-k">${icons.ticket} ${t('entryFee')}</div><div class="stat-v" data-k="fee"></div></div>
      </div>`);
    const actions = el('div', 'actions single');
    const btn = el('button', 'btn');
    actions.appendChild(btn);
    card.appendChild(actions);
    const q = (k: string) => card.querySelector(`[data-k="${k}"]`) as HTMLElement;
    const refresh = () => {
      const lvl = state.entranceLevel;
      const maxed = lvl >= ECONOMY.entranceMaxLevel;
      const rate = this.economy.visitorRate(lvl) * 60;
      const fee = this.economy.entryFee(lvl);
      const dRate = maxed ? 0 : this.economy.visitorRate(lvl + 1) * 60 - rate;
      const dFee = maxed ? 0 : this.economy.entryFee(lvl + 1) - fee;
      q('rate').innerHTML = `${rate.toFixed(0)}/min${dRate > 0.05 ? ` <span class="delta">+${dRate.toFixed(1)}</span>` : ''}`;
      q('fee').innerHTML = `${icons.coin} ${formatNumber(fee)}${dFee > 0 ? ` <span class="delta">+${formatNumber(dFee)}</span>` : ''}`;
      if (maxed) {
        btn.className = 'btn maxed';
        btn.innerHTML = `<span class="btn-price">${icons.star} ${t('maxed')}</span>`;
      } else {
        const cost = this.economy.entranceCost(lvl);
        btn.className = `btn ${state.coins >= cost ? '' : 'off'}`;
        btn.innerHTML = `<span class="btn-label">${icons.up} ${t('upgrade')} · ${t('level', { n: lvl })}</span><span class="btn-price">${icons.coin} ${formatNumber(cost)}</span>`;
      }
    };
    holdToRepeat(btn, () => {
      const r = this.economy.upgradeEntrance(state);
      if (!r.ok) {
        if (r.reason === 'coins') this.fail(t('notEnough'));
        return false;
      }
      this.audio.play('upgrade');
      haptics.tap();
      this.effects.confetti(this.world.gatePosition.x, 4, this.world.gatePosition.z, 12, 0.6);
      refresh();
      return true;
    });
    refresh();
    this.sheetRefresh = refresh;
  }

  // ---------------------------------------------------------------- purchase reactions

  private onUpgraded(def: EnclosureDef) {
    const st = this.data.park.enclosures[def.id];
    haptics.tap();
    if (st.level % ECONOMY.milestoneEvery === 0) {
      this.audio.play('milestone');
      this.banner(t('milestone'), `${SPECIES[def.speciesId].name} x${ECONOMY.milestoneMultiplier}`);
      this.effects.confetti(def.x, 2.5, def.z, 90, 1);
      for (const d of this.herds.get(def.id)!) d.roar();
    } else {
      this.audio.play('upgrade');
      this.effects.confetti(def.x, 2, def.z, 10, 0.6);
    }
    if (!this.data.tutorialDone) this.finishTutorial();
  }

  private onHatchBought(def: EnclosureDef) {
    const species = SPECIES[def.speciesId];
    const b = this.world.boundsFor(def, 1.6);
    const x = THREE.MathUtils.lerp(b.minX, b.maxX, 0.3 + this.rng() * 0.4);
    const z = THREE.MathUtils.lerp(b.minZ, b.maxZ, 0.3 + this.rng() * 0.4);
    this.audio.play('click');
    this.effects.spawnEgg(x, z, species.palette.base, () => {
      this.spawnDino(def, true, { x, z });
      this.audio.play('hatch');
      this.toast(`${icons.egg} ${t('hatched', { name: species.name })}`);
    });
    analytics.track('dino_hatched', { species: species.id });
    this.persist();
  }

  private onUnlocked(def: EnclosureDef) {
    const species = SPECIES[def.speciesId];
    const view = this.world.enclosures.get(def.id)!;
    view.setUnlocked(true);
    // Rails and scenery pop in.
    view.rails.scale.set(1, 0.01, 1);
    view.scenery.scale.setScalar(0.01);
    const t0 = performance.now();
    const grow = () => {
      const k = Math.min(1, (performance.now() - t0) / 500);
      const e = 1 - Math.pow(2, -8 * k) * Math.cos(k * 9);
      view.rails.scale.set(1, e, 1);
      view.scenery.scale.setScalar(Math.max(0.01, e));
      if (k < 1) requestAnimationFrame(grow);
    };
    grow();
    this.audio.play('unlock');
    haptics.tap(40);
    this.effects.confetti(def.x, 3, def.z, 140, 1.2);
    this.banner(t('newDino'), species.name);
    this.cam.focus(def.x * 0.6, def.z + 5, 24);
    this.effects.spawnEgg(def.x, def.z, species.palette.base, () => {
      this.spawnDino(def, true, { x: def.x, z: def.z });
      this.audio.play('hatch');
    });
    analytics.track('dinosaur_unlocked', { species: species.id });
    this.persist();
    // Rebuild the sheet into its "owned" layout.
    if (this.sheet?.kind === 'enclosure' && this.sheet.id === def.id) this.openSheet(this.sheet);
  }

  private fail(message?: string) {
    this.audio.play('error');
    if (message) this.toast(message);
  }

  // ---------------------------------------------------------------- tutorial

  private startTutorial() {
    const hand = el('div', 'tut-hand', icons.hand);
    const text = el('div', 'tut-text', t('tutorialUpgrade'));
    $('app').append(hand, text);
    this.tutorial = { hand, text };
  }

  private updateTutorial() {
    if (!this.tutorial) return;
    const { hand, text } = this.tutorial;
    let target: DOMRect | null = null;
    if (this.sheet?.kind === 'enclosure' && this.sheet.id === this.economy.park.enclosures[0].id) {
      target = ($('sheet').querySelector('.actions .btn') as HTMLElement | null)?.getBoundingClientRect() ?? null;
    } else if (!this.sheet) {
      target = this.labels[0].root.getBoundingClientRect();
    }
    const visible = !!target && target.width > 0;
    hand.style.display = text.style.display = visible ? '' : 'none';
    if (!target || !visible) return;
    const x = target.left + target.width / 2;
    const y = target.top + target.height / 2;
    hand.style.left = `${x}px`;
    hand.style.top = `${y + 4}px`;
    text.style.left = `${x}px`;
    text.style.top = `${target.top - 38}px`;
  }

  private finishTutorial() {
    this.data.tutorialDone = true;
    this.tutorial?.hand.remove();
    this.tutorial?.text.remove();
    this.tutorial = null;
    analytics.track('tutorial_completed');
    this.persist();
  }

  // ---------------------------------------------------------------- rewards & quests

  /** Income including live-only care modifiers — what the player actually earns right now. */
  private liveIncome(now: number) {
    return this.economy.incomePerSecond(this.data.park, now, this.interactions?.ticketMultiplier);
  }

  /** Grants coins immediately and plays the feedback: floating "+X" and coins flying to the counter. */
  private reward(amount: number, x: number, y: number) {
    this.economy.earn(this.data.park, amount);
    const text = el('div', 'float-text', `+${formatNumber(amount)}`);
    text.style.left = `${x}px`;
    text.style.top = `${y}px`;
    $('app').appendChild(text);
    setTimeout(() => text.remove(), 1200);
    const target = this.hudCoins.querySelector('.ico')!.getBoundingClientRect();
    const tx = target.left + target.width / 2 - 13;
    const ty = target.top + target.height / 2 - 13;
    const count = Math.min(8, 3 + Math.floor(Math.log10(Math.max(1, amount))));
    for (let i = 0; i < count; i++) {
      const c = el('div', 'fly-coin', icons.coin);
      const sx = x - 13 + (Math.random() - 0.5) * 50;
      const sy = y - 13 + (Math.random() - 0.5) * 30;
      c.style.transform = `translate(${sx}px, ${sy}px) scale(.4)`;
      $('app').appendChild(c);
      const delay = i * 45;
      // Burst outwards, then swoop into the coin counter.
      c.animate(
        [
          { transform: `translate(${sx}px, ${sy}px) scale(.4)` },
          { transform: `translate(${sx + (Math.random() - 0.5) * 70}px, ${sy - 40 - Math.random() * 30}px) scale(1.1)`, offset: 0.3 },
          { transform: `translate(${tx}px, ${ty}px) scale(.8)` },
        ],
        { duration: 750, delay, easing: 'cubic-bezier(.5,0,.75,.4)', fill: 'forwards' },
      ).onfinish = () => {
        c.remove();
        if (i === count - 1) this.bumpCoins();
        if (i % 2 === 0) this.audio.play('coin', 0.7);
      };
    }
  }

  private questEvent(kind: QuestKind) {
    if (recordQuestEvent(kind, this.data.quest)) this.refreshQuest();
  }

  private buildQuestBar() {
    this.questEl = el('button', '');
    this.questEl.id = 'quest';
    this.questEl.innerHTML = `<div class="q-icon">${icons.scroll}</div><div class="q-body"><div class="q-title"></div><div class="q-sub"></div><div class="q-bar"><div class="q-fill"></div></div></div><div class="q-reward">${icons.coin}<span></span></div>`;
    this.questEl.addEventListener('click', () => this.onQuestTap());
    $('app').appendChild(this.questEl);
    this.refreshQuest();
  }

  private questText(q: QuestDef): string {
    const name = (id?: string) => (id ? SPECIES[this.economy.enclosure(id).speciesId].name : '');
    switch (q.kind) {
      case 'level': return t('questLevel', { name: name(q.enclosure), n: q.target });
      case 'dinos': return t('questDinos', { name: name(q.enclosure), n: q.target });
      case 'unlock': return t('questUnlock', { name: name(q.enclosure) });
      case 'entrance': return t('questEntrance', { n: q.target });
      case 'feed': return t('questFeed', { n: q.target });
      case 'clean': return t('questClean', { n: q.target });
      case 'tip': return t('questTip', { n: q.target });
      case 'gift': return t(q.target === 1 ? 'questGift' : 'questGifts', { n: q.target });
      case 'roar': return t('questRoar', { n: q.target });
    }
  }

  private refreshQuest() {
    if (!this.questEl) return;
    const q = questAt(this.data.quest.index);
    const value = Math.min(q.target, questValue(q, this.data.quest, this.data.park));
    const done = questComplete(q, this.data.quest, this.data.park);
    const title = done ? t('questDone') : this.questText(q);
    const sub = done ? this.questText(q) : q.kind === 'unlock' ? '' : `${value} / ${q.target}`;
    const reward = formatNumber(questReward(q, this.liveIncome(Date.now())));
    const key = `${this.data.quest.index}|${value}|${done}|${reward}`;
    if (this.questEl.dataset.key === key) return;
    this.questEl.dataset.key = key;
    this.questEl.classList.toggle('done', done);
    this.questEl.querySelector('.q-title')!.textContent = title;
    this.questEl.querySelector('.q-sub')!.textContent = sub;
    (this.questEl.querySelector('.q-fill') as HTMLElement).style.width = `${(value / q.target) * 100}%`;
    this.questEl.querySelector('.q-reward span')!.textContent = done ? t('claim') : reward;
  }

  private onQuestTap() {
    const q = questAt(this.data.quest.index);
    if (questComplete(q, this.data.quest, this.data.park)) {
      const r = this.questEl.getBoundingClientRect();
      const amount = claimQuest(this.data.quest, this.data.park, this.liveIncome(Date.now()));
      this.audio.play('claim');
      haptics.tap(30);
      this.effects.confetti(this.cam.target.x, 3, this.cam.target.y, 60, 0.9);
      this.reward(amount, r.left + r.width - 40, r.top);
      analytics.track('mission_completed', { index: this.data.quest.index - 1 });
      this.refreshQuest();
      this.persist();
      return;
    }
    // Not done yet: show the player where to go.
    this.audio.play('click');
    switch (q.kind) {
      case 'level':
      case 'dinos':
      case 'unlock':
        this.openSheet({ kind: 'enclosure', id: q.enclosure! });
        break;
      case 'entrance':
        this.openSheet({ kind: 'entrance' });
        break;
      case 'feed': {
        const id = this.interactions.hungryEnclosure();
        if (id) {
          const d = this.economy.enclosure(id);
          this.cam.focus(d.x * 0.6, d.z + 5, 24);
        } else this.toast(t('hintFeed'));
        break;
      }
      case 'clean':
        this.toast(t('hintClean'));
        break;
      case 'tip':
        this.toast(t('hintTip'));
        break;
      case 'gift':
        this.toast(t('hintGift'));
        break;
      case 'roar':
        this.toast(t('hintRoar'));
        break;
    }
  }

  // ---------------------------------------------------------------- settings / modal / toasts

  private openSettings() {
    this.audio.play('click');
    const s = this.data.settings;
    const row = (key: 'sfx' | 'music' | 'haptics', label: string) =>
      `<div class="toggle-row"><span>${label}</span><button class="toggle ${s[key] ? 'on' : ''}" data-a="${key}"></button></div>`;
    const dev = import.meta.env.DEV ? `<button class="small-link" data-a="cheat">[dev] +1M coins</button><br>` : '';
    const modal = this.modal(`
      <h2>${t('settings')}</h2>
      ${row('sfx', t('sound'))}${row('music', t('music'))}${row('haptics', t('haptics'))}
      ${dev}<button class="small-link" data-a="reset">${t('resetProgress')}</button>`, true);
    const toggle = (key: 'sfx' | 'music' | 'haptics') => {
      s[key] = !s[key];
      modal.root.querySelector(`[data-a="${key}"]`)!.classList.toggle('on', s[key]);
      this.audio.setSfx(s.sfx);
      this.audio.setMusic(s.music);
      haptics.enabled = s.haptics;
      this.audio.play('click');
      this.persist();
    };
    modal.on('sfx', () => toggle('sfx'));
    modal.on('music', () => toggle('music'));
    modal.on('haptics', () => toggle('haptics'));
    modal.on('cheat', () => {
      this.economy.earn(this.data.park, 1_000_000);
      this.bumpCoins();
    });
    modal.on('reset', () => {
      if (!confirm(t('resetConfirm'))) return;
      this.saveMgr.reset();
      window.removeEventListener('beforeunload', this.beforeUnload);
      location.reload();
    });
  }

  private beforeUnload = () => this.persist();

  private modal(html: string, closable: boolean) {
    const back = el('div', 'modal-back');
    const box = el('div', 'modal', html);
    back.appendChild(box);
    if (closable) {
      const close = el('button', 'round-btn sheet-close', icons.close);
      box.appendChild(close);
      close.addEventListener('click', () => back.remove());
      back.addEventListener('click', (e) => e.target === back && back.remove());
    }
    $('modal-root').appendChild(back);
    return {
      root: box,
      close: () => back.remove(),
      on: (action: string, fn: () => void) => box.querySelector(`[data-a="${action}"]`)?.addEventListener('click', fn),
    };
  }

  private toast(html: string) {
    const n = el('div', 'toast', html);
    $('toasts').appendChild(n);
    setTimeout(() => n.remove(), 2300);
  }

  private banner(top: string, main: string) {
    const n = el('div', 'banner', `<div class="banner-top">${top}</div><div class="banner-main">${main}</div>`);
    $('toasts').appendChild(n);
    setTimeout(() => n.remove(), 3100);
  }

  installUnloadHook() {
    window.addEventListener('beforeunload', this.beforeUnload);
  }
}

/** localStorage can throw (private mode, disabled storage); fall back to memory so the game still runs. */
function safeStorage() {
  try {
    const k = '__dpt_probe';
    localStorage.setItem(k, '1');
    localStorage.removeItem(k);
    return localStorage;
  } catch {
    const map = new Map<string, string>();
    return {
      getItem: (k: string) => map.get(k) ?? null,
      setItem: (k: string, v: string) => void map.set(k, v),
      removeItem: (k: string) => void map.delete(k),
    };
  }
}
