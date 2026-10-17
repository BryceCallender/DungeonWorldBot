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

// Live updates: the server says when the bot changed the database, and the page swaps in a fresh copy of itself,
// keeping open moves, searches and scroll position.
(() => {
    const indicator = document.querySelector('.live');
    let refreshing = false;
    let again = false;
    let dropped = false;

    function connect() {
        const source = new EventSource('live');
        source.onopen = () => {
            setLive(true);
            // Changes made while disconnected were missed
            if (dropped) refresh();
            dropped = false;
        };
        source.onmessage = () => refresh();
        source.onerror = () => {
            setLive(false);
            dropped = true;
            // A closed stream (e.g. signed out) isn't retried by the browser, so retry here
            if (source.readyState === EventSource.CLOSED) setTimeout(connect, 10000);
        };
    }

    function setLive(on) {
        if (!indicator) return;
        indicator.classList.toggle('off', !on);
        indicator.title = on ? 'Live: updates as the bot changes things' : 'Reconnecting…';
    }

    async function refresh() {
        if (refreshing) { again = true; return; }
        refreshing = true;

        try {
            const response = await fetch(location.href, { credentials: 'same-origin', cache: 'no-store' });
            // Signed out: leave the page as it is
            if (response.redirected) return;
            // The server hiccuped (restarting, database busy mid-write): try once more shortly
            if (!response.ok) { retrySoon(); return; }

            const next = new DOMParser().parseFromString(await response.text(), 'text/html');
            const fresh = next.querySelector('main');
            const main = document.querySelector('main');
            if (!fresh || !main) return;

            const state = capture(main);
            main.innerHTML = fresh.innerHTML;
            restore(main, state);
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
        if (!indicator) return;
        indicator.classList.remove('pulse');
        void indicator.offsetWidth;
        indicator.classList.add('pulse');
    }

    if (window.EventSource) connect();
})();
