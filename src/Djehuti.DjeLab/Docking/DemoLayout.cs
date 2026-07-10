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
        var flow = new PaneDescriptor { Title = "Flow", Kind = "flow", AccentColor = "#e8b354" };
        var ibis = new PaneDescriptor { Title = "Ibis", Kind = "chat", AccentColor = "#7dd3fc", ChatPersona = "ibis" };
        var professor = new PaneDescriptor { Title = "Seshat", Kind = "chat", AccentColor = "#e8b354", ChatPersona = "seshat" };
        var data = new PaneDescriptor { Title = "Data", Kind = "data", AccentColor = "#58d68d" };
        var files = new PaneDescriptor { Title = "Files", Kind = "files", AccentColor = "#c792ea" };

        var panes = new Dictionary<string, PaneDescriptor>
        {
            [graph.Id] = graph,
            [editor.Id] = editor,
            [console.Id] = console,
            [flow.Id] = flow,
            [ibis.Id] = ibis,
            [professor.Id] = professor,
            [data.Id] = data,
            [files.Id] = files,
        };

        var flowGroup = new TabGroupNode();
        flowGroup.PaneIds.Add(graph.Id);
        flowGroup.PaneIds.Add(flow.Id);
        flowGroup.PaneIds.Add(editor.Id);
        flowGroup.ActiveIndex = 1;

        var consoleGroup = new TabGroupNode();
        consoleGroup.PaneIds.Add(console.Id);

        var rightGroup = new TabGroupNode();
        rightGroup.PaneIds.Add(ibis.Id);
        rightGroup.PaneIds.Add(professor.Id);
        rightGroup.PaneIds.Add(data.Id);
        rightGroup.PaneIds.Add(files.Id);

        var root = new SplitNode { Direction = SplitDirection.Row };
        var topArea = new SplitNode { Direction = SplitDirection.Row };
        topArea.Children.Add(flowGroup);
        topArea.Children.Add(rightGroup);
        topArea.Sizes = new List<double> { 0.72, 0.28 };

        root.Children.Add(topArea);
        root.Children.Add(consoleGroup);
        root.Sizes = new List<double> { 0.78, 0.22 };

        return (root, panes);
    }
}
