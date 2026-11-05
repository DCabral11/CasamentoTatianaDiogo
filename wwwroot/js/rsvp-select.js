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
