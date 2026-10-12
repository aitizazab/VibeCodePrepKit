// One action per call against our automation Chrome (port 9333, see chrome.mjs), off-screen, one tab. Each call holds
// FableAnimationTests/.work/gemini.lock while it runs (the tab is shared with flow.mjs).
//
//   node drive.mjs launch                      start Chrome off-screen (no-op if running)
//   node drive.mjs goto <url>                  navigate the one tab
//   node drive.mjs shot <out.png> [fullPage]   screenshot the tab
//   node drive.mjs eval "<js expression>"      evaluate in the page, print JSON
//   node drive.mjs evalfile <file.js>          evaluate a file's contents (an expression) in the page
//   node drive.mjs click "<selector>"          Playwright click (selector syntax incl. text=...)
//   node drive.mjs clickxy <x> <y>             mouse click at viewport coordinates
//   node drive.mjs upload "<selector>" f1 f2   set files on an <input type=file>
//   node drive.mjs chooser <x> <y> f1 [f2..]   click at x,y and answer the file chooser with files
//   node drive.mjs type "<text>"               keyboard type
//   node drive.mjs key <Key>                   keyboard press
//   node drive.mjs text                        visible text of the page (trimmed)
//   node drive.mjs download <x> <y> <outdir>   click at x,y and save the download
//   node drive.mjs hovershot <x> <y> <out.png> move the mouse there and screenshot
//   node drive.mjs close                       shut Chrome down
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
// Adapted 2026-10-10 from BasilDownfall/art/comic-style/faces/route2/drive.mjs (unchanged there).
const chrome = await import('./chrome.mjs');

const [cmd, ...args] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function tab() {
  chrome.lock(cmd + ' ' + args.join(' ').slice(0, 80));
  const { browser, context, page } = await chrome.launch();
  const pages = context.pages();
  // keep ONE tab: close any extra ones (popups Tripo may open)
  for (const p of pages) if (p !== page) await p.close().catch(() => {});
  await page.setViewportSize({ width: 1400, height: 860 }).catch(() => {});
  return { browser, context, page };
}

async function main() {
  if (cmd === 'close') { console.log((await chrome.close()) ? 'closed' : 'still running'); return; }
  const { browser, context, page } = await tab();
  try {
    if (cmd === 'launch') console.log('url', page.url());
    else if (cmd === 'goto') { await page.goto(args[0], { waitUntil: 'domcontentloaded', timeout: 90000 }); await sleep(4000); console.log(page.url()); }
    else if (cmd === 'shot') { await page.screenshot({ path: args[0], fullPage: args[1] === 'full', timeout: 60000 }); console.log('saved', args[0]); }
    else if (cmd === 'eval') console.log(JSON.stringify(await page.evaluate(args[0]), null, 1));
    else if (cmd === 'evalfile') console.log(JSON.stringify(await page.evaluate(fs.readFileSync(args[0], 'utf8')), null, 1));
    else if (cmd === 'click') { await page.click(args[0], { timeout: 15000 }); await sleep(1500); console.log('clicked'); }
    else if (cmd === 'hovershot') { await page.mouse.move(Number(args[0]), Number(args[1])); await sleep(1500); await page.screenshot({ path: args[2] }); console.log('saved', args[2]); }
    else if (cmd === 'clickxy') { await page.mouse.click(Number(args[0]), Number(args[1])); await sleep(1500); console.log('clicked'); }
    else if (cmd === 'upload') { await page.setInputFiles(args[0], args.slice(1)); await sleep(2000); console.log('uploaded'); }
    else if (cmd === 'chooser') {
      const wait = page.waitForEvent('filechooser', { timeout: 15000 });
      await page.mouse.click(Number(args[0]), Number(args[1]));
      const fc = await wait; await fc.setFiles(args.slice(2)); await sleep(2500); console.log('chosen');
    }
    else if (cmd === 'type') { await page.keyboard.type(args[0], { delay: 30 }); console.log('typed'); }
    else if (cmd === 'key') { await page.keyboard.press(args[0]); await sleep(800); console.log('pressed'); }
    else if (cmd === 'text') console.log((await page.evaluate(() => document.body.innerText)).replace(/\n{2,}/g, '\n').slice(0, Number(args[0] || 6000)));
    else if (cmd === 'download') {
      const wait = page.waitForEvent('download', { timeout: 180000 });
      await page.mouse.click(Number(args[0]), Number(args[1]));
      const d = await wait; const out = path.join(args[2], d.suggestedFilename()); await d.saveAs(out); console.log('saved', out);
    }
    else if (cmd === 'downloadsel') {
      const wait = page.waitForEvent('download', { timeout: 180000 });
      await page.click(args[0], { timeout: 15000 });
      const d = await wait; const out = path.join(args[1], d.suggestedFilename()); await d.saveAs(out); console.log('saved', out);
    }
    else if (cmd === 'waitgone') {
      // waitgone "<text>" [maxSeconds]: return once the page's text no longer contains <text> (e.g. "Generating")
      const max = Number(args[1] || 600) * 1000; const t0 = Date.now();
      while (Date.now() - t0 < max && (await page.evaluate(() => document.body.innerText)).includes(args[0])) await sleep(10000);
      console.log(((await page.evaluate(() => document.body.innerText)).includes(args[0]) ? 'still there after ' : 'gone after ') + Math.round((Date.now() - t0) / 1000) + ' s');
    }
    else { console.error('unknown command', cmd); process.exitCode = 2; }
  } finally {
    if (cmd === 'launch' || cmd === 'goto') await chrome.parkOffscreen();
    await browser.close(); // detaches only
    chrome.unlock();
  }
}
main().then(() => process.exit(process.exitCode || 0), (e) => { console.error('ERR', e.message); process.exit(1); });
