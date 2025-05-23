using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Drawing; // Requires System.Drawing.Common NuGet
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Avalonia.Input; // Add for KeyEventArgs, KeyModifiers
using Avalonia.Platform; // Add for IPlatformHandle, Screen, PixelPoint
using Avalonia.Threading; // Add for Dispatcher
using Avalonia.Media; // Add for Brushes, Pen
// using Avalonia.Controls.Shapes; // No longer needed for selection rectangle here
using System.Linq; // Add for FirstOrDefault
using System.Text.Json; // Add for JSON serialization
using System.Text.Json.Serialization; // For JsonConverter
using System.ComponentModel; // For TypeConverter
using System.Collections.Generic; // For List
using Avalonia.Platform.Storage; // For FolderPicker
using System.Reactive.Disposables; // For Disposable.Create
using AnimatedGif;

namespace ScreenCaptureTool;

// Enum to track which hotkey is being set
internal enum HotkeySettingMode { None, FullScreen, Region, GifRecord }

public partial class MainWindow : Window
{
    // Constants for Hotkey IDs (Still used internally by old config/UI, maybe remove later)
    // private const int HOTKEY_ID_FULLSCREEN = 9000;
    // private const int HOTKEY_ID_REGION = 9001;

    // State variables
    private bool _isCapturing = false; // Still useful to prevent concurrent captures? Maybe remove later.
    private HotkeySettingMode _settingHotkeyMode = HotkeySettingMode.None; // Track which hotkey is being set
    // private Avalonia.Point _startPoint; // Moved to RegionSelectionWindow
    // private Avalonia.Point _endPoint; // Moved to RegionSelectionWindow
    private System.Drawing.Bitmap? _fullScreenBitmapForRegionSelection = null; // Still needed to pass to RegionSelectionWindow and for cropping
    private readonly string _configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenCaptureTool", "config.json"); // Use System.IO.Path
    private Config _config = new(); // Initialize with default config

    // GIF Recording State
    private bool _isRecording = false;
    private System.Threading.Timer? _recordingTimer; // Nullable
    private List<System.Drawing.Bitmap> _gifFrames = new List<System.Drawing.Bitmap>();
    private int _framesPerSecond = 10; // Default FPS
    private Button RecordGifButton => this.FindControl<Button>("RecordGifButton")!;
    private Button FullScreenButton => this.FindControl<Button>("FullScreenButton")!;
    private Button RegionButton => this.FindControl<Button>("RegionButton")!;
    private Rect? _gifRecordingRegion = null; // To store the selected region for GIF recording

    // Hotkey registration disposables
    private IDisposable? _fullscreenHotkeyRegistration;
    private IDisposable? _regionHotkeyRegistration;
    private IDisposable? _gifRecordHotkeyRegistration; // Added for GIF Record hotkey

    // Controls (Removed Overlay related)
    private TextBlock StatusText => this.FindControl<TextBlock>("StatusText")!;
    private TextBox FullScreenHotkeyTextBox => this.FindControl<TextBox>("FullScreenHotkeyTextBox")!;
    private Button SetFullScreenHotkeyButton => this.FindControl<Button>("SetFullScreenHotkeyButton")!;
    private TextBox RegionHotkeyTextBox => this.FindControl<TextBox>("RegionHotkeyTextBox")!;
    private Button SetRegionHotkeyButton => this.FindControl<Button>("SetRegionHotkeyButton")!;
    private CheckBox UseDefaultPathCheckBox => this.FindControl<CheckBox>("UseDefaultPathCheckBox")!;
    private TextBox DefaultPathTextBox => this.FindControl<TextBox>("DefaultPathTextBox")!;
    private Button BrowseDefaultPathButton => this.FindControl<Button>("BrowseDefaultPathButton")!;
    // New controls for GIF Record Hotkey
    private TextBox GifRecordHotkeyTextBox => this.FindControl<TextBox>("GifRecordHotkeyTextBox")!;
    private Button SetGifRecordHotkeyButton => this.FindControl<Button>("SetGifRecordHotkeyButton")!;
    // Controls for Imgur Client ID settings are now in SettingsWindow.xaml.cs
    // private TextBox ImgurClientIdTextBox => this.FindControl<TextBox>("ImgurClientIdTextBox")!;
    // private TextBlock ImgurClientIdStatusText => this.FindControl<TextBlock>("ImgurClientIdStatusText")!;

    // Selection rectangle is now inside RegionSelectionWindow
    // private Avalonia.Controls.Shapes.Rectangle _selectionRectangle = ...;

    public MainWindow()
    {
        LogToFile("MainWindow: 构造函数开始");
        Console.WriteLine("MainWindow: 构造函数开始");

        try
        {
            InitializeComponent();
            LoadConfig(); // Load config after components are initialized
            UpdateUiFromConfig(); // Update UI based on loaded config
            LogToFile("MainWindow: 窗口已创建并加载配置");
            Console.WriteLine("窗口已创建并加载配置");
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 构造函数出错: {ex}");
            Console.WriteLine($"MainWindow: 构造函数出错: {ex}");
            throw;
        }

        LogToFile("MainWindow: 构造函数完成");
    }

    private void InitializeComponent()
    {
        try
        {
            LogToFile("MainWindow: InitializeComponent 开始");
            AvaloniaXamlLoader.Load(this);
            LogToFile("MainWindow: XAML已加载");
            Console.WriteLine("XAML已加载");

            // No longer need to add selection rectangle here
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: InitializeComponent 出错: {ex}");
            throw;
        }
    }

    // --- Config Methods --- (Keep as is)
    private void LoadConfig()
    {
        try
        {
            if (System.IO.File.Exists(_configPath)) // Use System.IO.File
            {
                LogToFile($"从 {_configPath} 加载配置");
                var json = System.IO.File.ReadAllText(_configPath); // Use System.IO.File
                 _config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
                 // Parse loaded strings to update Modifiers and VKCode
                 _config.UpdateModifiersAndVKCodeFromStrings();
                 LogToFile($"配置加载完成: FullScreen={_config.FullScreenHotkeyString}, Region={_config.RegionHotkeyString}, GifRecord={_config.GifRecordHotkeyString}, UseDefaultPath={_config.UseDefaultSavePath}, DefaultPath={_config.DefaultSavePath}, NextNum={_config.NextSaveFileNumber}, ImgurClientId={_config.ImgurClientId?.Substring(0, Math.Min(_config.ImgurClientId.Length, 5))}..."); // Log partial ClientID for privacy
            }
            else
            {
                 LogToFile($"配置文件不存在: {_configPath}, 使用默认配置");
                 _config = new Config(); // Ensure default config is used if file doesn't exist
                 _config.UpdateModifiersAndVKCodeFromStrings(); // Ensure Modifiers/VKCode are set from default string
            }
        }
        catch (Exception ex)
        {
            LogToFile($"加载配置出错: {ex.Message}");
            _config = new Config(); // Use default on error
            _config.UpdateModifiersAndVKCodeFromStrings(); // Ensure Modifiers/VKCode are set from default string
        }
    }

    private void SaveConfig()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_configPath); // Use System.IO.Path
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                LogToFile($"创建配置目录: {directory}");
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_config, options);
            System.IO.File.WriteAllText(_configPath, json); // Use System.IO.File
            LogToFile($"配置已保存到: {_configPath}");
        }
        catch (Exception ex)
        {
            LogToFile($"保存配置出错: {ex.Message}");
        }
    }

    private void UpdateUiFromConfig()
    {
        UpdateHotkeyDisplay();
        UpdateDefaultPathDisplay();
        // UpdateImgurSettingsDisplay(); // Removed, Imgur settings are in a separate window
    }

    private void UpdateHotkeyDisplay()
    {
         if (FullScreenHotkeyTextBox != null)
         {
              FullScreenHotkeyTextBox.Text = _config.FullScreenHotkeyString;
              LogToFile($"更新全屏热键显示: {FullScreenHotkeyTextBox.Text}");
         }
          if (RegionHotkeyTextBox != null)
         {
              RegionHotkeyTextBox.Text = _config.RegionHotkeyString;
              LogToFile($"更新区域热键显示: {RegionHotkeyTextBox.Text}");
         }
         if (GifRecordHotkeyTextBox != null) // Added for GIF Record Hotkey
         {
            GifRecordHotkeyTextBox.Text = _config.GifRecordHotkeyString;
            LogToFile($"更新GIF录制热键显示: {GifRecordHotkeyTextBox.Text}");
         }
         if (StatusText != null && _settingHotkeyMode == HotkeySettingMode.None)
         {
             UpdateStatusTextWithHotkeys();
         }
    }

     private void UpdateDefaultPathDisplay()
    {
         if (UseDefaultPathCheckBox != null)
         {
             UseDefaultPathCheckBox.IsChecked = _config.UseDefaultSavePath;
         }
         if (DefaultPathTextBox != null)
         {
             DefaultPathTextBox.Text = _config.DefaultSavePath ?? "";
             DefaultPathTextBox.IsEnabled = _config.UseDefaultSavePath;
         }
         if (BrowseDefaultPathButton != null)
         {
             BrowseDefaultPathButton.IsEnabled = _config.UseDefaultSavePath;
         }
         LogToFile($"更新默认路径显示: Enabled={_config.UseDefaultSavePath}, Path={_config.DefaultSavePath}");
    }

    // --- Logging --- (Keep as is)
    public static void LogToFile(string message)
    {
        try
        {
            string logPath = "avalonia_app_log.txt";
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            System.IO.File.AppendAllText(logPath, logMessage, Encoding.UTF8);
        }
        catch { /* Ignore logging errors */ }
    }

    // --- Screenshot Logic ---
    private async void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: 全屏截图按钮被点击");
        if (_isCapturing) { LogToFile("MainWindow: 正在进行其他截图，忽略本次触发"); return; } // Prevent concurrent captures

        _isCapturing = true; // Set capturing state
        try
        {
            StatusText.Text = "正在截取全屏...";
            await CaptureFullScreen();
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 全屏截图出错: {ex}");
            StatusText.Text = "截图失败: " + ex.Message;
            // this.Show(); // Don't force show main window on error
        }
        finally
        {
            _isCapturing = false; // Reset capturing state
            LogToFile("MainWindow: FullScreenButton_Click finished.");
        }
    }

    private async void RegionButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: 区域截图按钮被点击");
        if (_isCapturing) { LogToFile("MainWindow: 正在进行其他截图，忽略本次触发"); return; } // Prevent concurrent captures

        _isCapturing = true; // Set capturing state
        try
        {
            StatusText.Text = "准备区域截图中...";
            await StartRegionCapture(); // Now async
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 区域截图出错: {ex}");
            StatusText.Text = "截图失败: " + ex.Message;
            // this.Show(); // Don't force show main window on error
        }
        finally
        {
             _isCapturing = false; // Reset capturing state
             // Clean up the background bitmap if it exists (important!)
             _fullScreenBitmapForRegionSelection?.Dispose();
             _fullScreenBitmapForRegionSelection = null;
             LogToFile("MainWindow: RegionButton_Click finished, background bitmap disposed.");
        }
    }

    private async Task CaptureFullScreen()
    {
         LogToFile("MainWindow: 开始全屏截图");
         // Don't hide main window anymore
         // this.Hide();
         await Task.Delay(200); // Keep delay to allow UI changes (like status text) to render

         try
         {
            var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            LogToFile($"MainWindow: 屏幕尺寸: {screenWidth}x{screenHeight}");

            using (var bitmap = new System.Drawing.Bitmap(screenWidth, screenHeight))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
                }
                await SaveScreenshot(bitmap);
            }
         }
         catch (Exception ex)
         {
            LogToFile($"MainWindow: 全屏截图过程出错: {ex}");
            // this.Show(); // Don't force show
            throw;
         }
         // No finally block needed here as _isCapturing is handled by caller
     }

    // Rewritten StartRegionCapture to use RegionSelectionWindow
    private async Task StartRegionCapture()
    {
        LogToFile("MainWindow: 开始区域截图 (使用新窗口)");
        StatusText.Text = "准备区域截图中...";

        // 1. Capture the full screen background
        System.Drawing.Bitmap? bgBitmap = null;
        AvaloniaBitmap? avaloniaBgBitmap = null;
        try
        {
            // Don't hide main window
            await Task.Delay(200); // Allow UI to update

            var screen = this.Screens.Primary ?? this.Screens.All.FirstOrDefault();
            if (screen == null) throw new Exception("无法获取屏幕信息");

            var pixelSize = screen.Bounds.Size;
            int screenWidth = pixelSize.Width;
            int screenHeight = pixelSize.Height;
            LogToFile($"MainWindow: 屏幕信息 - 大小: {screenWidth}x{screenHeight}, 缩放: {screen.Scaling}");

            bgBitmap = new System.Drawing.Bitmap(screenWidth, screenHeight);
            using (var graphics = Graphics.FromImage(bgBitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
            }
            LogToFile("MainWindow: 全屏背景已捕获 (用于新窗口)");

            // Convert to Avalonia Bitmap for the selection window
            using (var ms = new MemoryStream())
            {
                bgBitmap.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                avaloniaBgBitmap = new AvaloniaBitmap(ms);
            }
            // Keep the original System.Drawing.Bitmap for cropping later
            _fullScreenBitmapForRegionSelection = bgBitmap;
            bgBitmap = null; // Transfer ownership to _fullScreenBitmapForRegionSelection
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 捕获背景截图出错: {ex}");
            StatusText.Text = "错误: 无法捕获屏幕背景";
            bgBitmap?.Dispose(); // Clean up if error occurred here
            avaloniaBgBitmap?.Dispose();
            _fullScreenBitmapForRegionSelection?.Dispose();
            _fullScreenBitmapForRegionSelection = null;
            _isCapturing = false; // Reset state
            return;
        }

        // 2. Show the RegionSelectionWindow
        Rect? selectedRect = null;
        try
        {
            if (avaloniaBgBitmap == null) throw new Exception("背景图转换失败");

            var selectionWindow = new RegionSelectionWindow(avaloniaBgBitmap);
            // ShowDialogAsync waits for the window to close
            await selectionWindow.ShowDialog<Rect?>(this); // Show as dialog relative to main window
            selectedRect = selectionWindow.SelectedRegion;
            LogToFile($"MainWindow: RegionSelectionWindow closed. SelectedRegion: {selectedRect}");
        }
        catch (Exception ex)
        {
             LogToFile($"MainWindow: 显示或处理 RegionSelectionWindow 时出错: {ex}");
             StatusText.Text = "区域选择失败";
        }
        finally
        {
             avaloniaBgBitmap?.Dispose(); // Dispose Avalonia bitmap now
        }


        // 3. Process the result
        if (selectedRect.HasValue && selectedRect.Value.Width > 0 && selectedRect.Value.Height > 0)
        {
            await CaptureRegion(selectedRect.Value); // Pass the selected Rect
        }
        else
        {
            LogToFile("MainWindow: 未选择有效区域或已取消");
            StatusText.Text = "区域截图已取消或无效";
            // Clean up the background bitmap if selection was cancelled or invalid
            _fullScreenBitmapForRegionSelection?.Dispose();
            _fullScreenBitmapForRegionSelection = null;
        }
        // _isCapturing is reset in the calling method's finally block
    }

    // Removed Overlay event handlers: Overlay_PointerPressed, Overlay_PointerMoved, Overlay_PointerReleased

    // Modified CaptureRegion to accept Rect and use stored background
    private async Task CaptureRegion(Rect selectionInWindowCoords)
    {
        LogToFile("MainWindow: 截取选定区域 (来自新窗口)");

        if (_fullScreenBitmapForRegionSelection == null)
        {
            LogToFile("MainWindow: 错误 - 背景截图丢失 (CaptureRegion)");
            StatusText.Text = "截图失败: 背景丢失";
            _isCapturing = false; // Ensure state reset
            return;
        }

        try
        {
            // We receive coordinates relative to the selection window (which was fullscreen)
            // We need to scale these coordinates based on the screen's scaling factor
            var screen = this.Screens.Primary ?? this.Screens.All.FirstOrDefault();
            double scaling = screen?.Scaling ?? 1.0;

            int cropX = (int)(selectionInWindowCoords.X * scaling);
            int cropY = (int)(selectionInWindowCoords.Y * scaling);
            int cropWidth = (int)(selectionInWindowCoords.Width * scaling);
            int cropHeight = (int)(selectionInWindowCoords.Height * scaling);

            // Ensure crop area is within the bounds of the background bitmap
            cropX = Math.Max(0, cropX);
            cropY = Math.Max(0, cropY);
            cropWidth = Math.Min(_fullScreenBitmapForRegionSelection.Width - cropX, cropWidth);
            cropHeight = Math.Min(_fullScreenBitmapForRegionSelection.Height - cropY, cropHeight);

            if (cropWidth <= 0 || cropHeight <= 0)
            {
                 LogToFile($"MainWindow: 无效的裁剪区域计算结果: {cropX},{cropY} {cropWidth}x{cropHeight}");
                 StatusText.Text = "截图失败: 无效区域";
                 return; // Exit early
            }

            LogToFile($"MainWindow: 裁剪区域 (背景图坐标): {cropX},{cropY} {cropWidth}x{cropHeight} (Scaling: {scaling})");

            using (var finalBitmap = new System.Drawing.Bitmap(cropWidth, cropHeight))
            {
                using (var graphics = Graphics.FromImage(finalBitmap))
                {
                    graphics.DrawImage(_fullScreenBitmapForRegionSelection,
                                       new System.Drawing.Rectangle(0, 0, cropWidth, cropHeight),
                                       new System.Drawing.Rectangle(cropX, cropY, cropWidth, cropHeight),
                                       GraphicsUnit.Pixel);
                }
                await SaveScreenshot(finalBitmap);
            }
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 区域截图过程出错: {ex}");
            StatusText.Text = "截图失败: " + ex.Message;
            // this.Show(); // Don't force show
        }
        // finally block removed, cleanup of _fullScreenBitmapForRegionSelection happens in caller (RegionButton_Click)
    }

    // SaveScreenshot remains largely the same, but ensure StatusText updates are safe
    private async Task SaveScreenshot(System.Drawing.Bitmap bitmap)
    {
        LogToFile("MainWindow: 保存截图");
        string? finalSavePath = null;

        try
        {
            if (_config.UseDefaultSavePath && !string.IsNullOrEmpty(_config.DefaultSavePath) && Directory.Exists(_config.DefaultSavePath))
            {
                // ... (default path logic remains the same) ...
                LogToFile($"MainWindow: 使用默认路径保存: {_config.DefaultSavePath}");
                string filename;
                string filePath;
                int currentFileNumber = _config.NextSaveFileNumber;
                do
                {
                    filename = $"{currentFileNumber}.png";
                    filePath = System.IO.Path.Combine(_config.DefaultSavePath, filename);
                    currentFileNumber++;
                } while (System.IO.File.Exists(filePath));

                LogToFile($"MainWindow: 自动保存到: {filePath}");
                bitmap.Save(filePath, ImageFormat.Png);
                finalSavePath = filePath;
                _config.NextSaveFileNumber = currentFileNumber;
                SaveConfig();

                // Use Dispatcher for UI update
                Dispatcher.UIThread.Post(() => StatusText.Text = "截图已自动保存到: " + finalSavePath);
                ShowPreview(bitmap);
            }
            else
            {
                 // ... (Save File Dialog logic remains the same) ...
                 if (_config.UseDefaultSavePath) LogToFile($"MainWindow: 默认路径无效或未设置 (Path='{_config.DefaultSavePath}'), 回退到另存为对话框");
                 else LogToFile($"MainWindow: 未启用默认路径，显示另存为对话框");

                var dialog = new SaveFileDialog { /* ... */ };
                // Need to ensure ShowAsync is called on UI thread if SaveScreenshot isn't already
                var result = await Dispatcher.UIThread.InvokeAsync(() => dialog.ShowAsync(this));

                if (result != null)
                {
                     LogToFile($"MainWindow: 用户选择保存路径: {result}");

                     // --- Ensure PNG format and extension ---
                     string correctedPath = result;
                     // Check if the path already has an extension, remove it if it's not png, or add .png if none exists.
                     string? currentExtension = System.IO.Path.GetExtension(correctedPath);
                     if (string.IsNullOrEmpty(currentExtension))
                     {
                         // No extension, add .png
                         correctedPath += ".png";
                         LogToFile($"MainWindow: 文件名无扩展名，添加 .png -> {correctedPath}");
                     }
                     else if (!currentExtension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                     {
                         // Incorrect extension, replace with .png
                         correctedPath = System.IO.Path.ChangeExtension(correctedPath, ".png");
                         LogToFile($"MainWindow: 文件名扩展名非png ({currentExtension})，更改为 .png -> {correctedPath}");
                     }
                     // If it already ends with .png (case-insensitive), do nothing.

                     finalSavePath = correctedPath;
                     ImageFormat format = ImageFormat.Png; // Always save as PNG
                     LogToFile($"MainWindow: 最终保存路径: {finalSavePath}, 格式: {format}");
                     // --- End Ensure PNG ---

                     bitmap.Save(finalSavePath, format); // Use corrected path and PNG format
                     ShowPreview(bitmap);
                     Dispatcher.UIThread.Post(() => StatusText.Text = "截图已保存到: " + finalSavePath); // Show corrected path
                }
                else
                {
                    LogToFile("MainWindow: 用户取消保存");
                    Dispatcher.UIThread.Post(() => StatusText.Text = "已取消保存");
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 保存截图出错: {ex}");
            Dispatcher.UIThread.Post(() => StatusText.Text = "保存失败: " + ex.Message);
        }
    }

    // ShowPreview remains largely the same
    private void ShowPreview(System.Drawing.Bitmap bitmap)
    {
         LogToFile("MainWindow: 显示预览");
         try
         {
             using (var memoryStream = new MemoryStream())
             {
                bitmap.Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;
                var avaloniaBitmap = new AvaloniaBitmap(memoryStream);

                // Show the TopmostWindow on the UI thread
                Dispatcher.UIThread.Post(() => {
                    var topWindow = new TopmostWindow(avaloniaBitmap);
                    topWindow.Closed += (s, e) => { LogToFile("TopmostWindow closed."); };
                    LogToFile("Showing TopmostWindow...");
                    topWindow.Show(); // Show non-modally
                });
             }
         }
         catch (Exception ex)
         {
            LogToFile($"MainWindow: 显示预览出错: {ex}");
            Dispatcher.UIThread.Post(() => StatusText.Text = "显示预览失败: " + ex.Message);
         }
    }

    // --- Hotkey Logic --- (Keep Initialize/Unregister/Setting logic as is)
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LogToFile("MainWindow: 窗口已打开");
        Console.WriteLine("MainWindow: 窗口已打开");
        Dispatcher.UIThread.Post(InitializeHotKeys, DispatcherPriority.Background);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        LogToFile("MainWindow: 窗口正在关闭，注销热键");
        UnregisterHotKeys();
        SaveConfig();
        base.OnClosing(e);
    }

     private void InitializeHotKeys()
     {
         _fullscreenHotkeyRegistration?.Dispose();
         _regionHotkeyRegistration?.Dispose();
         _gifRecordHotkeyRegistration?.Dispose(); // Unregister GIF Record hotkey
         _fullscreenHotkeyRegistration = null;
         _regionHotkeyRegistration = null;
         _gifRecordHotkeyRegistration = null; // Set to null
         bool fsSuccess = false;
         bool regionSuccess = false;
         bool gifSuccess = false; // Added for GIF Record hotkey
         string fsError = string.Empty;
         string regionError = string.Empty;
         string gifError = string.Empty; // Added for GIF Record hotkey

         try
         {
              LogToFile($"MainWindow: 尝试使用 User32Hotkey 注册全屏热键 {_config.FullScreenHotkeyString}");
              KeyGesture? fsGesture = KeyGestureConverter.StringToKeyGesture(_config.FullScreenHotkeyString);
              if (fsGesture != null && fsGesture.Key != Key.None && fsGesture.KeyModifiers != KeyModifiers.None)
              {
                  try
                  {
                     _fullscreenHotkeyRegistration = User32Hotkey.Create(fsGesture.Key, fsGesture.KeyModifiers, TriggerFullScreenCapture);
                     LogToFile($"MainWindow: 全局全屏热键 {_config.FullScreenHotkeyString} 注册成功");
                     fsSuccess = true;
                 }
                 catch (Exception ex) { /* ... error handling ... */ fsError = $"全屏热键注册失败: {ex.Message}"; LogToFile($"MainWindow: {fsError}"); Console.WriteLine($"Error registering global fullscreen hotkey: {ex}"); }
             }
             else { /* ... error handling ... */ fsError = "全屏热键配置无效"; LogToFile($"MainWindow: {fsError} ({_config.FullScreenHotkeyString})"); }

              LogToFile($"MainWindow: 尝试使用 User32Hotkey 注册区域热键 {_config.RegionHotkeyString}");
              KeyGesture? rGesture = KeyGestureConverter.StringToKeyGesture(_config.RegionHotkeyString);
              if (rGesture != null && rGesture.Key != Key.None && rGesture.KeyModifiers != KeyModifiers.None)
              {
                  try
                  {
                     _regionHotkeyRegistration = User32Hotkey.Create(rGesture.Key, rGesture.KeyModifiers, TriggerRegionCapture);
                     LogToFile($"MainWindow: 全局区域热键 {_config.RegionHotkeyString} 注册成功");
                     regionSuccess = true;
                 }
                 catch (Exception ex) { /* ... error handling ... */ regionError = $"区域热键注册失败: {ex.Message}"; LogToFile($"MainWindow: {regionError}"); Console.WriteLine($"Error registering global region hotkey: {ex}"); }
             }
             else { /* ... error handling ... */ regionError = "区域热键配置无效"; LogToFile($"MainWindow: {regionError} ({_config.RegionHotkeyString})"); }

            // Register GIF Record Hotkey
            LogToFile($"MainWindow: 尝试使用 User32Hotkey 注册GIF录制热键 {_config.GifRecordHotkeyString}");
            KeyGesture? gifGesture = KeyGestureConverter.StringToKeyGesture(_config.GifRecordHotkeyString);
            if (gifGesture != null && gifGesture.Key != Key.None && gifGesture.KeyModifiers != KeyModifiers.None)
            {
                try
                {
                    _gifRecordHotkeyRegistration = User32Hotkey.Create(gifGesture.Key, gifGesture.KeyModifiers, TriggerGifRecord);
                    LogToFile($"MainWindow: 全局GIF录制热键 {_config.GifRecordHotkeyString} 注册成功");
                    gifSuccess = true;
                }
                catch (Exception ex) { gifError = $"GIF录制热键注册失败: {ex.Message}"; LogToFile($"MainWindow: {gifError}"); Console.WriteLine($"Error registering global GIF record hotkey: {ex}"); }
            }
            else { gifError = "GIF录制热键配置无效"; LogToFile($"MainWindow: {gifError} ({_config.GifRecordHotkeyString})"); }
         }
         catch (Exception ex) { /* ... error handling ... */ LogToFile($"MainWindow: InitializeHotKeys 内部发生意外错误: {ex}"); StatusText.Text = "热键初始化时出错"; return; }

         if (fsSuccess && regionSuccess && gifSuccess) UpdateStatusTextWithHotkeys();
         else 
         {
            var errors = new List<string>();
            if (!fsSuccess) errors.Add($"全屏: {fsError}");
            if (!regionSuccess) errors.Add($"区域: {regionError}");
            if (!gifSuccess) errors.Add($"GIF: {gifError}");
            StatusText.Text = "热键注册部分失败: " + string.Join("; ", errors);
            // Fallback to show some info even if all fail
            if (!fsSuccess && !regionSuccess && !gifSuccess) UpdateStatusTextWithHotkeys(); 
         }
      }

     private void UnregisterHotKeys()
     {
         LogToFile("MainWindow: 注销全局热键...");
         _fullscreenHotkeyRegistration?.Dispose();
         _regionHotkeyRegistration?.Dispose();
         _gifRecordHotkeyRegistration?.Dispose(); // Unregister GIF Record hotkey
         _fullscreenHotkeyRegistration = null;
         _regionHotkeyRegistration = null;
         _gifRecordHotkeyRegistration = null; // Set to null
         LogToFile("MainWindow: 全局热键已注销 (通过 Dispose)");
     }

     private void SetFullScreenHotkeyButton_Click(object sender, RoutedEventArgs e) => StartSettingHotkey(HotkeySettingMode.FullScreen);
     private void SetRegionHotkeyButton_Click(object sender, RoutedEventArgs e) => StartSettingHotkey(HotkeySettingMode.Region);
     private void SetGifRecordHotkeyButton_Click(object sender, RoutedEventArgs e) => StartSettingHotkey(HotkeySettingMode.GifRecord); // Added for GIF Record

    private void StartSettingHotkey(HotkeySettingMode mode)
    {
        // ... (StartSettingHotkey logic remains the same) ...
        string type = "未知";
        switch(mode)
        {
            case HotkeySettingMode.FullScreen: type = "全屏"; break;
            case HotkeySettingMode.Region: type = "区域"; break;
            case HotkeySettingMode.GifRecord: type = "GIF录制"; break;
        }
        LogToFile($"MainWindow: 设置 {type} 热键按钮点击");
        StatusText.Text = $"请按下新的 {type} 快捷键组合 (或按 Esc 取消)...";
        _settingHotkeyMode = mode;
        SetFullScreenHotkeyButton.IsEnabled = false;
        SetRegionHotkeyButton.IsEnabled = false;
        SetGifRecordHotkeyButton.IsEnabled = false; // Disable GIF button too
        this.Focus();
        LogToFile("MainWindow: 进入热键设置模式，等待按键...");
    }

    // --- Default Path Settings Logic --- (Keep as is)
    private void UseDefaultPathCheckBox_Changed(object sender, RoutedEventArgs e) { /* ... */ if (_config != null && UseDefaultPathCheckBox != null) { _config.UseDefaultSavePath = UseDefaultPathCheckBox.IsChecked ?? false; LogToFile($"MainWindow: UseDefaultSavePath 设置为 {_config.UseDefaultSavePath}"); UpdateDefaultPathDisplay(); SaveConfig(); } }
    private async void BrowseDefaultPathButton_Click(object sender, RoutedEventArgs e) { /* ... */ LogToFile("MainWindow: 浏览默认路径按钮点击"); var topLevel = TopLevel.GetTopLevel(this); if (topLevel == null) return; var storageProvider = topLevel.StorageProvider; if (!storageProvider.CanPickFolder) { LogToFile("MainWindow: 存储提供者不支持选择文件夹"); StatusText.Text = "错误: 无法选择文件夹"; return; } var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "选择默认保存文件夹", AllowMultiple = false }); if (result != null && result.Count > 0) { var folder = result[0]; var folderUri = folder.Path; if (folderUri != null && folderUri.IsAbsoluteUri && folderUri.IsFile) { _config.DefaultSavePath = folderUri.LocalPath; LogToFile($"MainWindow: 选择的默认路径: {_config.DefaultSavePath}"); } else { LogToFile($"MainWindow: 获取文件夹路径失败或路径无效: {folderUri}"); StatusText.Text = "错误: 无法获取有效的文件夹路径"; _config.DefaultSavePath = null; } UpdateDefaultPathDisplay(); SaveConfig(); } else { LogToFile("MainWindow: 用户取消选择文件夹或选择无效"); } }

    // --- Keyboard Event Handling ---
    protected override void OnKeyDown(KeyEventArgs e)
    {
        LogToFile($"MainWindow: OnKeyDown - Key={e.Key}, Modifiers={e.KeyModifiers}, SettingMode={_settingHotkeyMode}, IsCapturing={_isCapturing}");
        bool handled = true;

        if (_settingHotkeyMode != HotkeySettingMode.None)
        {
            // ... (Hotkey setting logic remains the same) ...
            LogToFile("MainWindow: 处理热键设置...");
            if (e.Key == Key.Escape) { StatusText.Text = "已取消设置热键"; LogToFile("MainWindow: 取消设置热键 (Esc)"); }
            else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && e.Key != Key.LeftShift && e.Key != Key.RightShift && e.Key != Key.LeftAlt && e.Key != Key.RightAlt && e.Key != Key.LWin && e.Key != Key.RWin && e.Key != Key.System)
            {
                uint modifiers = 0; if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= NativeMethods.MOD_CONTROL; if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= NativeMethods.MOD_SHIFT; if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= NativeMethods.MOD_ALT;
                uint vkCode = KeyInterop.VirtualKeyFromKey(e.Key);
                if (vkCode != 0 && modifiers != 0)
                {
                    string newHotkeyString = KeyGestureConverter.KeyGestureToString(e.Key, e.KeyModifiers); LogToFile($"MainWindow: 捕获到新热键: {newHotkeyString} (Modifiers={modifiers}, VK={vkCode}) for {_settingHotkeyMode}");
                    UnregisterHotKeys();
                    if (_settingHotkeyMode == HotkeySettingMode.FullScreen) { _config.FullScreenModifiers = modifiers; _config.FullScreenVirtualKeyCode = vkCode; _config.FullScreenHotkeyString = newHotkeyString; }
                    else if (_settingHotkeyMode == HotkeySettingMode.Region) { _config.RegionModifiers = modifiers; _config.RegionVirtualKeyCode = vkCode; _config.RegionHotkeyString = newHotkeyString; }
                    else if (_settingHotkeyMode == HotkeySettingMode.GifRecord) { _config.GifRecordModifiers = modifiers; _config.GifRecordVirtualKeyCode = vkCode; _config.GifRecordHotkeyString = newHotkeyString; } // Added for GIF Record
                    InitializeHotKeys(); SaveConfig(); UpdateHotkeyDisplay();
                } else { StatusText.Text = "无效的热键组合 (需包含Ctrl/Shift/Alt + 普通键)"; LogToFile("MainWindow: 无效的热键组合"); }
            } else { LogToFile("MainWindow: 按下纯修饰键或Esc，等待或取消..."); if (e.Key != Key.Escape) handled = false; }
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && e.Key != Key.LeftShift && e.Key != Key.RightShift && e.Key != Key.LeftAlt && e.Key != Key.RightAlt && e.Key != Key.LWin && e.Key != Key.RWin && e.Key != Key.System)
            { _settingHotkeyMode = HotkeySettingMode.None; SetFullScreenHotkeyButton.IsEnabled = true; SetRegionHotkeyButton.IsEnabled = true; SetGifRecordHotkeyButton.IsEnabled = true; LogToFile("MainWindow: 退出热键设置模式"); if (StatusText != null && !StatusText.Text.Contains("注册成功") && !StatusText.Text.Contains("注册失败")) { Dispatcher.UIThread.Post(UpdateStatusTextWithHotkeys, DispatcherPriority.Background); } } // Enable GIF button too
        }
        // Removed _isCapturing check here, Esc is handled by RegionSelectionWindow now
        // else if (_isCapturing) { ... }
        else
        {
             handled = false; // If not setting hotkey, let base handle it
        }

        if (!handled)
        {
            base.OnKeyDown(e);
        }
    }

    private void UpdateStatusTextWithHotkeys()
    {
         if (StatusText != null && _settingHotkeyMode == HotkeySettingMode.None)
         {
              StatusText.Text = $"全屏: {_config.FullScreenHotkeyString} | 区域: {_config.RegionHotkeyString} | GIF: {_config.GifRecordHotkeyString}"; // Added GIF hotkey
         }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: 帮助按钮点击");
        var helpWindow = new HelpWindow();
         helpWindow.ShowDialog(this);
     }

     // --- Hotkey Trigger Methods (Called by User32Hotkey) ---
     private void TriggerFullScreenCapture()
     {
         LogToFile("MainWindow: 全局全屏热键触发");
         Dispatcher.UIThread.Post(() => FullScreenButton_Click(this, new RoutedEventArgs()), DispatcherPriority.Normal);
     }

     private void TriggerRegionCapture()
     {
         LogToFile("MainWindow: 全局区域热键触发");
         Dispatcher.UIThread.Post(() => RegionButton_Click(this, new RoutedEventArgs()), DispatcherPriority.Normal);
     }

    // New trigger for GIF Record hotkey
    private void TriggerGifRecord()
    {
        LogToFile("MainWindow: 全局GIF录制热键触发");
        Dispatcher.UIThread.Post(() => RecordGifButton_Click(this, new RoutedEventArgs()), DispatcherPriority.Normal);
    }

    private async void RecordGifButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile($"MainWindow: RecordGifButton clicked, _isRecording: {_isRecording}");
        if (_isCapturing) // Prevent GIF recording if screenshot is in progress
        {
            LogToFile("MainWindow: Cannot start GIF recording, a screenshot capture is in progress.");
            StatusText.Text = "正在进行截图操作，请稍后再试";
            return;
        }

        if (!_isRecording)
        {
            // Start Recording
            // 1. Prompt for region selection first
            StatusText.Text = "请选择GIF录制区域...";
            LogToFile("MainWindow: Initiating GIF region selection.");

            System.Drawing.Bitmap? bgBitmapForGif = null;
            AvaloniaBitmap? avaloniaBgBitmapForGif = null;
            Rect? selectedGifRegion = null;

            try
            {
                // Similar to StartRegionCapture for getting background
                await Task.Delay(200); // Allow UI to update
                var screen = this.Screens.Primary ?? this.Screens.All.FirstOrDefault();
                if (screen == null) throw new Exception("无法获取屏幕信息");

                var pixelSize = screen.Bounds.Size;
                bgBitmapForGif = new System.Drawing.Bitmap(pixelSize.Width, pixelSize.Height);
                using (var graphics = Graphics.FromImage(bgBitmapForGif))
                {
                    graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(pixelSize.Width, pixelSize.Height));
                }
                using (var ms = new MemoryStream())
                {
                    bgBitmapForGif.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    avaloniaBgBitmapForGif = new AvaloniaBitmap(ms);
                }
                // We don't need to keep bgBitmapForGif beyond this, as GIF frames are captured live.
                bgBitmapForGif.Dispose(); 
                bgBitmapForGif = null;

                var selectionWindow = new RegionSelectionWindow(avaloniaBgBitmapForGif, "选择GIF录制区域");
                await selectionWindow.ShowDialog<Rect?>(this);
                selectedGifRegion = selectionWindow.SelectedRegion;
            }
            catch (Exception ex)
            {
                LogToFile($"MainWindow: GIF region selection error: {ex}");
                StatusText.Text = "区域选择失败: " + ex.Message.Split('\n')[0];
                bgBitmapForGif?.Dispose(); // Ensure disposal
                avaloniaBgBitmapForGif?.Dispose();
                return;
            }
            finally
            {
                avaloniaBgBitmapForGif?.Dispose(); // Dispose Avalonia bitmap
            }

            if (!selectedGifRegion.HasValue || selectedGifRegion.Value.Width <= 1 || selectedGifRegion.Value.Height <= 1)
            {
                StatusText.Text = "GIF录制已取消或区域无效";
                LogToFile("MainWindow: GIF recording cancelled or invalid region.");
                return;
            }

            _gifRecordingRegion = selectedGifRegion.Value;
            LogToFile($"MainWindow: GIF recording region selected: {_gifRecordingRegion}");

            // Start 3-second countdown
            for (int i = 3; i > 0; i--)
            {
                SetStatus($"{i}秒后开始录制...", true);
                await Task.Delay(1000); // Wait 1 second
            }

            _isRecording = true;
            _gifFrames.Clear(); 
            RecordGifButton.Content = "停止录制";
            FullScreenButton.IsEnabled = false;
            RegionButton.IsEnabled = false;
            StatusText.Text = "GIF录制中...";
            LogToFile("MainWindow: GIF recording started for selected region.");

            _recordingTimer = new System.Threading.Timer(async _ => await CaptureGifFrame(), 
                                            null, 
                                            TimeSpan.Zero, 
                                            TimeSpan.FromMilliseconds(1000 / _framesPerSecond));
        }
        else
        {
            // Stop Recording
            _isRecording = false;
            if (_recordingTimer != null)
            {
                await _recordingTimer.DisposeAsync();
                _recordingTimer = null;
            }
            RecordGifButton.Content = "录制GIF";
            FullScreenButton.IsEnabled = true;
            RegionButton.IsEnabled = true;
            LogToFile("MainWindow: GIF recording stopped.");

            if (_gifFrames.Count > 0)
            {
                StatusText.Text = "正在保存GIF...";
                await SaveGifAsync(new List<System.Drawing.Bitmap>(_gifFrames)); // Pass a copy
            }
            else
            {
                StatusText.Text = "GIF录制已取消或无帧数据";
            }
            _gifFrames.Clear(); // Clear frames after attempting to save
        }
    }

    private async Task CaptureGifFrame()
    {
        if (!_isRecording || !_gifRecordingRegion.HasValue) return;

        Rect regionToCapture = _gifRecordingRegion.Value;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogToFile($"MainWindow: Capturing GIF frame for region {regionToCapture}.");
            try
            {
                var screen = Screens.Primary; // Assuming region is on primary screen for now
                                          // More robust: find screen containing the major part of regionToCapture
                if (screen == null)
                {
                    LogToFile("MainWindow: CaptureGifFrame - No primary screen found for GIF region.");
                    return; 
                }

                var scale = screen.Scaling;

                // RegionToCapture coordinates are logical pixels from RegionSelectionWindow (which covers primary screen)
                // We need to convert them to physical pixels for CopyFromScreen
                int scaledX = (int)(regionToCapture.X * scale);
                int scaledY = (int)(regionToCapture.Y * scale);
                int scaledWidth = (int)(regionToCapture.Width * scale);
                int scaledHeight = (int)(regionToCapture.Height * scale);

                // Ensure width and height are positive after scaling and conversion to int
                if (scaledWidth <= 0 || scaledHeight <= 0)
                {
                    LogToFile($"MainWindow: CaptureGifFrame - Invalid scaled dimensions for region: W={scaledWidth}, H={scaledHeight}. Original: {regionToCapture}");
                    // Optionally stop recording or skip frame
                    _isRecording = false; // Stop recording if region is invalid
                     if (_recordingTimer != null) _recordingTimer.Dispose(); _recordingTimer = null;
                     RecordGifButton.Content = "录制GIF";
                     FullScreenButton.IsEnabled = true;
                     RegionButton.IsEnabled = true;
                     StatusText.Text = "GIF区域无效，已停止";
                    return;
                }

                var bitmap = new System.Drawing.Bitmap(scaledWidth, scaledHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    // CopyFromScreen expects top-left X,Y in physical pixels of the screen
                    graphics.CopyFromScreen(scaledX, scaledY, 0, 0, new System.Drawing.Size(scaledWidth, scaledHeight), CopyPixelOperation.SourceCopy);
                }
                _gifFrames.Add(bitmap); 
                LogToFile($"MainWindow: GIF frame captured, total frames: {_gifFrames.Count}. Region: X={scaledX} Y={scaledY} W={scaledWidth} H={scaledHeight}");
            }
            catch (Exception ex)
            {
                LogToFile($"MainWindow: CaptureGifFrame error: {ex.Message}");
                // Optionally stop recording on error or skip frame
            }
        });
    }

    private async Task SaveGifAsync(List<System.Drawing.Bitmap> frames)
    {
        if (frames == null || frames.Count == 0)
        {
            LogToFile("MainWindow: SaveGifAsync - No frames to save.");
            StatusText.Text = "没有帧可用于保存GIF";
            return;
        }

        LogToFile($"MainWindow: SaveGifAsync - Saving {frames.Count} frames.");
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) 
        {
            LogToFile("MainWindow: SaveGifAsync - TopLevel is null.");
            StatusText.Text = "无法保存GIF: 内部错误";
            return;
        }

        var storageProvider = topLevel.StorageProvider;
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存GIF动画",
            SuggestedFileName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.gif",
            FileTypeChoices = new[] { new FilePickerFileType("GIF Image") { Patterns = new[] { "*.gif" } } },
            DefaultExtension = "gif"
        });

        if (file != null)
        {
            try
            {
                StatusText.Text = "正在编码GIF...";
                await Task.Run(() => // Run encoding on a background thread
                {
                    // New saving logic using AnimatedGif library
                    if (file == null) return;
                    string filePath = file.TryGetLocalPath(); // Attempt to get local path
                    if (string.IsNullOrEmpty(filePath))
                    {
                        LogToFile("MainWindow: SaveGifAsync - Could not get local path from storage file for AnimatedGif.");
                        // Fallback or error handling - AnimatedGif.Create needs a file path.
                        // For now, we'll log and skip saving if path is not available.
                        // A more robust solution might save to a temp path then stream to the storage file if direct path isn't usable by the lib.
                        frames.ForEach(f => f.Dispose()); // Clean up frames
                        frames.Clear();
                        StatusText.Text = "保存GIF失败: 无法获取本地路径";
                        return;
                    }

                    int frameDelayMilliseconds = 1000 / _framesPerSecond;
                    LogToFile($"MainWindow: Saving GIF with AnimatedGif. Target FPS: {_framesPerSecond}, Delay: {frameDelayMilliseconds}ms");

                    // AnimatedGif.Create takes file path and default frame delay in milliseconds.
                    using (var gif = AnimatedGif.AnimatedGif.Create(filePath, frameDelayMilliseconds))
                    {
                        foreach (var frameBitmap in frames)
                        {
                            // The AddFrame method in AnimatedGif typically expects an Image.
                            // We are using System.Drawing.Bitmap which is a subclass of Image.
                            // The `delay` parameter in AddFrame (-1 uses default, 0 is often not recommended as some viewers ignore it, >0 for specific delay in ms)
                            // The `quality` parameter is GifQuality.Default, GifQuality.Bit8, etc.
                            gif.AddFrame(frameBitmap, delay: -1, quality: GifQuality.Bit8); 
                            // We use delay: -1 to use the default delay set in AnimatedGif.Create.
                        }
                    }
                    LogToFile("MainWindow: AnimatedGif finished writing.");
                });

                LogToFile($"MainWindow: GIF saved to {file.Name}");
                StatusText.Text = $"GIF已保存: {file.Name}";
            }
            catch (Exception ex)
            {
                LogToFile($"MainWindow: SaveGifAsync error: {ex.Message}");
                StatusText.Text = "保存GIF失败: " + ex.Message;
            }
            finally
            {
                // Dispose all bitmaps in the frames list
                foreach (var frame in frames)
                {
                    frame.Dispose();
                }
                frames.Clear(); 
            }
        }
        else
        {
            LogToFile("MainWindow: SaveGifAsync - Save operation cancelled by user.");
            StatusText.Text = "GIF保存已取消";
            // Dispose frames if save is cancelled
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
            frames.Clear();
        }
    }

    public void SetStatus(string message, bool appendToLog = true)
    {
        if (StatusText != null)
        {
            StatusText.Text = message;
        }
        if (appendToLog)
        {
            LogToFile($"Status Update: {message}");
        }
    }

    public Config GetConfig() // New public method to access the current config
    {
        return _config;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: SettingsButton clicked.");
        var settingsWindow = new SettingsWindow(_config); // Pass the current config
        var result = await settingsWindow.ShowDialog<bool?>(this); // Show as dialog

        if (result.HasValue && result.Value) // If OK was clicked in SettingsWindow
        {
            LogToFile("MainWindow: SettingsWindow closed with OK. Saving configuration.");
            SaveConfig(); // Save any changes made to _config by SettingsWindow
            // Optionally, re-validate or update any UI elements in MainWindow that depend on settings
            // For Imgur Client ID, the check is done during upload, so no immediate UI update needed here usually.
            // However, we can update the general status if needed.
            if (string.IsNullOrWhiteSpace(_config.ImgurClientId))
            {
                 SetStatus("提示: Imgur Client ID 未设置，上传功能不可用。", false);
            }
            else
            {
                // UpdateStatusTextWithHotkeys(); // Or a more general status update
            }
        }
        else
        {
            LogToFile("MainWindow: SettingsWindow closed with Cancel or no explicit save.");
            // Reload config if settings were cancelled to revert any uncommitted changes in the passed _config object,
            // if SettingsWindow modifies it directly even on Cancel. For now, assume OK commits.
            // LoadConfig(); 
            // UpdateUiFromConfig(); 
        }
    }
}

// --- Config Class --- (Keep as is)
public class Config
{
    public string FullScreenHotkeyString { get; set; } = "Ctrl+Shift+U";
    public string RegionHotkeyString { get; set; } = "Ctrl+Shift+A";
    public string GifRecordHotkeyString { get; set; } = "Ctrl+Shift+G"; // Default GIF Record Hotkey
    public bool UseDefaultSavePath { get; set; } = false;
    public string? DefaultSavePath { get; set; } = null;
    public int NextSaveFileNumber { get; set; } = 1;
    public string? ImgurClientId { get; set; } = null; // Added for Imgur Client ID
    public string? ImgurClientSecret { get; set; } = null; // Added for Imgur Client Secret
    public string? ImgurAccessToken { get; set; } = null; // Added for Imgur Access Token
    public string? ImgurRefreshToken { get; set; } = null; // Added for Imgur Refresh Token
    public DateTime? ImgurTokenExpiresAt { get; set; } = null; // Added for Imgur Token Expiration
    [JsonIgnore] public uint FullScreenModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    [JsonIgnore] public uint FullScreenVirtualKeyCode { get; set; } = KeyInterop.VirtualKeyFromKey(Key.U);
    [JsonIgnore] public uint RegionModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    [JsonIgnore] public uint RegionVirtualKeyCode { get; set; } = KeyInterop.VirtualKeyFromKey(Key.A);
    [JsonIgnore] public uint GifRecordModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT; // Default modifiers for GIF Record
    [JsonIgnore] public uint GifRecordVirtualKeyCode { get; set; } = KeyInterop.VirtualKeyFromKey(Key.G); // Default VK_CODE for GIF Record

    public void UpdateModifiersAndVKCodeFromStrings()
    {
        KeyGesture? fsGesture = KeyGestureConverter.StringToKeyGesture(FullScreenHotkeyString);
        if (fsGesture != null)
        {
            uint modifiers = 0;
            if (fsGesture.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= NativeMethods.MOD_CONTROL;
            if (fsGesture.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
            if (fsGesture.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= NativeMethods.MOD_ALT;
            uint vkCode = KeyInterop.VirtualKeyFromKey(fsGesture.Key);

            if (vkCode != 0 && modifiers != 0)
            {
                this.FullScreenModifiers = modifiers;
                this.FullScreenVirtualKeyCode = vkCode;
            }
            else
            {
                ResetToDefaultFullScreenHotkey();
            }
        }
        else
        {
            ResetToDefaultFullScreenHotkey();
        }

        KeyGesture? rGesture = KeyGestureConverter.StringToKeyGesture(RegionHotkeyString);
        if (rGesture != null)
        {
            uint modifiers = 0;
            if (rGesture.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= NativeMethods.MOD_CONTROL;
            if (rGesture.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
            if (rGesture.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= NativeMethods.MOD_ALT;
            uint vkCode = KeyInterop.VirtualKeyFromKey(rGesture.Key);

            if (vkCode != 0 && modifiers != 0)
            {
                this.RegionModifiers = modifiers;
                this.RegionVirtualKeyCode = vkCode;
            }
            else
            {
                ResetToDefaultRegionHotkey();
            }
        }
        else
        {
            ResetToDefaultRegionHotkey();
        }

        KeyGesture? gifGesture = KeyGestureConverter.StringToKeyGesture(GifRecordHotkeyString);
        if (gifGesture != null)
        {
            uint modifiers = 0;
            if (gifGesture.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= NativeMethods.MOD_CONTROL;
            if (gifGesture.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
            if (gifGesture.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= NativeMethods.MOD_ALT;
            uint vkCode = KeyInterop.VirtualKeyFromKey(gifGesture.Key);

            if (vkCode != 0 && modifiers != 0)
            {
                this.GifRecordModifiers = modifiers;
                this.GifRecordVirtualKeyCode = vkCode;
            }
            else
            {
                ResetToDefaultGifRecordHotkey();
            }
        }
        else
        {
            ResetToDefaultGifRecordHotkey();
        }
    }

    private void ResetToDefaultFullScreenHotkey()
    {
        FullScreenHotkeyString = "Ctrl+Shift+U";
        FullScreenModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
        FullScreenVirtualKeyCode = KeyInterop.VirtualKeyFromKey(Key.U);
        LogToFile($"警告: 全屏快捷键无效或无法解析，已重置为默认值 {FullScreenHotkeyString}");
    }

    private void ResetToDefaultRegionHotkey()
    {
        RegionHotkeyString = "Ctrl+Shift+A";
        RegionModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
        RegionVirtualKeyCode = KeyInterop.VirtualKeyFromKey(Key.A);
        LogToFile($"警告: 区域快捷键无效或无法解析，已重置为默认值 {RegionHotkeyString}");
    }

    private void ResetToDefaultGifRecordHotkey()
    {
        GifRecordHotkeyString = "Ctrl+Shift+G";
        GifRecordModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
        GifRecordVirtualKeyCode = KeyInterop.VirtualKeyFromKey(Key.G);
        LogToFile($"警告: GIF录制快捷键无效或无法解析，已重置为默认值 {GifRecordHotkeyString}");
    }

    private static void LogToFile(string message) => MainWindow.LogToFile(message);
}

// --- Helper Classes --- (Keep as is)
public static class KeyGestureConverter
{
    public static string KeyGestureToString(Key key, KeyModifiers modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    public static KeyGesture? StringToKeyGesture(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Split('+');
        if (parts.Length == 0) return null;
        Key key = Key.None;
        KeyModifiers modifiers = KeyModifiers.None;
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (Enum.TryParse<Key>(trimmedPart, true, out var parsedKey))
            {
                key = parsedKey;
            }
            else if (trimmedPart.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Control;
            }
            else if (trimmedPart.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Shift;
            }
            else if (trimmedPart.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Alt;
            }
            else if (trimmedPart.Equals("Win", StringComparison.OrdinalIgnoreCase) || trimmedPart.Equals("Meta", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= KeyModifiers.Meta;
            }
        }
        if (key != Key.None)
        {
            return new KeyGesture(key, modifiers);
        }
        return null;
    }
}

public static class KeyInterop
{
    public static uint VirtualKeyFromKey(Key key) => key switch
    {
        Key.A => 0x41, Key.B => 0x42, Key.C => 0x43, Key.D => 0x44, Key.E => 0x45, Key.F => 0x46, Key.G => 0x47, Key.H => 0x48, Key.I => 0x49, Key.J => 0x4A, Key.K => 0x4B, Key.L => 0x4C, Key.M => 0x4D, Key.N => 0x4E, Key.O => 0x4F, Key.P => 0x50, Key.Q => 0x51, Key.R => 0x52, Key.S => 0x53, Key.T => 0x54, Key.U => 0x55, Key.V => 0x56, Key.W => 0x57, Key.X => 0x58, Key.Y => 0x59, Key.Z => 0x5A,
        Key.D0 => 0x30, Key.D1 => 0x31, Key.D2 => 0x32, Key.D3 => 0x33, Key.D4 => 0x34, Key.D5 => 0x35, Key.D6 => 0x36, Key.D7 => 0x37, Key.D8 => 0x38, Key.D9 => 0x39,
        Key.F1 => 0x70, Key.F2 => 0x71, Key.F3 => 0x72, Key.F4 => 0x73, Key.F5 => 0x74, Key.F6 => 0x75, Key.F7 => 0x76, Key.F8 => 0x77, Key.F9 => 0x78, Key.F10 => 0x79, Key.F11 => 0x7A, Key.F12 => 0x7B, Key.PrintScreen => 0x2C,
        _ => 0
    };
}
