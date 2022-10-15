using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MarkdownReader
{
    public  class TreeviewBuilder
    {
        private static TreeViewItemExpanded MakeTree
            ((int htag, string text, string id) item, TreeViewItemExpanded parent)
            => new()
            {
                Header = item.text,
                Parent = parent,
                Tag = item.id,
                Level = item.htag
            };

        public TreeViewItemExpanded BuildTree
            (TreeViewItemExpanded tree
            , List<(int htag, string text, string id)> list
            , int oldLevel
            )
        {
            if (list.Count == 0)
            {
                return tree;
            }

            var firstListItem = list.First();
            var restOfListItems = list.Skip(1).ToList();

            // NOTE: Larger htags should be inside smaller htags.

            if (firstListItem.htag > oldLevel)
            {
                var newTree = MakeTree(firstListItem, tree);

                tree.Items.Add(newTree);

                return BuildTree(newTree, restOfListItems, firstListItem.htag);
            }
            else if (firstListItem.htag == oldLevel)
            {
                var newTree = MakeTree(firstListItem, tree.Parent!);

                tree.Parent!.Items.Add(newTree);

                return BuildTree(newTree, restOfListItems, firstListItem.htag);
            }
            else if (firstListItem.htag < oldLevel)
            {
                var Parent = FindParent(firstListItem.htag, tree);

                var newtree = MakeTree(firstListItem, Parent);

                Parent.Items.Add(newtree);

                return BuildTree(newtree, restOfListItems, firstListItem.htag);
            }

            return tree;
        }

        private static TreeViewItemExpanded FindParent(int lvl, TreeViewItemExpanded currentTree)
        {
            if (currentTree.Parent == null) { return currentTree; }

            if (currentTree.Parent.Level < lvl)
            {
                return currentTree.Parent;
            }
            else if (currentTree.Parent.Level >= lvl)
            {
                return FindParent(lvl, currentTree.Parent);
            }

            return currentTree;
        }

        public TreeViewItemExpanded FindRoot(TreeViewItemExpanded t)
        {
            if (t.Header.ToString() == "<root>")
            {
                return t;
            }

            return t.Parent!.Header switch
            {
                "<root>" => t.Parent,
                _ => FindRoot(t.Parent)
            };
        }

        public void ExpandTreeViewStructure(TreeViewItem item)
        {
            item.IsExpanded = true;

            foreach (TreeViewItem i in item.Items)
            {
                ExpandTreeViewStructure(i);
            }
        }
    }
}
