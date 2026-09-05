const mainStatus = document.getElementById('guest-status');
const dependentSections = document.querySelectorAll('[data-rsvp-attendance-dependent]');
const familyCards = [];

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
            field.disabled = !canRespondForFamily;
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
updateAttendanceDependentFields();
