// The session's fill-in page (JourneyEditPage.razor): clicking the clue chart sets the new stop's time to that moment,
// and a line on the chart shows the time picked, however it was picked.
(() => {
    const chart = document.querySelector('[data-clues]');
    const field = document.querySelector('[data-stop-at]');
    if (!chart || !field) return;

    const cursor = chart.querySelector('[data-cursor]');
    const start = Number(chart.dataset.start), span = Number(chart.dataset.span);
    const left = Number(chart.dataset.left), plot = Number(chart.dataset.plot);
    const pad = n => String(n).padStart(2, '0');
    // As a datetime-local field wants it, in this browser's time
    const local = at => `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}T${pad(at.getHours())}:${pad(at.getMinutes())}`;

    function showPicked() {
        const at = new Date(field.value).getTime();
        if (Number.isNaN(at)) return;
        const x = left + Math.min(1, Math.max(0, (at - start) / span)) * plot;
        cursor.setAttribute('x1', x);
        cursor.setAttribute('x2', x);
    }

    chart.addEventListener('click', e => {
        // A roll's tick opens its message instead
        if (e.target.closest('a')) return;
        const point = chart.createSVGPoint();
        point.x = e.clientX;
        point.y = e.clientY;
        const { x } = point.matrixTransform(chart.getScreenCTM().inverse());
        const share = Math.min(1, Math.max(0, (x - left) / plot));
        field.value = local(new Date(start + share * span));
        showPicked();
    });
    field.addEventListener('input', showPicked);
    showPicked();
})();
