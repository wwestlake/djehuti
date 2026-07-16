// Renders an interactive piano keyboard showing MIDI notes as highlighted keys.
// Supports octaves 0-8 but typically displays 2-3 octaves in a UI.

const noteNames = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
const blackKeyNotes = [1, 3, 6, 8, 10]; // C#, D#, F#, G#, A#

export function renderPiano(elementId, startOctave, endOctave, highlightedMidiJson) {
    const container = document.getElementById(elementId);
    if (!container) return;

    container.innerHTML = '';

    let highlighted = new Set();
    try {
        const notes = JSON.parse(highlightedMidiJson || '[]');
        if (Array.isArray(notes)) {
            notes.forEach(n => highlighted.add(n));
        }
    } catch { /* skip parse errors */ }

    const width = (endOctave - startOctave + 1) * 7 * 20 + 2; // 7 white keys per octave
    const height = 200;

    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', width);
    svg.setAttribute('height', height);
    svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
    svg.style.border = '1px solid #ddd';
    svg.style.background = '#f9f9f9';
    svg.style.display = 'block';

    // Draw white keys
    let xPos = 1;
    for (let octave = startOctave; octave <= endOctave; octave++) {
        for (let noteIdx = 0; noteIdx < 7; noteIdx++) {
            const keyName = `${noteNames[noteIdx * 2]}${octave}`;
            const midiNote = octave * 12 + noteIdx * 2;
            const isHighlighted = highlighted.has(midiNote);

            const whiteKey = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            whiteKey.setAttribute('x', xPos);
            whiteKey.setAttribute('y', 20);
            whiteKey.setAttribute('width', 19);
            whiteKey.setAttribute('height', 160);
            whiteKey.setAttribute('fill', isHighlighted ? '#e8f4f8' : 'white');
            whiteKey.setAttribute('stroke', '#333');
            whiteKey.setAttribute('stroke-width', '1');
            whiteKey.setAttribute('rx', '3');
            svg.appendChild(whiteKey);

            // Key label
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', xPos + 9.5);
            label.setAttribute('y', 170);
            label.setAttribute('text-anchor', 'middle');
            label.setAttribute('font-size', '11');
            label.setAttribute('fill', '#666');
            label.textContent = noteNames[noteIdx * 2];
            svg.appendChild(label);

            xPos += 20;
        }
    }

    // Draw black keys
    xPos = 1;
    for (let octave = startOctave; octave <= endOctave; octave++) {
        for (let noteIdx = 0; noteIdx < 7; noteIdx++) {
            if (blackKeyNotes.includes(noteIdx * 2 + 1)) {
                const midiNote = octave * 12 + noteIdx * 2 + 1;
                const isHighlighted = highlighted.has(midiNote);
                const keyName = `${noteNames[noteIdx * 2 + 1]}${octave}`;

                const blackKey = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                blackKey.setAttribute('x', xPos + 13);
                blackKey.setAttribute('y', 20);
                blackKey.setAttribute('width', 12);
                blackKey.setAttribute('height', 100);
                blackKey.setAttribute('fill', isHighlighted ? '#2196F3' : '#222');
                blackKey.setAttribute('stroke', '#000');
                blackKey.setAttribute('stroke-width', '1');
                blackKey.setAttribute('rx', '2');
                blackKey.style.pointerEvents = 'none';
                svg.appendChild(blackKey);
            }
            xPos += 20;
        }
    }

    container.appendChild(svg);
}
