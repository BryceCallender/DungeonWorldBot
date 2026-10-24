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

// Session calendar popups open next to their day, flipping above or left when they'd run off screen
document.addEventListener('toggle', e => {
    const pop = e.target;
    if (!pop.classList?.contains('cal-pop') || e.newState !== 'open') return;

    const day = document.querySelector(`[popovertarget="${pop.id}"]`).getBoundingClientRect();
    const gap = 6;
    const below = day.bottom + gap + pop.offsetHeight <= innerHeight;
    pop.style.top = `${below ? day.bottom + gap : Math.max(gap, day.top - gap - pop.offsetHeight)}px`;
    pop.style.left = `${Math.max(gap, Math.min(day.left, innerWidth - pop.offsetWidth - gap))}px`;
}, true);

// They're placed for where the page was, so close them when it scrolls
addEventListener('scroll', () => document.querySelectorAll('.cal-pop:popover-open').forEach(p => p.hidePopover()), { passive: true });

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
            if (dropped) refresh();
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

// The encounter panel beside a scene's board. The board page can't be swapped out like the others without losing the
// board, so only the panel is fetched again when the bot changes something, keeping open enemies open.
(() => {
    const scene = document.querySelector('[data-encounter-panel]')?.dataset.encounterPanel;
    if (!scene) return;

    let fetching = false;
    let again = false;

    async function refresh() {
        if (fetching) { again = true; return; }
        fetching = true;
        try {
            const response = await fetch(`api/scenes/${scene}/encounter/panel`, { headers: { Accept: 'text/html' } });
            const panel = document.querySelector('[data-encounter-panel]');
            if (!response.ok || !panel) return;

            const open = new Set([...panel.querySelectorAll('[data-enemy] details[open]')].map(d => d.closest('[data-enemy]').dataset.enemy));
            const template = document.createElement('template');
            template.innerHTML = (await response.text()).trim();
            const fresh = template.content.querySelector('[data-encounter-panel]');
            if (!fresh) return;

            for (const enemy of fresh.querySelectorAll('[data-enemy]')) {
                if (open.has(enemy.dataset.enemy)) enemy.querySelector('details')?.setAttribute('open', '');
            }
            panel.replaceWith(fresh);
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
        if (connectedBefore) refresh();
        connectedBefore = true;
    };
    source.onmessage = refresh;
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
    for (const key of ['error', 'fightError', 'dialog', 'from']) url.searchParams.delete(key);
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
