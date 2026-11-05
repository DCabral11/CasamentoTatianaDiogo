(() => {
    const album = document.getElementById('storyCarousel');
    const carousel = bootstrap.Carousel.getOrCreateInstance(album, { interval: 7000, pause: false });
    const slides = [...album.querySelectorAll('.carousel-item')];
    const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
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

    function prepareNext() {
        const current = slides.indexOf(album.querySelector('.carousel-item.active'));
        loadPhoto(slides[(current + 1) % slides.length]);
    }

    function updatePlayback() {
        if (visible && !document.hidden && !reducedMotion.matches && !hovered && !album.matches(':focus-within')) {
            prepareNext();
            carousel.cycle();
        } else {
            carousel.pause();
        }
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

    album.addEventListener('slid.bs.carousel', updatePlayback);
    album.addEventListener('pointerenter', event => {
        hovered = event.pointerType === 'mouse';
        updatePlayback();
    });
    album.addEventListener('pointerleave', () => {
        hovered = false;
        updatePlayback();
    });
    album.addEventListener('focusin', updatePlayback);
    album.addEventListener('focusout', () => queueMicrotask(updatePlayback));
    document.addEventListener('visibilitychange', updatePlayback);
    reducedMotion.addEventListener('change', updatePlayback);
    new IntersectionObserver(([entry]) => {
        visible = entry.isIntersecting;
        updatePlayback();
    }).observe(album);
})();
