// Run a list of viewer steps in ONE connection to the Tripo Chrome (so the viewport override holds throughout).
//   node steps.mjs steps.json
// steps: [{"vp":[w,h,dsf]}, {"drag":[x0,y0,dx,dy], "button":"left"}, {"wheel":[x,y,dy]}, {"click":[x,y]},
//         {"shot":"out.png", "clip":[x,y,w,h]}, {"wait":ms}, {"key":"r"}, {"eval":"js"}]
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const HERE = path.dirname(fileURLToPath(import.meta.url));
// Adapted 2026-10-10 from BasilDownfall/art/comic-style/faces/route2/steps.mjs (unchanged there).
const chrome = await import('./chrome.mjs');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const steps = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
chrome.lock('steps ' + process.argv[2]);
const { browser, page } = await chrome.launch();
let cdp = null;
try {
  for (const s of steps) {
    if (s.vp) {
      cdp = cdp || await page.context().newCDPSession(page);
      await cdp.send('Emulation.setDeviceMetricsOverride', { width: s.vp[0], height: s.vp[1], deviceScaleFactor: s.vp[2] || 1, mobile: false });
      await sleep(1500);
    } else if (s.drag) {
      const [x, y, dx, dy] = s.drag; const b = s.button || 'left';
      if (s.shift) await page.keyboard.down('Shift');
      await page.mouse.move(x, y); await page.mouse.down({ button: b });
      const n = 20; for (let i = 1; i <= n; i++) { await page.mouse.move(x + dx * i / n, y + dy * i / n); await sleep(15); }
      await page.mouse.up({ button: b });
      if (s.shift) await page.keyboard.up('Shift');
      await sleep(600);
    } else if (s.wheel) {
      const [x, y, dy] = s.wheel; await page.mouse.move(x, y);
      const n = Math.max(1, Math.round(Math.abs(dy) / 100)); for (let i = 0; i < n; i++) { await page.mouse.wheel(0, Math.sign(dy) * 100); await sleep(60); }
      await sleep(700);
    } else if (s.click) { await page.mouse.click(s.click[0], s.click[1]); await sleep(s.after || 1200); }
    else if (s.key) { await page.keyboard.press(s.key); await sleep(800); }
    else if (s.wait) await sleep(s.wait);
    else if (s.zoomfit) {
      // wheel in small steps until the model's top (or bottom) edge reaches the target fraction of the clip
      const z = s.zoomfit; const tmp = path.resolve(HERE, 'tmp', '_zf.png');
      const clip = { x: z.clip[0], y: z.clip[1], width: z.clip[2], height: z.clip[3] };
      let edge = null;
      for (let i = 0; i < (z.max || 25); i++) {
        await page.screenshot({ path: tmp, clip, timeout: 60000 });
        const r = execFileSync('uv', ['run', '--with', 'pillow', '--with', 'numpy', 'python', path.join(HERE, 'maskedge.py'), tmp, path.resolve(HERE, z.plate), ...(z.band ? z.band.map(String) : [])], { encoding: 'utf8' }).trim().split(/\s+/).map(Number);
        edge = z.key === 'bottom' ? r[1] : z.key === 'cover' ? r[4] : r[0];
        const done = z.key === 'bottom' || z.key === 'cover' ? edge >= z.target : (edge >= 0 && edge <= z.target);
        if (done) break;
        await page.mouse.move(z.at ? z.at[0] : 740, z.at ? z.at[1] : 424);
        await page.mouse.wheel(0, z.step || -60); await sleep(700);
      }
      console.log('zoomfit', z.key, edge);
    }
    else if (s.eval) console.log(JSON.stringify(await page.evaluate(s.eval)));
    else if (s.shot) {
      const o = { path: path.resolve(HERE, s.shot), timeout: 60000 };
      if (s.clip) o.clip = { x: s.clip[0], y: s.clip[1], width: s.clip[2], height: s.clip[3] };
      await page.screenshot(o); console.log('saved', s.shot);
    }
  }
} finally {
  if (cdp && !process.env.KEEP_VP) await cdp.send('Emulation.clearDeviceMetricsOverride').catch(() => {});
  await browser.close();
  chrome.unlock();
}
process.exit(0);
