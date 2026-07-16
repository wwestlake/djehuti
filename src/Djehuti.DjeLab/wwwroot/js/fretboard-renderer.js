// Renders an interactive guitar fretboard diagram with highlighted positions.
// Standard tuning: E A D G B E (6 strings, 0-indexed from top)

const standardTuning = ['E', 'A', 'D', 'G', 'B', 'E'];

export function renderFretboard(elementId, numStrings, numFrets, highlightedJson) {
    const container = document.getElementById(elementId);
    if (!container) return;

    container.innerHTML = '';

    let highlighted = new Set();
    try {
        const positions = JSON.parse(highlightedJson || '[]');
        if (Array.isArray(positions)) {
            positions.forEach(pos => {
                if (typeof pos === 'object' && pos.string !== undefined && pos.fret !== undefined) {
                    highlighted.add(`${pos.string}-${pos.fret}`);
                }
            });
        }
    } catch { /* skip parse errors */ }

    const fretWidth = 40;
    const stringSpacing = 35;
    const width = numFrets * fretWidth + 60;
    const height = numStrings * stringSpacing + 40;

    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', width);
    svg.setAttribute('height', height);
    svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
    svg.style.border = '1px solid #ddd';
    svg.style.background = '#fef9f0';
    svg.style.display = 'block';

    // Draw fret numbers at top
    for (let fret = 1; fret <= numFrets; fret++) {
        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', 40 + (fret - 0.5) * fretWidth);
        text.setAttribute('y', 20);
        text.setAttribute('text-anchor', 'middle');
        text.setAttribute('font-size', '12');
        text.setAttribute('fill', '#666');
        text.textContent = fret;
        svg.appendChild(text);
    }

    // Draw strings and frets
    for (let string = 0; string < numStrings; string++) {
        const y = 30 + string * stringSpacing;

        // String line
        const stringLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        stringLine.setAttribute('x1', 40);
        stringLine.setAttribute('y1', y);
        stringLine.setAttribute('x2', 40 + numFrets * fretWidth);
        stringLine.setAttribute('y2', y);
        stringLine.setAttribute('stroke', '#8B6914');
        stringLine.setAttribute('stroke-width', '2');
        svg.appendChild(stringLine);

        // Fret dots
        for (let fret = 0; fret <= numFrets; fret++) {
            const x = 40 + fret * fretWidth;
            const isHighlighted = highlighted.has(`${string}-${fret}`);

            const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            dot.setAttribute('cx', x);
            dot.setAttribute('cy', y);
            dot.setAttribute('r', '6');
            dot.setAttribute('fill', isHighlighted ? '#FF6B6B' : '#e0e0e0');
            dot.setAttribute('stroke', isHighlighted ? '#C92A2A' : '#999');
            dot.setAttribute('stroke-width', '1');
            svg.appendChild(dot);
        }

        // String label
        const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        label.setAttribute('x', 15);
        label.setAttribute('y', y + 5);
        label.setAttribute('text-anchor', 'middle');
        label.setAttribute('font-size', '11');
        label.setAttribute('fill', '#666');
        label.textContent = string < standardTuning.length ? standardTuning[string] : '?';
        svg.appendChild(label);
    }

    // Draw fret vertical lines
    for (let fret = 1; fret <= numFrets; fret++) {
        const x = 40 + fret * fretWidth;
        const fretLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        fretLine.setAttribute('x1', x);
        fretLine.setAttribute('y1', 30);
        fretLine.setAttribute('x2', x);
        fretLine.setAttribute('y2', 30 + numStrings * stringSpacing);
        fretLine.setAttribute('stroke', '#ccc');
        fretLine.setAttribute('stroke-width', '0.5');
        svg.appendChild(fretLine);
    }

    // Draw fret inlays (dots on frets 3, 5, 7, 9, 12, etc.)
    const inlayFrets = [3, 5, 7, 9, 12, 15, 17, 19, 21];
    inlayFrets.forEach(fret => {
        if (fret <= numFrets) {
            const x = 40 + fret * fretWidth;
            const y = 30 + (numStrings - 1) * stringSpacing / 2;

            const inlay = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            inlay.setAttribute('cx', x);
            inlay.setAttribute('cy', y);
            inlay.setAttribute('r', '3');
            inlay.setAttribute('fill', '#ccc');
            svg.appendChild(inlay);
        }
    });

    container.appendChild(svg);
}
