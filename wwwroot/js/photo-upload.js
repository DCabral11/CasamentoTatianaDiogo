(function () {
    "use strict";
    const form = document.querySelector("[data-photo-upload]");
    if (!form || !window.fetch) return;

    const status = form.querySelector("[data-upload-status]");
    const error = form.querySelector("[data-upload-error]");
    const input = form.querySelector("[name='Files']");
    const maxBytes = Number(form.dataset.maxFileMb) * 1024 * 1024;
    const extensions = form.dataset.extensions.toLowerCase().split(",").map(x => x.trim());
    const types = { ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".png": "image/png", ".heic": "image/heic", ".heif": "image/heif" };
    let queue = [];
    let sending = false;
    input.addEventListener("change", () => {
        queue = [];
        error.hidden = true;
        error.textContent = "";
        input.removeAttribute("aria-invalid");
        input.setAttribute("aria-describedby", "photo-help");
    });

    async function request(url, options, retry = false) {
        for (let attempt = 0; ; attempt++) {
            try {
                const response = await fetch(url, options);
                const data = await response.json().catch(() => ({}));
                if (!response.ok) {
                    const failure = new Error(data.message || "Não foi possível enviar a fotografia. Atualiza a página e tenta novamente.");
                    failure.retryable = response.status >= 500 || response.status === 429;
                    throw failure;
                }
                return data;
            } catch (failure) {
                if (!retry || attempt >= 2 || failure.retryable === false) throw failure;
                await new Promise(resolve => setTimeout(resolve, 1000 * (attempt + 1)));
            }
        }
    }

    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (sending || !form.reportValidity()) return;
        error.hidden = true;
        const files = Array.from(input.files);
        if (!files.length) {
            error.textContent = "Escolhe pelo menos uma fotografia.";
            error.hidden = false;
            return;
        }
        const invalid = files.find(file => file.size <= 0 || file.size > maxBytes || !extensions.includes(file.name.slice(file.name.lastIndexOf(".")).toLowerCase()));
        if (invalid) {
            error.textContent = `Revê a fotografia «${invalid.name}»: usa um formato permitido e um tamanho até ${form.dataset.maxFileMb} MB.`;
            error.hidden = false;
            input.setAttribute("aria-invalid", "true");
            input.setAttribute("aria-describedby", "photo-help photo-error");
            input.focus();
            return;
        }
        if (!queue.length) queue = files.map(file => ({ file, uploaded: 0, completed: false, ticket: null }));
        const csrf = form.querySelector("[name='__RequestVerificationToken']").value;
        const guestName = form.querySelector("[name='GuestName']").value;
        const notes = form.querySelector("[name='Notes']").value;
        const controls = Array.from(form.querySelectorAll("input,textarea,button"));
        sending = true;
        controls.forEach(control => { control.disabled = true; });
        status.hidden = false;
        try {
            for (let i = 0; i < queue.length; i++) {
                const item = queue[i];
                if (item.completed) continue;
                status.textContent = `A preparar a fotografia ${i + 1} de ${queue.length}…`;
                if (!item.ticket) {
                    const ext = item.file.name.slice(item.file.name.lastIndexOf(".")).toLowerCase();
                    item.ticket = await request(form.dataset.beginUrl, {
                        method: "POST",
                        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
                        body: JSON.stringify({ fileName: item.file.name, contentType: types[ext] || item.file.type,
                            sizeBytes: item.file.size, guestName, notes })
                    });
                }
                while (!item.completed) {
                    const end = Math.min(item.uploaded + item.ticket.chunkSize, item.file.size);
                    const progress = await request(form.dataset.chunkUrl, {
                        method: "POST",
                        headers: { "Content-Type": "application/octet-stream", "X-CSRF-TOKEN": csrf,
                            "X-Upload-Token": item.ticket.token, "X-Upload-Offset": String(item.uploaded) },
                        body: item.file.slice(item.uploaded, end)
                    }, true);
                    if (!progress.completed && progress.uploadedBytes <= item.uploaded)
                        throw new Error("O envio não avançou. Tenta novamente dentro de momentos.");
                    item.uploaded = progress.uploadedBytes;
                    item.completed = progress.completed;
                    status.textContent = `Fotografia ${i + 1} de ${queue.length} — ${Math.round(item.uploaded / item.file.size * 100)}%`;
                }
            }
            status.textContent = "As tuas fotografias foram enviadas. Obrigado por partilhares este momento connosco!";
            form.reset();
            queue = [];
        } catch (failure) {
            error.textContent = failure.message;
            error.hidden = false;
        } finally {
            sending = false;
            controls.forEach(control => { control.disabled = false; });
        }
    });
}());
