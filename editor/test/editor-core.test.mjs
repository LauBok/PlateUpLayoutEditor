// Run: node editor/test/editor-core.test.mjs
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const require = createRequire(import.meta.url);
const here = dirname(fileURLToPath(import.meta.url));
const C = require('../editor-core.js');

const R = (id, x, y, rotation = 0) => ({ id, applianceId: 1, type: 'T', x, y, rotation });
let passed = 0;
const test = (name, fn) => { fn(); passed++; console.log('ok -', name); };

test('rotates clockwise like the C# core', () => {
  const placed = C.place([R('a', 0, 0, 90), R('b', 1, 0, 90)], 10, 20, 1);
  assert.deepEqual(placed.map((a) => [a.id, a.x, a.y, a.rotation]), [['a', 10, 21, 180], ['b', 10, 20, 180]]);
});

test('four turns are identity', () => {
  let items = [R('a', 0, 0), R('b', 2, 1, 270), R('c', 1, 3, 90)];
  const original = JSON.stringify(items);
  for (let i = 0; i < 4; i++) items = C.place(items, 0, 0, 1);
  assert.equal(JSON.stringify(items), original);
});

test('footprint keeps empty margins', () => {
  const placed = C.place([R('a', 2, 0)], 0, 0, 2, C.rect(0, 0, 2, 0));
  assert.deepEqual([placed[0].x, placed[0].y], [0, 0]);
});

test('extracts modules relative to the region', () => {
  const m = C.extractModule([R('a', 3, 4), R('b', 5, 6), R('c', 9, 9)], C.rect(3, 4, 5, 6), 'm');
  assert.equal(m.kind, 'module');
  assert.deepEqual(m.appliances.map((a) => [a.id, a.x, a.y]), [['a', 0, 0], ['b', 2, 2]]);
  assert.deepEqual(m.bounds, { minX: 0, minY: 0, maxX: 2, maxY: 2 });
});

test('accepts v1 prototype files and drops unknown state', () => {
  const doc = C.normalizeDocument({ version: 1, appliances: [{ id: '1', type: 'Counter', x: 1, y: 2, rotation: 270, state: { foo: 1 } }] });
  assert.equal(doc.version, 2);
  assert.equal(doc.appliances[0].state, undefined);
});

test('rejects overlaps but allows different layers', () => {
  assert.throws(() => C.normalizeDocument({ version: 2, appliances: [R('a', 0, 0), R('b', 0, 0)] }), /overlaps/);
  const ok = C.normalizeDocument({ version: 2, appliances: [R('a', 0, 0), { ...R('b', 0, 0), layer: 'Floor' }] });
  assert.equal(ok.appliances.length, 2);
});

test('sample layout is valid', () => {
  const sample = JSON.parse(readFileSync(join(here, '../../schema/examples/coffee-line.json'), 'utf8'));
  const doc = C.normalizeDocument(sample);
  assert.ok(doc.appliances.length > 0);
  assert.ok(doc.appliances.every((a) => a.applianceId !== 0));
});

test('catalog has the smart grabber with its game ID', () => {
  globalThis.PLATEUP_APPLIANCES = undefined;
  require('../catalog.js');
  const smart = globalThis.PLATEUP_APPLIANCES.find((a) => a.code === 'GrabberSmart');
  assert.equal(smart.id, -1238047163);
});

console.log(passed + ' tests passed');
