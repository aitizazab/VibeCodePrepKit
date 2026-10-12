// Starts one Tripo multi-view generation from a folder of views (front.png, side.png = the RIGHT side, back.png),
// with the Geometry & Texture settings Tripo remembers from last time (set once by hand: Ultra mesh, 4K texture,
// Remove Lighting, PBR, Quad, 30000 polygons; Privacy Private). Spends 60 credits. Returns once Tripo shows
// "Generating"; the model then appears at the top of the Assets panel a few minutes later.
//   node gen.mjs sheets/<name> [--dry]
import path from 'node:path';
const chrome = await import('./chrome.mjs');
const dir = process.argv[2];
const dry = process.argv.includes('--dry');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
chrome.lock('tripo gen ' + dir);
const { browser, page } = await chrome.launch();
const inputs = () => page.evaluate(() => [...document.querySelectorAll('input[type=file]')].map((i) => (i.closest('[class]')?.parentElement?.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 12)));
try {
  { await page.goto('https://studio.tripo3d.ai/workspace/generate', { waitUntil: 'domcontentloaded', timeout: 90000 }); await sleep(5000); }
  await page.setViewportSize({ width: 1400, height: 860 });
  await page.mouse.click(166, 188); await sleep(1500);            // the multi-view input tab
  for (const [slot, file] of [['Front', 'front.png'], ['Right', 'side.png'], ['Back', 'back.png']]) {
    const list = await inputs();
    const k = list.findIndex((t) => t.startsWith(slot));
    if (k < 0) throw new Error(`no ${slot} slot (inputs: ${JSON.stringify(list)})`);
    await page.setInputFiles(`input[type=file] >> nth=${k}`, path.resolve(dir, file));
    await sleep(2500);
  }
  const text = await page.evaluate(() => document.body.innerText);
  const priv = (text.match(/Privacy\s+(\w[\w ]*)/) || [])[1];
  const cost = (text.match(/Generate\s+(\d+)\s*$/m) || [])[1];
  console.log('privacy', priv, '· cost', cost, '· slots left', JSON.stringify(await inputs()));
  if (priv !== 'Private') throw new Error('privacy is not Private');
  if (!dry) {
    let t = '';
    for (let tries = 0; tries < 3 && !t.includes('Generating'); tries++) {
      await page.mouse.move(190, 816); await sleep(500);
      await page.mouse.click(190, 816);
      await sleep(6000);
      t = await page.evaluate(() => document.body.innerText);
    }
    console.log(t.includes('Generating') ? 'generating' : 'NOT generating: ' + t.slice(0, 300));
  }
} finally {
  await browser.close();
  chrome.unlock();
}
