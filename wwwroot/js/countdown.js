(() => {
    const period = document.querySelector('[data-wedding-period]');
    if (!period || !period.dataset.start || !period.dataset.end) return;

    const start = Number(period.dataset.start);
    const end = Number(period.dataset.end);
    const serverNow = Number(period.dataset.now);
    const loadedAt = Date.now();
    const countdown = period.querySelector('#countdown');
    const message = period.querySelector('[data-phase-message]');
    let timer;

    function update() {
        clearTimeout(timer);
        // Use the server's instant and elapsed time. A visitor's local time zone
        // or an offset on their device clock cannot move the wedding.
        const now = serverNow + (Date.now() - loadedAt);
        const phase = now < start ? 'before' : now < end ? 'during' : 'after';
        const text = period.dataset[phase];
        if (message.textContent !== text) message.textContent = text;
        message.hidden = !text;

        if (countdown) {
            countdown.hidden = phase !== 'before';
            if (!countdown.hidden) {
                const seconds = Math.ceil((start - now) / 1000);
                const values = { days: Math.floor(seconds / 86400), hours: Math.floor(seconds / 3600) % 24,
                    minutes: Math.floor(seconds / 60) % 60, seconds: seconds % 60 };
                for (const [unit, value] of Object.entries(values))
                    countdown.querySelector('[data-' + unit + ']').textContent = String(value).padStart(2, '0');
            }
        }
        if (phase !== 'after' && !document.hidden) {
            const delay = phase === 'during' ? end - now : countdown ? (start - now) % 1000 || 1000 : start - now;
            timer = setTimeout(update, Math.min(delay, 2147483647));
        }
    }

    update();
    document.addEventListener('visibilitychange', update);
    window.addEventListener('pageshow', update);
})();
