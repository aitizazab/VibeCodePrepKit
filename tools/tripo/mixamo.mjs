// Mixamo animations without a skin, through Mixamo's own web API in our signed-in automation Chrome (port 9333; see
// chrome.mjs). Each clip is exported on Mixamo's X Bot (Humanoid retargeting plays it on any character) as FBX 2019,
// 30 fps, no skin, and saved as <outDir>/<state>.fbx (the state name VhPeopleBuild gives it).
//   node mixamo.mjs <list.json> <outDir> [--only state,state]
// list.json: [{"state":"idle","name":"Breathing Idle","pick":0,"inplace":false}, ...]
//   name: the Mixamo product name (exact, case-insensitive); desc: its description (exact), to tell same-named clips
//   apart (Mixamo has seven called "Talking"); pick: which of what's left (0 = the first).
// Skips states whose file already exists. Holds FableAnimationTests/.work/gemini.lock while it runs.
import fs from 'node:fs';
import path from 'node:path';
const chrome = await import('./chrome.mjs');

const [listFile, outDir] = process.argv.slice(2);
const onlyArg = process.argv.indexOf('--only');
const only = onlyArg > 0 ? new Set(process.argv[onlyArg + 1].split(',')) : null;
const list = JSON.parse(fs.readFileSync(listFile, 'utf8')).filter((e) => !only || only.has(e.state));
fs.mkdirSync(outDir, { recursive: true });

chrome.lock('mixamo ' + path.basename(listFile));
const { browser, page } = await chrome.launch();
try {
  if (!page.url().includes('mixamo.com')) { await page.goto('https://www.mixamo.com/#/', { waitUntil: 'domcontentloaded', timeout: 90000 }); await new Promise((r) => setTimeout(r, 4000)); }
  for (const e of list) {
    const out = path.join(outDir, e.state + '.fbx');
    if (fs.existsSync(out)) { console.log('have', e.state); continue; }
    const res = await page.evaluate(async (e) => {
      const H = { Authorization: 'Bearer ' + localStorage.getItem('access_token'), 'X-Api-Key': 'mixamo2', 'X-Requested-With': 'XMLHttpRequest', 'Content-Type': 'application/json' };
      const get = async (u) => (await fetch(u, { headers: H })).json();
      const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
      const prim = await get('https://www.mixamo.com/api/v1/characters/primary');
      const charId = prim.primary_character_id;
      const found = { results: [] };
      for (let pg = 1; pg <= 3; pg++) {
        const b = await get('https://www.mixamo.com/api/v1/products?page=' + pg + '&limit=96&order=&type=Motion&query=' + encodeURIComponent(e.name));
        found.results.push(...b.results);
        if (b.results.length < 96) break;
      }
      let same = found.results.filter((x) => x.name.toLowerCase() === e.name.toLowerCase());
      if (e.desc) same = same.filter((x) => (x.description || '').toLowerCase() === e.desc.toLowerCase());
      const p = same[e.pick || 0];
      if (!p) return { err: 'not found: ' + e.name + ' (' + same.length + ' same-named)' };
      const prod = await get('https://www.mixamo.com/api/v1/products/' + p.id + '?similar=0&character_id=' + charId);
      const g = prod.details.gms_hash;
      const params = (g.params || []).map((x) => x[1]).join(',');
      const body = {
        character_id: charId,
        gms_hash: [{ 'model-id': g['model-id'], mirror: false, trim: g.trim || [0, 100], overdrive: 0, params, 'arm-space': g['arm-space'] || 0, inplace: !!e.inplace }],
        preferences: { format: 'fbx7_2019', skin: 'false', fps: '30', reducekf: '0' },
        product_name: p.name, type: 'Motion',
      };
      const ex = await fetch('https://www.mixamo.com/api/v1/animations/export', { method: 'POST', headers: H, body: JSON.stringify(body) });
      if (!ex.ok) return { err: 'export ' + ex.status + ' ' + (await ex.text()).slice(0, 200) };
      for (let i = 0; i < 60; i++) {
        await sleep(2000);
        const m = await get('https://www.mixamo.com/api/v1/characters/' + charId + '/monitor');
        if (m.status === 'completed') return { url: m.job_result, id: p.id, desc: p.description };
        if (m.status === 'failed') return { err: 'failed ' + JSON.stringify(m).slice(0, 200) };
      }
      return { err: 'timed out' };
    }, e);
    if (res.err) { console.log('FAIL', e.state, res.err); continue; }
    const r = await fetch(res.url);
    fs.writeFileSync(out, Buffer.from(await r.arrayBuffer()));
    console.log('saved', e.state, '<-', e.name, '[' + (res.desc || '') + ']', fs.statSync(out).size);
  }
} finally {
  await browser.close();
  chrome.unlock();
}
process.exit(0);
