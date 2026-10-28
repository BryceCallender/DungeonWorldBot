// The dashboard renders on the server with no Blazor interactivity, so these small helpers work on plain HTML.

// "Expand all" opens or closes every move in its card
document.addEventListener('click', e => {
    const button = e.target.closest('[data-expand]');
    if (!button) return;

    const details = [...button.closest('.card').querySelectorAll('details')];
    const open = details.some(d => !d.open);
    details.forEach(d => d.open = open);
    button.textContent = open ? 'Collapse all' : 'Expand all';
});

// [data-copy] buttons copy their text, like a command to paste into Discord, and say so for a moment
document.addEventListener('click', async e => {
    const button = e.target.closest('[data-copy]');
    if (!button) return;

    try {
        await navigator.clipboard.writeText(button.dataset.copy);
    } catch {
        // The clipboard API only works over HTTPS or on localhost
        const text = Object.assign(document.createElement('textarea'), { value: button.dataset.copy });
        document.body.append(text);
        text.select();
        document.execCommand('copy');
        text.remove();
    }

    button.classList.add('copied');
    clearTimeout(button.copiedTimer);
    button.copiedTimer = setTimeout(() => button.classList.remove('copied'), 1200);
});

// Search boxes hide the moves or items around them that don't match, searching descriptions too.
// A box searches its [data-filter-scope], or its card if it has none.
document.addEventListener('input', e => {
    const input = e.target.closest('[data-filter]');
    if (input) applyFilter(input);
});

function applyFilter(input) {
    const scope = input.closest('[data-filter-scope]') ?? input.closest('.card');
    const query = input.value.trim().toLowerCase();
    let shown = 0;

    for (const li of scope.querySelectorAll('ul.moves > li, ul.items > li')) {
        const match = !query || li.textContent.toLowerCase().includes(query);
        li.hidden = !match;
        if (match) shown++;

        // Open matches whose text is only in the description, so the hit is visible
        const details = li.querySelector('details');
        if (!details) continue;
        details.dataset.initial ??= details.open;
        const name = details.querySelector('summary .move-name');
        if (!query) details.open = details.dataset.initial === 'true';
        else if (match && name) details.open = !name.textContent.toLowerCase().includes(query);
    }

    // Hide whole groups (a class's moves at one level, a spell level) with nothing left in them
    for (const group of scope.querySelectorAll('[data-filter-group]')) {
        group.hidden = !group.querySelector('ul.moves > li:not([hidden]), ul.items > li:not([hidden])');
    }

    const none = scope.querySelector('.no-matches');
    if (none) none.hidden = shown > 0;
}

// Session calendar popups open next to their day, and card menus under their ⋯ (lined up on its right edge),
// flipping above or left when they'd run off screen
document.addEventListener('toggle', e => {
    const pop = e.target;
    if (!pop.matches?.('.cal-pop, .menu-pop') || e.newState !== 'open') return;

    const opener = document.querySelector(`[popovertarget="${pop.id}"]`).getBoundingClientRect();
    const gap = 6;
    const below = opener.bottom + gap + pop.offsetHeight <= innerHeight;
    const left = pop.classList.contains('menu-pop') ? opener.right - pop.offsetWidth : opener.left;
    pop.style.top = `${below ? opener.bottom + gap : Math.max(gap, opener.top - gap - pop.offsetHeight)}px`;
    pop.style.left = `${Math.max(gap, Math.min(left, innerWidth - pop.offsetWidth - gap))}px`;
}, true);

// They're placed for where the page was, so close them when it scrolls
addEventListener('scroll', () => document.querySelectorAll('.cal-pop:popover-open, .menu-pop:popover-open').forEach(p => p.hidePopover()), { passive: true });

// <time data-ago> shows how long ago it was ("2 minutes ago", "yesterday"), in the viewer's own language and time
// zone, which the server can't know. The text inside is what shows until this runs.
const relativeTime = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
const timeUnits = [['year', 31536000], ['month', 2592000], ['week', 604800], ['day', 86400], ['hour', 3600], ['minute', 60]];

function showTimes(root = document) {
    for (const el of root.querySelectorAll('time[data-ago]')) {
        const date = new Date(el.dateTime);
        if (isNaN(date)) continue;
        const seconds = (date - Date.now()) / 1000;
        const [unit, size] = timeUnits.find(([, size]) => Math.abs(seconds) >= size) ?? [];
        el.textContent = unit ? relativeTime.format(Math.round(seconds / size), unit) : 'just now';
        el.title = date.toLocaleString();
    }
}
showTimes();
// So "2 minutes ago" keeps counting
setInterval(showTimes, 60000);

// <time data-since> shows how long it's been, like a session's "2h 05m", counting up
function showElapsed() {
    for (const el of document.querySelectorAll('time[data-since]')) {
        const minutes = Math.max(0, Math.floor((Date.now() - new Date(el.dateTime)) / 60000));
        el.textContent = minutes >= 60 ? `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}m` : `${minutes}m`;
    }
}
showElapsed();
setInterval(showElapsed, 30000);

// On a phone the tabs scroll sideways, so bring the current one into view (Scenes is last and starts off the edge)
document.querySelector('.topbar nav a.active')?.scrollIntoView({ block: 'nearest', inline: 'nearest' });

// The jump bar underlines the section on screen:
// - the one just jumped to, while it's still where the jump left it (or anywhere on screen once at the bottom, since
//   sections near the end can't reach the top),
// - otherwise the last one whose top has passed just under the bar,
// - otherwise the last of all at the bottom of the page, or the first before any has reached the bar.
// Looked up fresh each time, since live updates swap the page out from under it.
function markSection() {
    const bar = document.querySelector('.jump');
    if (!bar) return;

    const links = [...bar.querySelectorAll('a[href*="#"]')]
        .map(a => [a, document.getElementById(decodeURIComponent(new URL(a.href).hash.slice(1)))])
        .filter(([a, section]) => section && new URL(a.href).pathname === location.pathname);
    if (links.length === 0) return;

    const barBottom = bar.getBoundingClientRect().bottom;
    const atBottom = innerHeight + scrollY >= document.documentElement.scrollHeight - 2;
    const target = links.find(([a]) => new URL(a.href).hash === location.hash);
    const targetTop = target?.[1].getBoundingClientRect().top;

    let current;
    if (target && (atBottom ? targetTop < innerHeight : Math.abs(targetTop - barBottom) < 48)) {
        current = target[0];
    } else if (atBottom) {
        current = links.at(-1)[0];
    } else {
        current = links[0][0];
        for (const [a, section] of links) {
            if (section.getBoundingClientRect().top <= barBottom + 32) current = a;
        }
    }

    for (const [a] of links) {
        if (a === current) a.setAttribute('aria-current', 'true');
        else a.removeAttribute('aria-current');
    }

    // Keep the underlined tab visible when the bar scrolls sideways (phones)
    if (current && (current.offsetLeft < bar.scrollLeft || current.offsetLeft + current.offsetWidth > bar.scrollLeft + bar.clientWidth))
        bar.scrollTo({ left: current.offsetLeft - 16, behavior: 'smooth' });
}

let markPending = false;
addEventListener('scroll', () => {
    if (markPending) return;
    markPending = true;
    requestAnimationFrame(() => { markPending = false; markSection(); });
}, { passive: true });
addEventListener('resize', markSection);
// A tab for a section already in place doesn't scroll, so nothing else would notice it
addEventListener('hashchange', markSection);
markSection();

// Tells the rest of the page (like the roll feed) that the live stream saw a change, whichever stream heard it
const changed = () => document.dispatchEvent(new Event('live-change'));

// Live updates: the server says when the bot changed the database, and the page swaps in a fresh copy of itself,
// keeping open moves, searches and scroll position.
(() => {
    // Pages for people who can't see the dashboard (like /denied) have nothing to keep live
    if (document.querySelector('[data-no-live]')) return;

    const indicator = document.querySelector('.live');
    const dot = indicator?.querySelector('.live-dot');
    let refreshing = false;
    let again = false;
    let dropped = false;
    // Once the sign-in has lapsed, every check would only be sent to Discord, so stop until they reload
    let signedOut = false;
    let lastHtml = document.querySelector('main')?.innerHTML;

    // The server pings every 20 seconds, so this long without one means the stream died without saying so (a laptop
    // waking up, a network change, the tunnel dropping it), which the browser can take minutes to notice
    const silence = 45000;
    let source = null;
    let live = false;
    let watchdog = null;
    let reconnectTimer = null;
    let attempts = 0;

    function connect() {
        if (signedOut) return;
        clearTimeout(reconnectTimer);
        reconnectTimer = null;
        source?.close();
        const current = source = new EventSource('live');
        expectPing();

        current.onopen = () => {
            setLive(true);
            // Changes made while disconnected were missed
            if (dropped) { refresh(); changed(); }
            dropped = false;
        };
        current.addEventListener('ping', () => {
            attempts = 0;
            setLive(true);
            expectPing();
        });
        current.onmessage = () => {
            expectPing();
            refresh();
            changed();
        };
        current.onerror = () => {
            if (current !== source) return;
            lost();
            // The browser retries a dropped stream itself, but not a closed one (an error page from the tunnel,
            // signed out), so retry that here
            if (current.readyState === EventSource.CLOSED) reconnectSoon();
        };
    }

    function lost() {
        setLive(false);
        dropped = true;
    }

    function expectPing() {
        clearTimeout(watchdog);
        watchdog = setTimeout(() => {
            lost();
            connect();
        }, silence);
    }

    // Soon at first, then backing off to every 30 seconds while the server stays unreachable
    function reconnectSoon() {
        if (reconnectTimer) return;
        const delay = Math.min(1000 * 2 ** attempts++, 30000);
        reconnectTimer = setTimeout(async () => {
            reconnectTimer = null;
            // Finds out whether they've been signed out, which connecting can't tell
            await refresh();
            connect();
        }, delay);
    }

    // Back online, or back on the tab after the browser put it to sleep: reconnect now instead of waiting
    function reconnectNow() {
        if (live || signedOut) return;
        attempts = 0;
        connect();
    }
    window.addEventListener('online', reconnectNow);
    window.addEventListener('pageshow', e => { if (e.persisted) { lost(); reconnectNow(); } });
    document.addEventListener('visibilitychange', () => { if (!document.hidden) reconnectNow(); });

    function setLive(on) {
        live = on;
        if (!indicator) return;
        dot?.classList.toggle('off', !on);
        indicator.title = on ? 'Live: updates as the bot changes things' : 'Reconnecting…';
    }

    // Swapping the page would close an open dialog and lose what's typed in it, so wait until it's closed
    let afterDialog = false;
    document.addEventListener('close', e => {
        if (e.target instanceof HTMLDialogElement && afterDialog) { afterDialog = false; refresh(); }
    }, true);

    async function refresh() {
        if (signedOut) return;
        if (document.querySelector('main dialog[open]')) { afterDialog = true; return; }
        if (refreshing) { again = true; return; }
        refreshing = true;

        try {
            // Not following redirects: a lapsed sign-in redirects to /login and on to Discord, which a fetch can't
            // reach, and retrying that would send them to Discord every few seconds
            const response = await fetch(location.href, { credentials: 'same-origin', cache: 'no-store', redirect: 'manual' });
            // Signed out: leave the page as it is
            if (response.type === 'opaqueredirect') {
                signedOut = true;
                clearTimeout(watchdog);
                source?.close();
                setLive(false);
                if (indicator) indicator.title = 'Signed out. Reload to sign in again.';
                return;
            }
            // The server hiccuped (restarting, database busy mid-write): try once more shortly
            if (!response.ok) { retrySoon(); return; }

            const next = new DOMParser().parseFromString(await response.text(), 'text/html');
            const fresh = next.querySelector('main');
            const main = document.querySelector('main');
            if (!fresh || !main) return;
            // Catching up after a reconnect often finds nothing new; leave the page alone then
            if (fresh.innerHTML === lastHtml) return;
            lastHtml = fresh.innerHTML;

            const state = capture(main);
            main.innerHTML = fresh.innerHTML;
            restore(main, state);
            showTimes(main);
            showElapsed();
            markSection();
            document.title = next.title;
            pulse();
        } catch {
            retrySoon();
        } finally {
            refreshing = false;
            if (again) { again = false; refresh(); }
        }
    }

    let retry = null;
    function retrySoon() {
        if (retry) return;
        retry = setTimeout(() => { retry = null; refresh(); }, 2000);
    }

    // Where something sits on the page, stable across refreshes
    const sectionKey = el => {
        const section = el.closest('section[id], .card, [data-filter-scope]');
        return section?.id || section?.querySelector('h1, h2, h3')?.textContent.trim() || '';
    };

    // Moves carry data-key (their name), since their summary text can change, like a use count going down
    const detailsKey = d => sectionKey(d) + '|' + (d.dataset.key ?? d.querySelector('summary')?.textContent.trim() ?? '');

    function capture(main) {
        const focused = document.activeElement;
        return {
            open: new Set([...main.querySelectorAll('details[open]')].map(detailsKey)),
            closed: new Set([...main.querySelectorAll('details:not([open])')].map(detailsKey)),
            filters: [...main.querySelectorAll('[data-filter]')].map(f => [sectionKey(f), f.value]),
            expanded: [...main.querySelectorAll('[data-expand]')].map(b => [sectionKey(b), b.textContent]),
            focus: focused?.matches?.('[data-filter]') ? [sectionKey(focused), focused.selectionStart, focused.selectionEnd] : null,
            scroll: window.scrollY,
        };
    }

    function restore(main, state) {
        for (const [key, value] of state.filters) {
            const input = [...main.querySelectorAll('[data-filter]')].find(f => sectionKey(f) === key);
            if (!input || !value) continue;
            input.value = value;
            applyFilter(input);
        }

        for (const d of main.querySelectorAll('details')) {
            const key = detailsKey(d);
            if (state.open.has(key)) d.open = true;
            else if (state.closed.has(key)) d.open = false;
        }

        for (const [key, text] of state.expanded) {
            const button = [...main.querySelectorAll('[data-expand]')].find(b => sectionKey(b) === key);
            if (button) button.textContent = text;
        }

        if (state.focus) {
            const [key, start, end] = state.focus;
            const input = [...main.querySelectorAll('[data-filter]')].find(f => sectionKey(f) === key);
            input?.focus({ preventScroll: true });
            input?.setSelectionRange(start, end);
        }

        window.scrollTo(0, state.scroll);
    }

    function pulse() {
        if (!dot) return;
        dot.classList.remove('pulse');
        void dot.offsetWidth;
        dot.classList.add('pulse');
    }

    if (window.EventSource) connect();
})();

// The encounter panel beside a scene's board, and the fight's timeline beside that. The board page can't be swapped out
// like the others without losing the board, so only these are fetched again when the bot changes something, keeping
// open enemies open and the timeline as it was.
(() => {
    const scene = document.querySelector('[data-encounter-panel]')?.dataset.encounterPanel;
    if (!scene) return;

    let fetching = false;
    let again = false;

    const fetchHtml = async url => {
        const response = await fetch(url, { headers: { Accept: 'text/html' } });
        if (!response.ok) throw new Error(`${url}: ${response.status}`);
        const template = document.createElement('template');
        template.innerHTML = (await response.text()).trim();
        return template.content;
    };

    // Whether you last left the timeline open, remembered between visits. It starts open.
    const timelineOpen = () => { try { return localStorage.getItem('timeline-open') !== 'false'; } catch { return true; } };
    const firstTimeline = document.querySelector('[data-timeline]');
    if (firstTimeline) firstTimeline.open = timelineOpen();
    // toggle doesn't bubble, so it's caught on the way down
    document.addEventListener('toggle', e => {
        if (!e.target.matches?.('[data-timeline]')) return;
        try { localStorage.setItem('timeline-open', String(e.target.open)); } catch { /* not remembered, then */ }
    }, true);

    // "Show older" swaps itself for the entries before the oldest shown, and another "Show older" if there are more
    document.addEventListener('click', async e => {
        const button = e.target.closest('[data-timeline-older]');
        if (!button || button.disabled) return;
        button.disabled = true;
        try {
            const older = await fetchHtml(`api/scenes/${scene}/encounter/timeline?before=${button.dataset.timelineOlder}`);
            button.closest('li').replaceWith(older);
        } catch {
            button.disabled = false;
        }
    });

    async function refreshPanel() {
        const panel = document.querySelector('[data-encounter-panel]');
        const fresh = (await fetchHtml(`api/scenes/${scene}/encounter/panel`)).querySelector('[data-encounter-panel]');
        if (!panel || !fresh) return;

        const open = new Set([...panel.querySelectorAll('[data-enemy] details[open]')].map(d => d.closest('[data-enemy]').dataset.enemy));
        for (const enemy of fresh.querySelectorAll('[data-enemy]')) {
            if (open.has(enemy.dataset.enemy)) enemy.querySelector('details')?.setAttribute('open', '');
        }
        panel.replaceWith(fresh);
        showRibbon();
    }

    // The spotlight ribbon comes in the panel, as a template, and is shown over the top of the board
    function showRibbon() {
        const host = document.querySelector('[data-ribbon-host]');
        const ribbon = document.querySelector('[data-encounter-panel] template[data-ribbon]');
        host?.replaceChildren(...(ribbon ? [ribbon.content.cloneNode(true)] : []));
    }
    showRibbon();

    async function refreshTimeline() {
        const host = document.querySelector('[data-timeline-host]');
        if (!host) return;
        // As much as is showing, so what "Show older" fetched stays
        const timeline = host.querySelector('[data-timeline]');
        const shown = host.querySelectorAll('[data-at]').length;
        const fresh = (await fetchHtml(`api/scenes/${scene}/encounter/timeline-card?timeline=${shown}`)).querySelector('[data-timeline]');
        if (!fresh) { host.replaceChildren(); return; }

        // Open or closed as it was, and scrolled where it was
        fresh.open = timeline ? timeline.open : timelineOpen();
        const scroll = timeline?.querySelector('ol').scrollTop ?? 0;
        host.replaceChildren(fresh);
        fresh.querySelector('ol').scrollTop = scroll;
    }

    async function refresh() {
        if (fetching) { again = true; return; }
        fetching = true;
        try {
            await Promise.all([refreshPanel(), refreshTimeline()]);
        } catch {
            // Offline for a moment; the next change tries again
        } finally {
            fetching = false;
            if (again) { again = false; refresh(); }
        }
    }

    // The browser reconnects a dropped stream itself; anything missed meanwhile is caught on reconnecting
    const source = new EventSource('live');
    let connectedBefore = false;
    source.onopen = () => {
        if (connectedBefore) { refresh(); changed(); }
        connectedBefore = true;
    };
    source.onmessage = () => { refresh(); changed(); };
})();

// Dialogs: buttons with data-open-dialog open the one with that ID, data-close-dialog closes its own, and clicking
// the dimmed backdrop closes it too. Listened for on the document, since live updates replace the buttons.
document.addEventListener('click', e => {
    const opener = e.target.closest('[data-open-dialog]');
    if (opener) {
        document.getElementById(opener.dataset.openDialog)?.showModal();
        return;
    }
    if (e.target.closest('[data-close-dialog]')) {
        e.target.closest('dialog')?.close();
        return;
    }
    // A click on the dialog itself, rather than anything in it, is on the backdrop
    if (e.target instanceof HTMLDialogElement && e.target.open) {
        const box = e.target.getBoundingClientRect();
        if (e.clientX < box.left || e.clientX > box.right || e.clientY < box.top || e.clientY > box.bottom) e.target.close();
    }
});
// A form sent back with a mistake reopens its dialog. The mistake comes in the address, so take it back out, or
// reloading would show it again.
if (document.querySelector('dialog[data-open-on-load]')) {
    document.querySelectorAll('dialog[data-open-on-load]').forEach(d => d.showModal());
    const url = new URL(location.href);
    for (const key of ['error', 'dialog', 'from']) url.searchParams.delete(key);
    history.replaceState(history.state, '', url);
}

// A character's picture, sent as soon as one's picked. Shrunk first, as the board does with its pictures, since it shows
// small (on the character's page and their tokens) and every board with their token loads it.
document.addEventListener('change', async e => {
    const input = e.target.closest?.('[data-portrait]');
    const file = input?.files?.[0];
    if (!file) return;

    try {
        const bitmap = await createImageBitmap(file);
        const scale = Math.min(1, 512 / Math.max(bitmap.width, bitmap.height));
        const canvas = new OffscreenCanvas(Math.round(bitmap.width * scale), Math.round(bitmap.height * scale));
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        bitmap.close();
        // WebP where the browser can make it (Safari can't, and gives PNG instead)
        const blob = await canvas.convertToBlob({ type: 'image/webp', quality: 0.85 });
        const files = new DataTransfer();
        files.items.add(new File([blob], 'portrait', { type: blob.type }));
        input.files = files.files;
    } catch {
        // Not something the browser can draw: send it as it is, and the server says what's wrong with it
    }
    input.form.submit();
});

// The bonds web: pointing at someone lights up their bonds and fades the rest. Listened for on the document, since live
// updates replace the web.
document.addEventListener('pointerover', e => {
    const web = e.target.closest?.('[data-bond-web]');
    if (!web) return;
    const id = e.target.closest('.bond-node')?.dataset.id;
    web.classList.toggle('focus', !!id);
    const related = new Set([id]);
    for (const edge of web.querySelectorAll('.bond-edge')) {
        const lit = !!id && (edge.dataset.from === id || edge.dataset.to === id);
        edge.classList.toggle('lit', lit);
        if (lit) { related.add(edge.dataset.from); related.add(edge.dataset.to); }
    }
    for (const node of web.querySelectorAll('.bond-node')) node.classList.toggle('lit', !!id && related.has(node.dataset.id));
});
document.addEventListener('pointerout', e => {
    const web = e.target.closest?.('[data-bond-web]');
    if (web && !web.contains(e.relatedTarget)) web.classList.remove('focus');
});

// The roll feed: when someone rolls 2d6 in the game chat, the dice tumble onto whatever page is open, land, and show
// how it went. Nat 12s burst, snake eyes shudder. Rides on the page's live stream, or opens its own on pages without
// one (stories, canvases with no fight). Not in a scene framed beside others, or each frame would show every roll.
(() => {
    if (document.documentElement.classList.contains('embedded')) return;

    // The pips lit on a 3×3 grid, for each face
    const faces = { 1: [4], 2: [0, 8], 3: [0, 4, 8], 4: [0, 2, 6, 8], 5: [0, 2, 4, 6, 8], 6: [0, 2, 3, 5, 6, 8] };
    const outcomes = { strong: 'Strong hit', weak: 'Weak hit', miss: 'Miss' };
    const maxShown = 3;
    const lifetime = 8000;
    const still = matchMedia('(prefers-reduced-motion: reduce)');

    let latest = null;
    let checking = false;
    let again = false;
    let feed = null;

    // Whether to show rolls at all, and with sound: the 🎲 menu in the top bar, remembered in this browser
    const settings = { show: true, sound: false };
    try { Object.assign(settings, JSON.parse(localStorage.getItem('roll-feed') ?? '{}')); } catch { /* the defaults, then */ }
    for (const input of document.querySelectorAll('[data-roll-setting]')) input.checked = settings[input.dataset.rollSetting];
    document.addEventListener('change', e => {
        const input = e.target.closest?.('[data-roll-setting]');
        if (!input) return;
        settings[input.dataset.rollSetting] = input.checked;
        try { localStorage.setItem('roll-feed', JSON.stringify(settings)); } catch { /* just for this visit, then */ }
        if (input.dataset.rollSetting === 'sound' && input.checked) { rattle(.5); clack(.55, 1); }
        if (input.dataset.rollSetting === 'show' && !input.checked) feed?.querySelectorAll('.roll-toast').forEach(dismiss);
    });

    // Dice clatter, made up on the spot rather than loaded: each clack a burst of noise through a band-pass filter, at a
    // slightly different pitch, so a tumble sounds like dice skittering across a table
    let audio = null;
    function clack(delay = 0, loudness = .35) {
        if (!settings.sound || !window.AudioContext) return;
        audio ??= new AudioContext();
        // Browsers start sound only after someone's clicked on the page; until then this is silent
        if (audio.state === 'suspended') audio.resume().catch(() => {});
        const length = Math.floor(audio.sampleRate * .045);
        const buffer = audio.createBuffer(1, length, audio.sampleRate);
        const data = buffer.getChannelData(0);
        for (let i = 0; i < length; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / length) ** 4;
        const source = Object.assign(audio.createBufferSource(), { buffer });
        const filter = audio.createBiquadFilter();
        filter.type = 'bandpass';
        filter.frequency.value = 1800 + Math.random() * 2200;
        filter.Q.value = 2.5;
        const gain = audio.createGain();
        gain.gain.value = loudness;
        source.connect(filter).connect(gain).connect(audio.destination);
        source.start(audio.currentTime + delay);
    }
    function rattle(seconds) {
        for (let t = 0; t < seconds; t += .05 + Math.random() * .09) clack(t, .12 + Math.random() * .15);
    }

    const el = (tag, className, text) => {
        const e = document.createElement(tag);
        if (className) e.className = className;
        if (text != null) e.textContent = text;
        return e;
    };

    function die() {
        const d = el('span', 'die');
        for (let i = 0; i < 9; i++) d.append(el('i'));
        return d;
    }

    function showFace(d, face) {
        const lit = faces[face];
        [...d.children].forEach((pip, i) => pip.classList.toggle('on', lit.includes(i)));
    }

    // Flickers through random faces while it tumbles, then lands on the real one
    function roll(d, face, after) {
        if (still.matches) { showFace(d, face); d.classList.add('landed'); return Promise.resolve(); }
        return new Promise(resolve => {
            d.classList.add('tumbling');
            const flicker = setInterval(() => showFace(d, 1 + Math.floor(Math.random() * 6)), 70);
            setTimeout(() => {
                clearInterval(flicker);
                showFace(d, face);
                d.classList.replace('tumbling', 'landed');
                resolve();
            }, after);
        });
    }

    function burst(toast) {
        if (still.matches) return;
        const sparks = el('span', 'roll-sparks');
        for (let i = 0; i < 18; i++) {
            const spark = el('i');
            spark.style.setProperty('--angle', `${i * 20 + Math.random() * 12}deg`);
            spark.style.setProperty('--reach', `${50 + Math.random() * 40}px`);
            sparks.append(spark);
        }
        toast.append(sparks);
        setTimeout(() => sparks.remove(), 1200);
    }

    function dismiss(toast) {
        if (toast.classList.contains('leaving')) return;
        clearTimeout(toast.timer);
        toast.classList.add('leaving');
        setTimeout(() => toast.remove(), still.matches ? 0 : 300);
    }

    // Leaves after a while, but not while the pointer's on it
    function expireLater(toast) {
        clearTimeout(toast.timer);
        toast.timer = setTimeout(() => dismiss(toast), lifetime);
    }

    async function show(r) {
        if (!settings.show) return;
        feed ??= document.body.appendChild(Object.assign(el('div', 'roll-feed'), { ariaLive: 'polite' }));

        const nat12 = r.first === 6 && r.second === 6;
        const nat2 = r.first === 1 && r.second === 1;
        const toast = el('div', `roll-toast ${r.outcome}${nat12 ? ' nat12' : ''}${nat2 ? ' nat2' : ''}${r.mine ? ' mine' : ''}`);
        // Their dice in their class's colour, as on their party card
        if (r.color) {
            toast.classList.add('classed');
            toast.style.setProperty('--class-light', r.color.light);
            toast.style.setProperty('--class-dark', r.color.dark);
        }
        toast.title = 'Click to dismiss';

        const who = el('div', 'roll-who');
        if (r.portrait) {
            const img = el('img', 'roll-avatar');
            img.src = r.portrait;
            img.alt = '';
            who.append(img);
        } else {
            who.append(el('span', 'roll-avatar', r.isDungeonMaster ? 'DM' : r.who.charAt(0)));
        }
        const names = el('div', 'roll-names');
        names.append(el('strong', null, r.mine ? `${r.who} (you)` : r.who));
        const what = [r.move, r.stat].filter(Boolean).join(' ');
        names.append(el('span', 'roll-move', what || 'rolls 2d6'));
        who.append(names);

        const first = die();
        const second = die();
        const modifier = r.total - r.first - r.second;
        const dice = el('div', 'roll-dice');
        dice.append(first, second);
        if (modifier) dice.append(el('span', 'roll-mod', modifier > 0 ? `+${modifier}` : `−${-modifier}`));
        const result = el('div', 'roll-result');
        result.append(el('span', 'roll-total', r.total));
        result.append(el('span', 'roll-outcome', nat12 ? 'Natural 12!' : nat2 ? 'Snake eyes' : outcomes[r.outcome]));
        dice.append(result);

        toast.append(who, dice);
        if (r.streak) toast.append(el('div', `roll-streak ${r.hot ? 'hot' : 'cold'}`, r.streak));
        toast.setAttribute('aria-label', `${r.who} rolled ${r.first} and ${r.second}${what ? ` for ${what}` : ''}: ${r.total}, ${outcomes[r.outcome]}`);
        toast.addEventListener('click', () => dismiss(toast));
        toast.addEventListener('pointerenter', () => clearTimeout(toast.timer));
        toast.addEventListener('pointerleave', () => expireLater(toast));

        feed.append(toast);
        const shown = feed.querySelectorAll('.roll-toast:not(.leaving)');
        for (let i = 0; i < shown.length - maxShown; i++) dismiss(shown[i]);

        if (!still.matches) rattle(.85);
        await Promise.all([roll(first, r.first, 750).then(() => clack(0, .5)), roll(second, r.second, 950).then(() => clack(0, .6))]);
        toast.classList.add('revealed');
        if (nat12) burst(toast);
        expireLater(toast);
    }

    async function check() {
        if (checking) { again = true; return; }
        checking = true;
        try {
            const url = latest ? `api/rolls/feed?after=${latest}` : 'api/rolls/feed';
            // A lapsed sign-in redirects to Discord, which a fetch can't follow; leave it until they reload
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', redirect: 'manual' });
            if (!response.ok) return false;
            const feedNow = await response.json();
            const first = latest === null;
            // With no rolls yet, every roll that comes is new
            latest = feedNow.latest ?? latest ?? '0';
            // The first look only finds where the chat's up to
            if (!first) {
                // A few at once (several people rolling together) land one after another
                feedNow.rolls.forEach((r, i) => setTimeout(() => show(r), i * 450));
            }
            return true;
        } catch {
            return false;
        } finally {
            checking = false;
            if (again) { again = false; check(); }
        }
    }

    (async () => {
        // Can't see the dashboard (like on /denied): nothing to show
        if (!await check()) return;
        document.addEventListener('live-change', check);

        // The page's own live stream, or the encounter panel's, already says when things change
        const shared = !document.querySelector('[data-no-live]') || document.querySelector('[data-encounter-panel]');
        if (shared || !window.EventSource) return;
        const source = new EventSource('live');
        let connectedBefore = false;
        source.onopen = () => {
            if (connectedBefore) check();
            connectedBefore = true;
        };
        source.onmessage = check;
    })();
})();
