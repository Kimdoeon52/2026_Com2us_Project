// 고른 CC0 녹음(dl/*.mp3) → 쿄다이 효과음 WAV (44.1k 16bit 모노) — 2026-11-12
// 사용: npm i mpg123-decoder@1.0.3 → picks.json의 각 id를 Openverse(https://api.openverse.org/v1/audio/<id>/)의 url에서 받아 dl/<id>.mp3로 저장
//       → node process.mjs <출력 폴더>. 결과를 09. Sound/Kyodai에 넣고 효과음 에셋 칸 교체
// 칸마다: 앞 무음 자르기 → 음높이 낮추기(거대 로봇 무게감) → 길이 자르기 + 끝 페이드 → 피크 정규화. 반복음은 이음매 크로스페이드
import fs from 'fs';
import { MPEGDecoder } from 'mpg123-decoder';

const OUT = process.argv[2];
const picks = JSON.parse(fs.readFileSync('picks.json'));
const SR = 44100;

// pitch: 재생 속도(1 = 원래, 0.8 = 낮고 느리게) / max: 최대 길이(초) / loop: 반복 길이(초, 이음매 없음) / skip: 앞에서 버릴 초
const P = {
  fistImpact:     { pitch: 0.78, max: 1.0 },
  palmImpact:     { pitch: 0.7,  max: 0.9 },
  strikeWhoosh:   { pitch: 0.85, max: 0.6 },
  handHit:        { pitch: 0.9,  max: 0.5 },
  handZapped:     { pitch: 1.0,  max: 0.8 },
  handThud:       { pitch: 0.8,  max: 0.8 },
  shockwave:      { pitch: 0.9,  max: 1.8 },
  laserCharge:    { pitch: 1.0,  loop: 2.0, skip: 1.0 },
  laserFire:      { pitch: 0.9,  max: 1.3 },
  laserBeam:      { pitch: 1.0,  loop: 2.5, skip: 0.8 },
  laserExplosion: { pitch: 0.85, max: 0.9 },
  crusherDescend: { pitch: 0.75, loop: 3.0, skip: 1.0 },
  crusherLock:    { pitch: 1.0,  max: 0.5 },
  crusherSlam:    { pitch: 0.75, max: 1.8 },
  crusherRise:    { pitch: 0.9,  max: 1.2 },
  tentacleEmerge: { pitch: 0.85, max: 1.5 },
  tentacleSwing:  { pitch: 0.8,  max: 0.5 },
  tentacleHit:    { pitch: 0.9,  max: 0.5 },
  tentacleDie:    { pitch: 0.8,  max: 1.2 },
  phaseTransition:{ pitch: 0.85, max: 3.5 },
  groggy:         { pitch: 0.85, max: 1.8 },
  defeat:         { pitch: 0.85, max: 4.5 },
};

async function decode(file) {
  const dec = new MPEGDecoder(); await dec.ready;
  const { channelData, sampleRate } = dec.decode(new Uint8Array(fs.readFileSync(file))); dec.free();
  const n = channelData[0].length, m = new Float32Array(n);
  for (let i = 0; i < n; i++) { let s = 0; for (const ch of channelData) s += ch[i]; m[i] = s / channelData.length; }
  return { m, sampleRate };
}

// 원본 rate → 44.1k로, 동시에 pitch만큼 느리게(낮게). 선형 보간
function resample(m, srcRate, pitch) {
  const step = srcRate / SR * pitch, n = Math.floor((m.length - 1) / step), o = new Float32Array(n);
  for (let i = 0; i < n; i++) { const x = i * step, k = Math.floor(x), f = x - k; o[i] = m[k] * (1 - f) + m[k + 1] * f; }
  return o;
}

function onset(m) {
  let pk = 0; for (const v of m) pk = Math.max(pk, Math.abs(v));
  const thr = pk * 0.02; let i = 0; while (i < m.length && Math.abs(m[i]) < thr) i++;
  return Math.max(0, i - Math.round(0.003 * SR)); // 3ms 여유
}

function wav(samples) {
  const b = Buffer.alloc(44 + samples.length * 2);
  b.write('RIFF', 0); b.writeUInt32LE(36 + samples.length * 2, 4); b.write('WAVE', 8); b.write('fmt ', 12);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(SR, 24); b.writeUInt32LE(SR * 2, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write('data', 36); b.writeUInt32LE(samples.length * 2, 40);
  for (let i = 0; i < samples.length; i++) b.writeInt16LE(Math.round(Math.max(-1, Math.min(1, samples[i])) * 32767), 44 + i * 2);
  return b;
}

const credits = [];
for (const [cue, list] of Object.entries(picks)) {
  const p = P[cue];
  for (let k = 0; k < list.length; k++) {
    const e = list[k];
    const { m, sampleRate } = await decode(`dl/${e.id}.mp3`);
    let s = resample(m, sampleRate, p.pitch);
    let out;
    if (p.loop) {
      // 안정된 가운데 구간 L + 크로스페이드 X → 끝이 처음으로 이어짐
      const L = Math.round(p.loop * SR), X = Math.round(0.25 * SR);
      let st = Math.round((p.skip || 0) * SR);
      if (st + L + X > s.length) st = Math.max(0, s.length - L - X);
      const Lr = Math.min(L, s.length - st - X);
      out = new Float32Array(Lr);
      for (let i = 0; i < Lr; i++) out[i] = s[st + i];
      for (let i = 0; i < X; i++) { const t = i / X; out[i] = s[st + i] * Math.sin(t * Math.PI / 2) + s[st + Lr + i] * Math.cos(t * Math.PI / 2); }
    } else {
      const on = onset(s);
      const len = Math.min(s.length - on, Math.round(p.max * SR));
      out = s.slice(on, on + len);
      const fo = Math.min(len, Math.round(Math.min(0.25, p.max * 0.3) * SR)); // 끝 페이드
      for (let i = 0; i < fo; i++) out[len - fo + i] *= Math.pow(1 - i / fo, 2);
      const fi = Math.min(len, Math.round(0.002 * SR));
      for (let i = 0; i < fi; i++) out[i] *= i / fi;
    }
    // 직류 성분 제거 + 피크 -1dB
    let mean = 0; for (const v of out) mean += v; mean /= out.length;
    let pk = 0; for (let i = 0; i < out.length; i++) { out[i] -= mean; pk = Math.max(pk, Math.abs(out[i])); }
    const g = pk > 0 ? 0.891 / pk : 1; for (let i = 0; i < out.length; i++) out[i] *= g;
    const name = `kyodai_${cue}_${k + 1}.wav`;
    fs.writeFileSync(`${OUT}/${name}`, wav(out));
    credits.push({ file: name, title: e.title, creator: e.creator, page: e.page, len: +(out.length / SR).toFixed(2) });
    console.log(name.padEnd(34), (out.length / SR).toFixed(2) + 's', e.title);
  }
}
fs.writeFileSync('credits.json', JSON.stringify(credits, null, 1));
