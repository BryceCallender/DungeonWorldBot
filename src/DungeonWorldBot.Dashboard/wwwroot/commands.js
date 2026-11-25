// The bot's commands, run from the dashboard, and the Play button on every page that offers them. A button with data-run-command (and data-run-as, whose Discord ID it runs
// as, and data-run-pick, a value to start the first option with) opens the command dialog (CommandDialog.razor) filled
// in with that command's options, from the list the bot writes of what it can do. Running it asks the bot, which runs it
// as if typed in game chat and posts there; what it said shows in the dialog. Options the bot suggests values for (like
// the forms a druid knows) show those values to tap, as Discord does while typing.

(() => {
    const askEvery = 600;
    const suggestEvery = 200;
    const typingPause = 300;

    let menu = null;
    let current = null;

    const dialog = () => document.querySelector('[data-command-dialog]');
    const part = name => dialog().querySelector(`[data-command-${name}]`);

    async function loadMenu() {
        if (menu) return menu;
        try {
            const response = await fetch('api/commands', { credentials: 'same-origin', cache: 'no-store' });
            if (response.ok) menu = await response.json();
        } catch {
            // Shown as nothing to run, below
        }
        return menu ?? { commands: [], party: [] };
    }

    // "merge-with" reads as "Merge with"
    const label = name => name.charAt(0).toUpperCase() + name.slice(1).replace(/-/g, ' ');

    const el = (tag, props = {}, ...children) => {
        const node = Object.assign(document.createElement(tag), props);
        node.append(...children.filter(c => c != null));
        return node;
    };

    document.addEventListener('click', async e => {
        const button = e.target.closest('[data-run-command]');
        if (!button) return;
        const as = button.dataset.runAs || document.querySelector('[data-act-as]')?.value || dialog().dataset.as;
        if (button.hasAttribute('data-run-now')) await runNow(button.dataset.runCommand, as, button.dataset.runPick);
        else await open(button.dataset.runCommand, as, button.dataset.runPick);
    });

    // Dice typed into the Play panel, like 3d6+1, rolled the same way
    document.addEventListener('submit', async e => {
        const form = e.target.closest('[data-quick-dice]');
        if (!form) return;
        e.preventDefault();
        const dice = form.elements.dice.value.trim();
        if (dice) await runNow('roll die', form.dataset.runAs, dice);
    });

    // One that needs nothing more filled in, like a plain 2d6 or a d20, runs without the form: what the bot said
    // shows in the Play panel, under the dice. Anywhere else, it opens the form as usual.
    async function runNow(path, as, pick) {
        const { commands } = await loadMenu();
        const command = commands.find(c => c.path === path);
        const result = sheet()?.open ? sheet().querySelector('[data-quick-result]') : null;
        if (!command || !result) {
            await open(path, as, pick);
            return;
        }

        const data = new FormData();
        data.set('command', path);
        data.set('as', as ?? '');
        data.set('__RequestVerificationToken', dialog().querySelector('input[name="__RequestVerificationToken"]')?.value ?? '');
        // The pick goes in the option it's meant for, as in the form: the first that takes a name or value
        const picked = pick ? command.options.find(o => o.type === 'Text' || o.type === 'Choice') : null;
        if (picked) data.set(`o.${picked.name}`, pick);

        result.replaceChildren(el('p', { className: 'small', textContent: 'Rolling…' }));
        const status = await ask('api/commands/run', data, askEvery);
        result.replaceChildren();
        if (status.error) result.append(el('p', { className: 'form-error', textContent: status.error }));
        if (status.html) result.insertAdjacentHTML('beforeend', status.html);
    }

    async function open(path, as, pick) {
        // Cleared before anything's fetched, so the last answer never shows under the next one
        part('reply').replaceChildren();
        showError(null);
        const menu = await loadMenu();
        const { commands } = menu;
        const command = commands.find(c => c.path === path);
        current = { command, as };

        part('title').textContent = command?.title ?? `/${path}`;
        part('description').textContent = command?.description ?? '';
        part('discord').textContent = command?.title ? `In Discord: /${path}` : '';
        part('reply').replaceChildren();
        showError(command ? null : "This can't be run from here. Use it in Discord instead.");
        part('submit').hidden = !command;

        const fields = part('fields');
        fields.replaceChildren(
            el('input', { type: 'hidden', name: 'command', value: path }),
            el('input', { type: 'hidden', name: 'as', value: as ?? '' }));
        // "Whose character" options ("leave it blank for your own") already act on the character being played, so
        // only the DM, who looks after everyone's, gets to pick one
        // What it needs first, then what's optional, each kept in the command's own order
        const options = (command?.options ?? [])
            .filter(o => menu.gameMaster || !isWhoseCharacter(o))
            .sort((a, b) => b.required - a.required);
        // The pick goes in the option it's meant for: the first that takes a name, like a move or form
        const picked = pick ? options.find(o => o.type === 'Text' || o.type === 'Choice') : null;
        const people = peopleFor(path, menu, as);
        for (const option of options) fields.append(field(option, people, option === picked ? pick : null));

        dialog().showModal();
        fields.querySelector('input:not([type=hidden]), select')?.focus();
    }

    const isWhoseCharacter = option => option.type === 'User' && !option.required && option.name === 'user';

    // Commands pointed at a person are mostly at someone else (who to aid, bond with or pay), so the character being
    // played is left out; damage and healing are most often their own, so it comes first there
    const selfFirst = ['character damage', 'character heal'];

    function peopleFor(path, menu, as) {
        const self = menu.playing ?? as;
        const others = menu.party.filter(p => p.id !== self);
        const mine = menu.party.filter(p => p.id === self);
        return selfFirst.includes(path) ? [...mine, ...others] : others;
    }

    function field(option, party, value) {
        const name = `o.${option.name}`;
        const id = `command-option-${option.name}`;
        const title = el('span', { className: 'field-label', textContent: label(option.name) + (option.required ? '' : ' (optional)') });
        const hint = option.description ? el('span', { className: 'small', textContent: option.description }) : null;

        if (option.type === 'Boolean') {
            const box = el('input', { type: 'checkbox', name, id, value: 'true', checked: option.default === 'true' });
            return el('div', { className: 'field' }, el('label', { className: 'check', htmlFor: id }, box, title), hint);
        }

        let input;
        if (option.type === 'Choice' || option.type === 'User') {
            const choices = option.type === 'User' ? party.map(p => ({ name: p.name, value: p.id })) : option.choices;
            input = el('select', { className: 'filter', name, id },
                option.required ? null : el('option', { value: '', textContent: '—' }),
                ...choices.map(c => el('option', { value: c.value, textContent: c.name })));
        } else {
            const numeric = option.type === 'Integer' || option.type === 'Number';
            input = el('input', {
                className: 'filter', name, id,
                type: numeric ? 'number' : 'text',
                inputMode: option.type === 'Integer' ? 'numeric' : undefined,
                step: option.type === 'Integer' ? '1' : 'any',
                placeholder: option.default ?? '',
                autocomplete: 'off',
            });
            if (option.min != null) input.min = option.min;
            if (option.max != null) input.max = option.max;
        }
        if (value) input.value = value;
        input.required = option.required;

        const wrapper = el('div', { className: 'field' }, el('label', { htmlFor: id }, title), hint, input);
        if (option.suggests) offerSuggestions(wrapper, input, option);
        return wrapper;
    }

    // ---- Suggestions -------------------------------------------------------------------------------------------

    function offerSuggestions(wrapper, input, option) {
        const list = el('div', { className: 'suggestions' });
        wrapper.append(list);
        let timer = null;
        let asked = 0;

        const load = async () => {
            const mine = ++asked;
            const data = new FormData(input.form);
            data.set('focused', option.name);
            const status = await ask('api/commands/suggest', data, suggestEvery);
            // Typed again since: this answer is stale
            if (mine !== asked || !status?.suggestions) return;
            list.replaceChildren(...status.suggestions.map(s => el('button', {
                type: 'button',
                className: 'chip suggestion',
                textContent: s.name,
                onclick: () => { input.value = s.value; list.replaceChildren(); },
            })));
        };

        input.addEventListener('focus', () => { if (!list.childElementCount) load(); });
        input.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(load, typingPause); });
    }

    // ---- Running -----------------------------------------------------------------------------------------------

    document.addEventListener('submit', async e => {
        const form = e.target.closest('[data-command-form]');
        if (!form) return;
        e.preventDefault();
        if (!current?.command) return;

        const missing = current.command.options.find(o => o.required && !form.elements[`o.${o.name}`]?.value.trim());
        if (missing) {
            showError(`Fill in ${label(missing.name).toLowerCase()} first.`);
            return;
        }
        showError(null);

        const submit = part('submit');
        const reply = part('reply');
        submit.disabled = true;
        reply.replaceChildren(el('p', { className: 'small', textContent: 'Asking the bot…' }));
        try {
            const status = await ask('api/commands/run', new FormData(form), askEvery);
            reply.replaceChildren();
            if (status.error) showError(status.error);
            if (status.html) reply.insertAdjacentHTML('beforeend', status.html);
            else if (!status.error) reply.append(el('p', { className: 'small', textContent: 'Done.' }));
            // What they can do may have changed (their HP, a form taken, who they're playing), so the Play sheet behind
            // is brought up to date, and the menu is asked for again next time
            menu = null;
            if (sheet()?.open) loadQuick();
        } finally {
            submit.disabled = false;
        }
    });

    // ---- The Play button ---------------------------------------------------------------------------------------

    // On every page: what the viewer can do right now, fetched fresh each time it opens. With sign-in off, who they're
    // playing as is picked in it and remembered in this browser.
    const sheet = () => document.querySelector('[data-quick-sheet]');
    const quickAs = () => sheet()?.querySelector('[data-quick-as]');
    const remembered = 'quick-as';

    try {
        const saved = localStorage.getItem(remembered);
        if (saved && quickAs()?.querySelector(`option[value="${CSS.escape(saved)}"]`)) quickAs().value = saved;
    } catch {
        // Storage off (a private window): start with the first
    }

    document.addEventListener('click', e => {
        if (!e.target.closest('[data-quick-open]')) return;
        sheet().showModal();
        loadQuick();
    });

    document.addEventListener('change', e => {
        if (!e.target.matches('[data-quick-as]')) return;
        try { localStorage.setItem(remembered, e.target.value); } catch { /* not remembered, that's all */ }
        loadQuick();
    });

    // ---- Who the Actions page is for, with sign-in off ----------------------------------------------------------

    // Signed in, the page only has what the player's character can use. With sign-in off it has everything, and each
    // command says what it's for (data-mechanic, data-dm), so only those for whoever's picked in "Act as" show. The
    // pick is shared with the Play button, and put back after a live update swaps the page out.
    const actAs = () => document.querySelector('[data-act-as]');

    function restoreActAs() {
        const select = actAs();
        if (!select) return;
        try {
            const saved = localStorage.getItem(remembered);
            if (saved && select.querySelector(`option[value="${CSS.escape(saved)}"]`)) select.value = saved;
        } catch {
            // Storage off: the first in the list
        }
        showFor(select.selectedOptions[0]);
    }

    function showFor(picked) {
        if (!picked) return;
        const isDM = picked.hasAttribute('data-dm');
        const has = new Set((picked.dataset.mechanics ?? '').split(' ').filter(Boolean));
        for (const item of document.querySelectorAll('.action-list > li')) {
            const needs = item.dataset.mechanic;
            item.classList.toggle('not-theirs', !isDM && (item.hasAttribute('data-dm') || (!!needs && !has.has(needs))));
        }
        for (const group of document.querySelectorAll('.action-group')) {
            const empty = !group.querySelector('.action-list > li:not(.not-theirs)');
            group.classList.toggle('not-theirs', empty);
            document.querySelector(`.jump a[href$="#${CSS.escape(group.id)}"]`)?.classList.toggle('not-theirs', empty);
        }
    }

    document.addEventListener('change', e => {
        if (!e.target.matches('[data-act-as]')) return;
        try { localStorage.setItem(remembered, e.target.value); } catch { /* not remembered, that's all */ }
        showFor(e.target.selectedOptions[0]);
    });
    document.addEventListener('live-refreshed', restoreActAs);
    restoreActAs();

    // Asked once more after a moment when it fails, since that's most often the dashboard restarting, then left with a
    // button to try again rather than a reload
    const quickRetryAfter = 1500;

    async function loadQuick(retried = false) {
        const body = sheet().querySelector('[data-quick-body]');
        const as = quickAs()?.value || sheet().dataset.as;
        if (!body.childElementCount) body.append(el('p', { className: 'small', textContent: 'Loading…' }));

        let response = null;
        try {
            response = await fetch(`api/commands/quick/${as}`, { credentials: 'same-origin', cache: 'no-store' });
        } catch {
            // Not reachable: tried again below
        }
        if (response?.ok) {
            body.innerHTML = await response.text();
            return;
        }
        if (!retried) {
            await new Promise(r => setTimeout(r, quickRetryAfter));
            return loadQuick(true);
        }

        const why = response?.status === 403
            ? 'You can only see your own actions.'
            : "Couldn't load your actions. The dashboard may be restarting.";
        body.replaceChildren(
            el('p', { className: 'empty', textContent: why }),
            el('button', { type: 'button', className: 'tool', textContent: 'Try again', onclick: () => loadQuick() }));
    }

    function showError(text) {
        const error = part('error');
        error.hidden = !text;
        error.textContent = text ?? '';
    }

    // Sends the form, then asks after it until the bot has answered (or the server says it's given up)
    async function ask(url, data, every) {
        let asked;
        try {
            const response = await fetch(url, { method: 'POST', body: data, credentials: 'same-origin' });
            asked = await response.json().catch(() => null);
            if (!response.ok || !asked || asked.error)
                return { done: true, error: asked?.error ?? "Couldn't ask the bot. Reload and try again." };
        } catch {
            return { done: true, error: "Couldn't reach the dashboard. Try again." };
        }

        for (;;) {
            await new Promise(r => setTimeout(r, every));
            try {
                const response = await fetch(`api/commands/${asked.id}`, { credentials: 'same-origin', cache: 'no-store' });
                if (response.status >= 400 && response.status < 500) return { done: true, error: "Couldn't find what the bot said." };
                const status = response.ok ? await response.json() : null;
                if (status?.done) return status;
            } catch {
                // A hiccup (the server restarting): ask again
            }
        }
    }
})();
