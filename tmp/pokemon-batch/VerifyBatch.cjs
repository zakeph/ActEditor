// Verify the delivered batch independently of the converter's in-memory state.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const root = __dirname;
const input = 'C:/Users/ShivaN/Desktop/pokes';
const output = path.join(root, 'output');
const expected = fs.readdirSync(input).filter(n => n.endsWith('-back.gif'))
  .map(n => n.slice(0, -9)).filter(n => n !== 'dragonite').sort();
const actual = fs.readdirSync(output, { withFileTypes: true })
  .filter(n => n.isDirectory()).map(n => n.name).sort();
function requireThat(ok, message) { if (!ok) throw Error(message); }
function readJson(file) { return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '')); }
function sha(file) { return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase(); }
requireThat(expected.length === 42, `Expected 42 additional source pairs; found ${expected.length}`);
requireThat(JSON.stringify(expected) === JSON.stringify(actual), 'Source/output species differ');
let sourceFrames = 0, protectedPixels = 0, checked = 0, renders = 0;
for (const name of expected) {
  const folder = path.join(output, name);
  const cfg = readJson(path.join(folder, 'pupilas-color.json'));
  requireThat(cfg.name === name && cfg.reviewed, `${name}: unreviewed config`);
  const palettes = [];
  for (const side of ['front', 'back']) {
    const meta = readJson(path.join(root, 'probe', `${name}-${side}.json`));
    const bytes = fs.readFileSync(path.join(root, 'probe', `${name}-${side}.bgra`));
    const masks = cfg[side];
    requireThat(meta.width === masks.width && meta.height === masks.height && meta.count === masks.frames.length, `${name}-${side}: mask dimensions`);
    requireThat(bytes.length === meta.width * meta.height * 4 * meta.count, `${name}-${side}: probe length`);
    palettes.push(...meta.palette);
    sourceFrames += meta.count;
    masks.frames.forEach((mask, f) => {
      requireThat(new Set(mask).size === mask.length, `${name}-${side}:${f}: duplicate mask`);
      for (const i of mask) {
        requireThat(Number.isInteger(i) && i >= 0 && i < meta.width * meta.height, `${name}-${side}:${f}: mask bounds`);
        const p = (f * meta.width * meta.height + i) * 4;
        requireThat(bytes[p + 3] === 255 && bytes[p] <= 16 && bytes[p] === bytes[p + 1] && bytes[p] === bytes[p + 2], `${name}-${side}:${f}: non-ink mask`);
      }
      protectedPixels += mask.length;
    });
  }
  requireThat(palettes.some(p => p.slice(0, 3).every((v, i) => v === cfg.color[i])), `${name}: outline color absent from source palette`);
  for (const variant of ['', 'original']) {
    for (const ext of ['act', 'spr']) requireThat(fs.statSync(path.join(folder, variant, `${name}.${ext}`)).size > 0, `${name}: empty ${ext}`);
    const report = fs.readFileSync(path.join(folder, variant, 'verification.txt'), 'utf8');
    requireThat(report.includes('PASS: opaque colors, alpha, timing, mirrors, alignment, slots and native rendering.'), `${name}: failed native report`);
  }
  const report = fs.readFileSync(path.join(folder, 'verification.txt'), 'utf8');
  requireThat(/PASS: \d+ frames compared between saved original and adjusted ACT\/SPR/.test(report), `${name}: missing saved comparison`);
  checked += Number(report.match(/Round-trip frames checked: (\d+)/)[1]);
  renders += Number(report.match(/Native ACT renderer distinct poses per direction checked: (\d+)/)[1]);
  const hashLines = fs.readFileSync(path.join(folder, 'fuentes-sha256.csv'), 'utf8').replace(/^\uFEFF/, '').trim().split(/\r?\n/).slice(1);
  requireThat(hashLines.length === 4, `${name}: source hash list`);
  hashLines.forEach(line => {
    const match = line.match(/^"([^"]+)","([A-F0-9]+)"$/);
    requireThat(match && sha(match[1]) === match[2], `${name}: source hash changed`);
  });
  const htmlPath = path.join(folder, 'comparacion.html');
  let html = fs.readFileSync(htmlPath, 'utf8');
  html = html.replaceAll('Borde marrón', 'Borde ajustado').replaceAll('Negro exterior', 'Negro salvo pupilas');
  fs.writeFileSync(htmlPath, html);
  requireThat(!html.includes('Dragonite') && !html.includes('dragonite.') && !html.includes('preview.html'), `${name}: stale pilot links`);
  const scripts = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
  requireThat(scripts.length === 1, `${name}: comparison script count`);
  const jsPath = path.join(root, 'html-check', `${name}.js`);
  fs.mkdirSync(path.dirname(jsPath), { recursive: true });
  fs.writeFileSync(jsPath, scripts[0][1]);
  const syntax = spawnSync(process.execPath, ['--check', jsPath], { encoding: 'utf8' });
  requireThat(syntax.status === 0, `${name}: comparison JS syntax ${syntax.stderr}`);
  const match = scripts[0][1].match(/^const variants=([\s\S]*?);let running=/);
  requireThat(match, `${name}: missing saved animation data`);
  const variants = JSON.parse(match[1]);
  requireThat(variants.length === 2, `${name}: comparison variants`);
  for (const variant of variants) {
    requireThat(variant.images.length > 0 && variant.images.every(i => i.startsWith('data:image/png;base64,')), `${name}: embedded sprites`);
    requireThat(variant.directions.length === 8, `${name}: directions`);
    variant.directions.forEach((direction, d) => {
      const side = [0, 1, 6, 7].includes(d) ? 'front' : 'back';
      const meta = readJson(path.join(root, 'probe', `${name}-${side}.json`));
      requireThat(direction.ms > 0 && direction.frames.length * direction.ms === meta.delays.reduce((a, b) => a + b, 0), `${name}:${d}: animation duration`);
      requireThat(direction.frames.every(f => Number.isInteger(f[0]) && f[0] >= 0 && f[0] < variant.images.length && typeof f[1] === 'boolean'), `${name}:${d}: sprite reference`);
    });
  }
}
const summary = { species: expected.length, sourceFrames, protectedPixelOccurrences: protectedPixels, checkedStepsPerVersion: checked, nativePosesPerVersion: renders, sourceHashesIntact: true, comparisonScriptsValid: true };
fs.writeFileSync(path.join(output, 'verificacion-lote.json'), JSON.stringify(summary, null, 2));
console.log('PASS ' + JSON.stringify(summary));
