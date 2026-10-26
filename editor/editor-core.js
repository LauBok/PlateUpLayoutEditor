// Layout logic shared by the browser editor and its Node tests.
// Mirrors src/LayoutCore (C#): same file format, same rotation convention.
//   x = world X, y = world Z (north/up is +y); rotation = facing, clockwise from +y.
(function (root) {
  'use strict';

  const FORMAT = 'plateup-layout';
  const VERSION = 2;

  const norm = (deg) => ((deg % 360) + 360) % 360;

  /** Clockwise rotation about the origin in a y-up grid: (x, y) -> (y, -x). */
  function rotatePoint(x, y, quarterTurns) {
    const turns = ((quarterTurns % 4) + 4) % 4;
    for (let i = 0; i < turns; i++) { const t = x; x = y; y = -t; }
    return [x, y];
  }

  const cloneRecord = (a) => JSON.parse(JSON.stringify(a));

  /**
   * Rotates records clockwise by quarterTurns around their footprint (or bounding box) and moves the
   * footprint's minimum corner to (originX, originY). Same as LayoutTransform.Place in C#.
   */
  function place(records, originX, originY, quarterTurns, footprint) {
    const items = records.map(cloneRecord);
    if (!items.length) return items;
    const turns = ((quarterTurns % 4) + 4) % 4;
    for (const a of items) {
      [a.x, a.y] = rotatePoint(a.x, a.y, turns);
      a.rotation = norm(a.rotation + 90 * turns);
    }
    let minX = Math.min(...items.map((a) => a.x));
    let minY = Math.min(...items.map((a) => a.y));
    if (footprint) {
      const c1 = rotatePoint(footprint.minX, footprint.minY, turns);
      const c2 = rotatePoint(footprint.maxX, footprint.maxY, turns);
      minX = Math.min(c1[0], c2[0]);
      minY = Math.min(c1[1], c2[1]);
    }
    for (const a of items) { a.x = a.x - minX + originX; a.y = a.y - minY + originY; }
    return items;
  }

  function rect(x1, y1, x2, y2) {
    return { minX: Math.min(x1, x2), minY: Math.min(y1, y2), maxX: Math.max(x1, x2), maxY: Math.max(y1, y2) };
  }
  const rectContains = (r, x, y) => x >= r.minX && x <= r.maxX && y >= r.minY && y <= r.maxY;
  const rectSize = (r) => [r.maxX - r.minX + 1, r.maxY - r.minY + 1];

  /** Footprint size after rotation. */
  function rotatedSize(r, quarterTurns) {
    const [w, h] = rectSize(r);
    return ((quarterTurns % 2) + 2) % 2 === 1 ? [h, w] : [w, h];
  }

  function boundsOf(records) {
    if (!records.length) return null;
    return rect(Math.min(...records.map((a) => a.x)), Math.min(...records.map((a) => a.y)),
      Math.max(...records.map((a) => a.x)), Math.max(...records.map((a) => a.y)));
  }

  /** Copies the records inside a rectangle into a module whose minimum corner is (0, 0). */
  function extractModule(records, region, name) {
    const picked = records.filter((a) => rectContains(region, a.x, a.y)).map(cloneRecord);
    for (const a of picked) { a.x -= region.minX; a.y -= region.minY; }
    const [w, h] = rectSize(region);
    return { format: FORMAT, version: VERSION, kind: 'module', name: name || 'Module', bounds: rect(0, 0, w - 1, h - 1), appliances: picked };
  }

  const layerOf = (a) => a.layer || 'Default';
  const cellKey = (x, y, layer) => x + ',' + y + ',' + (layer || 'Default');

  /** Validates and normalises a parsed document (v1 prototype or v2). Throws Error with a readable message. */
  function normalizeDocument(doc) {
    if (!doc || typeof doc !== 'object' || Array.isArray(doc)) throw new Error('Not a layout file');
    if (doc.version !== 1 && doc.version !== VERSION) throw new Error('Unsupported layout version ' + doc.version);
    if (!Array.isArray(doc.appliances)) throw new Error('Missing appliances list');
    const out = {
      format: FORMAT, version: VERSION, kind: doc.kind === 'module' ? 'module' : 'restaurant',
      name: typeof doc.name === 'string' ? doc.name : '', appliances: [],
    };
    if (doc.bounds) out.bounds = rect(doc.bounds.minX, doc.bounds.minY, doc.bounds.maxX, doc.bounds.maxY);
    if (Array.isArray(doc.tiles)) out.tiles = doc.tiles.map((t) => ({ x: t.x | 0, y: t.y | 0, room: t.room | 0, type: t.type, placeable: !!t.placeable }));
    if (Array.isArray(doc.walls)) out.walls = doc.walls.map((w) => ({ x: w.x | 0, y: w.y | 0, dir: w.dir === 'N' ? 'N' : 'E' }));
    const ids = new Set();
    const cells = new Set();
    doc.appliances.forEach((a, i) => {
      const label = 'appliance #' + (i + 1);
      if (!a || typeof a !== 'object') throw new Error(label + ' is invalid');
      if (!Number.isInteger(a.x) || !Number.isInteger(a.y)) throw new Error(label + ': x and y must be integers');
      const rotation = norm(Number(a.rotation) || 0);
      if (rotation % 90 !== 0) throw new Error(label + ': rotation must be a multiple of 90');
      const applianceId = Number.isInteger(a.applianceId) ? a.applianceId : 0;
      const type = typeof a.type === 'string' ? a.type : '';
      if (!applianceId && !type) throw new Error(label + ': needs applianceId or type');
      let id = typeof a.id === 'string' && a.id ? a.id : 'a' + (i + 1);
      if (ids.has(id)) throw new Error(label + ": duplicate id '" + id + "'");
      ids.add(id);
      const rec = { id, applianceId, type, x: a.x, y: a.y, rotation };
      if (a.layer && a.layer !== 'Default') rec.layer = String(a.layer);
      const state = cleanState(a.state);
      if (state) rec.state = state;
      const key = cellKey(rec.x, rec.y, rec.layer);
      if (cells.has(key)) throw new Error(label + ': overlaps another appliance at ' + rec.x + ',' + rec.y);
      cells.add(key);
      out.appliances.push(rec);
    });
    return out;
  }

  /** Keeps only the state fields the mod understands (v1 free-form state is dropped). */
  function cleanState(state) {
    if (!state || typeof state !== 'object') return null;
    const s = {};
    const push = state.pushDirection || state.grabDirection;   // "grabDirection" = early name of this field
    if (['Up', 'Left', 'Right'].includes(push)) s.pushDirection = push;
    if (typeof state.grabSpecificType === 'boolean') s.grabSpecificType = state.grabSpecificType;
    if (Number.isInteger(state.specificType) && state.specificType !== 0) s.specificType = state.specificType;
    if (Array.isArray(state.specificComponents)) s.specificComponents = state.specificComponents.filter(Number.isInteger);
    return Object.keys(s).length ? s : null;
  }

  function freshId(taken) {
    let n = 1;
    while (taken.has('a' + n)) n++;
    taken.add('a' + n);
    return 'a' + n;
  }

  const api = { FORMAT, VERSION, norm, rotatePoint, place, rect, rectContains, rectSize, rotatedSize, boundsOf, extractModule, normalizeDocument, cleanState, layerOf, cellKey, freshId };
  root.LayoutCore = api;
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
