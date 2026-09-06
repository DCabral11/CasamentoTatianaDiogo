(function () {
    "use strict";

    const modalElement = document.getElementById("rsvp-back-navigation-modal");
    if (!modalElement || !window.bootstrap) {
        return;
    }

    const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement, { backdrop: "static", keyboard: false });
    const returnUrl = new URL(window.location.href);
    const guardedUrl = new URL(window.location.href);
    guardedUrl.searchParams.set("rsvpGuard", "1");
    const guardState = { rsvpSubmissionGuard: true };
    let navigatingToSearch = false;

    // The distinct guard URL prevents browsers from coalescing duplicate history entries.
    // Back always lands on the safe RSVP search URL first, never on the submitted POST.
    window.history.replaceState({ rsvpSubmissionComplete: true }, "", returnUrl);
    window.history.pushState(guardState, "", guardedUrl);

    window.addEventListener("popstate", () => {
        modal.show();
    });

    modalElement.querySelector("[data-rsvp-back-stay]").addEventListener("click", () => {
        modal.hide();
    });

    modalElement.querySelector("[data-rsvp-back-search]").addEventListener("click", () => {
        navigatingToSearch = true;
        window.location.replace("/Rsvp");
    });

    modalElement.addEventListener("hidden.bs.modal", () => {
        if (!navigatingToSearch) {
            window.history.pushState(guardState, "", guardedUrl);
        }
    });
}());
