// A fight played back from its timeline, on its page (FightPage.razor): fighters down each side, and each step lights up
// whoever acted, draws an arrow to who they aimed at, and lands any damage on them. Everyone starts at full health and
// loses what each hit did, greying out when they fall.
(() => {
    const root = document.querySelector('[data-replay]');
    const data = JSON.parse(document.querySelector('[data-replay-data]')?.textContent ?? '{}');
    if (!root || !data.steps?.length) return;

    const still = matchMedia('(prefers-reduced-motion: reduce)');
    const el = (tag, className, text) => Object.assign(document.createElement(tag), className ? { className } : {}, text != null ? { textContent: text } : {});

    // Each fighter's card, by key, with their face as the page drew it (Face.razor: their picture, or their initial)
    const faces = root.querySelector('template[data-faces]')?.content;
    const cards = new Map();
    for (const f of data.fighters) {
        const card = el('div', 'fighter');
        card.dataset.key = f.key;
        if (f.color) {
            card.classList.add('classed');
            card.style.setProperty('--class-light', f.color.light);
            card.style.setProperty('--class-dark', f.color.dark);
        }
        const face = el('span', 'fighter-face');
        const drawn = faces?.querySelector(`[data-face="${CSS.escape(f.key)}"]`);
        if (drawn) face.append(...drawn.cloneNode(true).childNodes);
        else face.textContent = f.name.charAt(0);
        const body = el('div', 'fighter-body');
        body.append(el('span', 'fighter-name', f.name));
        if (f.tracked) {
            const bar = el('div', 'fighter-hp');
            bar.append(el('div'));
            body.append(bar);
        }
        card.append(face, body);
        root.querySelector(`[data-side="${f.side}"]`).append(card);
        cards.set(f.key, { fighter: f, card });
    }

    const caption = root.querySelector('[data-caption]');
    const arrow = root.querySelector('[data-arrow]');
    const scrub = root.querySelector('[data-scrub]');
    const count = root.querySelector('[data-count]');
    const play = root.querySelector('[data-play]');
    const speed = root.querySelector('[data-speed]');
    const list = root.querySelector('[data-log]');
    const log = [...list.querySelectorAll('li')];
    let at = 0;
    let timer = null;

    // How hurt everyone is after the first n steps, as the page worked it out: enemies exactly for whoever runs the
    // fight, in quarters for players, who never get an enemy's max HP; the party exactly for all
    function showHealth(n) {
        const shares = data.health[n] ?? {};
        for (const { fighter, card } of cards.values()) {
            if (!fighter.tracked) continue;
            const share = shares[fighter.key] ?? 1;
            const fill = card.querySelector('.fighter-hp div');
            fill.style.width = `${share * 100}%`;
            fill.className = share <= .25 ? 'critical' : share <= .5 ? 'low' : '';
            card.classList.toggle('down', share <= 0);
            card.title = fighter.maxHP ? `${fighter.name}: ${Math.round(share * fighter.maxHP)}/${fighter.maxHP} HP` : fighter.name;
        }
    }

    // Keeps the step in view by scrolling the log alone; scrollIntoView would drag the whole page along with it
    function follow(item) {
        if (!item) return;
        const a = item.getBoundingClientRect(), box = list.getBoundingClientRect();
        const by = a.top < box.top ? a.top - box.top - 4 : a.bottom > box.bottom ? a.bottom - box.bottom + 4 : 0;
        if (by) list.scrollBy({ top: by, behavior: still.matches ? 'auto' : 'smooth' });
    }

    // Fits the log into what's left of the screen below it, so the page itself never has to scroll. Beside the stage
    // it's as tall as the stage already (the stylesheet sees to that); this is for when it's stacked underneath.
    function fitLog() {
        const top = list.getBoundingClientRect().top + scrollY;
        const below = parseFloat(getComputedStyle(list.closest('main') ?? document.body).paddingBottom) || 0;
        list.style.maxHeight = `${Math.max(160, innerHeight - top - below)}px`;
    }

    function drawArrow(from, to) {
        if (!from || !to) { arrow.classList.remove('on'); return; }
        const box = root.querySelector('.replay-stage').getBoundingClientRect();
        const a = from.getBoundingClientRect(), b = to.getBoundingClientRect();
        const leftToRight = a.left < b.left;
        const x1 = (leftToRight ? a.right : a.left) - box.left, y1 = a.top + a.height / 2 - box.top;
        const x2 = (leftToRight ? b.left - 6 : b.right + 6) - box.left, y2 = b.top + b.height / 2 - box.top;
        const bow = (y2 - y1) * .15 - 30;
        arrow.querySelector(':scope > path').setAttribute('d', `M${x1},${y1} Q${(x1 + x2) / 2},${(y1 + y2) / 2 + bow} ${x2},${y2}`);
        arrow.classList.add('on');
    }

    // Shows the fight as it stood after n steps; animate plays the nth step landing
    function show(n, animate) {
        at = Math.max(0, Math.min(n, data.steps.length));
        scrub.value = at;
        count.textContent = `${at} / ${data.steps.length}`;
        for (const { card } of cards.values()) card.classList.remove('acting', 'targeted', 'hit');
        log.forEach((li, i) => { li.classList.toggle('done', i < at - 1); li.classList.toggle('now', i === at - 1); });
        follow(log[at - 1]);

        const step = data.steps[at - 1];
        showHealth(at);
        if (!step) {
            caption.textContent = 'Press play to watch the fight again.';
            drawArrow(null, null);
            return;
        }

        const actor = cards.get(step.actor)?.card;
        const target = step.target ? cards.get(step.target)?.card : null;
        actor?.classList.add('acting');
        target?.classList.add('targeted');
        caption.replaceChildren(
            el('strong', null, step.actorName), el('br'),
            document.createTextNode(step.action + (step.targetName ? ` → ${step.targetName}` : '')),
            ...(step.roll ? [el('br'), el('span', 'small', `🎲 ${step.roll}`)] : []),
            ...(step.result ? [el('br'), el('span', 'small', step.result)] : []));
        drawArrow(actor, target);

        if (animate && target && step.damage) {
            target.classList.remove('hit');
            void target.offsetWidth;
            target.classList.add('hit');
            if (!still.matches) {
                const floater = el('span', 'floater', `−${step.damage}`);
                target.append(floater);
                setTimeout(() => floater.remove(), 1300);
            }
        }
    }

    function stop() {
        clearTimeout(timer);
        timer = null;
        play.textContent = '▶';
        play.setAttribute('aria-label', 'Play');
    }

    function tick() {
        if (at >= data.steps.length) { stop(); return; }
        show(at + 1, true);
        timer = setTimeout(tick, Number(speed.value));
    }

    function start() {
        if (at >= data.steps.length) show(0, false);
        play.textContent = '⏸';
        play.setAttribute('aria-label', 'Pause');
        tick();
    }

    play.addEventListener('click', () => (timer ? stop() : start()));
    root.querySelectorAll('[data-step]').forEach(b => b.addEventListener('click', () => { stop(); show(at + Number(b.dataset.step), Number(b.dataset.step) > 0); }));
    scrub.addEventListener('input', () => { stop(); show(Number(scrub.value), false); });
    log.forEach((li, i) => li.addEventListener('click', () => { stop(); show(i + 1, true); }));
    document.addEventListener('keydown', e => {
        if (e.target.closest('input, select, textarea')) return;
        if (e.key === 'ArrowRight') { stop(); show(at + 1, true); }
        else if (e.key === 'ArrowLeft') { stop(); show(at - 1, false); }
        else if (e.key === ' ') { e.preventDefault(); timer ? stop() : start(); }
    });
    addEventListener('resize', () => { fitLog(); show(at, false); });
    // Refitted whenever the stage above it changes height too, as it does when the caption grows
    new ResizeObserver(fitLog).observe(root.querySelector('.replay-stage'));

    show(0, false);
})();
