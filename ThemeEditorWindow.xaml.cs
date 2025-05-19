using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ImagenGenC
{
    public partial class ThemeEditorWindow : Window
    {
        public string ThemeName
        {
            get => ThemeNameTextBox.Text.Trim();
            set => ThemeNameTextBox.Text = value;
        }
        public string ThemeDescription
        {
            get => ThemeDescriptionTextBox.Text.Trim();
            set => ThemeDescriptionTextBox.Text = value;
        }
        public string ThemeSaveFolder
        {
            get => ThemeSaveFolderTextBox.Text.Trim();
            set => ThemeSaveFolderTextBox.Text = value;
        }
        public List<ThemeSubcategory> Subcategories { get; set; } = new List<ThemeSubcategory>();

        public ThemeEditorWindow()
        {
            InitializeComponent();
            SubcategoriesList.ItemsSource = Subcategories;
        }

        public void SetTheme(ThemeItem theme)
        {
            ThemeName = theme.Name;
            ThemeDescription = theme.Description;
            ThemeSaveFolder = theme.SaveFolder;
            Subcategories = new List<ThemeSubcategory>(theme.Subcategories);
            SubcategoriesList.ItemsSource = null;
            SubcategoriesList.ItemsSource = Subcategories;
        }

        public ThemeItem GetTheme()
        {
            return new ThemeItem
            {
                Name = ThemeName,
                Description = ThemeDescription,
                SaveFolder = ThemeSaveFolder,
                Subcategories = new List<ThemeSubcategory>(Subcategories)
            };
        }

        private void BrowseThemeSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ThemeSaveFolderTextBox.Text = dialog.SelectedPath;
            }
        }

        private void BrowseSubcategorySaveFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SubcategorySaveFolderTextBox.Text = dialog.SelectedPath;
            }
        }

        private void AddSubcategory_Click(object sender, RoutedEventArgs e)
        {
            var name = SubcategoryNameTextBox.Text.Trim();
            var desc = SubcategoryDescriptionTextBox.Text.Trim();
            var folder = SubcategorySaveFolderTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                System.Windows.MessageBox.Show("Subcategory name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Subcategories.Add(new ThemeSubcategory { Name = name, Description = desc, SaveFolder = folder });
            SubcategoriesList.ItemsSource = null;
            SubcategoriesList.ItemsSource = Subcategories;
            SubcategoryNameTextBox.Clear();
            SubcategoryDescriptionTextBox.Clear();
            SubcategorySaveFolderTextBox.Clear();
        }

        private void DeleteSubcategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ThemeSubcategory subcat)
            {
                Subcategories.Remove(subcat);
                SubcategoriesList.ItemsSource = null;
                SubcategoriesList.ItemsSource = Subcategories;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ThemeName))
            {
                System.Windows.MessageBox.Show("Theme name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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