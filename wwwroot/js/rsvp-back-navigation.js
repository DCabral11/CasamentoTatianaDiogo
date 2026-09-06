(function () {
    "use strict";

    const modalElement = document.getElementById("rsvp-back-navigation-modal");
    if (!modalElement || !window.bootstrap) {
        return;
    }

    const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement, { backdrop: "static", keyboard: false });
    const currentUrl = window.location.href;
    const guardState = { rsvpSubmissionGuard: true };
    let navigatingToSearch = false;

    // A second entry lets us intercept Back without returning to the submitted POST.
    window.history.pushState(guardState, "", currentUrl);

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
            window.history.pushState(guardState, "", currentUrl);
        }
    });
}());
