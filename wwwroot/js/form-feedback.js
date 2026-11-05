(() => {
    // Keep native validation available on forms without a client-side validator.
    document.querySelectorAll("form [data-valmsg-summary]").forEach(summary => {
        const form = summary.closest("form");
        const descriptions = new Map();

        function clearAssociations() {
            for (const [field, ids] of descriptions) {
                const remaining = (field.getAttribute("aria-describedby") || "").split(/\s+/).filter(id => id && !ids.includes(id));
                if (remaining.length) field.setAttribute("aria-describedby", remaining.join(" "));
                else field.removeAttribute("aria-describedby");
                field.removeAttribute("aria-invalid");
            }
            descriptions.clear();
        }

        function linkErrors(clientErrors = []) {
            clearAssociations();
            summary.querySelectorAll("li").forEach((item, index) => {
                const field = clientErrors[index]?.element || form.elements.namedItem(item.dataset.field);
                if (!field?.id || field.type === "hidden" || field.disabled) return;
                const message = item.textContent;
                const link = document.createElement("a");
                link.href = "#" + encodeURIComponent(field.id);
                const label = field.labels?.[0]?.textContent.trim();
                link.textContent = label ? label + ": " + message : message;
                link.addEventListener("click", event => {
                    event.preventDefault();
                    field.focus();
                });
                item.replaceChildren(link);
                item.id = field.id + "-feedback-" + index;
                field.setAttribute("aria-invalid", "true");
                field.setAttribute("aria-describedby", [field.getAttribute("aria-describedby"), item.id].filter(Boolean).join(" "));
                descriptions.set(field, [...(descriptions.get(field) || []), item.id]);
            });
            summary.focus();
        }

        if (summary.classList.contains("validation-summary-errors")) linkErrors();
        if (window.jQuery?.validator) {
            window.jQuery(form).on("invalid-form.validate", (_event, validator) => {
                const errors = [...validator.errorList];
                // Unobtrusive validation fills the summary during the same event.
                queueMicrotask(() => linkErrors(errors));
            });
        }
    });
})();
