(() => {
    const album = document.getElementById('storyCarousel');
    const carousel = bootstrap.Carousel.getOrCreateInstance(album, { pause: false });
    const slides = [...album.querySelectorAll('.carousel-item')];
    const toggle = album.querySelector('[data-rotation]');
    const rotationIcon = toggle.querySelector('.bi');
    const position = album.querySelector('[data-position]');
    const picker = album.querySelector('.carousel-indicators');
    const thumbnails = [...picker.querySelectorAll('button')];
    const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
    let rotationEnabled = !reducedMotion.matches && !album.matches(':focus-within');
    let pointerIntent;
    let timer;
    let visible = false;
    let hovered = false;
    let requestedSlide = null;

    function loadPhoto(slide) {
        const image = slide.querySelector('img');
        if (image.dataset.src) {
            if (image.dataset.srcset) image.srcset = image.dataset.srcset;
            image.src = image.dataset.src;
            delete image.dataset.src;
        }
        return image;
    }

    function updatePlayback() {
        clearTimeout(timer);
        const playing = rotationEnabled && visible && !document.hidden && !hovered;
        const action = rotationEnabled ? 'Pausar apresentação' : 'Iniciar apresentação';
        toggle.setAttribute('aria-label', action);
        toggle.title = action;
        rotationIcon.className = rotationEnabled ? 'bi bi-pause-fill' : 'bi bi-play-fill';
        position.setAttribute('aria-live', playing ? 'off' : 'polite');
        if (!playing) return;

        const current = slides.indexOf(album.querySelector('.carousel-item.active'));
        const next = loadPhoto(slides[(current + 1) % slides.length]);
        timer = setTimeout(() => {
            if (next.complete && next.naturalWidth) carousel.next();
            else updatePlayback();
        }, 7000);
    }

    album.addEventListener('slide.bs.carousel', async event => {
        const target = event.relatedTarget;
        requestedSlide = target;
        const image = loadPhoto(target);
        if (image.complete && image.naturalWidth) return;

        // Keep the current photograph on screen while a slow connection loads the next one.
        event.preventDefault();
        image.loading = 'eager';
        try {
            await image.decode();
        } catch {
            return;
        }
        if (requestedSlide === target) carousel.to(event.to);
    });

    album.addEventListener('slid.bs.carousel', event => {
        position.textContent = `Fotografia ${event.to + 1} de ${slides.length}`;
        updatePlayback();
    });
    album.addEventListener('focusin', () => {
        rotationEnabled = false;
        updatePlayback();
    });
    // A pointer click focuses the button before the click event. Honour the action shown on press.
    toggle.addEventListener('pointerdown', () => { pointerIntent = !rotationEnabled; });
    toggle.addEventListener('click', event => {
        rotationEnabled = event.detail && pointerIntent !== undefined ? pointerIntent : !rotationEnabled;
        pointerIntent = undefined;
        requestedSlide = null;
        updatePlayback();
    });
    toggle.hidden = false;
    updatePlayback();

    picker.addEventListener('keydown', event => {
        if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
        const current = thumbnails.indexOf(event.target);
        if (current < 0) return;
        let index;
        switch (event.key) {
            case 'ArrowLeft': index = (current + slides.length - 1) % slides.length; break;
            case 'ArrowRight': index = (current + 1) % slides.length; break;
            case 'Home': index = 0; break;
            case 'End': index = slides.length - 1; break;
            default: return;
        }
        event.preventDefault();
        event.stopPropagation();
        thumbnails[index].focus();
        requestedSlide = null;
        carousel.to(index);
    });
    album.addEventListener('pointerenter', event => {
        hovered = event.pointerType === 'mouse';
        updatePlayback();
    });
    album.addEventListener('pointerleave', () => {
        hovered = false;
        updatePlayback();
    });
    document.addEventListener('visibilitychange', updatePlayback);
    reducedMotion.addEventListener('change', () => {
        if (reducedMotion.matches) rotationEnabled = false;
        updatePlayback();
    });
    new IntersectionObserver(([entry]) => {
        visible = entry.isIntersecting;
        updatePlayback();
    }).observe(album);
})();
