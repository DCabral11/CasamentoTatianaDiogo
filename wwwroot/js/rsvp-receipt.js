(() => {
    // The server has committed the response; do not restore a stale form draft.
    try { sessionStorage.removeItem('casamento.rsvp.pending'); } catch { }
    document.querySelector('[data-print-receipt]')?.addEventListener('click', () => window.print());
})();
