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
        private readonly TreeviewBuilder treeviewBuilder;

        public MainWindowViewModel(TreeviewBuilder treeviewBuilder)
        {
            this.treeviewBuilder = treeviewBuilder;

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

        private void SetTitle(string path)
        {
            var title = Path.GetFileNameWithoutExtension(path);

            Title = title;
        }

        private void GenerateMarkdown(string markdown, string path)
        {
            SetTitle(path);

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

                var newHtml = PopulateSideBarChapters(html);

                Html = newHtml;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string PopulateSideBarChapters(string text)
        {
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

            var treeResult = this.treeviewBuilder.BuildTree(new TreeViewItemExpanded { Header = "<root>" }, chapters, 0);

            TreeViewItem rootItem = this.treeviewBuilder.FindRoot(treeResult);

            this.treeviewBuilder.ExpandTreeViewStructure(rootItem);

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

            GenerateMarkdown(markdown, path);
        }
    }
}
