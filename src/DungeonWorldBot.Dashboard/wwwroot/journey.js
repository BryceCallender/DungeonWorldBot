// The party's journey played back on the campaign's map (JourneyPage.razor), over a session or the whole campaign. It
// opens on the whole map with the road drawn; playing it starts there, then flies in on each leg as the party's marker
// travels it, the trail drawing behind, and stops at each place to show what happened there, before pulling back out.
// Each session begins with its name over the whole map. Whoever runs the game pins places by clicking the map, which
// saves them as tokens on the map scene (CampaignMap.cs).
(() => {
    const root = document.querySelector('[data-journey]');
    const data = JSON.parse(document.querySelector('[data-journey-data]')?.textContent ?? '{}');
    if (!root || !data.stops?.length) return;

    const SVG = 'http://www.w3.org/2000/svg';
    const TRAIL = '#c2410c';
    const still = matchMedia('(prefers-reduced-motion: reduce)');

    const svg = root.querySelector('[data-map]');
    const stage = root.querySelector('.journey-stage');
    const caption = root.querySelector('[data-caption]');
    const title = root.querySelector('[data-title]');
    const speed = root.querySelector('[data-speed]');
    const status = root.querySelector('[data-status]');
    const playLabel = root.querySelector('[data-play-label]');
    const ask = root.querySelector('[data-ask]');
    const items = [...root.querySelectorAll('[data-stop]')];

    const frame = data.frame;
    const full = [frame.x, frame.y, frame.width, frame.height];
    // Sizes are worked out for a map 800 across, and grow with bigger ones, so markers look the same on any map
    const unit = frame.width / 800;
    const places = new Map(Object.entries(data.places));
    const spotOf = place => places.get(place.toLowerCase());

    const make = (tag, attrs = {}, parent) => {
        const node = document.createElementNS(SVG, tag);
        for (const [name, value] of Object.entries(attrs)) node.setAttribute(name, value);
        parent?.append(node);
        return node;
    };

    // ---- The map ----------------------------------------------------------------------------------------------------

    // Parchment with a faint grid, under the map's own pictures when it has some
    const defs = make('defs', {}, svg);
    const grid = make('pattern', { id: 'journey-grid', width: 100 * unit, height: 100 * unit, patternUnits: 'userSpaceOnUse' }, defs);
    make('path', { d: `M ${100 * unit} 0 L 0 0 0 ${100 * unit}`, fill: 'none', stroke: '#d9c49a', 'stroke-width': unit }, grid);
    const pad = 2000 * unit;
    make('rect', { x: frame.x - pad, y: frame.y - pad, width: frame.width + pad * 2, height: frame.height + pad * 2, fill: '#f3e6c8' }, svg);
    make('rect', { x: frame.x - pad, y: frame.y - pad, width: frame.width + pad * 2, height: frame.height + pad * 2, fill: 'url(#journey-grid)' }, svg);
    for (const p of data.pictures) make('image', { href: p.src, x: p.x, y: p.y, width: p.width, height: p.height, preserveAspectRatio: 'none' }, svg);

    const trail = make('path', {
        fill: 'none', stroke: TRAIL, 'stroke-width': 4 * unit, 'stroke-linecap': 'round', 'stroke-linejoin': 'round',
        'stroke-dasharray': `${unit} ${10 * unit}`,
    }, svg);
    const pins = make('g', {}, svg);
    const party = make('g', { visibility: 'hidden' }, svg);
    make('circle', { r: 15 * unit, fill: TRAIL, stroke: '#fff', 'stroke-width': 3 * unit }, party);
    make('text', { 'text-anchor': 'middle', dy: 6 * unit, 'font-size': 16 * unit }, party).textContent = '🧭';

    let view = full.slice();
    const setView = v => { view = v; svg.setAttribute('viewBox', v.map(n => n.toFixed(1)).join(' ')); };
    const moveParty = spot => { party.setAttribute('transform', `translate(${spot.x},${spot.y})`); party.setAttribute('visibility', 'visible'); };

    // The stops that are on the map, numbered as on the page
    const route = () => data.stops.map((stop, i) => ({ ...stop, i, spot: spotOf(stop.place) })).filter(s => s.spot);

    // One pin a place, showing the latest time the party was there, so a place they keep going back to isn't buried
    // under its own pins
    const pinned = new Map();
    const clearPins = () => { pins.replaceChildren(); pinned.clear(); };

    function pin(stop) {
        const key = stop.place.toLowerCase();
        if (pinned.has(key)) { pinned.get(key).textContent = stop.i + 1; return; }

        const g = make('g', {}, pins);
        const { x, y } = stop.spot;
        make('circle', { cx: x, cy: y, r: 12 * unit, fill: '#fff', stroke: TRAIL, 'stroke-width': 2.5 * unit }, g);
        const number = make('text', { x, y: y + 4.5 * unit, 'text-anchor': 'middle', 'font-size': 12 * unit, 'font-weight': 700, fill: '#7c2d12' }, g);
        number.textContent = stop.i + 1;
        pinned.set(key, number);
        const label = make('text', {
            x, y: y - 19 * unit, 'text-anchor': 'middle', 'font-size': 14 * unit, 'font-weight': 600, fill: '#4a2a0a',
            stroke: '#f3e6c8', 'stroke-width': 4 * unit, 'paint-order': 'stroke',
        }, g);
        label.textContent = stop.place;
    }

    // A gentle curve from one stop to the next, bowing one way then the other so the road wanders like a real one
    const bend = (a, b, n) => {
        const length = Math.hypot(b.x - a.x, b.y - a.y) || 1;
        const sway = (n % 2 ? -1 : 1) * Math.min(60 * unit, length * .25);
        return { x: (a.x + b.x) / 2 + (-(b.y - a.y) / length) * sway, y: (a.y + b.y) / 2 + ((b.x - a.x) / length) * sway };
    };
    const along = (a, c, b, t) => {
        const u = 1 - t;
        return { x: u * u * a.x + 2 * u * t * c.x + t * t * b.x, y: u * u * a.y + 2 * u * t * c.y + t * t * b.y };
    };

    // The whole road at once: every pin, the trail through them, and the party at the end
    function drawAll() {
        clearPins();
        const stops = route();
        let d = '';
        stops.forEach((stop, n) => {
            pin(stop);
            if (n === 0) { d = `M${stop.spot.x} ${stop.spot.y}`; return; }
            const c = bend(stops[n - 1].spot, stop.spot, n);
            d += ` Q${c.x} ${c.y} ${stop.spot.x} ${stop.spot.y}`;
        });
        trail.setAttribute('d', d);
        if (stops.length) moveParty(stops.at(-1).spot);
        else party.setAttribute('visibility', 'hidden');
    }

    // ---- Playing it back ----------------------------------------------------------------------------------------

    let run = 0;
    // Slow, normal or fast, as a share of the usual time everything takes
    const pace = () => Number(speed.value) || 1;
    const ease = t => t < .5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2;
    const tween = (ms, step, id) => new Promise(done => {
        if (still.matches) { step(1); done(); return; }
        const start = performance.now();
        const frameStep = now => {
            if (id !== run) return done();
            const t = Math.min(1, (now - start) / (ms * pace()));
            step(ease(t));
            if (t < 1) requestAnimationFrame(frameStep); else done();
        };
        requestAnimationFrame(frameStep);
    });
    const wait = ms => new Promise(done => setTimeout(done, ms * pace()));
    const lerp = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);

    // Framed on a leg with room around it, in the map's shape, never closer in than two fifths of the whole map
    function around(a, b) {
        const margin = 110 * unit;
        const aspect = frame.height / frame.width;
        let width = Math.max(Math.abs(b.x - a.x) + margin * 2, (Math.abs(b.y - a.y) + margin * 2) / aspect, frame.width * .4);
        width = Math.min(width, frame.width);
        const height = width * aspect;
        return [(a.x + b.x) / 2 - width / 2, (a.y + b.y) / 2 - height / 2, width, height];
    }

    // What happened at a stop: the same lines as its entry in the list beside the map
    function show(stop) {
        items.forEach(li => li.classList.toggle('now', li === items[stop.i]));
        const heading = document.createElement('h2');
        heading.textContent = `${stop.i + 1}. ${stop.place}`;
        const moments = items[stop.i].querySelector('.moments, .journey-quiet')?.cloneNode(true);
        caption.replaceChildren(heading, ...(moments ? [moments] : []));
        caption.hidden = false;
        keepInView(items[stop.i]);
    }

    // The stop in the middle of the list beside the map, when the list scrolls on its own. Stacked under the map, the
    // page would scroll instead, taking the map out of sight, so it's left alone.
    const list = root.querySelector('[data-stops]');
    function keepInView(item) {
        if (list.scrollHeight <= list.clientHeight) return;
        const top = item.offsetTop - (list.clientHeight - item.offsetHeight) / 2;
        list.scrollTo({ top, behavior: still.matches ? 'auto' : 'smooth' });
    }

    // A session's name over the whole map, as its part of the road begins
    async function announce(stop, id) {
        caption.hidden = true;
        const from = view.slice();
        await tween(1200, t => setView(lerp(from, full, t)), id);
        if (id !== run) return;
        title.textContent = status.textContent = data.sessions[stop.session];
        title.hidden = false;
        await wait(2000);
        // Unless it's been played again meanwhile, which shows its own
        if (id === run) title.hidden = true;
    }

    // Long enough to read what happened there
    const readingTime = stop => Math.min(5000, 1600 + 700 * items[stop.i].querySelectorAll('.moment').length);

    async function play() {
        const id = ++run;
        const stops = route();
        if (stops.length === 0) { status.textContent = 'Pin the places on the map to follow the road.'; return; }
        playLabel.textContent = 'Play again';
        clearPins();
        trail.setAttribute('d', '');
        caption.hidden = true;
        setView(full);
        moveParty(stops[0].spot);
        await announce(stops[0], id);
        if (id !== run) return;

        pin(stops[0]);
        show(stops[0]);
        let d = `M${stops[0].spot.x} ${stops[0].spot.y}`;
        await wait(readingTime(stops[0]));

        for (let n = 1; n < stops.length; n++) {
            if (id !== run) return;
            const a = stops[n - 1].spot, b = stops[n].spot, c = bend(a, b, n);
            if (stops[n].session !== stops[n - 1].session) await announce(stops[n], id);
            if (id !== run) return;
            caption.hidden = true;

            // A session that starts where the last one left off has no road to travel
            if (Math.hypot(b.x - a.x, b.y - a.y) >= 1) {
                status.textContent = `On the road to ${stops[n].place}…`;
                const from = view.slice(), to = around(a, b);
                await tween(1200, t => setView(lerp(from, to, t)), id);
                await tween(1800, t => {
                    moveParty(along(a, c, b, t));
                    // The trail so far: the legs before, and this one cut off at where the party's got to
                    const points = Array.from({ length: 21 }, (_, s) => along(a, c, b, (s / 20) * t));
                    trail.setAttribute('d', `${d} L${points.map(p => `${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' L')}`);
                }, id);
                if (id !== run) return;
            }

            d += ` Q${c.x} ${c.y} ${b.x} ${b.y}`;
            trail.setAttribute('d', d);
            pin(stops[n]);
            show(stops[n]);
            status.textContent = `${n + 1} of ${stops.length}`;
            await wait(readingTime(stops[n]));
        }

        if (id !== run) return;
        caption.hidden = true;
        items.forEach(li => li.classList.remove('now'));
        const from = view.slice();
        await tween(1400, t => setView(lerp(from, full, t)), id);
        status.textContent = 'The whole road, start to finish';
    }

    root.querySelector('[data-play]').addEventListener('click', play);

    // ---- Pinning places (whoever runs the game) -------------------------------------------------------------------

    let asking = null;

    function askFor(place) {
        asking = place;
        if (place) ask.querySelector('[data-ask-text]').textContent = `Where's ${place}? Click the map.`;
        ask.hidden = !place;
        stage.classList.toggle('asking', !!place);
        if (place) { run++; caption.hidden = true; title.hidden = true; setView(full); drawAll(); }
    }

    // The next stop that isn't on the map yet, or none
    const nextUnplaced = () => data.stops.find(stop => !spotOf(stop.place))?.place ?? null;

    // Where on the map a click was, in the map's own units
    function boardPoint(e) {
        const point = svg.createSVGPoint();
        point.x = e.clientX;
        point.y = e.clientY;
        return point.matrixTransform(svg.getScreenCTM().inverse());
    }

    async function save(place, spot) {
        const form = new FormData();
        form.append('name', place);
        form.append('x', spot.x.toFixed(1));
        form.append('y', spot.y.toFixed(1));
        form.append('__RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '');
        const response = await fetch('api/map/places', { method: 'POST', body: form });
        if (!response.ok) throw new Error((await response.text()) || `Couldn't save that (${response.status})`);
    }

    svg.addEventListener('click', async e => {
        if (!asking) return;
        const place = asking;
        const spot = boardPoint(e);
        try {
            await save(place, spot);
        } catch (error) {
            status.textContent = error.message;
            return;
        }
        places.set(place.toLowerCase(), { x: spot.x, y: spot.y });
        root.querySelectorAll(`[data-pin="${CSS.escape(place)}"]`).forEach(b => b.textContent = 'Move');
        drawAll();
        const next = nextUnplaced();
        askFor(next);
        status.textContent = next ? `Pinned ${place}.` : 'Every place is on the map. Press play.';
    });

    ask.querySelector('[data-ask-skip]').addEventListener('click', () => askFor(null));
    root.querySelectorAll('[data-pin]').forEach(button => button.addEventListener('click', () => askFor(button.dataset.pin)));

    setView(full);
    drawAll();
    if (data.canPlace) askFor(nextUnplaced());
})();
