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
function showConflict() {
    conflictNotice.hidden = false;
    responseForm.querySelector('[data-rsvp-submit]').disabled = true;
    stoppedChecking = true;
}
async function checkEditors() {
    if (checkingEditor || stoppedChecking || document.hidden) return;
    checkingEditor = true;
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
    } finally { checkingEditor = false; }
}
document.querySelector('[data-rsvp-reload]')?.addEventListener('click', () => {
    try { sessionStorage.removeItem(draftKey); } catch { }
});
responseForm.addEventListener('submit', event => {
    if (!conflictNotice.hidden) event.preventDefault();
});
document.addEventListener('visibilitychange', checkEditors);
window.addEventListener('pageshow', checkEditors);
setInterval(checkEditors, 30000);
checkEditors();
