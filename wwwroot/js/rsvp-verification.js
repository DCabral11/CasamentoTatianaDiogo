(() => {
    const codeForm = document.querySelector('[data-code-form]');
    if (codeForm) {
        // The code field is server rendered. This only makes refresh/bookmarks use
        // the safe GET route instead of resubmitting the email request.
        const url = new URL(codeForm.dataset.resumeUrl, location.href);
        if (url.origin === location.origin) {
            history.replaceState(history.state, '', url.pathname);
            delete document.body.dataset.postResult;
        }
    }
    const requestForm = document.querySelector('[data-send-code]');
    const submitButton = requestForm?.querySelector('button');
    if (!submitButton) return;
    const label = submitButton.textContent;
    const unavailable = submitButton.disabled;
    requestForm.addEventListener('submit', () => {
        submitButton.disabled = true;
        submitButton.textContent = 'A enviar código…';
    });
    window.addEventListener('pageshow', () => {
        submitButton.disabled = unavailable;
        submitButton.textContent = label;
    });
})();
