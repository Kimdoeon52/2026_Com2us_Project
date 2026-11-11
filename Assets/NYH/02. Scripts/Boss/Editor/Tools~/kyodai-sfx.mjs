// 쿄다이 효과음 합성기 (2026-11-05). 외부 라이브러리 없이 Node만으로 WAV(44.1kHz 16bit 모노)를 만든다.
// 사용: node kyodai-sfx.mjs <출력 폴더>
// 파일 이름 = kyodai_<KyodaiSfxData 칸 이름>_<번호>.wav — 빌더(2D 쿄다이 만들기·갱신)가 이름으로 칸을 채운다.
// 분위기: 묵직한 기계음 + 살짝 거친(레트로) 질감 — 도트 그림체에 맞게. 같은 시드면 같은 소리.
import fs from 'fs';
import path from 'path';

const SR = 44100;
const TAU = Math.PI * 2;
const out = process.argv[2];
if (!out) { console.error('출력 폴더를 주세요'); process.exit(1); }
fs.mkdirSync(out, { recursive: true });

// ───────── 기본 도구 ─────────
function rng(seed) { // mulberry32
  return () => { seed |= 0; seed = (seed + 0x6D2B79F5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}
const lerp = (a, b, t) => a + (b - a) * t;
const clamp01 = t => Math.max(0, Math.min(1, t));
const expLerp = (a, b, t) => a * Math.pow(b / a, clamp01(t));
const dec = (t, tau) => (t < 0 ? 0 : Math.exp(-t / tau));
const att = (t, a) => (t < 0 ? 0 : t < a ? t / a : 1);
const after = (t, h, tau) => (t < h ? 1 : Math.exp(-(t - h) / tau)); // h초까지 유지 → 그다음 감쇠

// RBJ 바이쿼드 — 주파수를 샘플마다 바꿔도 된다
class Biquad {
  constructor(type, f = 1000, q = 0.707) { this.type = type; this.x1 = this.x2 = this.y1 = this.y2 = 0; this.set(f, q); }
  set(f, q = this.q) {
    this.q = q; f = Math.max(20, Math.min(SR * 0.45, f));
    const w = TAU * f / SR, c = Math.cos(w), s = Math.sin(w), a = s / (2 * q);
    let b0, b1, b2; const a0 = 1 + a, a1 = -2 * c, a2 = 1 - a;
    if (this.type === 'lp') { b0 = (1 - c) / 2; b1 = 1 - c; b2 = (1 - c) / 2; }
    else if (this.type === 'hp') { b0 = (1 + c) / 2; b1 = -(1 + c); b2 = (1 + c) / 2; }
    else { b0 = a; b1 = 0; b2 = -a; } // bp
    this.b0 = b0 / a0; this.b1 = b1 / a0; this.b2 = b2 / a0; this.a1 = a1 / a0; this.a2 = a2 / a0;
  }
  run(x) { const y = this.b0 * x + this.b1 * this.x1 + this.b2 * this.x2 - this.a1 * this.y1 - this.a2 * this.y2;
    this.x2 = this.x1; this.x1 = x; this.y2 = this.y1; this.y1 = y; return y; }
}

// 위상 누적 오실레이터 (주파수가 변해도 끊김 없음)
class Osc {
  constructor(shape, phase = 0) { this.shape = shape; this.p = phase; }
  run(f) { this.p = (this.p + f / SR) % 1; const p = this.p;
    if (this.shape === 'sin') return Math.sin(TAU * p);
    if (this.shape === 'saw') return 2 * p - 1;
    if (this.shape === 'sq') return p < 0.5 ? 1 : -1;
    return 1 - 4 * Math.abs(p - 0.5); } // tri
}

// 쇠붙이 울림 — 어울리지 않는 배음들(종·판 소리)
function metal(f0, ratios, tau, r) {
  const oscs = ratios.map(() => new Osc('sin', r()));
  return t => { let s = 0; ratios.forEach((k, i) => { s += oscs[i].run(f0 * k) * dec(t, tau / (1 + i * 0.6)) / (1 + i * 0.7); }); return s; };
}

function render(seconds, fn) {
  const n = Math.round(seconds * SR), buf = new Float32Array(n);
  for (let i = 0; i < n; i++) buf[i] = fn(i / SR, i);
  return buf;
}

// 마무리: 살짝 눌러 붙이고(tanh) → 레트로 질감(2배 다운샘플 유지 + 12bit) → 최고점 맞추기 → 끝 페이드
function finish(buf, { drive = 1.2, crush = true, peak = 0.89, fade = 0.01 } = {}) {
  for (let i = 0; i < buf.length; i++) buf[i] = Math.tanh(buf[i] * drive);
  if (crush) for (let i = 0; i < buf.length; i++) { const h = i & ~1; buf[i] = Math.round(buf[h] * 2048) / 2048; }
  let m = 0; for (const v of buf) m = Math.max(m, Math.abs(v));
  if (m > 0) for (let i = 0; i < buf.length; i++) buf[i] *= peak / m;
  const f = Math.round(fade * SR);
  for (let i = 0; i < f && i < buf.length; i++) buf[buf.length - 1 - i] *= i / f;
  return buf;
}

// 반복 소리: 앞뒤를 겹쳐 이음매 없이 (len + xf 만큼 만들어 꼬리를 머리에 섞는다)
function loop(seconds, xf, fn, opts) {
  const all = render(seconds + xf, fn), n = Math.round(seconds * SR), x = Math.round(xf * SR);
  const outb = new Float32Array(n);
  for (let i = 0; i < n; i++) outb[i] = all[i];
  for (let i = 0; i < x; i++) { const t = i / x; outb[i] = all[i] * Math.sin(t * Math.PI / 2) + all[n + i] * Math.cos(t * Math.PI / 2); }
  return finish(outb, { ...opts, fade: 0 });
}

function writeWav(file, buf) {
  const n = buf.length, b = Buffer.alloc(44 + n * 2);
  b.write('RIFF', 0); b.writeUInt32LE(36 + n * 2, 4); b.write('WAVE', 8); b.write('fmt ', 12);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(SR, 24);
  b.writeUInt32LE(SR * 2, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write('data', 36); b.writeUInt32LE(n * 2, 40);
  for (let i = 0; i < n; i++) b.writeInt16LE(Math.round(Math.max(-1, Math.min(1, buf[i])) * 32767), 44 + i * 2);
  fs.writeFileSync(file, b);
}

const sounds = {};
const add = (name, count, make) => { for (let v = 0; v < count; v++) sounds[`kyodai_${name}_${v + 1}`] = make(rng(1000 + Object.keys(sounds).length * 97 + v), v); };

// ───────── 손 ─────────
add('fistImpact', 3, (r, v) => { // 쿵 + 흙먼지 + 쇠 울림
  const sub = new Osc('sin'), lp = new Biquad('lp'), hp = new Biquad('hp', 3000), ring = metal(260 + r() * 80, [1, 2.32, 3.87, 5.13], 0.22, r);
  return finish(render(0.9, t => {
    lp.set(expLerp(1800, 250, t / 0.3));
    const n = r() * 2 - 1;
    return sub.run(expLerp(110 - v * 8, 36, t / 0.25)) * dec(t, 0.26) * 1.1
      + lp.run(n) * dec(t, 0.09) * 0.9 + hp.run(n) * dec(t, 0.008) * 0.5 + ring(t) * 0.25;
  }), { drive: 1.6 });
});
add('palmImpact', 2, (r) => { // 손뼉 — 짧은 잡음 세 번 + 낮은 쿵
  const bp = new Biquad('bp', 1100, 0.8), sub = new Osc('sin'), ring = metal(190 + r() * 40, [1, 2.7, 4.4], 0.12, r);
  const taps = [0, 0.009 + r() * 0.004, 0.02 + r() * 0.006];
  return finish(render(0.6, t => {
    const n = r() * 2 - 1; let e = 0; for (const s of taps) e += dec(t - s, 0.03) * (t >= s ? 1 : 0);
    return bp.run(n) * e * 1.2 + sub.run(expLerp(95, 48, t / 0.2)) * dec(t, 0.13) + ring(t) * 0.2;
  }), { drive: 1.4 });
});
add('strikeWhoosh', 3, (r, v) => { // 휙 — 대역이 올라갔다 내려오는 바람 + 낮은 붕
  const len = 0.28 + v * 0.04, bp = new Biquad('bp', 400, 1.2), hum = new Osc('saw'), lp = new Biquad('lp', 220);
  return finish(render(len + 0.08, t => {
    const k = clamp01(t / len), c = k < 0.4 ? expLerp(300, 1900, k / 0.4) : expLerp(1900, 450, (k - 0.4) / 0.6);
    bp.set(c); const e = Math.pow(Math.sin(Math.PI * k), 2);
    return bp.run(r() * 2 - 1) * e * 1.4 + lp.run(hum.run(58)) * e * 0.35;
  }), { drive: 1.1, peak: 0.8 });
});
add('handHit', 3, (r, v) => { // 쨍 — 금속 판 맞는 소리
  const ring = metal(480 + v * 70 + r() * 30, [1, 2.76, 5.4, 8.93], 0.11, r), hp = new Biquad('hp', 2500), thunk = new Osc('sin');
  return finish(render(0.35, t => {
    const n = r() * 2 - 1;
    return ring(t) * 0.8 + hp.run(n) * dec(t, 0.005) * 0.7 + thunk.run(expLerp(180, 110, t / 0.05)) * dec(t, 0.04) * 0.6;
  }), { drive: 1.5 });
});
add('handZapped', 2, (r) => { // 지지직 감전 → 힘 빠지며 툭
  const sq = new Osc('sq'), lp = new Biquad('lp', 2600), fall = new Osc('sin'), hp = new Biquad('hp', 3500);
  let f = 160, hold = 0, gate = 1;
  return finish(render(0.75, t => {
    if (--hold <= 0) { hold = Math.round(SR * (0.006 + r() * 0.012)); f = 80 + r() * 200; gate = r() < 0.8 ? 1 : 0.2; }
    const e = t < 0.28 ? 1 : dec(t - 0.28, 0.14), n = r() * 2 - 1;
    return lp.run(sq.run(f)) * e * gate * 0.7 + hp.run(n) * (r() < 0.04 ? 1 : 0.15) * e * 0.6
      + fall.run(expLerp(900, 140, t / 0.6)) * dec(t, 0.3) * 0.35;
  }), { drive: 1.3, peak: 0.85 });
});
add('handThud', 2, (r) => { // 바닥에 떨어져 쿵
  const sub = new Osc('sin'), lp = new Biquad('lp', 400), ring = metal(210 + r() * 30, [1, 2.4, 3.9], 0.06, r);
  return finish(render(0.45, t => sub.run(expLerp(85, 44, t / 0.18)) * dec(t, 0.12) + lp.run(r() * 2 - 1) * dec(t, 0.05) * 0.8 + ring(t) * 0.25),
    { drive: 1.5 });
});
add('shockwave', 1, (r) => { // 바닥을 타고 구르는 진동
  const lp = new Biquad('lp', 260), sub = new Osc('sin'), hp = new Biquad('hp', 2000);
  return finish(render(1.3, t => {
    const e = att(t, 0.02) * dec(t, 0.45), trem = 0.65 + 0.35 * Math.sin(TAU * 12 * t), n = r() * 2 - 1;
    return lp.run(n) * e * trem * 1.6 + sub.run(46) * e * 0.8 + hp.run(n) * (r() < 0.01 ? 1 : 0) * e * 0.5;
  }), { drive: 1.4 });
});

// ───────── 어깨 레이저 ─────────
sounds.kyodai_laserCharge_1 = (() => { // 반복 1초 — 웅웅 모이는 소리 (게임에서 음을 올린다)
  const r = rng(7), a = new Osc('saw'), b = new Osc('saw', 0.3), lp = new Biquad('lp', 1300), s1 = new Osc('sin'), s2 = new Osc('sin'), hp = new Biquad('hp', 4500);
  return loop(1.0, 0.08, t => lp.run(a.run(110) + b.run(111)) * 0.45
    + (s1.run(440) * 0.25 + s2.run(660) * 0.15) * (0.5 + 0.5 * Math.sin(TAU * 8 * t)) + hp.run(r() * 2 - 1) * 0.12, { drive: 1.2, peak: 0.75 });
})();
add('laserFire', 2, (r, v) => { // 발사 — 위에서 아래로 쓸어내리는 굉음 + 붕
  const sw = new Osc('sin'), zap = new Osc('sq'), lpz = new Biquad('lp', 1800), bp = new Biquad('bp', 2500, 0.7), sub = new Osc('sin');
  return finish(render(0.8, t => {
    const n = r() * 2 - 1;
    return sw.run(expLerp(1800 + v * 300, 120, t / 0.35)) * dec(t, 0.3) * 0.7 + lpz.run(zap.run(expLerp(240, 55, t / 0.3))) * dec(t, 0.25) * 0.45
      + bp.run(n) * dec(t, 0.06) * 0.9 + sub.run(60) * dec(t, 0.2) * 0.8;
  }), { drive: 1.5 });
});
sounds.kyodai_laserBeam_1 = (() => { // 반복 1초 — 지지직 타는 광선
  const r = rng(11), a = new Osc('saw'), b = new Osc('saw', 0.5), sub = new Osc('sq'), lp = new Biquad('lp', 1800), sl = new Biquad('lp', 300), bp = new Biquad('bp', 3000, 1.5);
  let gate = 0, hold = 0;
  return loop(1.0, 0.1, t => {
    if (--hold <= 0) { hold = Math.round(SR * (0.004 + r() * 0.02)); gate = r() < 0.5 ? 1 : 0.25; }
    lp.set(1500 + 500 * Math.sin(TAU * 5 * t));
    return lp.run(a.run(82) + b.run(165)) * 0.45 + sl.run(sub.run(41)) * 0.5 + bp.run(r() * 2 - 1) * gate * 0.5;
  }, { drive: 1.4, peak: 0.75 });
})();
add('laserExplosion', 3, (r, v) => { // 바닥 폭발 펑
  const lp = new Biquad('lp'), sub = new Osc('sin'), hp = new Biquad('hp', 2500);
  return finish(render(0.9, t => {
    lp.set(expLerp(2600, 180, t / 0.5)); const n = r() * 2 - 1;
    return lp.run(n) * dec(t, 0.22) * 1.3 + sub.run(expLerp(75 - v * 6, 34, t / 0.3)) * dec(t, 0.2) + hp.run(n) * (r() < 0.02 ? 1 : 0) * dec(t, 0.3) * 0.5;
  }), { drive: 1.7 });
});

// ───────── 내려찍는 기물 ─────────
sounds.kyodai_crusherDescend_1 = (() => { // 반복 1초 — 유압 웅 + 체인 딸깍(초당 12번)
  const r = rng(23), hum = new Osc('saw'), lp = new Biquad('lp', 600), whine = new Osc('sin'), tick = metal(1400, [1, 2.3], 0.01, r), hp = new Biquad('hp', 1500);
  return loop(1.0, 0.06, t => {
    const tt = (t * 12) % 1 / 12;
    return lp.run(hum.run(55)) * 0.6 + whine.run(330) * (0.15 + 0.08 * Math.sin(TAU * 6 * t)) + tick(tt) * 0.4 + hp.run(r() * 2 - 1) * dec(tt, 0.003) * 0.3;
  }, { drive: 1.3, peak: 0.75 });
})();
add('crusherLock', 1, (r) => { // 삐삐삐 경고 + 끼익
  const sq = new Osc('sq'), lp = new Biquad('lp', 3000), bp = new Biquad('bp', 900, 4), fm = new Osc('sin');
  return finish(render(0.6, t => {
    const slot = Math.floor(t / 0.1), inBeep = (t % 0.1) < 0.07 && slot < 4;
    const beep = inBeep ? lp.run(sq.run(slot % 2 ? 660 : 880)) * 0.45 : lp.run(0);
    return beep + bp.run((r() * 2 - 1)) * (0.6 + 0.4 * fm.run(23)) * dec(t, 0.25) * 0.9;
  }), { drive: 1.2, peak: 0.8 });
});
add('crusherSlam', 2, (r, v) => { // 가장 큰 쾅
  const sub = new Osc('sin'), lp = new Biquad('lp'), hp = new Biquad('hp', 3000), ring = metal(170 + v * 25, [1, 2.32, 3.87, 5.13, 7.2], 0.55, r);
  return finish(render(1.6, t => {
    lp.set(expLerp(2600, 180, t / 0.45)); const n = r() * 2 - 1;
    return sub.run(expLerp(75, 27, t / 0.4)) * dec(t, 0.45) * 1.2 + lp.run(n) * dec(t, 0.2) * 1.1 + hp.run(n) * dec(t, 0.01) * 0.6 + ring(t) * 0.3;
  }), { drive: 2.0 });
});
add('crusherRise', 1, (r) => { // 치익 증기 + 유압이 올라가는 웅
  const hp = new Biquad('hp', 2500), saw = new Osc('saw'), lp = new Biquad('lp', 900);
  return finish(render(0.9, t => hp.run(r() * 2 - 1) * att(t, 0.02) * dec(t, 0.32) * 0.8
    + lp.run(saw.run(expLerp(80, 170, t / 0.8))) * att(t, 0.1) * dec(t, 0.5) * 0.5), { drive: 1.2, peak: 0.8 });
});

// ───────── 촉수 ─────────
add('tentacleEmerge', 2, (r) => { // 땅이 갈라지는 우르릉 + 질척 + 마디 딸깍
  const lp = new Biquad('lp', 200), bp = new Biquad('bp', 600, 1.5), tick = metal(1100 + r() * 300, [1, 2.5], 0.012, r);
  const ticks = [0.15, 0.3, 0.42, 0.51, 0.58, 0.63];
  let am = 1, hold = 0;
  return finish(render(0.85, t => {
    if (--hold <= 0) { hold = Math.round(SR * (0.02 + r() * 0.03)); am = r(); }
    let tk = 0; for (const s of ticks) if (t >= s) tk += tick(t - s) * dec(t - s, 0.02);
    const n = r() * 2 - 1;
    return lp.run(n) * att(t, 0.3) * after(t, 0.3, 0.3) * 1.6 + bp.run(n) * am * dec(t, 0.35) * 0.6 + tk * 0.35;
  }), { drive: 1.4, peak: 0.85 });
});
add('tentacleSwing', 2, (r, v) => { // 낮은 휙 + 끝에 채찍 짝
  const len = 0.36 + v * 0.04, bp = new Biquad('bp', 300, 1.1), hp = new Biquad('hp', 3000);
  return finish(render(len + 0.1, t => {
    const k = clamp01(t / len), c = k < 0.5 ? expLerp(200, 950, k / 0.5) : expLerp(950, 300, (k - 0.5) / 0.5);
    bp.set(c); const n = r() * 2 - 1, crack = t - len * 0.72;
    return bp.run(n) * Math.pow(Math.sin(Math.PI * k), 2) * 1.4 + hp.run(n) * (crack >= 0 ? dec(crack, 0.012) : 0) * 1.2;
  }), { drive: 1.2, peak: 0.82 });
});
add('tentacleHit', 3, (r, v) => { // 퍽 — 둔탁한 금속 + 질척
  const th = new Osc('sin'), bp = new Biquad('bp', 1400 + v * 200, 1), ring = metal(340 + v * 40, [1, 2.6], 0.04, r);
  return finish(render(0.25, t => th.run(expLerp(220, 115, t / 0.06)) * dec(t, 0.05) + bp.run(r() * 2 - 1) * dec(t, 0.03) * 0.9 + ring(t) * 0.3),
    { drive: 1.5 });
});
add('tentacleDie', 2, (r) => { // 낮아지는 신음 + 부서지는 잔해
  const saw = new Osc('saw'), lp = new Biquad('lp', 800), vib = new Osc('sin'), clk = new Biquad('hp', 2000), lpn = new Biquad('lp', 500);
  return finish(render(1.05, t => {
    const f = expLerp(165, 40, t / 0.8) * (1 + 0.04 * vib.run(7)), n = r() * 2 - 1;
    return lp.run(saw.run(f)) * att(t, 0.03) * dec(t, 0.45) * 0.7 + clk.run(n) * (r() < 0.02 * (1 - t) ? 1 : 0) * 0.8 + lpn.run(n) * dec(t, 0.3) * 0.5;
  }), { drive: 1.4, peak: 0.85 });
});

// ───────── 보스 ─────────
add('phaseTransition', 1, (r) => { // 시동 걸리듯 올라가는 굉음 + 경보 → 쾅
  const a = new Osc('saw'), b = new Osc('saw', 0.4), lp = new Biquad('lp'), siren = new Osc('sin'), rl = new Biquad('lp', 150), sub = new Osc('sin'), boomLp = new Biquad('lp');
  return finish(render(2.5, t => {
    const k = clamp01(t / 1.6); lp.set(expLerp(300, 3000, k));
    const f = expLerp(50, 300, k), rise = t < 1.6 ? att(t, 0.1) : dec(t - 1.6, 0.25), boom = t - 1.6, n = r() * 2 - 1;
    boomLp.set(expLerp(2500, 150, clamp01(boom / 0.6)));
    return lp.run(a.run(f) + b.run(f * 1.01)) * rise * 0.5 + siren.run(750 + 150 * Math.sin(TAU * 2 * t)) * rise * 0.18
      + rl.run(n) * att(t, 0.5) * 0.6 * (t < 1.6 ? 1 : dec(boom, 0.4))
      + (boom >= 0 ? sub.run(expLerp(60, 28, boom / 0.5)) * dec(boom, 0.45) + boomLp.run(n) * dec(boom, 0.25) : 0);
  }), { drive: 1.6 });
});
add('groggy', 1, (r) => { // 전원이 꺼지듯 내려가는 소리 + 털털
  const saw = new Osc('saw'), lp = new Biquad('lp'), hp = new Biquad('hp', 2500);
  return finish(render(1.4, t => {
    const k = clamp01(t / 1.2); lp.set(expLerp(3000, 200, k));
    const sputter = (Math.sin(TAU * lerp(14, 4, k) * t) > 0.2 ? 1 : 0.3);
    return lp.run(saw.run(expLerp(300, 35, k))) * sputter * after(t, 0.9, 0.2) * 0.8 + hp.run(r() * 2 - 1) * (r() < 0.01 ? 1 : 0) * 0.5;
  }), { drive: 1.3, peak: 0.85 });
});
add('defeat', 1, (r) => { // 큰 폭발 + 뒤따르는 작은 폭발 셋 + 잔해
  const main = { lp: new Biquad('lp'), sub: new Osc('sin') };
  const pops = [0.28, 0.52, 0.85].map(s => ({ s, lp: new Biquad('lp'), sub: new Osc('sin') }));
  const debris = new Biquad('hp', 2500);
  return finish(render(2.8, t => {
    const n = r() * 2 - 1;
    main.lp.set(expLerp(4000, 140, clamp01(t / 1.2)));
    let s = main.lp.run(n) * dec(t, 0.6) * 1.3 + main.sub.run(expLerp(55, 24, t / 0.8)) * dec(t, 0.7) * 1.2;
    for (const p of pops) { const u = t - p.s; if (u < 0) continue; p.lp.set(expLerp(2500, 200, clamp01(u / 0.4)));
      s += p.lp.run(n) * dec(u, 0.18) * 0.7 + p.sub.run(expLerp(70, 35, u / 0.3)) * dec(u, 0.2) * 0.6; }
    return s + debris.run(n) * (r() < 0.006 * (1 - t / 2.8) ? 1 : 0) * 0.8;
  }), { drive: 1.8 });
});

for (const [name, buf] of Object.entries(sounds)) writeWav(path.join(out, `${name}.wav`), buf);
console.log(`${Object.keys(sounds).length}개 → ${out}`);
