namespace Djehuti.Architect.Models;

/// <summary>
/// The kind of thing a component represents. Deliberately coarse (not a full
/// C4 "element type" taxonomy) -- this is meant to be inferred reliably from
/// source code or asked of a user in a couple of words, not chosen from a
/// large enumeration.
/// </summary>
public enum ComponentKind
{
    Module,
    Service,
    Library,
    ExternalSystem,
    Database,
    Ui
}

/// <summary>
/// One node in the architecture: a module, service, library, external
/// system, database, or UI. Components nest via ParentId (e.g. a Module
/// inside a Service) so the same list can render as either a C4 container
/// view (top-level only) or a component view (one level deeper), rather
/// than needing separate container/component collections.
/// </summary>
public sealed class ArchitectureComponent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ComponentKind Kind { get; set; } = ComponentKind.Module;
    public string? Description { get; set; }

    /// <summary>e.g. "F#/ASP.NET Core", "Blazor WebAssembly", "PostgreSQL".</summary>
    public string? Technology { get; set; }

    /// <summary>Id of the component this one is nested inside. Null = top-level.</summary>
    public string? ParentId { get; set; }

    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// How one component relates to another. Kept to the handful of relationship
/// shapes that actually show up in architecture diagrams -- this is not
/// trying to capture every possible code-level dependency type.
/// </summary>
public enum ConnectionKind
{
    Calls,
    Reads,
    Writes,
    Publishes,
    Subscribes,
    DependsOn
}

/// <summary>A directed relationship between two components, by Id.</summary>
public sealed class ArchitectureConnection
{
    public string Id { get; set; } = "";
    public string FromComponentId { get; set; } = "";
    public string ToComponentId { get; set; } = "";
    public ConnectionKind Kind { get; set; } = ConnectionKind.Calls;
    public string? Description { get; set; }

    /// <summary>e.g. "HTTPS", "gRPC", "SQL", "AMQP". Optional.</summary>
    public string? Protocol { get; set; }
}

/// <summary>
/// A place components run: a server, container, cloud region, or similar.
/// Nests via ParentId the same way components do (e.g. an EC2 instance
/// inside an AWS region) so deployment diagrams can show real topology
/// depth without a separate nesting mechanism.
/// </summary>
public sealed class DeploymentNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>e.g. "production", "staging", "aws-ec2", "on-prem".</summary>
    public string? Environment { get; set; }

    /// <summary>e.g. "Ubuntu 24.04", "Docker", "AWS Lambda".</summary>
    public string? Technology { get; set; }

    public List<string> HostedComponentIds { get; set; } = [];
    public string? ParentId { get; set; }
}

/// <summary>
/// The complete architecture of one system -- the single source of truth
/// this tool generates diagrams and documentation *from*, per the tool's
/// own operating principle. Diagrams and docs are always derived views of
/// this, never maintained separately.
/// </summary>
public sealed class ArchitectureModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Bumped on breaking changes to this schema's shape, so older
    /// saved models can be migrated instead of silently misread.</summary>
    public string SchemaVersion { get; set; } = "1.0";

    public List<ArchitectureComponent> Components { get; set; } = [];
    public List<ArchitectureConnection> Connections { get; set; } = [];
    public List<DeploymentNode> DeploymentNodes { get; set; } = [];

    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>The local path or GitHub URL this model was generated or
    /// last refreshed from, if any -- supports the "repeated refresh as the
    /// repository changes" requirement without a separate tracking record.</summary>
    public string? SourceRepository { get; set; }
}
