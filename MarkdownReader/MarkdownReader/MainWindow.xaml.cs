using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MarkdownReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly MainWindowViewModel ViewModel = new(new TreeviewBuilder());

        public MainWindow()
        {
            DataContext = ViewModel;
            MainWindowViewModel.HtmlChanged += LoadHtml!;

            InitializeComponent();

            // This allows you to drag a .md file onto the exe or shortcut of
            // this program and it will automatically be loaded.
            var args = Environment.GetCommandLineArgs();

            if (args?.Length > 1)
            {
                ViewModel.FilePath = args[1];
                //DisplayFile(args[1]);
            }
        }

        private void LoadHtml(object sender, EventArgs e)
        {
            browser.NavigateToString(ViewModel.Html);
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
                ViewModel.FilePath = e.Uri.ToString();

                //DisplayFile(e.Uri.ToString());
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

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new();

            if (ofd.ShowDialog() == true)
            {
                ViewModel.FilePath = ofd.FileName;
                //DisplayFile(ofd.FileName);
            }
        }

        private void MenuItem_Click_Exit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);

            //DisplayFile(dataString);
            ViewModel.FilePath = dataString;
        }

    }
}
