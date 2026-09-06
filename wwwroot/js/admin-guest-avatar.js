const avatarSelect = document.getElementById('AvatarFileName');
const avatarPreview = document.getElementById('avatar-preview');
const avatarScale = document.getElementById('AvatarScale');
const avatarPositionX = document.getElementById('AvatarPositionX');
const avatarPositionY = document.getElementById('AvatarPositionY');

function updateAvatarCrop() {
    if (!avatarPreview) {
        return;
    }

    avatarPreview.style.setProperty('--avatar-scale', avatarScale?.value || '1');
    avatarPreview.style.setProperty('--avatar-position-x', `${avatarPositionX?.value || 50}%`);
    avatarPreview.style.setProperty('--avatar-position-y', `${avatarPositionY?.value || 50}%`);

    document.querySelectorAll('[data-avatar-value]').forEach((output) => {
        const input = document.getElementById(output.dataset.avatarValue);
        output.value = output.dataset.avatarValue === 'AvatarScale'
            ? `${Number(input?.value || 1).toFixed(2)}×`
            : `${input?.value || 50}%`;
    });
}

avatarSelect?.addEventListener('change', () => {
    const fileName = avatarSelect.value;

    if (fileName) {
        const image = document.createElement('img');
        image.src = `/images/guests/${encodeURIComponent(fileName)}`;
        image.alt = '';
        avatarPreview.replaceChildren(image);
        return;
    }

    const icon = document.createElement('i');
    icon.className = 'bi bi-person-heart';
    avatarPreview.replaceChildren(icon);
});

document.querySelectorAll('[data-avatar-control]').forEach((control) => {
    control.addEventListener('input', updateAvatarCrop);
});

document.getElementById('reset-avatar-crop')?.addEventListener('click', () => {
    if (avatarScale) avatarScale.value = '1';
    if (avatarPositionX) avatarPositionX.value = '50';
    if (avatarPositionY) avatarPositionY.value = '50';
    updateAvatarCrop();
});

updateAvatarCrop();
