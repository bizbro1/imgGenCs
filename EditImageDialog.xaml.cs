using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace ImagenGenC
{
    public partial class EditImageDialog : Window
    {
        private DrawingAttributes _paintAttributes;
        private DrawingAttributes _eraseAttributes;
        private bool _maskVisible = true;
        public string EditPrompt => PromptTextBox.Text.Trim();
        public string MaskPngPath { get; private set; } = string.Empty;

        public EditImageDialog(string imagePath)
        {
            InitializeComponent();
            // Load image
            if (File.Exists(imagePath))
            {
                ImagePreview.Source = new BitmapImage(new System.Uri(imagePath));
            }
            // Set up drawing attributes
            _paintAttributes = new DrawingAttributes
            {
                Color = Colors.White,
                Width = 24,
                Height = 24,
                IsHighlighter = false,
                IgnorePressure = true
            };
            _eraseAttributes = new DrawingAttributes
            {
                Color = Colors.Black,
                Width = 24,
                Height = 24,
                IsHighlighter = false,
                IgnorePressure = true
            };
            MaskCanvas.DefaultDrawingAttributes = _paintAttributes;
            MaskCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void PaintButton_Click(object sender, RoutedEventArgs e)
        {
            MaskCanvas.DefaultDrawingAttributes = _paintAttributes;
            MaskCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void EraseButton_Click(object sender, RoutedEventArgs e)
        {
            MaskCanvas.DefaultDrawingAttributes = _eraseAttributes;
            MaskCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void ClearMaskButton_Click(object sender, RoutedEventArgs e)
        {
            MaskCanvas.Strokes.Clear();
        }

        private void ToggleMaskButton_Click(object sender, RoutedEventArgs e)
        {
            _maskVisible = !_maskVisible;
            MaskCanvas.Opacity = _maskVisible ? 0.5 : 0.0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Export mask as PNG
            var rtb = new RenderTargetBitmap((int)MaskCanvas.ActualWidth, (int)MaskCanvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(MaskCanvas);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            var tempPath = Path.Combine(Path.GetTempPath(), $"mask_{System.DateTime.Now.Ticks}.png");
            using (var fs = new FileStream(tempPath, FileMode.Create))
            {
                encoder.Save(fs);
            }
            MaskPngPath = tempPath;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 