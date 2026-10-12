// The Tripo / Mixamo automation Chrome: OUR automation Chrome on port 9333 (profile ~/.playwright-chrome-profile),
// the same one FableAnimationTests/art-tools/flow.mjs uses for Gemini. Off-screen, never focused, one tab.
//
// Changed 2026-10-12 (People agent): this used to start a second Chrome on another profile (port 9472). The standing
// rule is one automation Chrome on 9333, so everything goes through FableAnimationTests/art-tools/chrome.ps1
// (start / hide / close; it refuses a port held by a Chrome that isn't ours) and own-chrome.mjs (attach to the one tab).
//
// The tab is shared with flow.mjs, so each drive.mjs / steps.mjs call holds FableAnimationTests/.work/gemini.lock for
// its duration (the same lock flow.mjs takes); a call refuses to run while another program holds it.
//
// Library use:  import { launch, connect, close, parkOffscreen, lock, unlock } from './chrome.mjs'
import fs from 'node:fs';
import { spawnSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';
import path from 'node:path';

const FAT = 'C:\\Users\\User\\Desktop\\yougotgamesonyophone\\FableAnimationTests';
const PS1 = path.join(FAT, 'art-tools', 'chrome.ps1');
const LOCK = path.join(FAT, '.work', 'gemini.lock');
// playwright-core is installed in FableAnimationTests/node_modules, not in VibeJamKit.
const { chromium } = await import(pathToFileURL(path.join(FAT, 'node_modules', 'playwright-core', 'index.mjs')).href);
const { attach } = await import(pathToFileURL(path.join(FAT, 'art-tools', 'own-chrome.mjs')).href);
export const PORT = 9333;

function ps1(action) {
  const r = spawnSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', PS1, action], { encoding: 'utf8' });
  return { ok: r.status === 0, out: ((r.stdout || '') + (r.stderr || '')).trim() };
}

let held = false;
export function lock(what) {
  if (held) return;
  fs.mkdirSync(path.dirname(LOCK), { recursive: true });
  try { fs.mkdirSync(LOCK); } catch {
    let owner = ''; try { owner = fs.readFileSync(path.join(LOCK, 'owner'), 'utf8').trim(); } catch { /* none */ }
    throw new Error(`.work/gemini.lock is held (${owner || 'no owner file'}): another run is using the Chrome tab`);
  }
  fs.writeFileSync(path.join(LOCK, 'owner'), `tools/tripo · pid ${process.pid} · ${what || ''} · ${new Date().toISOString()}\n`);
  held = true;
  process.on('exit', unlock);
}
export function unlock() {
  if (!held) return;
  fs.rmSync(LOCK, { recursive: true, force: true });
  held = false;
}

export async function launch() {
  if (!ps1('check').ok) {
    const s = ps1('start');
    if (!s.ok) throw new Error('chrome.ps1 start failed: ' + s.out);
  }
  return connect();
}

export async function connect() {
  const { browser, page } = await attach(chromium);
  return { browser, context: browser.contexts()[0], page };
}

// chrome.ps1 hide moves the window left of every monitor without activating it.
export async function parkOffscreen() { ps1('hide'); }

export async function close() {
  const r = ps1('close');
  return r.ok && /closed|not running/.test(r.out);
}
