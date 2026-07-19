using System.Text.Json;
using System.Text.Json.Serialization;

namespace Djehuti.Architect.Services;

/// <summary>
/// Shared serialization settings for ArchitectureModel -- string enums (not
/// numeric) so the JSONB stored in architect_models.model_json is readable
/// directly in Postgres, and stable across future ComponentKind/
/// ConnectionKind member reordering.
/// </summary>
public static class ModelJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };
}
