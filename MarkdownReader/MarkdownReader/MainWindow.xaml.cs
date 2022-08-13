using Markdig;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MarkdownReader
{
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

                var htmlBase = $"<base href=\"{path}\">";

                string html = htmlBase + "\n<!DOCTYPE html>\n<style>body{font-family:Helvetica,Arial}</style>\n";
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
            //int count = 0;

            var htmlArray = text.Split('\n');

            Func<string, string> func = (s) =>
            {
                if (Regex.IsMatch(s, @"<h\d\s"))
                {
                    //count++;

                    //s = Regex.Replace(s, @"<h(\d)\s", @"$0 href='#" + count + "' ");

                    var text = Regex.Match(s, @">(.+)<").Groups[1].Value;

                    var item = new TreeViewItem();
                    item.Header = text;

                    tvChapters.Items.Add(item);
                }

                return s;
            };

            var newHtml = "";
            newHtml = htmlArray.Select(s => func(s)).ToList().Aggregate((a, b) => a + b);

            return newHtml;
        }

        private void tvChapters_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var headerText = ((TreeViewItem)tvChapters.SelectedItem).Header.ToString();
            headerText = headerText?.ToLower().Replace(' ', '-');

            //browser.InvokeScript("eval", "document.getElementById(\"" + headerText + "\").scrollIntoView();");
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


    }
}
