// pixelfx 이펙트 → Unity용 스프라이트 시트 (빈 여백·사라진 꼬리 프레임을 잘라 텍스처를 줄인다)
// node pixelfx-export.mjs <pixelfx 폴더> <출력 폴더> <이름> <id> [opts JSON] [꼬리 기준 비율] [기준점 "x,y" | -] [도트 배율]
//   기준점: 원본 프레임 픽셀(위에서부터). 비우거나 - 면 이펙트 원점(바닥 효과는 발밑) — 차지 구슬처럼 공중에 있는 효과는 그 중심을 준다
//   도트 배율: 2면 2×2칸을 도트 하나로 합쳐 뭉툭하게(화면 크기는 그대로 — JSON pixelScale을 보고 가져오기가 PPU를 나눈다)
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import { mkdirSync, writeFileSync } from "node:fs";

const [root, outDir, name, id, optsJson, cutArg, pivotArg, pixelArg] = process.argv.slice(2);
const CUT = cutArg ? +cutArg : 0.005; // 가장 진한 프레임 대비 이 비율보다 옅어지면 꼬리로 보고 자른다
const K = pixelArg ? Math.max(1, Math.round(+pixelArg)) : 1;
const lib = await import(pathToFileURL(join(root, "tools/lib/fxlib.mjs")).href);
const fx = await lib.loadEffect(id);
const r = lib.renderFrames(fx, { bg: "none", options: optsJson ? JSON.parse(optsJson) : undefined });

// 도트 합치기: K×K칸 → 도트 하나. 알파는 가장 진한 칸(가는 불티가 사라지지 않게), 색은 알파 가중 평균
const w = Math.ceil(r.w / K), h = Math.ceil(r.h / K);
const pics = r.frames.map((f) => {
  if (K === 1) return f.clear;
  const out = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    let rs = 0, gs = 0, bs = 0, as = 0, am = 0;
    for (let dy = 0; dy < K; dy++) for (let dx = 0; dx < K; dx++) {
      const sx = x * K + dx, sy = y * K + dy;
      if (sx >= r.w || sy >= r.h) continue;
      const i = (sy * r.w + sx) * 4, a = f.clear[i + 3];
      rs += f.clear[i] * a; gs += f.clear[i + 1] * a; bs += f.clear[i + 2] * a; as += a;
      if (a > am) am = a;
    }
    if (as === 0) continue;
    const o = (y * w + x) * 4;
    out[o] = rs / as; out[o + 1] = gs / as; out[o + 2] = bs / as; out[o + 3] = am;
  }
  return out;
});

// 프레임마다 보이는 픽셀 범위와 알파 합
const ALPHA_MIN = 10; // 이보다 옅은 픽셀은 안 보이는 걸로 친다
const info = pics.map((pic) => {
  let x0 = w, y0 = h, x1 = -1, y1 = -1, sum = 0;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const a = pic[(y * w + x) * 4 + 3];
    if (a < ALPHA_MIN) continue;
    sum += a;
    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
  }
  return { x0, y0, x1, y1, sum };
});

// 꼬리 자르기: 가장 진한 프레임의 CUT(기본 0.5%)보다 옅어진 뒤의 프레임은 버린다 (마지막 한 장은 빈 프레임으로 남겨 자연스럽게 끝나게)
const peak = Math.max(...info.map((i) => i.sum));
let last = info.length - 1;
while (!fx.meta.loop && last > 0 && info[last].sum < peak * CUT) last--; // 반복 효과는 자르면 이음새가 끊긴다
const n = Math.min(info.length, last + 2);

// 모든 프레임을 덮는 상자 (+1px 여백)
let X0 = w, Y0 = h, X1 = 0, Y1 = 0;
for (let i = 0; i < n; i++) {
  const b = info[i];
  if (b.x1 < 0) continue;
  X0 = Math.min(X0, b.x0); Y0 = Math.min(Y0, b.y0); X1 = Math.max(X1, b.x1); Y1 = Math.max(Y1, b.y1);
}
X0 = Math.max(0, X0 - 1); Y0 = Math.max(0, Y0 - 1); X1 = Math.min(w - 1, X1 + 1); Y1 = Math.min(h - 1, Y1 + 1);
const cw = X1 - X0 + 1, ch = Y1 - Y0 + 1;

const cols = Math.min(n, Math.max(1, Math.ceil(Math.sqrt((n * ch) / cw)))), rows = Math.ceil(n / cols);
const img = lib.canvas(cols * cw, rows * ch, [0, 0, 0, 0]);
const frames = [];
for (let i = 0; i < n; i++) {
  const src = pics[i], crop = new Uint8ClampedArray(cw * ch * 4);
  for (let y = 0; y < ch; y++) crop.set(src.subarray(((y + Y0) * w + X0) * 4, ((y + Y0) * w + X0 + cw) * 4), y * cw * 4);
  const x = (i % cols) * cw, y = Math.floor(i / cols) * ch;
  img.paste(crop, cw, ch, x, y);
  frames.push({ filename: `${name} ${i}`, frame: { x, y, w: cw, h: ch }, duration: Math.round(1000 / r.fps) });
}

const [ox0, oy0] = pivotArg && pivotArg !== "-" ? pivotArg.split(",").map(Number) : fx.meta.origin;
const ox = ox0 / K, oy = oy0 / K;
const json = {
  frames,
  meta: {
    app: "https://github.com/patch-gg/pixelfx", image: `${name}.png`, size: { w: cols * cw, h: rows * ch },
    // pivot: 잘린 프레임 기준 픽셀 (위에서부터) — 이펙트의 기준점(바닥 효과는 발밑). pixelScale: 도트 하나가 원본 몇 칸인지
    pixelfx: { id: fx.id, name: fx.meta.name, fps: r.fps, loop: fx.meta.loop, frames: n, frameSize: [cw, ch], pivot: [ox - X0, oy - Y0], columns: cols, pixelScale: K, options: optsJson ? JSON.parse(optsJson) : {} },
  },
};
mkdirSync(outDir, { recursive: true });
writeFileSync(join(outDir, `${name}.png`), img.png());
writeFileSync(join(outDir, `${name}.json`), JSON.stringify(json, null, 1));
console.log(`${name}: ${id}  ${r.frames.length}→${n}프레임  ${r.w}x${r.h}→${cw}x${ch}(도트 ×${K})  시트 ${cols * cw}x${rows * ch}  기준점 ${(ox - X0).toFixed(1)},${(oy - Y0).toFixed(1)}`);

// ── 쿄다이에 쓴 것 (2026-11-05, pixelfx 폴더에서 실행, O = <프로젝트>/Assets/NYH/03. Image/Effect/pixelfx) ──
// 붉은 팔레트 F = PAL.fire ["#1f0b10","#4a1118","#8c1d1c","#cc3a1a","#f2701f","#ffae35","#ffe27a","#fff8d6"]
// node <이 파일> . O Fx_주먹충격   impacts/ground-slam       '{"reach":72}'       0.03 - 2
// node <이 파일> . O Fx_바닥폭발   explosions/small-explosion '{"size":0.55}'     0.04 - 2
// node <이 파일> . O Fx_손피격     impacts/blunt-hit          '{}'                 0.01 64,49 2   (기준점 = 덩어리 중심 — 손 한가운데에 겹치게)
// node <이 파일> . O Fx_레이저차지 arcane/spell-charge        '{"palette":F}'      0 57,52 2      (반복 — 꼬리 안 자름, 기준점 = 구슬 중심)
// node <이 파일> . O Fx_레이저발사 arcane/mana-burst          '{"palette":F}'      0.02 102,91 2
// node <이 파일> . O Fx_레이저불티 impacts/hit-spark          '{"height":0}'       0.01 - 2
// node <이 파일> . O Fx_레이저끝   impacts/beam-splash        '{}'                 0 - 2          (반복 — pixelfx 폴더에 새로 만든 효과, 흰 심 레이저 끝)
// 그다음 Unity: PNG 선택 → 메뉴 NYH/이펙트/pixelfx 시트 가져오기 (또는 NYH/쿄다이/2D 쿄다이 만들기·갱신이 자동으로)
