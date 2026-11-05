(() => {
    const menu = document.getElementById("nav");
    const button = document.querySelector('[aria-controls="nav"]');
    button.closest("nav").addEventListener("keydown", event => {
        if (event.key !== "Escape" || !menu.classList.contains("show") || !button.getClientRects().length) return;
        event.preventDefault();
        bootstrap.Collapse.getOrCreateInstance(menu, { toggle: false }).hide();
        button.focus();
    });
})();
