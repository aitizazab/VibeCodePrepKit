// The Tripo automation Chrome: launched off-screen, never focused, attached over CDP.
// Adapted (2026-10-10) from FableAnimationTests/BasilDownfall/tools/chrome.mjs, which stays unchanged.
//
// It reuses BasilDownfall's logged-in Tripo profile (.chrome-tripo) on port 9472. Before attaching or closing,
// portOwner() checks the process listening on the port runs THAT profile, so another agent's Chrome (9471, 9333,
// 9222, ...) is never driven or shut down. If a Chrome is already running on the profile, refuse rather than share.
//
// Library use:  import { launch, connect, close, profilePids } from './chrome.mjs'
import fs from 'node:fs';
import { spawn, execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const FAT = 'C:\\Users\\User\\Desktop\\yougotgamesonyophone\\FableAnimationTests';
// playwright-core is installed in FableAnimationTests/node_modules, not in VibeJamKit.
const { chromium } = await import(pathToFileURL(path.join(FAT, 'node_modules', 'playwright-core', 'index.mjs')).href);
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

export const PROFILE = path.join(FAT, 'BasilDownfall', '.chrome-tripo');
export const PORT = 9472;
const MATCH = PROFILE.toLowerCase().replace(/'/g, "''");

const OFFSCREEN = { left: -3000, top: -3000, width: 1440, height: 900 };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const ps = (cmd) => execFileSync('powershell', ['-NoProfile', '-Command', cmd], { encoding: 'utf8' });

// 'ours' | 'foreign' | 'free': who is listening on PORT.
export function portOwner() {
  return ps(`$c = Get-NetTCPConnection -State Listen -LocalPort ${PORT} -ErrorAction SilentlyContinue | Select-Object -First 1; ` +
    `if (-not $c) { 'free' } else { $p = Get-CimInstance Win32_Process -Filter ("ProcessId=" + $c.OwningProcess); ` +
    `if ($p.CommandLine -and $p.CommandLine.ToLower().Contains('${MATCH}')) { 'ours' } else { 'foreign' } }`).trim();
}

async function version() {
  const owner = portOwner();
  if (owner === 'free') return null;
  if (owner === 'foreign') throw new Error(`port ${PORT} belongs to another program's Chrome; refusing to touch it`);
  try {
    const res = await fetch(`http://127.0.0.1:${PORT}/json/version`);
    return res.ok ? await res.json() : null;
  } catch { return null; }
}

// Chrome processes (browser + children) that use the Tripo profile.
export function profilePids() {
  const out = ps(`Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" | ` +
    `Where-Object { $_.CommandLine -and $_.CommandLine.ToLower().Contains('${MATCH}') } | ` +
    `ForEach-Object { $_.ProcessId }`);
  return out.split(/\s+/).filter(Boolean).map(Number);
}

// Marker so a later call can tell "the Chrome I started" from "someone else's Chrome on the same profile".
const MINE = path.join(HERE, '.chrome-started-here');

export async function launch() {
  if (await version()) {
    if (!fs.existsSync(MINE)) throw new Error('a Chrome is already running on the Tripo profile (another session?); not interfering');
    return connect();
  }
  if (profilePids().length) {
    throw new Error('Chrome is already running on the Tripo profile without the debug port (another session or the sign-in window); not interfering');
  }
  const child = spawn(CHROME, [
    `--user-data-dir=${PROFILE}`,
    `--remote-debugging-port=${PORT}`,
    '--no-startup-window',
    '--no-first-run', '--no-default-browser-check',
    '--disable-backgrounding-occluded-windows',
    '--disable-renderer-backgrounding',
    '--disable-background-timer-throttling',
    '--disable-features=CalculateNativeWinOcclusion',
  ], { detached: true, stdio: 'ignore' });
  child.unref();
  fs.writeFileSync(MINE, String(child.pid));
  for (let i = 0; i < 60 && !(await version()); i++) await sleep(250);
  if (!(await version())) throw new Error(`Chrome did not open its debug port ${PORT}`);
  return connect();
}

export async function connect() {
  if (portOwner() !== 'ours') throw new Error(`no Chrome with the Tripo profile is listening on port ${PORT}`);
  const browser = await chromium.connectOverCDP(`http://127.0.0.1:${PORT}`);
  const context = browser.contexts()[0];
  let page = context.pages()[0];
  if (!page) {
    const cdp = await browser.newBrowserCDPSession();
    const waitPage = context.waitForEvent('page');
    await cdp.send('Target.createTarget', { url: 'about:blank', newWindow: true, background: true, ...OFFSCREEN });
    page = await waitPage;
  }
  await parkOffscreen(browser, page);
  return { browser, context, page };
}

export async function parkOffscreen(browser, page) {
  const cdp = await browser.newBrowserCDPSession();
  const { targetInfos } = await cdp.send('Target.getTargets');
  const mine = targetInfos.find((t) => t.type === 'page' && t.url === page.url()) || targetInfos.find((t) => t.type === 'page');
  if (!mine) return;
  const { windowId, bounds } = await cdp.send('Browser.getWindowForTarget', { targetId: mine.targetId });
  if (bounds.windowState !== 'normal') await cdp.send('Browser.setWindowBounds', { windowId, bounds: { windowState: 'normal' } });
  if (bounds.left !== OFFSCREEN.left || bounds.top !== OFFSCREEN.top) {
    await cdp.send('Browser.setWindowBounds', { windowId, bounds: OFFSCREEN });
  }
}

// Closes only a Chrome this folder started.
export async function close() {
  if (!fs.existsSync(MINE)) return profilePids().length === 0;
  if (await version()) {
    try {
      const browser = await chromium.connectOverCDP(`http://127.0.0.1:${PORT}`);
      const cdp = await browser.newBrowserCDPSession();
      await cdp.send('Browser.close');
    } catch { /* the connection drops as Chrome exits */ }
    for (let i = 0; i < 40 && profilePids().length; i++) await sleep(250);
  }
  const done = profilePids().length === 0;
  if (done) fs.rmSync(MINE, { force: true });
  return done;
}
