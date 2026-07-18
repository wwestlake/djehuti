using System.Text;
using Djehuti.Architect.Models;

namespace Djehuti.Architect.Services;

/// <summary>
/// Converts an ArchitectureModel into Mermaid diagram source text. This is
/// the "diagrams are generated from the model, never hand-maintained"
/// pipeline in concrete form -- nothing here mutates or reads back from the
/// rendered diagram, it is a one-way model-to-text projection.
///
/// v1 covers the C4 container view only (top-level components as
/// containers/boundaries, one level of nesting for components inside a
/// boundary). There is deliberately no Person/Actor element yet -- the
/// model has no actor concept, and the first real use of this (reading a
/// repository) has no way to discover "the end user" from source code
/// alone -- so v1 only renders component-to-component relationships.
/// Deployment-node rendering (C4Deployment) is a separate method to add
/// once this is verified working end to end.
/// </summary>
public static class MermaidGenerator
{
    public static string GenerateContainerDiagram(ArchitectureModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("C4Container");
        if (!string.IsNullOrWhiteSpace(model.Name))
            sb.AppendLine($"    title {Escape(model.Name)}");
        sb.AppendLine();

        var byParent = model.Components.ToLookup(c => c.ParentId);
        var topLevel = byParent[null];

        foreach (var component in topLevel)
        {
            var children = byParent[component.Id].ToList();
            if (children.Count > 0)
            {
                sb.AppendLine($"    System_Boundary({SafeId(component.Id)}, \"{Escape(component.Name)}\") {{");
                foreach (var child in children)
                    sb.AppendLine("        " + RenderElement(child));
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine("    " + RenderElement(component));
            }
        }

        if (model.Connections.Count > 0)
        {
            sb.AppendLine();
            foreach (var connection in model.Connections)
            {
                var label = Escape(connection.Description ?? connection.Kind.ToString());
                var line = string.IsNullOrWhiteSpace(connection.Protocol)
                    ? $"    Rel({SafeId(connection.FromComponentId)}, {SafeId(connection.ToComponentId)}, \"{label}\")"
                    : $"    Rel({SafeId(connection.FromComponentId)}, {SafeId(connection.ToComponentId)}, \"{label}\", \"{Escape(connection.Protocol)}\")";
                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    private static string RenderElement(ArchitectureComponent component)
    {
        var id = SafeId(component.Id);
        var name = Escape(component.Name);
        var tech = Escape(component.Technology ?? "");
        var desc = Escape(component.Description ?? "");

        return component.Kind switch
        {
            ComponentKind.Database => $"ContainerDb({id}, \"{name}\", \"{tech}\", \"{desc}\")",
            ComponentKind.ExternalSystem => $"System_Ext({id}, \"{name}\", \"{desc}\")",
            _ => $"Container({id}, \"{name}\", \"{tech}\", \"{desc}\")"
        };
    }

    /// <summary>Mermaid C4 element ids must be bare identifiers -- no spaces,
    /// punctuation, or leading digits. Model ids come from wherever the
    /// model was generated (hand-authored JSON now, an AI reading a
    /// repository later) and can't be assumed safe already.</summary>
    private static string SafeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "unknown";
        var chars = id.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
        var safe = new string(chars);
        return char.IsDigit(safe[0]) ? "_" + safe : safe;
    }

    /// <summary>Mermaid element labels are double-quoted, single-line
    /// strings -- strip characters that would break or inject into the
    /// diagram syntax rather than trying to escape them.</summary>
    private static string Escape(string text) =>
        text.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ").Trim();
}
