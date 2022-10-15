using Markdig;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using File = System.IO.File;

namespace MarkdownReader
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel()
        {
            FilePath = null;
            Title = null;
            Html = null;
            SideBarChapters = new ObservableCollection<TreeViewItem>();
        }

        public ObservableCollection<TreeViewItem> SideBarChapters { get; set; }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private string? filePath;
        public string? FilePath
        {
            get => this.filePath;
            set
            {
                if (this.filePath != value && value != null)
                {
                    filePath = value;
                    PropertyChanged(this, new PropertyChangedEventArgs(nameof(FilePath)));
                    DisplayFile(value);
                }
            }
        }

        private string? title;
        public string? Title
        {
            get => title;
            set
            {
                if (this.title != value)
                {
                    title = value;
                    PropertyChanged(this, new PropertyChangedEventArgs(nameof(Title)));
                }
            }
        }

        private string? html;
        public string? Html
        {
            get => html;
            set
            {
                if (this.html != value)
                {
                    html = value;
                    PropertyChanged(this, new PropertyChangedEventArgs(nameof(Html)));
                    OnHtmlChanged(EventArgs.Empty);
                }
            }
        }

        public static event EventHandler? HtmlChanged;


        protected virtual void OnHtmlChanged(EventArgs e)
        {
            HtmlChanged?.Invoke(this, e);
        }

        private void DisplayTitle(string path)
        {
            var title = Path.GetFileNameWithoutExtension(path);

            Title = title;
        }

        private void LoadMarkdown(string markdown, string path)
        {
            DisplayTitle(path);

            try
            {
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UsePipeTables()
                    .UseMediaLinks()
                    .Build();

                string htmlBase = $"<base href=\"{path}\">";
                string script = "<script>function ScrollTo(id){document.getElementById(id).scrollIntoView();}</script>\n";
                string html = htmlBase + "\n<!DOCTYPE html>\n<style>body{font-family:Helvetica,Arial}table, th, td {border-collapse: collapse; border: 1px solid black;padding:.5em;}</style>\n" + script;
                html += Markdig.Markdown.ToHtml(markdown, pipeline);

                var newHtml = PopulateChapters(html);

                //browser.NavigateToString(newHtml);
                Html = newHtml;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string PopulateChapters(string text)
        {
            //tvChapters.Items.Clear();
            SideBarChapters.Clear();

            List<(int htag, string text, string id)> chapters = new();
            var count = 0;

            string formatHeadingsAndGetChapters(string s)
            {
                if (Regex.IsMatch(s, @"<h\d\s"))
                {
                    // used to make unique ids for the headers to avoid headers,
                    // with the same text, from all scrolling to the same occurance.
                    count++;
                    var id = $"heading{count}";
                    s = Regex.Replace(s, "id=\".+\"", $"id=\"{id}\"");

                    // header tag eg. h1
                    var htag = Regex.Match(s, @"<h(\d)\s").Groups[1].Value;
                    var text = Regex.Match(s, @">(.+)<").Groups[1].Value;

                    chapters.Add((int.Parse(htag), text, id));
                }

                return s;
            }

            var htmlLines = text.Split('\n');

            var newHtml = htmlLines
                .Select(s => formatHeadingsAndGetChapters(s))
                .Aggregate((a, b) => a + "\n" + b);

            var treeResult = BuildTree(new TreeViewItemExpanded { Header = "<root>" }, chapters, 0);

            TreeViewItem rootItem = FindRoot(treeResult);

            ExpandTreeViewStructure(rootItem);

            //tvChapters.Items.Add(rootItem);
            SideBarChapters.Add(rootItem);

            return newHtml;
        }

        private void DisplayFile(string path)
        {
            if (Path.GetExtension(path).ToUpper() != ".MD")
            {
                MessageBox.Show("File must be a markdown file (.md)", "Error");
                return;
            }

            if (path[..8] == "file:///") // Open drag and drop file.
            {
                path = path[8..];
            }

            string markdown = File.ReadAllText(path);

            LoadMarkdown(markdown, path);
        }

        private static TreeViewItemExpanded MakeTree
            ((int htag, string text, string id) item, TreeViewItemExpanded parent)
            => new()
            {
                Header = item.text,
                Parent = parent,
                Tag = item.id,
                Level = item.htag
            };

        private TreeViewItemExpanded BuildTree
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

        private TreeViewItemExpanded FindRoot(TreeViewItemExpanded t)
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

        private void ExpandTreeViewStructure(TreeViewItem item)
        {
            item.IsExpanded = true;

            foreach (TreeViewItem i in item.Items)
            {
                ExpandTreeViewStructure(i);
            }
        }
    }
}
