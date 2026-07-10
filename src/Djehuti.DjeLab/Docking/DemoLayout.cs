namespace Djehuti.DjeLab.Docking;

/// <summary>Fake starting layout used to try out the docking mechanics before
/// any real pane content (graphs, code editor, BYOK chat, data tables) exists.</summary>
public static class DemoLayout
{
    public static (DockNode root, Dictionary<string, PaneDescriptor> panes) CreateDefault()
    {
        var graph = new PaneDescriptor { Title = "Graph", Kind = "graph", AccentColor = "#3fa9f5" };
        var editor = new PaneDescriptor { Title = "Editor", Kind = "editor", AccentColor = "#7dd3fc" };
        var console = new PaneDescriptor { Title = "Log", Kind = "console", AccentColor = "#8fa0bd" };
        var ibis = new PaneDescriptor { Title = "Ibis", Kind = "chat", AccentColor = "#7dd3fc", ChatPersona = "ibis" };
        var professor = new PaneDescriptor { Title = "Seshat", Kind = "chat", AccentColor = "#e8b354", ChatPersona = "seshat" };
        var data = new PaneDescriptor { Title = "Data", Kind = "data", AccentColor = "#58d68d" };
        var files = new PaneDescriptor { Title = "Files", Kind = "files", AccentColor = "#c792ea" };

        var panes = new Dictionary<string, PaneDescriptor>
        {
            [graph.Id] = graph,
            [editor.Id] = editor,
            [console.Id] = console,
            [ibis.Id] = ibis,
            [professor.Id] = professor,
            [data.Id] = data,
            [files.Id] = files,
        };

        var graphGroup = new TabGroupNode();
        graphGroup.PaneIds.Add(graph.Id);

        var editorGroup = new TabGroupNode();
        editorGroup.PaneIds.Add(editor.Id);

        var consoleGroup = new TabGroupNode();
        consoleGroup.PaneIds.Add(console.Id);

        var leftColumn = new SplitNode { Direction = SplitDirection.Column };
        leftColumn.Children.Add(graphGroup);
        leftColumn.Children.Add(editorGroup);
        leftColumn.Sizes = new List<double> { 0.62, 0.38 };

        var rightGroup = new TabGroupNode();
        rightGroup.PaneIds.Add(ibis.Id);
        rightGroup.PaneIds.Add(professor.Id);
        rightGroup.PaneIds.Add(data.Id);
        rightGroup.PaneIds.Add(files.Id);

        var root = new SplitNode { Direction = SplitDirection.Row };
        var topArea = new SplitNode { Direction = SplitDirection.Row };
        topArea.Children.Add(leftColumn);
        topArea.Children.Add(rightGroup);
        topArea.Sizes = new List<double> { 0.65, 0.35 };

        root.Children.Add(topArea);
        root.Children.Add(consoleGroup);
        root.Sizes = new List<double> { 0.78, 0.22 };

        return (root, panes);
    }
}
