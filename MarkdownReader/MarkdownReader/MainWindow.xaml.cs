using Markdig;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MarkdownReader
{
    public class TreeViewItemExpanded : TreeViewItem
    {
        public new TreeViewItemExpanded? Parent { get; set; }
        public int Level { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // This allows you to drag a .md file onto the exe or shortcut of
            // this program and it will automatically be loaded.
            var args = Environment.GetCommandLineArgs();

            if (args?.Length > 1)
            {
                DisplayFile(args[1]);
            }
        }

        private void DisplayTitle(string path)
        {
            var title = Path.GetFileNameWithoutExtension(path);

            this.Title = title;
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
                string html = htmlBase + "\n<!DOCTYPE html>\n<style>body{font-family:Helvetica,Arial}</style>\n" + script;
                html += Markdig.Markdown.ToHtml(markdown, pipeline);

                var newHtml = PopulateChapters(html);

                browser.NavigateToString(newHtml);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string PopulateChapters(string text)
        {
            tvChapters.Items.Clear();

            List<(int htag, string text, string id)> chapters = new();
            var count = 0;

            string formatHeadingsAndGetChapters(string s)
            {
                if (Regex.IsMatch(s, @"<h\d\s"))
                {
                    // used to make unique ids for the headers to avoid headers
                    // with the same text from all scrolling to the same occurance.
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

            tvChapters.Items.Add(rootItem);

            return newHtml;
        }

        private void TvChapters_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = (TreeViewItem)tvChapters.SelectedItem;

            if (selectedItem == null || selectedItem.Header.ToString() == "<root>")
            {
                return;
            }

            browser.InvokeScript("ScrollTo", selectedItem.Tag.ToString());
        }

        private void Browser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            if (e.Uri == null)
            {
                // Do nothing
            }
            else if (Path.GetExtension(e.Uri.ToString()).ToUpper() == ".MD")
            {
                DisplayFile(e.Uri.ToString());
            }
            else if (e.Uri != null)
            {
                try
                {
                    // Attempt to follow link in MD file by opening it in the default browser.
                    ProcessStartInfo psi = new()
                    {
                        FileName = e.Uri.ToString(),
                        UseShellExecute = true
                    };

                    Process.Start(psi);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    e.Cancel = true;
                }
            }
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

            pathToFile.Text = path;

            string markdown = File.ReadAllText(path);

            LoadMarkdown(markdown, path);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new();

            if (ofd.ShowDialog() == true)
            {
                DisplayFile(ofd.FileName);
            }
        }

        private void MenuItem_Click_Exit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);

            DisplayFile(dataString);
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
            => t.Parent!.Header switch
            {
                "<root>" => t.Parent,
                _ => FindRoot(t.Parent)
            };

        void ExpandTreeViewStructure(TreeViewItem item)
        {
            item.IsExpanded = true;

            foreach (TreeViewItem i in item.Items)
            {
                ExpandTreeViewStructure(i);
            }
        }
    }
}
