(function () {
    "use strict";

    const modalElement = document.getElementById("delete-confirmation-modal");
    if (!modalElement || !window.bootstrap) {
        return;
    }

    const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);
    const message = modalElement.querySelector("[data-delete-confirmation-message]");
    const acceptButton = modalElement.querySelector("[data-delete-confirmation-accept]");
    let pendingForm = null;

    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.matches("[data-delete-confirmation]") || form.dataset.deleteConfirmed === "true") {
            return;
        }

        event.preventDefault();
        pendingForm = form;
        message.textContent = form.dataset.deleteMessage || "Esta ação não pode ser anulada.";
        modal.show();
    });

    acceptButton.addEventListener("click", () => {
        if (!pendingForm) {
            return;
        }

        pendingForm.dataset.deleteConfirmed = "true";
        modal.hide();
        pendingForm.requestSubmit();
        pendingForm = null;
    });

    modalElement.addEventListener("hidden.bs.modal", () => {
        pendingForm = null;
    });
}());
