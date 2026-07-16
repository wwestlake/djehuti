// Renders ABC notation music using abcjs library (loaded via CDN in index.html).
// ABC is a text-based notation format: https://abcnotation.com/
//
// If abcjs is not available, falls back to displaying the ABC text in a <pre>.

export function renderAbc(elementId, abcText) {
    const container = document.getElementById(elementId);
    if (!container) return;

    container.innerHTML = '';

    // Try to use abcjs if available
    if (typeof ABCJS !== 'undefined' && ABCJS.renderAbc) {
        try {
            ABCJS.renderAbc(elementId, abcText, {
                responsive: 'resize',
                staffwidth: 800
            });
            return;
        } catch (e) {
            // Fall through to fallback rendering
        }
    }

    // Fallback: display as formatted text
    const pre = document.createElement('pre');
    pre.style.padding = '10px';
    pre.style.background = '#f5f5f5';
    pre.style.border = '1px solid #ddd';
    pre.style.borderRadius = '4px';
    pre.style.fontSize = '12px';
    pre.style.fontFamily = 'monospace';
    pre.style.overflow = 'auto';
    pre.textContent = abcText;
    container.appendChild(pre);
}
