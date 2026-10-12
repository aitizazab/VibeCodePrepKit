"""The plan of Night 1's hill for the creator's review page (docs/LARCH-HILL.md): a top-down SVG, north (up the hill)
at the top, every box shaded by its height, the four routes and the way in drawn over it, the story's places labelled.

  dotnet test core-tests/DeepBlock/DeepBlock.Tests.csproj --filter "Name=ExportThePlan"   (writes larch-plan.json)
  uv run python tools/art/night1-hill-plan.py <larch-plan.json> <out.svg>
"""
import json
import sys

S = 5.0                      # pixels a metre
X0, X1, Z0, Z1 = -86.0, 64.0, -50.0, 182.0
PAD, LEGEND = 30, 300
W = int((X1 - X0) * S) + PAD * 2 + LEGEND
H = int((Z1 - Z0) * S) + PAD * 2


def px(x):
    return PAD + (x - X0) * S


def pz(z):
    return PAD + (Z1 - z) * S


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def hexc(c):
    return '#%02x%02x%02x' % c


LOW, HIGH = (176, 226, 160), (120, 96, 168)
ROUTES = {
    'The way in': ('#ffffff', '6,5'),
    'The road': ('#f08a3c', ''),
    'The garden stairs': ('#2e9e5a', ''),
    'The rooftops': ('#d8435a', ''),
    'The creek ravine': ('#2f7fd0', ''),
}


def fill(p):
    k = p['kind']
    if k == 'Water':
        return '#8fd3ea', 0.9
    t = max(0.0, min(1.0, (p['top'] + 2.0) / 44.0))
    base = lerp(LOW, HIGH, t)
    if k in ('House', 'Shop'):
        base = lerp(base, (240, 150, 140), 0.45)
    elif k in ('Roof',):
        base = lerp(base, (230, 120, 110), 0.5)
    elif k in ('BrickWall',):
        base = lerp(base, (200, 120, 120), 0.35)
    elif k == 'Lane':
        base = lerp(base, (190, 186, 214), 0.6)
    elif k == 'Hedge':
        base = (110, 170, 120)
    elif k in ('Fence', 'Picket'):
        base = (250, 248, 240)
    return hexc(base), 1.0


def main():
    plan = json.load(open(sys.argv[1], encoding='utf-8'))
    out = []
    out.append(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Segoe UI, Arial, sans-serif">')
    out.append(f'<rect width="{W}" height="{H}" fill="#fde3cc"/>')
    pieces = [p for p in plan['pieces'] if p['kind'] not in ('Boundary', 'Gate') and p['solid'] or p['kind'] == 'Water']
    pieces.sort(key=lambda p: (p['top'], p['kind'] in ('Fence', 'Picket', 'Hedge')))
    for p in pieces:
        c, a = fill(p)
        x, y = px(p['x0']), pz(p['z1'])
        w, h = (p['x1'] - p['x0']) * S, (p['z1'] - p['z0']) * S
        if w < 0.5 or h < 0.5:
            continue
        out.append(f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" fill="{c}" fill-opacity="{a}" stroke="#5a4a78" stroke-opacity="0.35" stroke-width="0.6"><title>{p["name"]} (top {p["top"]:.1f} m)</title></rect>')
    # Walt's gate
    for p in plan['pieces']:
        if p['kind'] == 'Gate':
            out.append(f'<rect x="{px(p["x0"]):.1f}" y="{pz(p["z1"]):.1f}" width="{(p["x1"]-p["x0"])*S:.1f}" height="{max(3,(p["z1"]-p["z0"])*S):.1f}" fill="#ffd060" stroke="#7a4a00"/>')
    # routes
    for r in plan['routes']:
        col, dash = ROUTES.get(r['name'], ('#000', ''))
        pts = ' '.join(f'{px(x):.1f},{pz(z):.1f}' for x, z in r['points'])
        out.append(f'<polyline points="{pts}" fill="none" stroke="#3a2a50" stroke-width="7" stroke-opacity="0.35" stroke-linejoin="round"/>')
        out.append(f'<polyline points="{pts}" fill="none" stroke="{col}" stroke-width="4" stroke-linejoin="round" stroke-linecap="round"' + (f' stroke-dasharray="{dash}"' if dash else '') + '/>')
        x, z = r['points'][-1]
        out.append(f'<circle cx="{px(x):.1f}" cy="{pz(z):.1f}" r="6" fill="{col}" stroke="#3a2a50" stroke-width="1.5"/>')
    # places
    spots = {s['id']: s for s in plan['spots']}

    def label(x, z, text, dx=8, dy=4, size=15, weight='600'):
        tx, ty = px(x) + dx, pz(z) + dy
        w = len(text) * size * 0.56 + 10
        out.append(f'<rect x="{tx-5:.1f}" y="{ty-size:.1f}" width="{w:.1f}" height="{size+7}" rx="4" fill="#fffaf2" fill-opacity="0.88"/>')
        out.append(f'<text x="{tx:.1f}" y="{ty:.1f}" font-size="{size}" font-weight="{weight}" fill="#2e2240">{text}</text>')

    def dot(x, z, col='#2e2240'):
        out.append(f'<circle cx="{px(x):.1f}" cy="{pz(z):.1f}" r="4.5" fill="{col}" stroke="#fff" stroke-width="1.5"/>')

    ax, az = plan['arrival']
    dot(ax, az, '#ffffff')
    label(ax, az, 'Nadia arrives (the whole climb ahead)', 10, -8)
    for sid, text, dx, dy in [
        ('walt', "Walt, in his gate", 12, -10),
        ('paper', "Saturday's paper (yard bench)", 10, 4),
        ('sprinkler', "The neighbours' loop (on the far bank)", 8, 22),
        ('chatter', 'The neighbour at his grill', 8, 4),
        ('ledger', "Walt's ledger", -100, 4),
        ('garden_gate', 'The garden gate: the ending', 10, 6),
        ('barbecue', 'Elias at the barbecue', 8, 4),
    ]:
        s = spots.get(sid)
        if s:
            dot(s['x'], s['z'])
            label(s['x'], s['z'], text, dx, dy)
    hx, hz = plan['house']
    label(hx - 8, hz + 9, 'THE HOUSE + TOWER + beacon', -60, 0, 17, '800')
    label(-53, 9, "Walt's bike shop", -40, 0, 14)
    label(-12, 104.5, 'the lookout (the vista)', -190, 4, 13)
    for z, t in [(28, 'the foot  0 m'), (51, 'terrace 1  12 m'), (85, 'terrace 2  24 m'), (150, 'the top  36 m (house on a 40 m knoll)')]:
        label(X0 + 1, z, t, 2, 0, 13, '700')
    label(-30, -4, 'the creek (8 m: the first double jump)', -30, 4, 13)
    # legend
    lx = PAD + (X1 - X0) * S + 24
    out.append(f'<text x="{lx}" y="{PAD+10}" font-size="22" font-weight="800" fill="#2e2240">Night 1: Larch Street on a hill</text>')
    out.append(f'<text x="{lx}" y="{PAD+34}" font-size="13" fill="#4a3a60">North (up the hill) at the top. 1 m = {S:g} px.</text>')
    y = PAD + 70
    times = {'The way in': 'arrival to Walt, 41 m, 4 s', 'The road': 'two switchbacks, 308 m, 32 s', 'The garden stairs': 'flights and back lanes, 91 m, 10 s',
             'The rooftops': 'nine double jumps up, 81 m, 12 s', 'The creek ravine': 'culvert slide, 28 falls, 254 m, 30 s'}
    for name, (col, dash) in ROUTES.items():
        out.append(f'<line x1="{lx}" y1="{y}" x2="{lx+40}" y2="{y}" stroke="{col}" stroke-width="5"' + (f' stroke-dasharray="{dash}"' if dash else '') + '/>')
        out.append(f'<line x1="{lx}" y1="{y}" x2="{lx+40}" y2="{y}" stroke="#3a2a50" stroke-width="1" stroke-opacity="0.4"/>')
        out.append(f'<text x="{lx+50}" y="{y+5}" font-size="15" font-weight="700" fill="#2e2240">{name}</text>')
        out.append(f'<text x="{lx+50}" y="{y+22}" font-size="12" fill="#4a3a60">{times[name]}</text>')
        y += 44
    y += 6
    for i in range(6):
        t = i / 5
        out.append(f'<rect x="{lx + i*38}" y="{y}" width="38" height="16" fill="{hexc(lerp(LOW, HIGH, t))}"/>')
    out.append(f'<text x="{lx}" y="{y+34}" font-size="12" fill="#4a3a60">ground height: low (the creek) to high (the top)</text>')
    out.append(f'<rect x="{lx}" y="{y+48}" width="16" height="10" fill="#ffd060" stroke="#7a4a00"/><text x="{lx+24}" y="{y+58}" font-size="12" fill="#4a3a60">Walt\'s gate (shut until his argument)</text>')
    out.append('</svg>')
    open(sys.argv[2], 'w', encoding='utf-8').write('\n'.join(out))
    print('wrote', sys.argv[2])


if __name__ == '__main__':
    main()
