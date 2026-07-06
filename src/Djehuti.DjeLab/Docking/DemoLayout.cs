namespace Djehuti.DjeLab.Docking;

/// <summary>Fake starting layout used to try out the docking mechanics before
/// any real pane content (graphs, DSL console, BYOK chat, data tables) exists.</summary>
public static class DemoLayout
{
    public static (DockNode root, Dictionary<string, PaneDescriptor> panes) CreateDefault()
    {
        var graph = new PaneDescriptor { Title = "Graph", Kind = "graph", AccentColor = "#3fa9f5" };
        var console = new PaneDescriptor { Title = "Console", Kind = "console", AccentColor = "#8fa0bd" };
        var chat = new PaneDescriptor { Title = "Chat", Kind = "chat", AccentColor = "#e8b354" };
        var data = new PaneDescriptor { Title = "Data", Kind = "data", AccentColor = "#58d68d" };

        var panes = new Dictionary<string, PaneDescriptor>
        {
            [graph.Id] = graph,
            [console.Id] = console,
            [chat.Id] = chat,
            [data.Id] = data,
        };

        var graphGroup = new TabGroupNode();
        graphGroup.PaneIds.Add(graph.Id);

        var consoleGroup = new TabGroupNode();
        consoleGroup.PaneIds.Add(console.Id);

        var leftColumn = new SplitNode { Direction = SplitDirection.Column };
        leftColumn.Children.Add(graphGroup);
        leftColumn.Children.Add(consoleGroup);
        leftColumn.Sizes = new List<double> { 0.65, 0.35 };

        var rightGroup = new TabGroupNode();
        rightGroup.PaneIds.Add(chat.Id);
        rightGroup.PaneIds.Add(data.Id);

        var root = new SplitNode { Direction = SplitDirection.Row };
        root.Children.Add(leftColumn);
        root.Children.Add(rightGroup);
        root.Sizes = new List<double> { 0.65, 0.35 };

        return (root, panes);
    }
}
