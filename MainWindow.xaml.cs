using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Microsoft.Win32;
using Azure.AI.OpenAI;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Data;
using System.Drawing;
using System.Windows.Ink;
using System.Windows.Media;

namespace ImagenGenC
{
    public class PromptHistoryItem
    {
        public string UserPrompt { get; set; } = string.Empty;
        public string FullPrompt { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string ThemeName { get; set; } = string.Empty;
        public DateTime Created { get; set; } = DateTime.Now;
    }

    public class ThemeSubcategory
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SaveFolder { get; set; } = string.Empty;
    }

    public class ThemeItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SaveFolder { get; set; } = string.Empty;
        public List<ThemeSubcategory> Subcategories { get; set; } = new List<ThemeSubcategory>();
    }

    public class ThemeCard
    {
        public ThemeItem Theme { get; set; } = new ThemeItem();
        public bool IsExpanded { get; set; }
        public bool IsEditing { get; set; }
        public string NewSubcategoryName { get; set; } = string.Empty;
        public string NewSubcategoryDescription { get; set; } = string.Empty;
        public string NewSubcategorySaveFolder { get; set; } = string.Empty;
    }

    public class AnalyticsData
    {
        public int TotalGenerations { get; set; }
        public int SuccessfulGenerations { get; set; }
        public int FailedGenerations { get; set; }
        public double TotalCost { get; set; }
        public Dictionary<string, int> ThemeUsage { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> PromptUsage { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ModelUsage { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, double> ModelCosts { get; set; } = new Dictionary<string, double>();
        public List<GenerationRecord> RecentGenerations { get; set; } = new List<GenerationRecord>();
    }

    public class GenerationRecord
    {
        public DateTime Timestamp { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-image-1";
        public bool Success { get; set; }
        public double Cost { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NewThemeCard { }

    public class ThemeCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ThemeTemplate { get; set; }
        public DataTemplate? NewThemeTemplate { get; set; }
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is NewThemeCard)
                return NewThemeTemplate!;
            return ThemeTemplate!;
        }
    }

    public partial class MainWindow : Window
    {
        private OpenAIClient _openAIClient;
        private readonly List<PromptHistoryItem> _promptHistory;
        private string? _currentImagePath;
        private const int MaxHistoryItems = 20;
        private const string PlaceholderText = "Enter your image prompt here...";
        private string _theme = string.Empty;
        private string _details = string.Empty;
        private const string ThemeFile = "theme.txt";
        private const string DetailsFile = "details.txt";
        private const string ApiKeyFile = "api_key.txt";
        private const string ThemesFile = "themes.json";
        private const string HistoryFile = "history.json";
        private const string AnalyticsFile = "analytics.json";
        private List<ThemeItem> _themes = new List<ThemeItem>();
        private ThemeItem? _selectedTheme = null;
        private AnalyticsData _analytics = new AnalyticsData();
        private ThemeSubcategory? _selectedSubcategory = null;
        private bool _editMode = false;
        private DrawingAttributes _paintAttributes = new DrawingAttributes { Color = Colors.White, Width = 24, Height = 24, IsHighlighter = false, IgnorePressure = true };
        private DrawingAttributes _eraseAttributes = new DrawingAttributes { Color = Colors.Black, Width = 24, Height = 24, IsHighlighter = false, IgnorePressure = true };
        private bool _maskVisible = true;

        public MainWindow()
        {
            InitializeComponent();
            _promptHistory = new List<PromptHistoryItem>();
            LoadThemes();
            LoadApiKeyToSettingsTab();
            LoadHistory();
            LoadAnalytics();
            
            // Initialize OpenAI service
            var apiKey = LoadApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                System.Windows.MessageBox.Show("Please set your OpenAI API key in the settings.", "API Key Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _openAIClient = new OpenAIClient(apiKey, new OpenAIClientOptions());

            // Set up TextBox behavior
            PromptTextBox.Text = PlaceholderText;
            PromptTextBox.Foreground = System.Windows.Media.Brushes.Gray;

            PromptTextBox.GotFocus += (s, e) => {
                if (PromptTextBox.Text == PlaceholderText)
                {
                    PromptTextBox.Text = "";
                    PromptTextBox.Foreground = System.Windows.Media.Brushes.Black;
                }
            };

            PromptTextBox.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
                {
                    PromptTextBox.Text = PlaceholderText;
                    PromptTextBox.Foreground = System.Windows.Media.Brushes.Gray;
                }
            };
        }

        private void LoadThemes()
        {
            try
            {
                if (File.Exists(ThemesFile))
                {
                    var json = File.ReadAllText(ThemesFile);
                    _themes = System.Text.Json.JsonSerializer.Deserialize<List<ThemeItem>>(json) ?? new List<ThemeItem>();
                    System.Diagnostics.Debug.WriteLine($"Loaded {_themes.Count} themes from file");
                }
                else
                {
                    _themes = new List<ThemeItem>();
                    System.Diagnostics.Debug.WriteLine("No themes file found, starting with empty list");
                }

                // Insert NewThemeCard at the start
                var themeList = new List<object> { new NewThemeCard() };
                themeList.AddRange(_themes);
                ThemeGrid.ItemsSource = null;
                ThemeGrid.ItemsSource = themeList;

                // Update theme dropdowns
                var themeComboList = new List<ThemeItem> { new ThemeItem { Name = "All Themes" } };
                themeComboList.AddRange(_themes);

                ThemeComboBox.ItemsSource = null;
                ThemeComboBox.ItemsSource = themeComboList;
                ThemeComboBox.DisplayMemberPath = "Name";
                if (themeComboList.Count > 0)
                {
                    ThemeComboBox.SelectedIndex = 0;
                    _selectedTheme = themeComboList[0];
                }

                GalleryThemeComboBox.ItemsSource = null;
                GalleryThemeComboBox.ItemsSource = themeComboList;
                GalleryThemeComboBox.DisplayMemberPath = "Name";
                if (themeComboList.Count > 0)
                {
                    GalleryThemeComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading themes: {ex.Message}");
                System.Windows.MessageBox.Show($"Error loading themes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveThemes()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_themes);
                File.WriteAllText(ThemesFile, json);
                System.Diagnostics.Debug.WriteLine($"Saved {_themes.Count} themes to file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving themes: {ex.Message}");
                System.Windows.MessageBox.Show($"Error saving themes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSubcategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is ThemeCard card)
            {
                if (string.IsNullOrEmpty(card.NewSubcategoryName))
                {
                    System.Windows.MessageBox.Show("Subcategory name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var subcategory = new ThemeSubcategory
                {
                    Name = card.NewSubcategoryName,
                    Description = card.NewSubcategoryDescription,
                    SaveFolder = card.NewSubcategorySaveFolder
                };

                card.Theme.Subcategories.Add(subcategory);
                SaveThemes();
                LoadThemes();

                // Clear input fields
                card.NewSubcategoryName = string.Empty;
                card.NewSubcategoryDescription = string.Empty;
                card.NewSubcategorySaveFolder = string.Empty;
            }
        }

        private void DeleteSubcategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button &&
                button.DataContext is ThemeSubcategory subcategory &&
                button.Tag is ThemeCard card)
            {
                card.Theme.Subcategories.Remove(subcategory);
                SaveThemes();
                LoadThemes();
            }
        }

        private void BrowseSaveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (sender is System.Windows.Controls.Button button)
                {
                    if (button.DataContext is ThemeCard card)
                    {
                        card.NewSubcategorySaveFolder = dialog.SelectedPath;
                    }
                }
            }
        }

        private string BuildFullPrompt(string userPrompt)
        {
            var sb = new StringBuilder();
            if (_selectedTheme != null)
            {
                sb.AppendLine(_selectedTheme.Name);
                if (!string.IsNullOrWhiteSpace(_selectedTheme.Description))
                {
                    sb.AppendLine(_selectedTheme.Description);
                }
            }
            if (_selectedSubcategory != null)
            {
                sb.AppendLine(_selectedSubcategory.Name);
                if (!string.IsNullOrWhiteSpace(_selectedSubcategory.Description))
                {
                    sb.AppendLine(_selectedSubcategory.Description);
                }
            }
            sb.AppendLine(userPrompt);
            return sb.ToString();
        }

        private string LoadApiKey()
        {
            try
            {
                return File.ReadAllText("api_key.txt");
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task<string?> GenerateImageWithOpenAIAsync(string prompt, string size, string quality)
        {
            var apiKey = LoadApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                System.Windows.MessageBox.Show("OpenAI API key is missing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            var url = "https://api.openai.com/v1/images/generations";
            var requestBody = new
            {
                model = "gpt-image-1",
                prompt = prompt,
                size = size,
                quality = quality
            };
            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            using (var client = new HttpClient())
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                var response = await client.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                // Log the response to a file for debugging
                try
                {
                    File.AppendAllText("openai_log.txt", $"\n---\n{DateTime.Now}\nRequest: {json}\nResponse: {responseString}\n---\n");
                }
                catch { }

                if (!response.IsSuccessStatusCode)
                {
                    System.Windows.MessageBox.Show($"OpenAI API error: {response.StatusCode}\n{responseString}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
                // Parse the URL or b64_json from the response
                using (var doc = System.Text.Json.JsonDocument.Parse(responseString))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("data", out var dataElem) && dataElem.GetArrayLength() > 0)
                    {
                        var first = dataElem[0];
                        if (first.TryGetProperty("url", out var urlElem))
                        {
                            return urlElem.GetString();
                        }
                        else if (first.TryGetProperty("b64_json", out var b64Elem))
                        {
                            // Save the base64 string to a temp file and return the file path
                            var base64String = b64Elem.GetString();
                            if (!string.IsNullOrEmpty(base64String))
                            {
                                var imageData = Convert.FromBase64String(base64String);
                                var tempPath = Path.Combine(Path.GetTempPath(), $"generated_image_{DateTime.Now.Ticks}.png");
                                File.WriteAllBytes(tempPath, imageData);
                                return tempPath;
                            }
                        }
                    }
                }
                System.Windows.MessageBox.Show($"No image URL or b64_json found in OpenAI response.\n\nFull response:\n{responseString}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a prompt.", "Input Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                GenerateButton.IsEnabled = false;
                var userPrompt = PromptTextBox.Text;
                var fullPrompt = BuildFullPrompt(userPrompt);
                
                // Get selected options
                var sizeItem = SizeComboBox.SelectedItem as ComboBoxItem;
                var qualityItem = QualityComboBox.SelectedItem as ComboBoxItem;
                var formatItem = FormatComboBox.SelectedItem as ComboBoxItem;

                if (sizeItem?.Content == null || qualityItem?.Content == null || formatItem?.Content == null)
                {
                    System.Windows.MessageBox.Show("Please select all image options.", "Options Required", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var size = sizeItem.Content.ToString() ?? "1024x1024";
                var quality = qualityItem.Content.ToString()?.ToLower() ?? "standard";
                var format = formatItem.Content.ToString()?.ToLower() ?? "png";

                // Call OpenAI API directly
                var imagePathOrUrl = await GenerateImageWithOpenAIAsync(fullPrompt, size, quality);
                if (string.IsNullOrEmpty(imagePathOrUrl))
                {
                    return;
                }

                byte[] imageData;
                if (imagePathOrUrl.StartsWith("http://") || imagePathOrUrl.StartsWith("https://"))
                {
                    using (var httpClient = new HttpClient())
                    {
                        imageData = await httpClient.GetByteArrayAsync(imagePathOrUrl);
                    }
                }
                else
                {
                    imageData = File.ReadAllBytes(imagePathOrUrl);
                }

                var image = new BitmapImage();
                using (var ms = new MemoryStream(imageData))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
                GeneratedImage.Source = image;

                // Save image temporarily
                var tempPath = Path.Combine(Path.GetTempPath(), $"generated_image_{DateTime.Now.Ticks}.{format}");
                File.WriteAllBytes(tempPath, imageData);
                _currentImagePath = tempPath;
                RepromptButton.IsEnabled = true;

                // Auto-save to theme or subcategory folder if set
                string? saveFolder = null;
                if (_selectedSubcategory != null && !string.IsNullOrWhiteSpace(_selectedSubcategory.SaveFolder) && Directory.Exists(_selectedSubcategory.SaveFolder))
                {
                    saveFolder = _selectedSubcategory.SaveFolder;
                }
                else if (_selectedTheme != null && !string.IsNullOrWhiteSpace(_selectedTheme.SaveFolder) && Directory.Exists(_selectedTheme.SaveFolder))
                {
                    saveFolder = _selectedTheme.SaveFolder;
                }
                if (!string.IsNullOrWhiteSpace(saveFolder))
                {
                    try
                    {
                        var fileName = $"generated_image_{DateTime.Now:yyyyMMdd_HHmmss}.{format}";
                        var savePath = Path.Combine(saveFolder, fileName);
                        File.WriteAllBytes(savePath, imageData);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to auto-save image to folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // Add to history (user prompt + full prompt + image path + theme)
                _promptHistory.Insert(0, new PromptHistoryItem 
                { 
                    UserPrompt = userPrompt,
                    FullPrompt = fullPrompt,
                    ImagePath = tempPath, 
                    ThemeName = _selectedTheme?.Name ?? "", 
                    Created = DateTime.Now 
                });
                if (_promptHistory.Count > MaxHistoryItems)
                {
                    _promptHistory.RemoveAt(_promptHistory.Count - 1);
                }
                SaveHistory();
                HistoryListView.ItemsSource = null;
                HistoryListView.ItemsSource = _promptHistory;

                // Track generation
                TrackGeneration(userPrompt, _selectedTheme?.Name ?? "", size, quality, true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"An error occurred: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentImagePath))
            {
                System.Windows.MessageBox.Show("No image to save.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|WebP Image|*.webp",
                Title = "Save Generated Image"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    if (_currentImagePath.StartsWith("http://") || _currentImagePath.StartsWith("https://"))
                    {
                        using (var httpClient = new System.Net.Http.HttpClient())
                        {
                            var imageBytes = await httpClient.GetByteArrayAsync(_currentImagePath);
                            File.WriteAllBytes(saveDialog.FileName, imageBytes);
                        }
                    }
                    else
                    {
                        File.Copy(_currentImagePath, saveDialog.FileName, true);
                    }
                    System.Windows.MessageBox.Show("Image saved successfully!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to save image: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            GeneratedImage.Source = null;
            PromptTextBox.Clear();
            _currentImagePath = null;
            RepromptButton.IsEnabled = false;
        }

        private void HistoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryListView.SelectedItem is PromptHistoryItem selectedItem)
            {
                // Load the selected image
                if (File.Exists(selectedItem.ImagePath))
                {
                    var image = new BitmapImage(new Uri(selectedItem.ImagePath));
                    GeneratedImage.Source = image;
                    _currentImagePath = selectedItem.ImagePath;
                    RepromptButton.IsEnabled = true;
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = LoadApiKey();
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter your OpenAI API key:", 
                "API Key Settings", 
                apiKey);

            if (!string.IsNullOrEmpty(input))
            {
                try
                {
                    File.WriteAllText("api_key.txt", input);
                    _openAIClient = new OpenAIClient(input, new OpenAIClientOptions());
                    System.Windows.MessageBox.Show("API key saved successfully!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to save API key: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadApiKeyToSettingsTab()
        {
            if (File.Exists(ApiKeyFile))
            {
                ApiKeyTextBox.Text = File.ReadAllText(ApiKeyFile);
            }
        }

        private void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var key = ApiKeyTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(key))
            {
                File.WriteAllText(ApiKeyFile, key);
                System.Windows.MessageBox.Show("API key saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadHistory()
        {
            if (File.Exists(HistoryFile))
            {
                var json = File.ReadAllText(HistoryFile);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<PromptHistoryItem>>(json);
                if (items != null)
                {
                    _promptHistory.Clear();
                    _promptHistory.AddRange(items);
                }
            }
            HistoryListView.ItemsSource = null;
            HistoryListView.ItemsSource = _promptHistory;
        }

        private void SaveHistory()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_promptHistory);
            File.WriteAllText(HistoryFile, json);
        }

        private void GalleryThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GalleryThemeComboBox.SelectedItem is ThemeItem selectedTheme)
            {
                if (selectedTheme.Name == "All Themes")
                {
                    // Show all items if "All Themes" is selected
                    GalleryListView.ItemsSource = _promptHistory;
                    GallerySubcategoryComboBox.ItemsSource = null;
                    GallerySubcategoryComboBox.IsEnabled = false;
                }
                else
                {
                    // Filter the gallery items by the selected theme
                    var filteredItems = _promptHistory.Where(item => item.ThemeName == selectedTheme.Name).ToList();
                    GalleryListView.ItemsSource = filteredItems;
                    // Populate subcategory ComboBox
                    if (selectedTheme.Subcategories != null && selectedTheme.Subcategories.Count > 0)
                    {
                        GallerySubcategoryComboBox.ItemsSource = selectedTheme.Subcategories;
                        GallerySubcategoryComboBox.DisplayMemberPath = "Name";
                        GallerySubcategoryComboBox.SelectedIndex = -1;
                        GallerySubcategoryComboBox.IsEnabled = true;
                    }
                    else
                    {
                        GallerySubcategoryComboBox.ItemsSource = null;
                        GallerySubcategoryComboBox.IsEnabled = false;
                    }
                }
            }
            else
            {
                // Show all items if no theme is selected
                GalleryListView.ItemsSource = _promptHistory;
                GallerySubcategoryComboBox.ItemsSource = null;
                GallerySubcategoryComboBox.IsEnabled = false;
            }
        }

        private void GallerySubcategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GalleryThemeComboBox.SelectedItem is ThemeItem selectedTheme && selectedTheme.Name != "All Themes")
            {
                var filteredItems = _promptHistory.Where(item => item.ThemeName == selectedTheme.Name).ToList();
                if (GallerySubcategoryComboBox.SelectedItem is ThemeSubcategory subcat)
                {
                    // Filter by subcategory name in prompt (since PromptHistoryItem does not store subcategory directly)
                    filteredItems = filteredItems.Where(item => item.FullPrompt.Contains(subcat.Name)).ToList();
                }
                GalleryListView.ItemsSource = filteredItems;
            }
        }

        private void HistoryItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is PromptHistoryItem item)
            {
                if (File.Exists(item.ImagePath))
                {
                    var image = new BitmapImage(new Uri(item.ImagePath));
                    GeneratedImage.Source = image;
                    _currentImagePath = item.ImagePath;
                }
            }
        }

        private void LoadAnalytics()
        {
            if (File.Exists(AnalyticsFile))
            {
                var json = File.ReadAllText(AnalyticsFile);
                _analytics = System.Text.Json.JsonSerializer.Deserialize<AnalyticsData>(json) ?? new AnalyticsData();
            }
        }

        private void SaveAnalytics()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_analytics);
            File.WriteAllText(AnalyticsFile, json);
        }

        private double CalculateCost(string size, string quality, string model)
        {
            // OpenAI's pricing as of 2024
            const double standardCost = 0.04; // per image
            const double hdCost = 0.08; // per image
            
            var baseCost = quality.ToLower() == "hd" ? hdCost : standardCost;
            
            // Track model usage
            if (!_analytics.ModelUsage.ContainsKey(model))
                _analytics.ModelUsage[model] = 0;
            _analytics.ModelUsage[model]++;

            // Track model costs
            if (!_analytics.ModelCosts.ContainsKey(model))
                _analytics.ModelCosts[model] = 0;
            _analytics.ModelCosts[model] += baseCost;

            return baseCost;
        }

        private void TrackGeneration(string prompt, string theme, string size, string quality, bool success, string errorMessage = "")
        {
            var model = "gpt-image-1"; // Default model
            var cost = CalculateCost(size, quality, model);
            var record = new GenerationRecord
            {
                Timestamp = DateTime.Now,
                Prompt = prompt,
                Theme = theme,
                Size = size,
                Quality = quality,
                Model = model,
                Success = success,
                Cost = cost,
                ErrorMessage = errorMessage
            };

            _analytics.TotalGenerations++;
            if (success)
            {
                _analytics.SuccessfulGenerations++;
                _analytics.TotalCost += cost;
            }
            else
            {
                _analytics.FailedGenerations++;
            }

            // Track theme usage
            if (!string.IsNullOrEmpty(theme))
            {
                if (!_analytics.ThemeUsage.ContainsKey(theme))
                    _analytics.ThemeUsage[theme] = 0;
                _analytics.ThemeUsage[theme]++;
            }

            // Track prompt usage (simplified version of prompt)
            var simplifiedPrompt = prompt.Length > 50 ? prompt.Substring(0, 50) + "..." : prompt;
            if (!_analytics.PromptUsage.ContainsKey(simplifiedPrompt))
                _analytics.PromptUsage[simplifiedPrompt] = 0;
            _analytics.PromptUsage[simplifiedPrompt]++;

            // Add to recent generations (keep last 100)
            _analytics.RecentGenerations.Insert(0, record);
            if (_analytics.RecentGenerations.Count > 100)
                _analytics.RecentGenerations.RemoveAt(_analytics.RecentGenerations.Count - 1);

            SaveAnalytics();
            UpdateAnalyticsDisplay();
        }

        private void UpdateAnalyticsDisplay()
        {
            if (AnalyticsTab != null)
            {
                // Update summary statistics
                TotalGenerationsText.Text = _analytics.TotalGenerations.ToString();
                SuccessfulGenerationsText.Text = _analytics.SuccessfulGenerations.ToString();
                FailedGenerationsText.Text = _analytics.FailedGenerations.ToString();
                TotalCostText.Text = $"${_analytics.TotalCost:F2}";

                // Update model usage
                ModelUsageList.ItemsSource = _analytics.ModelUsage
                    .OrderByDescending(x => x.Value)
                    .Select(x => new 
                    { 
                        Model = x.Key, 
                        Count = x.Value,
                        Cost = _analytics.ModelCosts.ContainsKey(x.Key) ? _analytics.ModelCosts[x.Key] : 0
                    });

                // Update theme usage chart
                ThemeUsageList.ItemsSource = _analytics.ThemeUsage
                    .OrderByDescending(x => x.Value)
                    .Select(x => new { Theme = x.Key, Count = x.Value });

                // Update recent generations
                RecentGenerationsList.ItemsSource = _analytics.RecentGenerations;
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ThemeItem selectedTheme)
            {
                _selectedTheme = selectedTheme.Name == "All Themes" ? null : selectedTheme;
                // Update subcategory ComboBox
                if (_selectedTheme != null && _selectedTheme.Subcategories != null && _selectedTheme.Subcategories.Count > 0)
                {
                    SubcategoryComboBox.ItemsSource = _selectedTheme.Subcategories;
                    SubcategoryComboBox.DisplayMemberPath = "Name";
                    SubcategoryComboBox.SelectedIndex = 0;
                }
                else
                {
                    SubcategoryComboBox.ItemsSource = null;
                    _selectedSubcategory = null;
                }
            }
        }

        private void SubcategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubcategoryComboBox.SelectedItem is ThemeSubcategory subcat)
            {
                _selectedSubcategory = subcat;
            }
            else
            {
                _selectedSubcategory = null;
            }
        }

        private void EditThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is ThemeItem theme)
            {
                var editor = new ThemeEditorWindow();
                editor.SetTheme(theme);
                if (editor.ShowDialog() == true)
                {
                    var updatedTheme = editor.GetTheme();
                    // Prevent duplicate name (except for itself)
                    if (_themes.Any(t => t.Name == updatedTheme.Name && t != theme))
                    {
                        System.Windows.MessageBox.Show("A theme with this name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // Update the theme in the list
                    theme.Name = updatedTheme.Name;
                    theme.Description = updatedTheme.Description;
                    theme.SaveFolder = updatedTheme.SaveFolder;
                    theme.Subcategories = updatedTheme.Subcategories;
                    SaveThemes();
                    LoadThemes();
                }
            }
        }

        private void DeleteThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is ThemeItem theme)
            {
                var result = System.Windows.MessageBox.Show($"Are you sure you want to delete the theme '{theme.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _themes.Remove(theme);
                    SaveThemes();
                    LoadThemes();
                }
            }
        }

        // New Theme Button handler
        private void NewThemeButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = new ThemeEditorWindow();
            if (editor.ShowDialog() == true)
            {
                var newTheme = editor.GetTheme();
                // Prevent duplicate theme names
                if (_themes.Exists(t => t.Name == newTheme.Name))
                {
                    System.Windows.MessageBox.Show("A theme with this name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _themes.Add(newTheme);
                SaveThemes();
                LoadThemes();
            }
        }

        private async void RepromptButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentImagePath) && File.Exists(_currentImagePath))
            {
                SetEditMode(true);
            }
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

        private async void SubmitEditButton_Click(object sender, RoutedEventArgs e)
        {
            // Export mask as PNG
            var rtb = new RenderTargetBitmap((int)MaskCanvas.ActualWidth, (int)MaskCanvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(MaskCanvas);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            var tempMaskPath = Path.Combine(Path.GetTempPath(), $"mask_{DateTime.Now.Ticks}.png");
            using (var fs = new FileStream(tempMaskPath, FileMode.Create))
            {
                encoder.Save(fs);
            }
            string editPrompt = EditPromptTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(editPrompt))
            {
                System.Windows.MessageBox.Show("Please describe what to change.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Call OpenAI image edit API (reuse logic from RepromptButton_Click, but use tempMaskPath)
            try
            {
                RepromptButton.IsEnabled = false;
                string? pngPath = _currentImagePath;
                if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
                {
                    System.Windows.MessageBox.Show("No valid image to edit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var apiKey = LoadApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    System.Windows.MessageBox.Show("OpenAI API key is missing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var url = "https://api.openai.com/v1/images/edits";
                using (var client = new HttpClient())
                using (var form = new MultipartFormDataContent())
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
                    // Add image file
                    var imageBytes = File.ReadAllBytes(pngPath);
                    var imageContent = new ByteArrayContent(imageBytes);
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    form.Add(imageContent, "image", Path.GetFileName(pngPath));
                    // Add mask file
                    if (string.IsNullOrEmpty(tempMaskPath) || !File.Exists(tempMaskPath))
                    {
                        System.Windows.MessageBox.Show("No valid mask to submit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    var maskBytes = File.ReadAllBytes(tempMaskPath);
                    var maskContent = new ByteArrayContent(maskBytes);
                    maskContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    form.Add(maskContent, "mask", Path.GetFileName(tempMaskPath));
                    // Add prompt
                    form.Add(new StringContent(editPrompt), "prompt");
                    form.Add(new StringContent("1"), "n");
                    form.Add(new StringContent("1024x1024"), "size");
                    form.Add(new StringContent("b64_json"), "response_format");
                    var response = await client.PostAsync(url, form);
                    var responseString = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Windows.MessageBox.Show($"OpenAI API error: {response.StatusCode}\n{responseString}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    using (var doc = System.Text.Json.JsonDocument.Parse(responseString))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("data", out var dataElem) && dataElem.GetArrayLength() > 0)
                        {
                            var first = dataElem[0];
                            if (first.TryGetProperty("b64_json", out var b64Elem))
                            {
                                var base64String = b64Elem.GetString();
                                if (!string.IsNullOrEmpty(base64String))
                                {
                                    var editedImageData = Convert.FromBase64String(base64String);
                                    var tempPath = Path.Combine(Path.GetTempPath(), $"edited_image_{DateTime.Now.Ticks}.png");
                                    File.WriteAllBytes(tempPath, editedImageData);
                                    var image = new BitmapImage();
                                    using (var ms = new MemoryStream(editedImageData))
                                    {
                                        image.BeginInit();
                                        image.CacheOption = BitmapCacheOption.OnLoad;
                                        image.StreamSource = ms;
                                        image.EndInit();
                                        image.Freeze();
                                    }
                                    GeneratedImage.Source = image;
                                    _currentImagePath = tempPath;
                                    RepromptButton.IsEnabled = true;
                                    // Add to history
                                    _promptHistory.Insert(0, new PromptHistoryItem
                                    {
                                        UserPrompt = editPrompt,
                                        FullPrompt = editPrompt,
                                        ImagePath = tempPath,
                                        ThemeName = _selectedTheme?.Name ?? "",
                                        Created = DateTime.Now
                                    });
                                    if (_promptHistory.Count > MaxHistoryItems)
                                    {
                                        _promptHistory.RemoveAt(_promptHistory.Count - 1);
                                    }
                                    SaveHistory();
                                    HistoryListView.ItemsSource = null;
                                    HistoryListView.ItemsSource = _promptHistory;
                                    System.Windows.MessageBox.Show("Image edited successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                                    // Exit edit mode
                                    SetEditMode(false);
                                    return;
                                }
                            }
                        }
                    }
                    System.Windows.MessageBox.Show("No edited image found in OpenAI response.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RepromptButton.IsEnabled = true;
            }
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode(false);
        }

        private void SetEditMode(bool enabled)
        {
            _editMode = enabled;
            EditControlsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            MaskCanvas.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (!enabled)
            {
                MaskCanvas.Strokes.Clear();
                EditPromptTextBox.Text = string.Empty;
            }
        }

        private void GalleryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GalleryListView.SelectedItem is PromptHistoryItem selectedItem)
            {
                // Load the selected image into the main image display
                if (System.IO.File.Exists(selectedItem.ImagePath))
                {
                    var image = new BitmapImage(new System.Uri(selectedItem.ImagePath));
                    GeneratedImage.Source = image;
                    _currentImagePath = selectedItem.ImagePath;
                    RepromptButton.IsEnabled = true;
                }
            }
        }
    }
} 