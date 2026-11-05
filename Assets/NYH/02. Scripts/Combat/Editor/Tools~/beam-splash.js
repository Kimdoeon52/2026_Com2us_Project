// The end-of-beam flare (spraying rays, shock rings, a fountain of sparks) follows the technique of Sprite Fusion's
// open-source energy beam (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
import { fit, cycle, fly, flyV, loopNoise, fract, ease, sparkle, band, TAU, WHITE } from "../../core/index.js";

const D = 1.2;

export const meta = {
  name: "Beam splash",
  desc: "Where a hot laser hits the floor: a white-hot pool throbbing flat, red-white rays spitting up, a ring sliding out, molten sparks fountaining and heat haze rising.",
  tags: ["impact", "laser", "beam", "splash", "sparks", "molten", "scifi", "loop"],
  size: [112, 80],
  origin: [56, 64],
  duration: D,
  loop: true,
  fps: 30,
  bg: "ground",
  options: { palette: ["#2a0610", "#6a0f18", "#b81e22", "#ff4a3a", "#ff9a8a", "#ffd8d0", "#ffffff"], seed: 3 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7), S = o.seed, ph = t / D;

  // light first: a wide red pool on the floor, a tight white heart
  p.glow(0, -2, 34, C[3], 0.45);
  p.glow(0, -3, 14, C[5], 0.55);
  p.glow(0, -2, 6, WHITE, 0.5);

  // flat shock rings sliding out along the floor
  // one solid red ring at a time, quick out then gone (a dotted ring reads as noise)
  for (let k = 0; k < 2; k++) {
    const q = fract(ph * 3 + k / 2);
    if (q > 0.55) continue;
    const u = q / 0.55, r = 5 + 20 * ease.outCubic(u);
    band(p, 0, 0, r, 0.2, 1, 1.6 * (1 - u), [C[1], C[2], C[3], C[4]], (g) => Math.sin(g) > -0.2); // near half only
  }

  // the fan of rays spitting up and out: fat white roots, red tips, each flickering on its own beat
  for (let i = 0; i < 7; i++) {
    const a = -Math.PI / 2 + (i - 3) * 0.42 + loopNoise(ph, 3, S * 5 + i) * 0.12;
    const l = 4 + 20 * Math.max(0, loopNoise(ph, 7, S * 13 + i));
    if (l < 5) continue;
    const x1 = Math.cos(a) * l, y1 = -2 + Math.sin(a) * l;
    p.beam(0, -2, Math.cos(a) * l * 0.45, -2 + Math.sin(a) * l * 0.45, 1.2, C[5]);
    p.line(0, -2, x1, y1, (u) => (u < 0.4 ? WHITE : u < 0.75 ? C[5] : C[3]), (u) => (u < 0.6 ? 1 : 1.6 - u));
    p.glowLine(0, -2, x1, y1, 3, C[4], 0.25);
  }

  // the pool: a throbbing white-hot blob pressed flat on the floor with a red rim
  const pr = 6 + Math.sin(ph * TAU * 8) * 1 + loopNoise(ph, 5, S) * 1.2;
  p.ellipse(0, -1, pr + 2, (pr + 2) * 0.5, C[3]);
  p.ellipse(0, -1, pr + 1, (pr + 1) * 0.5, C[4]);
  p.ellipse(0, -1, pr, pr * 0.5, C[5]);
  p.ellipse(0, -1.5, pr * 0.6, pr * 0.32, WHITE);

  // molten sparks thrown up and out, arcing down onto the floor
  cycle(t, D, 70, S * 7 + 2, (g) => g.range(0.25, 0.6), (age, life, r) => {
    const a = -Math.PI / 2 + r.range(-1.25, 1.25), sp = r.range(70, 210), vx = Math.cos(a) * sp, vy = Math.sin(a) * sp;
    const [x, y] = fly(0, -2, vx, vy, age, 520, 1.6), [ux, uy] = flyV(vx, vy, age, 520, 1.6), k = 1 - age / life;
    if (y > -0.5) return;
    const head = k > 0.6 ? WHITE : k > 0.3 ? C[5] : C[4], s = Math.hypot(ux, uy) || 1, len = Math.min(6, 1 + s * 0.02);
    p.line(x - (ux / s) * len, y - (uy / s) * len, x, y, (u) => (u > 0.5 ? head : C[3]));
    p.glow(x, y, 3, head, 0.4 * k);
  });

  // hot drops that land and glow out on the floor
  cycle(t, D, 10, S * 7 + 3, 0.5, (age, life, r) => {
    const x = r.range(-22, 22), k = 1 - age / life;
    p.px(x, 0, k > 0.5 ? C[5] : C[4]);
    if (k > 0.6) p.px(x + 1, 0, C[4]);
    p.glow(x, 0, 3, C[4], 0.3 * k);
  });

  // glints
  cycle(t, D, 2, S * 7 + 4, 0.25, (age, life, r) => {
    const k = Math.sin((age / life) * Math.PI);
    sparkle(p, r.range(-10, 10), r.range(-16, -4), 1 + 2.5 * k, WHITE, C[5], 0.5 * k);
  });

  // heat haze lifting off
  cycle(t, D, 6, S * 7 + 5, (g) => g.range(0.8, 1.2), (age, life, r) => {
    const q = age / life, x = r.range(-8, 8), y = -4 - q * r.range(14, 22), rad = r.range(3, 5) * (0.7 + 0.8 * q);
    p.disc(x, y, rad, C[3], (dd) => 0.22 * Math.sin(q * Math.PI) * (1 - dd * dd));
  });
}
