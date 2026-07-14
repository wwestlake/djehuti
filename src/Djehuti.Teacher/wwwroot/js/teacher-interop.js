export function renderMath(el) {
    // renderMathInElement (KaTeX's auto-render extension) scans el's text
    // nodes for the configured delimiters and replaces matches in place --
    // it's idempotent (skips nodes it already rendered), so calling this on
    // every re-render of the lesson board is safe and cheap. window.katex/
    // renderMathInElement come from the CDN <script> tags in index.html; if
    // that CDN failed to load for some reason, just skip silently rather
    // than throwing (math falls back to plain text, lesson still readable).
    if (!el || typeof window.renderMathInElement !== 'function') return;
    window.renderMathInElement(el, {
        delimiters: [
            { left: '$$', right: '$$', display: true },
            { left: '\\[', right: '\\]', display: true },
            { left: '\\(', right: '\\)', display: false },
            { left: '$', right: '$', display: false },
        ],
        throwOnError: false,
    });
}
