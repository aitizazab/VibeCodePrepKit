(async () => { const out = []; for (const i of [...document.querySelectorAll('img')].filter(i => i.src.includes('/multiview_'))) {
  const b = await (await fetch(i.src)).blob(); const r = new FileReader();
  out.push(await new Promise(res => { r.onload = () => res({ n: i.src.match(/multiview_\d/)[0], d: r.result }); r.readAsDataURL(b); })); } return out; })()
