const avatarSelect = document.getElementById('AvatarFileName');
const avatarPreview = document.getElementById('avatar-preview');

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
