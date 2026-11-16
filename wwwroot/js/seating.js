document.getElementById('print-seating')?.addEventListener('click', () => window.print());
const search = document.getElementById('seat-search');
if (search) {
    const normalize = text => text.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase('pt');
    const rows = [...document.querySelectorAll('#seating-guests-list [data-name]')]
        .map(row => ({ row, name: normalize(row.dataset.name) }));
    document.getElementById('seat-search-box').hidden = false;
    search.addEventListener('input', () => {
        const query = normalize(search.value.trim());
        for (const item of rows) item.row.hidden = !item.name.includes(query);
        document.getElementById('seat-no-match').hidden = rows.some(item => !item.row.hidden);
    });
}
