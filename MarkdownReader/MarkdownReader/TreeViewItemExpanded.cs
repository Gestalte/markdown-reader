using System.Windows.Controls;

namespace MarkdownReader
{
    public class TreeViewItemExpanded : TreeViewItem
    {
        public new TreeViewItemExpanded? Parent { get; set; }
        public int Level { get; set; }
    }
}
