namespace Djehuti.DjeLab.Docking;

public sealed record DockLayoutSnapshot(DockNode Root, Dictionary<string, PaneDescriptor> Panes);
