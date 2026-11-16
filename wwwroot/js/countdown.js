(function () {
    const el = document.getElementById('countdown');

    if (!el) return;

    const target = new Date(el.dataset.weddingDate);

    if (isNaN(target)) {
       el.innerHTML = '<p>Wedding date will be announced soon</p>';
       return;
    }

    function tick() {
        const diff = target - new Date();

        if (diff <= 0) {
            el.innerHTML = '<div><strong>Today</strong><span>Celebra!</span></div>';
            return;
        }

        const s = Math.floor(diff / 1000);
        const d = Math.floor(s / 86400);
        const h = Math.floor((s % 86400) / 3600);
        const m = Math.floor((s % 3600) / 60);
        const sec = s % 60;

        el.querySelector('[data-days]').textContent = String(d).padStart(2, '0');
        el.querySelector('[data-hours]').textContent = String(h).padStart(2, '0');
        el.querySelector('[data-minutes]').textContent = String(m).padStart(2, '0');
        el.querySelector('[data-seconds]').textContent = String(sec).padStart(2, '0');
    }

    tick();
    setInterval(tick, 1000);
})();
