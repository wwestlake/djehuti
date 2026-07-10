namespace Djehuti.DjeLab.Docking;

/// <summary>Fake starting layout used to try out the docking mechanics before
/// any real pane content (graphs, code editor, BYOK chat, data tables) exists.</summary>
public static class DemoLayout
{
    public static (DockNode root, Dictionary<string, PaneDescriptor> panes) CreateDefault()
    {
        var graph = new PaneDescriptor { Title = "Graph", Kind = "graph", AccentColor = "#3fa9f5" };
        var editor = new PaneDescriptor { Title = "Editor", Kind = "editor", AccentColor = "#7dd3fc" };
        var chat = new PaneDescriptor { Title = "Chat", Kind = "chat", AccentColor = "#e8b354" };
        var data = new PaneDescriptor { Title = "Data", Kind = "data", AccentColor = "#58d68d" };
        var files = new PaneDescriptor { Title = "Files", Kind = "files", AccentColor = "#c792ea" };

        var panes = new Dictionary<string, PaneDescriptor>
        {
            [graph.Id] = graph,
            [editor.Id] = editor,
            [chat.Id] = chat,
            [data.Id] = data,
            [files.Id] = files,
        };

        var graphGroup = new TabGroupNode();
        graphGroup.PaneIds.Add(graph.Id);

        var editorGroup = new TabGroupNode();
        editorGroup.PaneIds.Add(editor.Id);

        var leftColumn = new SplitNode { Direction = SplitDirection.Column };
        leftColumn.Children.Add(graphGroup);
        leftColumn.Children.Add(editorGroup);
        leftColumn.Sizes = new List<double> { 0.65, 0.35 };

        var rightGroup = new TabGroupNode();
        rightGroup.PaneIds.Add(chat.Id);
        rightGroup.PaneIds.Add(data.Id);
        rightGroup.PaneIds.Add(files.Id);

        var root = new SplitNode { Direction = SplitDirection.Row };
        root.Children.Add(leftColumn);
        root.Children.Add(rightGroup);
        root.Sizes = new List<double> { 0.65, 0.35 };

        return (root, panes);
    }
}
