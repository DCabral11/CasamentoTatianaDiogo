(() => {
    let messages = JSON.parse(document.getElementById('ui-text')?.textContent || '{}');
    window.weddingMessages = messages;
    window.weddingText = (key, ...args) => (messages[key] || key).replace(/\{(\d+)\}/g, (match, index) =>
        index < args.length ? String(args[index]) : match);
    window.setWeddingLanguage = (language, catalog) => {
        document.documentElement.lang = language;
        messages = catalog || messages;
        window.weddingMessages = messages;
        document.querySelectorAll('[data-i18n-text]').forEach(node => { node.textContent = window.weddingText(node.dataset.i18nText); });
        for (const attribute of ['title', 'aria-label']) document.querySelectorAll(`[data-i18n-${attribute}]`).forEach(node => {
            node.setAttribute(attribute, window.weddingText(node.getAttribute(`data-i18n-${attribute}`)));
        });
    };
    document.querySelectorAll('[data-language-form]').forEach(form => {
        form.querySelector('[data-language-submit]').hidden = true;
        form.querySelector('select').addEventListener('change', () => form.requestSubmit());
        form.addEventListener('submit', () => {
            window.dispatchEvent(new Event('wedding:before-language-change'));
            const destination = form.elements.returnUrl;
            if (location.hash && !destination.value.includes('#')) destination.value += location.hash;
        });
    });
})();
