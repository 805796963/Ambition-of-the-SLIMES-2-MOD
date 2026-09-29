// Generates the in-game style promo poster for the Mouse Control plugin.
//
// Everything is drawn from lines and functions: the stone wall and the isometric floor are grids whose
// per-tile shading comes from a hash of the tile coordinates, and the click ripple is a geometric series.
// Layers are built as separate arrays and concatenated in draw order, so the composition cannot silently
// end up in the wrong order.
//
// Run with: node promo-mouse-ingame.gen.mjs   (writes promo-mouse-ingame.svg and .html)
import { writeFileSync } from "node:fs";

const W = 1600, H = 900;
const hash = (a, b) => { const s = Math.sin(a * 12.9898 + b * 78.233) * 43758.5453; return s - Math.floor(s); };
const shade = (hex, f) => {
  const n = parseInt(hex.slice(1), 16);
  const c = (v) => Math.max(0, Math.min(255, Math.round(v * f)));
  return "#" + [c((n >> 16) & 255), c((n >> 8) & 255), c(n & 255)].map(v => v.toString(16).padStart(2, "0")).join("");
};

// ---------------------------------------------------------------- floor
const floor = [];
const TW = 150, TH = 76, OY = 408;
{
  // The transform (col,row) -> (x,y) rotates the grid, so the loop spans the inverse image of the canvas.
  const uMin = (2 * -200) / TW, uMax = (2 * (W + 200)) / TW;
  const vMin = (2 * (200 - OY)) / TH, vMax = (2 * (H + 160 - OY)) / TH;
  const colMin = Math.floor((uMin + vMin) / 2) - 1, colMax = Math.ceil((uMax + vMax) / 2) + 1;
  const rowMin = Math.floor((vMin - uMax) / 2) - 1, rowMax = Math.ceil((vMax - uMin) / 2) + 1;
  for (let row = rowMin; row <= rowMax; row++) {
    for (let col = colMin; col <= colMax; col++) {
      const cx = (col - row) * (TW / 2), cy = OY + (col + row) * (TH / 2);
      const pts = cx + "," + (cy - TH / 2) + " " + (cx + TW / 2) + "," + cy + " " + cx + "," + (cy + TH / 2) + " " + (cx - TW / 2) + "," + cy;
      floor.push('<polygon points="' + pts + '" fill="' + shade("#dbd1a6", 0.93 + hash(row * 3 + 1, col * 7 + 2) * 0.15) + '" stroke="#b7ad82" stroke-width="2"/>');
      if (hash(col * 5, row * 11) > 0.88) {
        floor.push('<rect x="' + (cx - 12) + '" y="' + (cy - 8) + '" width="9" height="14" fill="#7fa64a"/>');
        floor.push('<rect x="' + (cx - 1) + '" y="' + (cy - 15) + '" width="9" height="21" fill="#6c9440"/>');
        floor.push('<rect x="' + (cx + 10) + '" y="' + (cy - 5) + '" width="9" height="11" fill="#8cb355"/>');
      }
    }
  }
}

// ---------------------------------------------------------------- wall (over the floor)
const wall = [];
const WALL_ROWS = 4, WALL_Y = -18, WALL_H = 46, WALL_BLOCK = 90;
{
  for (let row = 0; row < WALL_ROWS; row++) {
    const y = WALL_Y + row * WALL_H;
    for (let col = -1; col < 19; col++) {
      const x = col * 94 + (row % 2 ? 47 : 0);
      wall.push('<rect x="' + x + '" y="' + y + '" width="' + WALL_BLOCK + '" height="42" fill="' + shade("#c3c98a", 0.86 + hash(row * 2, col * 3) * 0.24) + '" stroke="#6f7a52" stroke-width="3"/>');
      wall.push('<line x1="' + x + '" y1="' + (y + 3) + '" x2="' + (x + WALL_BLOCK) + '" y2="' + (y + 3) + '" stroke="#e6eaba" stroke-width="3" stroke-opacity="0.6"/>');
    }
  }
  const wallBottom = WALL_Y + WALL_ROWS * WALL_H;
  wall.push('<rect x="0" y="' + wallBottom + '" width="' + W + '" height="24" fill="#000000" fill-opacity="0.18"/>');
  wall.push('<rect x="0" y="' + (wallBottom + 24) + '" width="' + W + '" height="6" fill="#7f8a5e"/>');
}

// ---------------------------------------------------------------- trees
const trees = [];
{
  const tree = (x, y, s) => {
    trees.push('<rect x="' + (x - 8 * s) + '" y="' + (y - 12 * s) + '" width="' + 16 * s + '" height="' + 36 * s + '" fill="#5c4a2e"/>');
    for (let i = 0; i < 3; i++) {
      const w = (78 - i * 17) * s, h = 48 * s, top = y - (36 + i * 32) * s;
      trees.push('<polygon points="' + x + "," + top + " " + (x + w / 2) + "," + (top + h) + " " + (x - w / 2) + "," + (top + h) + '" fill="' + ["#2f5d33", "#3a6f3c", "#477f47"][i] + '"/>');
    }
  };
  tree(74, 856, 1.15); tree(190, 802, 0.7); tree(1532, 830, 1.0); tree(1428, 778, 0.58);
}

// ---------------------------------------------------------------- pointer
const pointer = [];
const CURSOR_X = 1246, CURSOR_Y = 654;
{
  for (let i = 5; i >= 0; i--) {
    const r = 30 * Math.pow(1.5, i);
    pointer.push('<circle cx="' + CURSOR_X + '" cy="' + CURSOR_Y + '" r="' + r.toFixed(1) + '" fill="none" stroke="#fff8dc" stroke-opacity="' + (0.7 * Math.exp(-i / 2.6)).toFixed(3) + '" stroke-width="5"/>');
    pointer.push('<circle cx="' + CURSOR_X + '" cy="' + CURSOR_Y + '" r="' + r.toFixed(1) + '" fill="none" stroke="#8b6b3a" stroke-opacity="' + (0.28 * Math.exp(-i / 2.6)).toFixed(3) + '" stroke-width="2"/>');
  }
  pointer.push('<polygon points="1246,614 1326,654 1246,694 1166,654" fill="none" stroke="#ffe45c" stroke-width="7"/>');
  pointer.push('<polygon points="1246,614 1326,654 1246,694 1166,654" fill="none" stroke="#7a6420" stroke-width="2"/>');
  const arrow = [[0, 0], [-52, -138], [-21, -111], [9, -180], [47, -162], [16, -95], [57, -88], [0, 0]];
  pointer.push('<path d="' + arrow.map(([dx, dy], i) => (i === 0 ? "M" : "L") + (CURSOR_X + dx) + " " + (CURSOR_Y + dy)).join(" ") + ' Z" fill="#fbfbf5" stroke="#3a3327" stroke-width="5" stroke-linejoin="round"/>');
}

// ---------------------------------------------------------------- panel and copy
const panel = [];
{
  const px = 78, py = 424, pw = 816, ph = 400;
  panel.push('<rect x="' + px + '" y="' + py + '" width="' + pw + '" height="' + ph + '" rx="18" fill="#f6ecca" stroke="#6b5a3a" stroke-width="7"/>');
  panel.push('<rect x="' + (px + 12) + '" y="' + (py + 12) + '" width="' + (pw - 24) + '" height="' + (ph - 24) + '" rx="11" fill="none" stroke="#cdbd90" stroke-width="3"/>');

  const font = '"Microsoft YaHei", "Meiryo", "Segoe UI", sans-serif';
  const bullets = [
    ["左键点击 = 点选并确认", "click to confirm"],
    ["右键 = 取消", "right click to cancel"],
    ["按住拖动 = 光标扫过格子", "drag to sweep the cursor"],
    ["滚轮 = 上下选择", "wheel to navigate"],
  ];
  bullets.forEach(([cn, en], i) => {
    const y = py + 96 + i * 68;
    panel.push('<rect x="' + (px + 46) + '" y="' + (y - 25) + '" width="24" height="24" fill="#7fa64a" stroke="#4e6b2e" stroke-width="3"/>');
    panel.push('<text x="' + (px + 92) + '" y="' + y + '" fill="#4a3d28" font-family=\'' + font + '\' font-size="33" font-weight="700">' + cn + "</text>");
    panel.push('<text x="' + (px + 98) + '" y="' + (y + 30) + '" fill="#8f7f5c" font-family=\'' + font + '\' font-size="23">' + en + "</text>");
  });
  panel.push('<text x="' + (px + 46) + '" y="' + (py + ph - 36) + '" fill="#8f7f5c" font-family=\'' + font + '\' font-size="22">BepInEx 6 (IL2CPP) ・ 只注入游戏自己的按键事件 / no game state written</text>');
}

// ---------------------------------------------------------------- logo style title and footer
const copy = [];
{
  const font = '"Microsoft YaHei", "Meiryo", "Segoe UI", sans-serif';
  const logo = (x, y, size, text) => '<text x="' + x + '" y="' + y + '" font-family=\'' + font + '\' font-size="' + size + '" font-weight="800" fill="#ffffff" stroke="#3b3b3b" stroke-width="' + (size * 0.14).toFixed(1) + '" paint-order="stroke" stroke-linejoin="round">' + text + "</text>";
  copy.push(logo(66, 122, 82, "鼠标操作"));
  copy.push(logo(68, 190, 46, "Mouse Control"));
  copy.push('<text x="' + (W - 56) + '" y="112" fill="#ffffff" stroke="#3b3b3b" stroke-width="5" paint-order="stroke" font-family=\'' + font + '\' font-size="34" font-weight="700" text-anchor="end">史莱姆的大野望</text>');
  copy.push('<text x="' + (W - 56) + '" y="154" fill="#f2f2ea" stroke="#3b3b3b" stroke-width="4" paint-order="stroke" font-family=\'' + font + '\' font-size="26" text-anchor="end">Ambition of the SLIMES 2 ・ マウス操作</text>');
  copy.push('<text x="' + (W - 40) + '" y="' + (H - 22) + '" fill="#5f5136" font-family=\'' + font + '\' font-size="24" text-anchor="end">1790 lines of per-window hit tests ・ your-link-here</text>');
}

// Draw order: floor, then the wall over it, then everything else.
const background = '<rect width="' + W + '" height="' + H + '" fill="#cdbd90"/>';
const layers = [background, ...floor, ...wall, ...trees, ...pointer, ...panel, ...copy];
const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="' + W + '" height="' + H + '" viewBox="0 0 ' + W + " " + H + '" shape-rendering="crispEdges">\n' + layers.join("\n") + "\n</svg>\n";

if (layers.indexOf(...wall) < layers.indexOf(...floor)) throw new Error("the wall must be drawn over the floor");
writeFileSync(new URL("./promo-mouse-ingame.svg", import.meta.url), svg);
writeFileSync(new URL("./promo-mouse-ingame.html", import.meta.url),
  '<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;padding:0;background:#000;overflow:hidden}svg{display:block}</style></head><body>' + svg + "</body></html>");
console.log("floor:", floor.length, "wall:", wall.length, "total layers:", layers.length);
