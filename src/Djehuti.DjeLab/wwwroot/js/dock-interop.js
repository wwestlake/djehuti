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
