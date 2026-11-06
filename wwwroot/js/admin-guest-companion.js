(() => {
    const isPlusOne = document.getElementById('IsPlusOne');
    const allow = document.getElementById('AllowPlusOne');
    const main = document.getElementById('MainGuestId');
    if (!isPlusOne || !allow || !main) return;
    function update() {
        document.querySelector('[data-main-guest]').hidden = !isPlusOne.checked;
        main.required = isPlusOne.checked;
        if (!isPlusOne.checked) main.value = '';
    }
    isPlusOne.addEventListener('change', () => {
        if (isPlusOne.checked) allow.checked = false;
        update();
    });
    allow.addEventListener('change', () => {
        if (allow.checked) isPlusOne.checked = false;
        update();
    });
    update();
})();
