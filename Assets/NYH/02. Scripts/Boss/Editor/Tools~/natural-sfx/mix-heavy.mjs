// 둔탁·묵직한 내려찍기 (2026-11-12): 녹음 여러 겹 + 로우패스(고음 깎기) + 음높이 낮춤 + 합성 저음 쿵
// 사용: node mix.mjs <출력 폴더>
import fs from 'fs';
import { MPEGDecoder } from 'mpg123-decoder';
const OUT = process.argv[2]; const SR = 44100;
const report = JSON.parse(fs.readFileSync('report.json'));
const id = (cue, i) => report[cue][i].id;

// layers: 녹음 { src, pitch, gain, lp(Hz), at(초) } / sub: 합성 저음 { f0→f1 Hz, decay 초, gain }
const RECIPES = {
  fistImpact_1: { len: 1.1, layers: [
    { src: id('shockwave', 3), pitch: 0.8, gain: 0.8, lp: 1600 },         // gloss stomp sub firm — 낮은 쿵
    { src: id('fistImpact', 1), pitch: 0.6, gain: 0.7, lp: 2200 },      // Heavy Metal Impact — 금속 질감만 살짝
  ], sub: { f0: 110, f1: 50, decay: 0.25, gain: 0.35 } },
  fistImpact_2: { len: 1.1, layers: [
    { src: id('handThud', 4), pitch: 0.85, gain: 1.0, lp: 2400 },
    { src: id('fistImpact', 0), pitch: 0.6, gain: 0.5, lp: 2000 },        // Heavy Metal Thud on Ground
    { src: id('shockwave', 3), pitch: 0.75, gain: 0.3, lp: 1400 },
  ], sub: { f0: 105, f1: 48, decay: 0.28, gain: 0.35 } },
  crusherSlam_1: { len: 1.8, layers: [
    { src: id('crusherSlam', 0), pitch: 0.65, gain: 1.0, lp: 1800 },      // Seismic Slam
    { src: id('shockwave', 3), pitch: 0.7, gain: 0.3, lp: 1400 },
    { src: id('handThud', 0), pitch: 0.55, gain: 0.6, lp: 2000 },
  ], sub: { f0: 95, f1: 42, decay: 0.4, gain: 0.4 } },
  crusherSlam_2: { len: 1.8, layers: [
    { src: id('shockwave', 2), pitch: 0.85, gain: 0.7, lp: 1600 },         // Deep Boom
    { src: id('handThud', 0), pitch: 0.6, gain: 1.0, lp: 2200 },        // ANI Big Pipe Hit
  ], sub: { f0: 100, f1: 45, decay: 0.4, gain: 0.4 } },
};

async function load(srcId) {
  const dec = new MPEGDecoder(); await dec.ready;
  const { channelData, sampleRate } = dec.decode(new Uint8Array(fs.readFileSync(`dl/${srcId}.mp3`))); dec.free();
  const n = channelData[0].length, m = new Float32Array(n);
  for (let i = 0; i < n; i++) { let s = 0; for (const ch of channelData) s += ch[i]; m[i] = s / channelData.length; }
  return { m, sampleRate };
}
function resample(m, rate, pitch) {
  const step = rate / SR * pitch, n = Math.floor((m.length - 1) / step), o = new Float32Array(n);
  for (let i = 0; i < n; i++) { const x = i * step, k = Math.floor(x), f = x - k; o[i] = m[k] * (1 - f) + m[k + 1] * f; }
  return o;
}
function lowpass(x, fc) { // RBJ 2차 로우패스 두 번 = 24dB/oct
  for (let pass = 0; pass < 2; pass++) {
    const w = 2 * Math.PI * fc / SR, q = 0.707, al = Math.sin(w) / (2 * q), c = Math.cos(w);
    const b0 = (1 - c) / 2, b1 = 1 - c, b2 = (1 - c) / 2, a0 = 1 + al, a1 = -2 * c, a2 = 1 - al;
    let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
    for (let i = 0; i < x.length; i++) { const y = (b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0; x2 = x1; x1 = x[i]; y2 = y1; y1 = y; x[i] = y; }
  }
  return x;
}
function trimOnset(s) { let pk = 0; for (const v of s) pk = Math.max(pk, Math.abs(v)); let i = 0; while (i < s.length && Math.abs(s[i]) < pk * 0.05) i++; return s.subarray(Math.max(0, i - 40)); }
function norm(s) { let pk = 0; for (const v of s) pk = Math.max(pk, Math.abs(v)); const g = pk ? 1 / pk : 1; for (let i = 0; i < s.length; i++) s[i] *= g; return s; }
function wav(samples) {
  const b = Buffer.alloc(44 + samples.length * 2);
  b.write('RIFF', 0); b.writeUInt32LE(36 + samples.length * 2, 4); b.write('WAVE', 8); b.write('fmt ', 12);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(SR, 24); b.writeUInt32LE(SR * 2, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write('data', 36); b.writeUInt32LE(samples.length * 2, 40);
  for (let i = 0; i < samples.length; i++) b.writeInt16LE(Math.round(Math.max(-1, Math.min(1, samples[i])) * 32767), 44 + i * 2);
  return b;
}

for (const [name, r] of Object.entries(RECIPES)) {
  const N = Math.round(r.len * SR), mix = new Float32Array(N);
  for (const L of r.layers) {
    const { m, sampleRate } = await load(L.src);
    const s = norm(lowpass(trimOnset(resample(m, sampleRate, L.pitch)).slice(), L.lp));
    const at = Math.round((L.at || 0) * SR);
    for (let i = 0; i < s.length && at + i < N; i++) mix[at + i] += s[i] * L.gain;
  }
  // 합성 저음 쿵: 주파수가 떨어지는 사인 + 지수 감쇠 (2ms 어택)
  let ph = 0;
  for (let i = 0; i < N; i++) {
    const t = i / SR, f = r.sub.f1 + (r.sub.f0 - r.sub.f1) * Math.exp(-t / 0.08);
    ph += 2 * Math.PI * f / SR;
    mix[i] += Math.sin(ph) * Math.exp(-t / r.sub.decay) * Math.min(1, t / 0.002) * r.sub.gain;
  }
  // 부드러운 포화(뭉툭하게) → 끝 페이드 → 피크 -1dB
  for (let i = 0; i < N; i++) mix[i] = Math.tanh(mix[i] * 1.3);
  const fo = Math.round(0.3 * SR); for (let i = 0; i < fo; i++) mix[N - fo + i] *= Math.pow(1 - i / fo, 2);
  let pk = 0; for (const v of mix) pk = Math.max(pk, Math.abs(v)); for (let i = 0; i < N; i++) mix[i] *= 0.891 / pk;
  fs.writeFileSync(`${OUT}/kyodai_${name}.wav`, wav(mix));
  console.log(name, r.len + 's');
}
