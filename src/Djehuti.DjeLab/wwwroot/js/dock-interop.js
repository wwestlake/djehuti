// Read-only DOM measurement helpers for the docking workspace.
// Deliberately does not touch/move any DOM node Blazor owns -- these are
// one-shot getBoundingClientRect reads used to convert pointer/drag pixel
// positions into layout fractions and drop zones.

export function getRect(el) {
    const r = el.getBoundingClientRect();
    return { width: r.width, height: r.height, left: r.left, top: r.top };
}
