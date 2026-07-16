using System;
using System.Collections.Generic;

namespace Djehuti.DjeLab.Services;

/// Orchestrates rendering of Spinoza emit() descriptors into interactive UI.
/// Takes render descriptors from Spinoza programs and produces Blazor components.
public sealed class RenderEngine
{
    private readonly List<RenderDescriptor> _descriptors = new();
    private readonly Dictionary<string, object> _paramValues = new();

    /// Collect all rendered descriptors from a program run
    public void AddDescriptor(RenderDescriptor descriptor)
    {
        if (descriptor != null)
        {
            _descriptors.Add(descriptor);
        }
    }

    /// Clear state for next program run
    public void Reset()
    {
        _descriptors.Clear();
    }

    /// Get all collected descriptors (for Blazor component rendering)
    public IReadOnlyList<RenderDescriptor> Descriptors => _descriptors.AsReadOnly();

    /// Track parameter values that should be sent back to the program on re-run
    public void SetParameterValue(string name, object? value)
    {
        if (value != null)
        {
            _paramValues[name] = value;
        }
    }

    /// Get parameter values to pass into the next program evaluation
    public IReadOnlyDictionary<string, object> ParameterValues => _paramValues;

    /// Parse a render descriptor from the Spinoza Value emitted by render()
    public RenderDescriptor? ParseDescriptor(object? value)
    {
        // The value should be a dictionary with { type, id, props }
        // This gets populated by Spinoza's render() builtin
        return RenderDescriptor.TryDeserialize(value);
    }

    /// Get a strongly-typed descriptor if it matches the expected type
    public T? GetDescriptorAs<T>(RenderDescriptor descriptor) where T : RenderDescriptor
    {
        return descriptor.Type switch
        {
            "graph" when typeof(T) == typeof(GraphDescriptor) =>
                new GraphDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "button" when typeof(T) == typeof(ButtonDescriptor) =>
                new ButtonDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "slider" when typeof(T) == typeof(SliderDescriptor) =>
                new SliderDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "input" when typeof(T) == typeof(InputDescriptor) =>
                new InputDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "notation" when typeof(T) == typeof(NotationDescriptor) =>
                new NotationDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "piano" when typeof(T) == typeof(PianoDescriptor) =>
                new PianoDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "fretboard" when typeof(T) == typeof(FretboardDescriptor) =>
                new FretboardDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "music" when typeof(T) == typeof(MusicDescriptor) =>
                new MusicDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "math" when typeof(T) == typeof(MathDescriptor) =>
                new MathDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "text" when typeof(T) == typeof(TextDescriptor) =>
                new TextDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            "container" when typeof(T) == typeof(ContainerDescriptor) =>
                new ContainerDescriptor { Type = descriptor.Type, Props = descriptor.Props } as T,
            _ => null
        };
    }
}
