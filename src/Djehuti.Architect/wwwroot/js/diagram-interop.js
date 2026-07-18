// Renders Mermaid diagram source (plain text, generated server-side from an
// ArchitectureModel) as SVG, using Mermaid loaded from the CDN <script> tag
// in index.html. Same "cosmetic feature, fail quietly" posture as Teacher's
// KaTeX/VexFlow interop -- if Mermaid didn't load or the source has a syntax
// problem, the container shows the raw error text rather than throwing back
// into Blazor and taking down the page.

let _initialized = false;
let _renderCounter = 0;

function ensureInitialized() {
    if (_initialized || typeof mermaid === 'undefined') return;
    mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'dark' });
    _initialized = true;
}

export async function renderDiagram(elementId, diagramSource) {
    const container = document.getElementById(elementId);
    if (!container) return;

    if (typeof mermaid === 'undefined') {
        container.textContent = 'Diagram renderer failed to load.';
        return;
    }

    ensureInitialized();

    // Mermaid needs a fresh id per render call, not per element -- reusing
    // one id across re-renders throws "element with id already exists".
    const renderId = `mermaid-render-${_renderCounter++}`;

    try {
        const { svg } = await mermaid.render(renderId, diagramSource);
        container.innerHTML = svg;
    } catch (err) {
        container.textContent = `Diagram failed to render: ${err && err.message ? err.message : err}`;
    }
}
