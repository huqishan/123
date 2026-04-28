using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ControlLibrary.Controls.SearchBox.Control
{
    /// <summary>
    /// Navigation search box with themed icon and placeholder handling.
    /// </summary>
    public partial class SearchBox : UserControl
    {
        private const string NavMutedIconBrushKey = "NavMutedIconBrush";

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(SearchBox),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextChanged));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(SearchBox),
                new PropertyMetadata("Search"));

        private bool _isUpdatingText;

        public SearchBox()
        {
            InitializeComponent();
            InitializeSearchIcon();
            UpdateTextBoxText(Text);
            UpdateSearchPlaceholder();
        }

        public event TextChangedEventHandler? TextChanged;

        public string Text
        {
            get => (string?)GetValue(TextProperty) ?? string.Empty;
            set => SetValue(TextProperty, value ?? string.Empty);
        }

        public string PlaceholderText
        {
            get => (string?)GetValue(PlaceholderTextProperty) ?? string.Empty;
            set => SetValue(PlaceholderTextProperty, value ?? string.Empty);
        }

        private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is SearchBox searchBox)
            {
                searchBox.UpdateTextBoxText((string?)e.NewValue ?? string.Empty);
            }
        }

        private void InitializeSearchIcon()
        {
            FrameworkElement searchIcon = IconFactory.Create(IconFactory.Search, Brushes.White, 14);
            ApplyIconBrushResource(searchIcon, NavMutedIconBrushKey);
            SearchIconHost.Content = searchIcon;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdatingText && !string.Equals(Text, SearchTextBox.Text, StringComparison.Ordinal))
            {
                SetCurrentValue(TextProperty, SearchTextBox.Text);
            }

            UpdateSearchPlaceholder();
            TextChanged?.Invoke(this, e);
        }

        private void UpdateTextBoxText(string text)
        {
            if (SearchTextBox is null)
            {
                return;
            }

            if (string.Equals(SearchTextBox.Text, text, StringComparison.Ordinal))
            {
                UpdateSearchPlaceholder();
                return;
            }

            try
            {
                _isUpdatingText = true;
                SearchTextBox.Text = text;
            }
            finally
            {
                _isUpdatingText = false;
            }

            UpdateSearchPlaceholder();
        }

        private void UpdateSearchPlaceholder()
        {
            if (SearchPlaceholderText is null)
            {
                return;
            }

            SearchPlaceholderText.Visibility = string.IsNullOrWhiteSpace(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static void ApplyIconBrushResource(DependencyObject element, string brushResourceKey)
        {
            if (element is Shape shape)
            {
                shape.SetResourceReference(Shape.StrokeProperty, brushResourceKey);
            }
            else if (element is TextBlock textBlock)
            {
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, brushResourceKey);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                ApplyIconBrushResource(VisualTreeHelper.GetChild(element, i), brushResourceKey);
            }
        }
    }
}
