using Djehuti.Architect.Models;

namespace Djehuti.Architect.Services;

/// <summary>
/// A hand-authored model for exercising the diagram pipeline before repo
/// reading exists to generate one automatically. Modeled on djehuti's own
/// real architecture (the system this tool itself lives in) rather than a
/// made-up example, so the first working diagram is immediately legible to
/// anyone on this project.
/// </summary>
public static class SampleModels
{
    public static ArchitectureModel DjehutiOverview()
    {
        var model = new ArchitectureModel
        {
            Name = "Djehuti Platform (overview)",
            Description = "Web portal, learning app, and API backed by a shared PostgreSQL database.",
            GeneratedAt = DateTimeOffset.UtcNow,
        };

        model.Components.AddRange(
        [
            new ArchitectureComponent { Id = "portal", Name = "Lagdaemon.Web", Kind = ComponentKind.Ui, Technology = "React", Description = "Public portal: forum, blog, downloads." },
            new ArchitectureComponent { Id = "teacher", Name = "Djehuti Teacher", Kind = ComponentKind.Ui, Technology = "Blazor WebAssembly", Description = "AI-tutored lesson plans." },
            new ArchitectureComponent { Id = "architect", Name = "Djehuti Architect", Kind = ComponentKind.Ui, Technology = "Blazor WebAssembly", Description = "This tool." },
            new ArchitectureComponent { Id = "api", Name = "Djehuti API", Kind = ComponentKind.Service, Technology = "F# / ASP.NET Core", Description = "Auth, content, forum, and analysis endpoints." },
            new ArchitectureComponent { Id = "db", Name = "PostgreSQL", Kind = ComponentKind.Database, Technology = "PostgreSQL 16", Description = "Primary datastore for all apps." },
        ]);

        model.Connections.AddRange(
        [
            new ArchitectureConnection { Id = "c1", FromComponentId = "portal", ToComponentId = "api", Kind = ConnectionKind.Calls, Protocol = "HTTPS" },
            new ArchitectureConnection { Id = "c2", FromComponentId = "teacher", ToComponentId = "api", Kind = ConnectionKind.Calls, Protocol = "HTTPS" },
            new ArchitectureConnection { Id = "c3", FromComponentId = "architect", ToComponentId = "api", Kind = ConnectionKind.Calls, Protocol = "HTTPS" },
            new ArchitectureConnection { Id = "c4", FromComponentId = "api", ToComponentId = "db", Kind = ConnectionKind.Reads, Description = "Reads", Protocol = "SQL" },
            new ArchitectureConnection { Id = "c5", FromComponentId = "api", ToComponentId = "db", Kind = ConnectionKind.Writes, Description = "Writes", Protocol = "SQL" },
        ]);

        return model;
    }
}
