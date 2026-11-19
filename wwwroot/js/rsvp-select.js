const mainStatus = document.getElementById('guest-status');
const dependentSections = document.querySelectorAll('[data-rsvp-attendance-dependent]');
const familyCards = [];
const responseForm = mainStatus.form;
const draftKey = 'casamento.rsvp.pending';

// Keep only editable answers in this tab. Invitation IDs, verification codes and
// antiforgery inputs must always come from the newly verified server form.
const answerFields = Array.from(responseForm.querySelectorAll('input:not([type="hidden"]), select, textarea'));
function answerKey(field) {
    const companion = field.closest('[data-plus-one]');
    return companion ? 'plus-' + companion.dataset.plusOne + ':' + field.id : field.id;
}
function isChoice(field) {
    return field.type === 'checkbox' || field.type === 'radio';
}

responseForm.addEventListener('submit', () => {
    try {
        const answers = Object.fromEntries(answerFields.map(field =>
            [answerKey(field), isChoice(field) ? field.checked : field.value]));
        sessionStorage.setItem(draftKey, JSON.stringify({
            scope: responseForm.dataset.rsvpDraft, expires: Date.now() + 30 * 60_000, answers
        }));
    } catch {
        // The server still returns the submitted values when browser storage is unavailable.
    }
});

function restoreSubmittedAnswers() {
    try {
        const draft = JSON.parse(sessionStorage.getItem(draftKey));
        if (!draft) return;
        sessionStorage.removeItem(draftKey);
        if (draft.scope !== responseForm.dataset.rsvpDraft || draft.expires <= Date.now()) return;
        for (const field of answerFields) {
            const value = draft.answers[answerKey(field)];
            if (isChoice(field) && typeof value === 'boolean') field.checked = value;
            else if (!isChoice(field) && typeof value === 'string') field.value = value;
        }
        document.getElementById('rsvp-draft-restored').hidden = false;
    } catch {
        // Use the server-rendered form if there is no readable draft.
    }
}

document.querySelectorAll('[data-family-card-toggle]').forEach((toggle) => {
    const card = document.getElementById(toggle.dataset.familyCardToggle);
    const status = card?.querySelector('[data-family-status]');

    if (!card || !status) {
        return;
    }

    familyCards.push({ toggle, card, status });
    toggle.addEventListener('change', updateFamilyCards);
});

function updateFamilyCards() {
    const canRespondForFamily = Boolean(mainStatus?.value);

    familyCards.forEach(({ toggle, card, status }) => {
        toggle.disabled = !canRespondForFamily;
        card.hidden = !canRespondForFamily || !toggle.checked;
        status.required = canRespondForFamily && toggle.checked;
        card.querySelectorAll('input, select, textarea').forEach((field) => {
            field.disabled = !canRespondForFamily || !toggle.checked;
        });
    });
}

function updateAttendanceDependentFields() {
    const canContinue = mainStatus?.value === 'Attending';

    dependentSections.forEach((section) => {
        section.classList.toggle('rsvp-section-disabled', !canContinue);
        section.querySelectorAll('input, select, textarea, button').forEach((field) => {
            field.disabled = !canContinue;
        });
    });

    updateFamilyCards();
}

mainStatus?.addEventListener('change', updateAttendanceDependentFields);
restoreSubmittedAnswers();
updateAttendanceDependentFields();

// Presence is advisory. The database version check is mandatory at save time.
const editorNotice = document.getElementById('rsvp-other-editor');
const conflictNotice = document.getElementById('rsvp-concurrency-conflict');
const presenceUnavailable = document.getElementById('rsvp-presence-unavailable');
let checkingEditor = false;
let stoppedChecking = !conflictNotice.hidden;
let submitting = false;
let presencePageActive = true;
let presenceTimer;
const presenceInterval = 60_000;
const presenceIdleTimeout = 3 * 60_000;
let lastActivity = Date.now();
let lastCheck = -Infinity;

function schedulePresenceCheck() {
    clearTimeout(presenceTimer);
    if (stoppedChecking || submitting || !presencePageActive || document.hidden || Date.now() - lastActivity >= presenceIdleTimeout) return;
    presenceTimer = setTimeout(checkEditors, Math.max(0, presenceInterval - (Date.now() - lastCheck)));
}

function notePresenceActivity() {
    lastActivity = Date.now();
    // Resume idle polling without sending a request on every keystroke.
    if (!checkingEditor) schedulePresenceCheck();
}
function showConflict() {
    conflictNotice.hidden = false;
    responseForm.querySelector('[data-rsvp-submit]').disabled = true;
    stoppedChecking = true;
    clearTimeout(presenceTimer);
}
async function checkEditors() {
    clearTimeout(presenceTimer);
    if (checkingEditor || stoppedChecking || submitting || !presencePageActive || document.hidden || Date.now() - lastActivity >= presenceIdleTimeout) return;
    if (Date.now() - lastCheck < presenceInterval) {
        schedulePresenceCheck();
        return;
    }
    checkingEditor = true;
    lastCheck = Date.now();
    try {
        const body = new URLSearchParams();
        for (const name of ['GuestId', 'EditSessionId', '__RequestVerificationToken']) {
            body.set(name, responseForm.elements.namedItem(name).value);
        }
        const result = await fetch(responseForm.dataset.editStatusUrl, { method: 'POST', body, credentials: 'same-origin', cache: 'no-store' });
        if (!result.ok || !result.headers.get('content-type')?.includes('application/json')) throw new Error('status-unavailable');
        const status = await result.json();
        editorNotice.hidden = !status.otherEditor;
        presenceUnavailable.hidden = true;
        if (status.hasChanged) showConflict();
    } catch {
        presenceUnavailable.hidden = false;
    } finally {
        checkingEditor = false;
        schedulePresenceCheck();
    }
}
document.querySelector('[data-rsvp-reload]')?.addEventListener('click', () => {
    try { sessionStorage.removeItem(draftKey); } catch { }
});
responseForm.addEventListener('submit', event => {
    if (!conflictNotice.hidden) event.preventDefault();
    else if (!event.defaultPrevented) {
        submitting = true;
        clearTimeout(presenceTimer);
    }
});
for (const eventName of ['input', 'change', 'pointerdown', 'keydown', 'focusin']) {
    responseForm.addEventListener(eventName, notePresenceActivity, { passive: true });
}
document.addEventListener('visibilitychange', () => {
    if (document.hidden) clearTimeout(presenceTimer);
    else notePresenceActivity();
});
window.addEventListener('pagehide', () => {
    presencePageActive = false;
    clearTimeout(presenceTimer);
});
window.addEventListener('pageshow', () => {
    presencePageActive = true;
    submitting = false;
    notePresenceActivity();
});
checkEditors();
