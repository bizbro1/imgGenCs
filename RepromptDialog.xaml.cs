using System.Windows;
using System.Windows.Media.Imaging;

namespace ImagenGenC
{
    public partial class RepromptDialog : Window
    {
        public string NewPrompt => PromptTextBox.Text.Trim();
        public string MaskPath => MaskPathTextBox.Text.Trim();

        public RepromptDialog(string imagePath, string initialPrompt)
        {
            InitializeComponent();
            if (System.IO.File.Exists(imagePath))
            {
                ImagePreview.Source = new BitmapImage(new System.Uri(imagePath));
            }
            PromptTextBox.Text = string.Empty;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseMaskButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG Mask (*.png)|*.png",
                Title = "Select Mask PNG File"
            };
            if (dialog.ShowDialog() == true)
            {
                MaskPathTextBox.Text = dialog.FileName;
            }
        }
    }
} 