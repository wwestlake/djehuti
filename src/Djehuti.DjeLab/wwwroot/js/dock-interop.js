// Small, read-only DOM helpers shared by pane content (docking mechanics,
// chat auto-scroll, etc). Deliberately does not touch/move any DOM node
// Blazor owns -- these are one-shot reads/scrolls, never reparenting.

export function getRect(el) {
    const r = el.getBoundingClientRect();
    return { width: r.width, height: r.height, left: r.left, top: r.top };
}

export function scrollToBottom(el) {
    if (el) el.scrollTop = el.scrollHeight;
}

export function autoGrowTextarea(el, maxHeightPx) {
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = Math.min(el.scrollHeight, maxHeightPx) + 'px';
}

export function resetTextareaHeight(el) {
    // Clears the auto-grow inline override so the element falls back to its
    // CSS min-height -- avoids racing Blazor's render to re-measure scrollHeight
    // against content that may not have been cleared from the DOM yet.
    if (el) el.style.height = '';
}
