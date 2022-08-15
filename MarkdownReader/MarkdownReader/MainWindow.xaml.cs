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
        public TreeViewItemExpanded Parent { get; set; }
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
        }

        private void loadMarkdown(string markdown, string path)
        {
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

            List<(int htag, string text, string id)> chapters = new List<(int, string, string)>();
            var count = 0;

            Func<string, string> formatHeadingsAndGetChapters = (s) =>
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
            };

            var htmlLines = text.Split('\n');

            var newHtml = htmlLines
                .Select(s => formatHeadingsAndGetChapters(s))
                .Aggregate((a, b) => a + b);

            var treeResult = buildTree(new TreeViewItemExpanded { Header = "root" }, chapters, 0);

            TreeViewItem x = findRoot(treeResult);

            Expand(x);

            tvChapters.Items.Add(x);

            return newHtml;
        }

        void Expand(TreeViewItem item)
        {
            item.IsExpanded = true;

            foreach (TreeViewItem i in item.Items)
            {
                Expand(i);
            }
        }

        private void tvChapters_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = (TreeViewItem)tvChapters.SelectedItem;

            if (selectedItem == null)
            {
                return;
            }

            browser.InvokeScript("ScrollTo", selectedItem.Tag.ToString());
        }

        private void browser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
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
                    ProcessStartInfo psi = new ProcessStartInfo();

                    psi.FileName = e.Uri.ToString();
                    psi.UseShellExecute = true;

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

            if (path.Substring(0, 8) == "file:///") // Open drag and drop file.
            {
                path = path.Substring(8);
            }

            string filepath = path;

            pathToFile.Text = filepath;

            string markdown = File.ReadAllText(filepath);

            loadMarkdown(markdown, path);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();

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

        Func<TreeViewItemExpanded, TreeViewItemExpanded, TreeViewItemExpanded> cons = (t1, t2) =>
        {
            t1.Items.Add(t2);
            return t2;
        };

        Func<List<(int htag, string text, string id)>, (int htag, string text, string id)> car = (lst)
            => lst.First();

        Func<List<(int htag, string text, string id)>, List<(int htag, string text, string id)>> cdr = (lst)
            => lst.Skip(1).ToList();

        TreeViewItemExpanded buildTree(TreeViewItemExpanded tree, List<(int htag, string text, string id)> list, int oldLevel)
        {
            if (list.Count == 0)
            {
                return tree;
            }

            var c = car(list);

            if (c.htag > oldLevel)
            {
                var t = new TreeViewItemExpanded
                {
                    Header = c.text,
                    Parent = tree,
                    Tag=c.id,
                    Level= c.htag
                };

                return buildTree(cons(tree, t), cdr(list), c.htag);
            }
            else if (c.htag == oldLevel)
            {
                var t = new TreeViewItemExpanded
                {
                    Header = c.text,
                    Parent = tree.Parent,
                    Tag = c.id,
                    Level = c.htag
                };

                tree.Parent.Items.Add(t);

                return buildTree(t, cdr(list), c.htag);
            }
            else if (c.htag < oldLevel) // TODO: This part doesn't work right.
            { // TODO: Figure out how to move up exact levels eg. from 5 to 2 instead of always going up one level.

                TreeViewItemExpanded findParent(int lvl, TreeViewItemExpanded currTree)
                {
                    if (currTree.Parent == null) { return currTree; }

                    if (currTree.Parent.Level < lvl)
                    {
                        return currTree.Parent;
                    }
                    else if (currTree.Parent.Level >= lvl)
                    {
                        return findParent(lvl, currTree.Parent);
                    }

                    return currTree;
                };

                var Parent = findParent(c.htag, tree);

                var t = new TreeViewItemExpanded
                {
                    Header = c.text,
                    Parent = Parent,
                    Tag = c.id,
                    Level = c.htag
                };

                Parent.Items.Add(t);

                return buildTree(t, cdr(list), c.htag);
            }

            return tree;
        }

        TreeViewItemExpanded findRoot(TreeViewItemExpanded t)
        => t.Parent.Header switch
        {
            "root" => t.Parent,
            _ => findRoot(t.Parent)
        };
    }
}
