namespace Djehuti.Teacher.Services;

// A music-theory score: notes and lyrics as plain data, not audio. Rendered
// to real sheet music by VexFlow (music-interop.js), and readable/writable
// by the tutor AI via the directive syntax parsed in TutorChat.razor -- the
// AI never sees or produces SVG, only this JSON shape.
public sealed class MusicNote
{
    public string Id { get; set; } = "";
    // e.g. "C4", "F#5" -- letter, optional # or b, octave. Matches VexFlow's
    // EasyScore shorthand directly (see music-interop.js).
    public string Pitch { get; set; } = "C4";
    // VexFlow duration code: w (whole), h (half), q (quarter), 8, 16, ...
    public string Duration { get; set; } = "q";
    public string? Lyric { get; set; }
}

public sealed class MusicMeasure
{
    public List<MusicNote> Notes { get; set; } = new();
}

public sealed class MusicScore
{
    public string Clef { get; set; } = "treble";
    public string TimeSignature { get; set; } = "4/4";
    public string KeySignature { get; set; } = "C";
    public List<MusicMeasure> Measures { get; set; } = new();
}
