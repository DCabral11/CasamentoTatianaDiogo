(() => {
    const category = document.getElementById("CategoryId");
    const supplier = document.getElementById("SupplierId");
    function filterSuppliers() {
        for (const option of supplier.querySelectorAll("option[data-category]")) {
            option.hidden = option.disabled = option.dataset.category !== category.value;
            if (option.selected && option.disabled) supplier.value = "";
        }
    }
    category.addEventListener("change", filterSuppliers);
    filterSuppliers();
})();
