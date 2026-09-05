document.querySelectorAll('.timeline').forEach((timeline) => {
    const positionLine = () => {
        const icons = timeline.querySelectorAll('.timeline-icon');

        if (!icons.length) {
            return;
        }

        const first = icons[0];
        const last = icons[icons.length - 1];
        const timelineBounds = timeline.getBoundingClientRect();
        const firstBounds = first.getBoundingClientRect();
        const lastBounds = last.getBoundingClientRect();

        timeline.style.setProperty('--timeline-line-start', `${firstBounds.top - timelineBounds.top + firstBounds.height / 2}px`);
        timeline.style.setProperty('--timeline-line-end', `${timelineBounds.bottom - lastBounds.top - lastBounds.height / 2}px`);
    };

    const updateLine = () => requestAnimationFrame(positionLine);
    new ResizeObserver(updateLine).observe(timeline);
    window.addEventListener('resize', updateLine);
    updateLine();
});
