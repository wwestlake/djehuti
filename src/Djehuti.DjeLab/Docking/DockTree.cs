namespace Djehuti.DjeLab.Docking;

/// <summary>
/// Pure(ish) tree-mutation helpers over a DockNode tree. Nodes are mutated in
/// place where possible; operations that can change the identity of the root
/// (e.g. a split collapsing to its single remaining child) return the
/// (possibly new) root, which callers must assign back.
/// </summary>
public static class DockTree
{
    public static DockNode? FindNode(DockNode node, string id)
    {
        if (node.Id == id) return node;
        if (node is SplitNode split)
        {
            foreach (var child in split.Children)
            {
                var found = FindNode(child, id);
                if (found != null) return found;
            }
        }
        return null;
    }

    public static TabGroupNode? FindGroupContainingPane(DockNode node, string paneId)
    {
        if (node is TabGroupNode group && group.PaneIds.Contains(paneId)) return group;
        if (node is SplitNode split)
        {
            foreach (var child in split.Children)
            {
                var found = FindGroupContainingPane(child, paneId);
                if (found != null) return found;
            }
        }
        return null;
    }

    public static bool TryFindParent(DockNode root, string targetId, out SplitNode? parent, out int index)
    {
        parent = null;
        index = -1;
        return TryFindParentRec(root, targetId, ref parent, ref index);
    }

    private static bool TryFindParentRec(DockNode node, string targetId, ref SplitNode? parent, ref int index)
    {
        if (node is not SplitNode split) return false;

        for (int i = 0; i < split.Children.Count; i++)
        {
            if (split.Children[i].Id == targetId)
            {
                parent = split;
                index = i;
                return true;
            }
        }
        foreach (var child in split.Children)
        {
            if (TryFindParentRec(child, targetId, ref parent, ref index)) return true;
        }
        return false;
    }

    public static void InsertAsTab(DockNode root, string targetGroupId, string paneId)
    {
        if (FindNode(root, targetGroupId) is not TabGroupNode target) return;
        if (!target.PaneIds.Contains(paneId)) target.PaneIds.Add(paneId);
        target.ActiveIndex = target.PaneIds.IndexOf(paneId);
    }

    /// <summary>Removes a pane from wherever it currently lives, collapsing an
    /// emptied tab group and, if that leaves a split with only one child, the
    /// split itself. Returns the (possibly new) root.</summary>
    public static DockNode RemovePane(DockNode root, string paneId)
    {
        var group = FindGroupContainingPane(root, paneId);
        if (group == null) return root;

        group.PaneIds.Remove(paneId);
        if (group.ActiveIndex >= group.PaneIds.Count)
            group.ActiveIndex = Math.Max(0, group.PaneIds.Count - 1);

        if (group.PaneIds.Count > 0) return root;

        return RemoveNodeAndCollapse(root, group.Id);
    }

    private static DockNode RemoveNodeAndCollapse(DockNode root, string nodeId)
    {
        if (root.Id == nodeId) return root; // caller-level concern; a lone empty root has nothing to collapse into

        if (!TryFindParent(root, nodeId, out var parent, out var index) || parent == null) return root;

        parent.Children.RemoveAt(index);
        if (parent.Sizes.Count > index) parent.Sizes.RemoveAt(index);
        RebalanceSizes(parent);

        if (parent.Children.Count != 1) return root;

        var remaining = parent.Children[0];
        if (parent.Id == root.Id) return remaining;

        if (TryFindParent(root, parent.Id, out var grandParent, out var parentIndex) && grandParent != null)
        {
            grandParent.Children[parentIndex] = remaining;
        }
        return root;
    }

    private static void RebalanceSizes(SplitNode split)
    {
        if (split.Children.Count == 0) { split.Sizes = new List<double>(); return; }
        var even = 1.0 / split.Children.Count;
        split.Sizes = Enumerable.Repeat(even, split.Children.Count).ToList();
    }

    /// <summary>Moves a pane into a tab strip at a specific index -- covers both
    /// reordering within the same group and merging into a different group.</summary>
    public static DockNode MoveToTabStrip(DockNode root, string targetGroupId, string paneId, int insertIndex)
    {
        root = RemovePane(root, paneId);
        if (FindNode(root, targetGroupId) is not TabGroupNode target) return root;

        insertIndex = Math.Clamp(insertIndex, 0, target.PaneIds.Count);
        target.PaneIds.Insert(insertIndex, paneId);
        target.ActiveIndex = insertIndex;
        return root;
    }

    /// <summary>Moves a pane to a zone (Center = merge as tab, edges = split the
    /// target group in that direction) relative to a target group's content body.</summary>
    public static DockNode MoveToZone(DockNode root, string targetGroupId, string paneId, DockZone zone)
    {
        var sourceGroup = FindGroupContainingPane(root, paneId);
        if (sourceGroup != null && sourceGroup.Id == targetGroupId && zone == DockZone.Center)
            return root; // dropping onto its own group's body is a no-op

        root = RemovePane(root, paneId);

        if (FindNode(root, targetGroupId) == null)
        {
            // Target group was collapsed away by its own removal (rare edge case
            // for a 2-pane group where source == target); fall back to docking
            // against the whole tree instead of losing the pane.
            return SplitAndInsert(root, root.Id, paneId, DockZone.Right);
        }

        return SplitAndInsert(root, targetGroupId, paneId, zone);
    }

    private static DockNode SplitAndInsert(DockNode root, string targetGroupId, string paneId, DockZone zone)
    {
        if (zone == DockZone.Center)
        {
            InsertAsTab(root, targetGroupId, paneId);
            return root;
        }

        var direction = zone is DockZone.Left or DockZone.Right ? SplitDirection.Row : SplitDirection.Column;
        var newFirst = zone is DockZone.Left or DockZone.Top;

        var newGroup = new TabGroupNode();
        newGroup.PaneIds.Add(paneId);

        if (root.Id == targetGroupId)
        {
            var split = new SplitNode { Direction = direction };
            if (newFirst) { split.Children.Add(newGroup); split.Children.Add(root); }
            else { split.Children.Add(root); split.Children.Add(newGroup); }
            split.Sizes = new List<double> { 0.5, 0.5 };
            return split;
        }

        if (!TryFindParent(root, targetGroupId, out var parent, out var index) || parent == null) return root;

        var existingTarget = parent.Children[index];

        if (parent.Direction == direction)
        {
            var insertIndex = newFirst ? index : index + 1;
            var existingSize = parent.Sizes[index];
            var half = existingSize / 2.0;

            parent.Children.Insert(insertIndex, newGroup);
            parent.Sizes.Insert(insertIndex, half);
            var targetNewIndex = parent.Children.IndexOf(existingTarget);
            parent.Sizes[targetNewIndex] = half;
        }
        else
        {
            var innerSplit = new SplitNode { Direction = direction };
            if (newFirst) { innerSplit.Children.Add(newGroup); innerSplit.Children.Add(existingTarget); }
            else { innerSplit.Children.Add(existingTarget); innerSplit.Children.Add(newGroup); }
            innerSplit.Sizes = new List<double> { 0.5, 0.5 };
            parent.Children[index] = innerSplit;
        }

        return root;
    }
}
