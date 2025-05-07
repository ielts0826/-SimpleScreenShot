using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media; // Required for Brushes
using System; // Ensure System namespace is imported for Math class

namespace ScreenCaptureTool;

public partial class SettingsWindow : Window
{
    private Config _config; // To hold the configuration object passed from MainWindow
    private TextBox ImgurClientIdTextBox => this.FindControl<TextBox>("ImgurClientIdTextBox")!;
    private TextBlock ImgurClientIdStatusText => this.FindControl<TextBlock>("ImgurClientIdStatusText")!;

    // Parameterless constructor for XAML designer preview
    public SettingsWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        // For designer preview, create a dummy config or handle null _config gracefully in UpdateImgurClientIdStatus
        _config = new Config(); 
        LoadConfigValues();
    }

    // Constructor to be called from MainWindow, passing the actual config object
    public SettingsWindow(Config config)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        _config = config;
        LoadConfigValues();

        ImgurClientIdTextBox.TextChanged += ImgurClientIdTextBox_TextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadConfigValues()
    {
        if (ImgurClientIdTextBox != null && _config != null)
        {
            ImgurClientIdTextBox.Text = _config.ImgurClientId ?? string.Empty;
        }
        UpdateImgurClientIdStatus();
    }

    private void ImgurClientIdTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // Update the status text as the user types
        UpdateImgurClientIdStatus(true);
    }

    private void UpdateImgurClientIdStatus(bool typing = false)
    {
        if (ImgurClientIdStatusText == null || _config == null) return;

        string currentId = ImgurClientIdTextBox?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentId))
        {
            ImgurClientIdStatusText.Text = "Client ID 为空。Imgur 上传功能将不可用。";
            ImgurClientIdStatusText.Foreground = Avalonia.Media.Brushes.OrangeRed;
        }
        // Basic check, a real Client ID from Imgur is typically 15 chars for anonymous, or longer for registered apps.
        else if (currentId == "YOUR_IMGUR_CLIENT_ID_PLACEHOLDER" || currentId.Length < 10) 
        {
            ImgurClientIdStatusText.Text = "当前 Client ID 似乎无效或太短。请确保输入正确的 Client ID。";
            ImgurClientIdStatusText.Foreground = Avalonia.Media.Brushes.OrangeRed;
        }
        else
        {
            string statusTextWhenTyping = "Client ID 格式初步有效。点击 \"确定\" 保存。";
            string statusTextWhenSet = "Imgur Client ID 已设置。";
            ImgurClientIdStatusText.Text = typing ? statusTextWhenTyping : statusTextWhenSet;
            ImgurClientIdStatusText.Foreground = Avalonia.Media.Brushes.LightGreen;
        }
    }

    private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_config != null && ImgurClientIdTextBox != null)
        {
            _config.ImgurClientId = ImgurClientIdTextBox.Text?.Trim();
            MainWindow.LogToFile($"SettingsWindow: OK clicked. ImgurClient ID set to: {_config.ImgurClientId?.Substring(0, Math.Min(_config.ImgurClientId?.Length ?? 0, 5))}...");
        }
        this.Close(true); // Close the window, returning true to indicate OK/Save
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MainWindow.LogToFile("SettingsWindow: Cancel clicked.");
        this.Close(false); // Close the window, returning false to indicate Cancel
    }
} 