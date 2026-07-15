// Renders a MusicScore (plain JSON: measures of notes, each with a pitch,
// duration, optional lyric) as real sheet music using VexFlow, loaded from
// the CDN <script> tag in index.html. Same "cosmetic feature, fail quietly"
// posture as KaTeX -- if VexFlow didn't load, or a single note's optional
// styling call throws, the score still renders as plainly as possible
// rather than taking down the whole lesson page.
//
// Every note gets a click listener that calls back into C# with its id --
// that's the "click a note, it tells the AI" wiring. Highlighted note ids
// (driven by a click or by the AI) are passed in and painted with setStyle
// before draw, no partial DOM patching -- the whole score is cleared and
// redrawn on every call, which is simple and fast enough for a teaching
// score of a few measures.

function parsePitch(pitch) {
    // "C4" / "F#5" / "Bb3" -> letter, accidental, octave
    const m = /^([A-Ga-g])([#b]?)(\d)$/.exec(pitch || 'C4');
    if (!m) return ['C', '', '4'];
    return [m[1].toUpperCase(), m[2], m[3]];
}

export function renderScore(elementId, scoreJson, highlightedJson, dotNetRef) {
    const container = document.getElementById(elementId);
    if (!container || typeof Vex === 'undefined') return;
    container.innerHTML = '';

    let score;
    try { score = JSON.parse(scoreJson); } catch { return; }
    let highlighted;
    try { highlighted = new Set(JSON.parse(highlightedJson || '[]')); } catch { highlighted = new Set(); }

    const { Renderer, Stave, StaveNote, Voice, Formatter, Annotation, Accidental } = Vex.Flow;

    const measures = score.measures || [];
    if (measures.length === 0) return;

    const measureWidth = 220;
    const width = Math.max(300, measures.length * measureWidth + 40);
    const height = 160;

    const renderer = new Renderer(container, Renderer.Backends.SVG);
    renderer.resize(width, height);
    const context = renderer.getContext();

    const [beats, beatValueStr] = (score.timeSignature || '4/4').split('/');
    const beatValue = parseInt(beatValueStr, 10) || 4;

    let x = 10;
    measures.forEach((measure, mi) => {
        const stave = new Stave(x, 20, measureWidth);
        if (mi === 0) {
            try {
                stave.addClef(score.clef || 'treble');
                stave.addTimeSignature(score.timeSignature || '4/4');
            } catch { /* cosmetic */ }
        }
        stave.setContext(context).draw();

        const notesData = measure.notes || [];
        const staveNotes = notesData.map(n => {
            const [letter, accidental, octave] = parsePitch(n.pitch);
            const staveNote = new StaveNote({ keys: [`${letter}${accidental}/${octave}`], duration: n.duration || 'q' });
            if (accidental) {
                try { staveNote.addModifier(new Accidental(accidental), 0); } catch { /* cosmetic */ }
            }
            if (n.lyric) {
                try {
                    const ann = new Annotation(n.lyric);
                    if (Annotation.VerticalJustify) {
                        ann.setVerticalJustification(Annotation.VerticalJustify.BOTTOM ?? Annotation.VerticalJustify.BELOW);
                    }
                    staveNote.addModifier(ann, 0);
                } catch { /* cosmetic */ }
            }
            if (n.id && highlighted.has(n.id)) {
                staveNote.setStyle({ fillStyle: '#f5a623', strokeStyle: '#f5a623' });
            }
            staveNote.__noteId = n.id;
            return staveNote;
        });

        if (staveNotes.length > 0) {
            try {
                const voice = new Voice({ num_beats: parseInt(beats, 10) || staveNotes.length, beat_value: beatValue });
                voice.setStrict(false);
                voice.addTickables(staveNotes);
                new Formatter().joinVoices([voice]).format([voice], measureWidth - 30);
                voice.draw(context, stave);
            } catch { /* if a hand-authored score doesn't add up, skip beaming/formatting errors rather than blanking the page */ }

            staveNotes.forEach(sn => {
                if (!sn.__noteId || !dotNetRef) return;
                const el = sn.getSVGElement && sn.getSVGElement();
                if (el) {
                    el.style.cursor = 'pointer';
                    el.addEventListener('click', () => {
                        dotNetRef.invokeMethodAsync('NotifyNoteClicked', sn.__noteId);
                    });
                }
            });
        }

        x += measureWidth;
    });
}
