(function () {
    "use strict";

    document.querySelectorAll("form").forEach((form) => {
        form.noValidate = true;
    });

    if (!window.jQuery || !window.jQuery.validator) {
        return;
    }

    function decorateSummary(summary) {
        if (!summary || !summary.classList.contains("validation-summary-errors")) {
            return;
        }

        summary.classList.add("feedback-card", "feedback-card-error", "form-feedback-card");

        if (!summary.querySelector(".form-feedback-heading")) {
            const heading = document.createElement("p");
            heading.className = "form-feedback-heading";
            heading.innerHTML = '<i class="bi bi-exclamation-circle" aria-hidden="true"></i> Revê os campos assinalados';
            summary.prepend(heading);
        }
    }

    function decorateAllSummaries(scope) {
        scope.querySelectorAll("[data-valmsg-summary='true'], .validation-summary-errors").forEach(decorateSummary);
    }

    window.jQuery(document).on("invalid-form.validate", "form", function () {
        const summary = this.querySelector("[data-valmsg-summary='true']");
        window.setTimeout(() => {
            decorateSummary(summary);
            summary?.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }, 0);
    });

    window.jQuery(document).on("focusout", "form :input", function () {
        if (this.form) {
            window.setTimeout(() => decorateAllSummaries(this.form), 0);
        }
    });

    decorateAllSummaries(document);
}());
