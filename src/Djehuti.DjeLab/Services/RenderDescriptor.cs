using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Djehuti.DjeLab.Services;

/// Describes what the rendering engine should display.
/// Programs emit these through the render() builtin, the host collects
/// them, and the RenderEngine turns them into interactive Blazor components.
public class RenderDescriptor
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("props")]
    public Dictionary<string, object?> Props { get; init; } = new();

    /// Null if serialization fails
    public static RenderDescriptor? TryDeserialize(object? value)
    {
        if (value is not Dictionary<string, object?> dict)
            return null;

        if (dict.TryGetValue("type", out var typeObj) && typeObj is string type)
        {
            var props = dict.ContainsKey("props") && dict["props"] is Dictionary<string, object?> p
                ? p
                : new Dictionary<string, object?>();

            var id = dict.TryGetValue("id", out var idObj) && idObj is string idStr
                ? idStr
                : Guid.NewGuid().ToString("N")[..8];

            return new RenderDescriptor
            {
                Type = type,
                Id = id,
                Props = props
            };
        }

        return null;
    }
}

/// Graph/chart rendering
public sealed class GraphDescriptor : RenderDescriptor
{
    public string? Title => Props.TryGetValue("title", out var t) ? (string?)t : null;
    public double[]? X => Props.TryGetValue("x", out var x) ? x as double[] : null;
    public double[]? Y => Props.TryGetValue("y", out var y) ? y as double[] : null;
    public string? XLabel => Props.TryGetValue("xLabel", out var xl) ? (string?)xl : null;
    public string? YLabel => Props.TryGetValue("yLabel", out var yl) ? (string?)yl : null;
    public string Mode => (Props.TryGetValue("mode", out var m) ? (string?)m : null) ?? "lines";
    public string Color => (Props.TryGetValue("color", out var c) ? (string?)c : null) ?? "#1f77b4";
}

/// Interactive button
public sealed class ButtonDescriptor : RenderDescriptor
{
    public string Label => (Props.TryGetValue("label", out var l) ? (string?)l : null) ?? "Button";
    public bool Disabled => Props.TryGetValue("disabled", out var d) && d is bool && (bool)d;
    public string? OnClick => Props.TryGetValue("onClick", out var oc) ? (string?)oc : null;
}

/// Slider/range control
public sealed class SliderDescriptor : RenderDescriptor
{
    public string Label => (Props.TryGetValue("label", out var l) ? (string?)l : null) ?? "Slider";
    public double Min => Props.TryGetValue("min", out var m) && m is double dMin ? dMin : 0;
    public double Max => Props.TryGetValue("max", out var m) && m is double dMax ? dMax : 100;
    public double Value => Props.TryGetValue("value", out var v) && v is double dVal ? dVal : 50;
    public double Step => Props.TryGetValue("step", out var s) && s is double dStep ? dStep : 1;
    public string? Unit => Props.TryGetValue("unit", out var u) ? (string?)u : null;
    public string? VariableName => Props.TryGetValue("variableName", out var vn) ? (string?)vn : null;
}

/// Text input field
public sealed class InputDescriptor : RenderDescriptor
{
    public string Label => (Props.TryGetValue("label", out var l) ? (string?)l : null) ?? "Input";
    public string? Value => Props.TryGetValue("value", out var v) ? (string?)v : null;
    public string? Placeholder => Props.TryGetValue("placeholder", out var p) ? (string?)p : null;
    public string? VariableName => Props.TryGetValue("variableName", out var vn) ? (string?)vn : null;
}

/// Music notation (MusicScore JSON)
public sealed class NotationDescriptor : RenderDescriptor
{
    public string? ScoreJson => Props.TryGetValue("scoreJson", out var sj) ? (string?)sj : null;
    public List<string>? Highlighted => Props.TryGetValue("highlighted", out var h) ? h as List<string> : null;
    public string? Title => Props.TryGetValue("title", out var t) ? (string?)t : null;
}

/// Piano keyboard
public sealed class PianoDescriptor : RenderDescriptor
{
    public List<int>? Highlighted => Props.TryGetValue("highlighted", out var h) ? h as List<int> : null;
    public int StartOctave => (Props.TryGetValue("startOctave", out var so) && so is int soInt) ? soInt : 3;
    public int EndOctave => (Props.TryGetValue("endOctave", out var eo) && eo is int eoInt) ? eoInt : 5;
    public string? Title => Props.TryGetValue("title", out var t) ? (string?)t : null;
}

/// Guitar fretboard
public sealed class FretboardDescriptor : RenderDescriptor
{
    public List<object>? Highlighted => Props.TryGetValue("highlighted", out var h) ? h as List<object> : null;
    public int NumStrings => (Props.TryGetValue("numStrings", out var ns) && ns is int nsInt) ? nsInt : 6;
    public int NumFrets => (Props.TryGetValue("numFrets", out var nf) && nf is int nfInt) ? nfInt : 12;
    public string? Title => Props.TryGetValue("title", out var t) ? (string?)t : null;
}

/// ABC notation for music
public sealed class MusicDescriptor : RenderDescriptor
{
    public string? Abc => Props.TryGetValue("abc", out var a) ? (string?)a : null;
    public string? Title => Props.TryGetValue("title", out var t) ? (string?)t : null;
}

/// LaTeX math expression
public sealed class MathDescriptor : RenderDescriptor
{
    public string? LaTeX => Props.TryGetValue("latex", out var l) ? (string?)l : null;
    public bool Display => Props.TryGetValue("display", out var d) && d is bool && (bool)d;
}

/// Text/label
public sealed class TextDescriptor : RenderDescriptor
{
    public string Content => (Props.TryGetValue("content", out var c) ? (string?)c : null) ?? "";
    public string Size => (Props.TryGetValue("size", out var s) ? (string?)s : null) ?? "medium";
    public string Align => (Props.TryGetValue("align", out var a) ? (string?)a : null) ?? "left";
}

/// Container for layout
public sealed class ContainerDescriptor : RenderDescriptor
{
    public string Layout => (Props.TryGetValue("layout", out var l) ? (string?)l : null) ?? "column";
    public List<string>? Children => Props.TryGetValue("children", out var c) ? c as List<string> : null;
}
