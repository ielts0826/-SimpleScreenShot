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
using Avalonia.Controls.Shapes; // Add for Rectangle
using System.Linq; // Add for FirstOrDefault
using System.Text.Json; // Add for JSON serialization
using System.Text.Json.Serialization; // For JsonConverter
using System.ComponentModel; // For TypeConverter
using System.Collections.Generic; // For List
using Avalonia.Platform.Storage; // For FolderPicker

namespace ScreenCaptureTool;

// Enum to track which hotkey is being set
internal enum HotkeySettingMode { None, FullScreen, Region }

public partial class MainWindow : Window
{
    // Constants for Hotkey IDs
    private const int HOTKEY_ID_FULLSCREEN = 9000;
    private const int HOTKEY_ID_REGION = 9001;

    // State variables
    private bool _isCapturing = false;
    private HotkeySettingMode _settingHotkeyMode = HotkeySettingMode.None; // Track which hotkey is being set
    private Avalonia.Point _startPoint;
    private Avalonia.Point _endPoint;
    private System.Drawing.Bitmap? _fullScreenBitmapForRegionSelection = null; // 用于区域选择时的背景
    private readonly string _configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenCaptureTool", "config.json"); // Use System.IO.Path
    private Config _config = new(); // Initialize with default config

    // Controls
    private Grid Overlay => this.FindControl<Grid>("Overlay");
    private Canvas SelectionCanvas => this.FindControl<Canvas>("SelectionCanvas");
    private Avalonia.Controls.Image BackgroundCaptureImage => this.FindControl<Avalonia.Controls.Image>("BackgroundCaptureImage");
    private TextBlock StatusText => this.FindControl<TextBlock>("StatusText");
    private TextBox FullScreenHotkeyTextBox => this.FindControl<TextBox>("FullScreenHotkeyTextBox");
    private Button SetFullScreenHotkeyButton => this.FindControl<Button>("SetFullScreenHotkeyButton");
    private TextBox RegionHotkeyTextBox => this.FindControl<TextBox>("RegionHotkeyTextBox");
    private Button SetRegionHotkeyButton => this.FindControl<Button>("SetRegionHotkeyButton");
    private CheckBox UseDefaultPathCheckBox => this.FindControl<CheckBox>("UseDefaultPathCheckBox");
    private TextBox DefaultPathTextBox => this.FindControl<TextBox>("DefaultPathTextBox");
    private Button BrowseDefaultPathButton => this.FindControl<Button>("BrowseDefaultPathButton");


    // 用于绘制选框的矩形
    private Avalonia.Controls.Shapes.Rectangle _selectionRectangle = new Avalonia.Controls.Shapes.Rectangle
    {
        Stroke = Avalonia.Media.Brushes.Red, // 使用 Avalonia.Media.Brushes
        StrokeThickness = 2,
        Fill = Avalonia.Media.Brushes.Transparent, // 使用 Avalonia.Media.Brushes
        IsVisible = false
    };

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

            // 将选框矩形添加到Canvas
            if (SelectionCanvas != null)
            {
                 SelectionCanvas.Children.Add(_selectionRectangle);
                 LogToFile("MainWindow: 选框矩形已添加到Canvas");
            }
            else
            {
                 LogToFile("MainWindow: 错误 - 未找到SelectionCanvas");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: InitializeComponent 出错: {ex}");
            throw;
        }
    }

    // --- Config Methods ---
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
                 LogToFile($"配置加载完成: FullScreen={_config.FullScreenHotkeyString}, Region={_config.RegionHotkeyString}, UseDefaultPath={_config.UseDefaultSavePath}, DefaultPath={_config.DefaultSavePath}, NextNum={_config.NextSaveFileNumber}");
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

    // Update UI elements based on the loaded configuration
    private void UpdateUiFromConfig()
    {
        UpdateHotkeyDisplay();
        UpdateDefaultPathDisplay();
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
         // Update status text based on registration status (done in InitializeHotkey)
         if (StatusText != null && _settingHotkeyMode == HotkeySettingMode.None) // Avoid overwriting "Press key..." message
         {
             UpdateStatusTextWithHotkeys(); // Use helper method
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
             DefaultPathTextBox.Text = _config.DefaultSavePath ?? ""; // Show empty string if null
             DefaultPathTextBox.IsEnabled = _config.UseDefaultSavePath; // Enable/disable based on checkbox
         }
         if (BrowseDefaultPathButton != null)
         {
             BrowseDefaultPathButton.IsEnabled = _config.UseDefaultSavePath; // Enable/disable based on checkbox
         }
         LogToFile($"更新默认路径显示: Enabled={_config.UseDefaultSavePath}, Path={_config.DefaultSavePath}");
    }


    // --- Logging ---
    public static void LogToFile(string message) // Made public static
    {
        try
        {
            string logPath = "avalonia_app_log.txt";
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            System.IO.File.AppendAllText(logPath, logMessage, Encoding.UTF8); // Use System.IO.File
        }
        catch
        {
            // 如果日志记录失败，忽略异常
        }
    }

    // --- Screenshot Logic ---
    private async void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: 全屏截图按钮被点击");
        try
        {
            StatusText.Text = "正在截取全屏...";
            await CaptureFullScreen(); // 使用 await
            // StatusText will be updated by SaveScreenshot or ShowPreview
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 全屏截图出错: {ex}");
            StatusText.Text = "截图失败: " + ex.Message;
            this.Show(); // Ensure window is visible after error
        }
    }

    private void RegionButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: 区域截图按钮被点击");
        try
        {
            StatusText.Text = "请选择截图区域";
            StartRegionCapture();
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 区域截图出错: {ex}");
            StatusText.Text = "截图失败: " + ex.Message;
        }
    }

    private async Task CaptureFullScreen() // 改为 async Task
    {
        LogToFile("MainWindow: 开始全屏截图");

        // 截图前隐藏窗口
        this.Hide();
        await Task.Delay(200); // 短暂延迟确保窗口完全隐藏

        try
        {
            // 获取屏幕尺寸
            var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

            LogToFile($"MainWindow: 屏幕尺寸: {screenWidth}x{screenHeight}");

            // 创建位图
            using (var bitmap = new System.Drawing.Bitmap(screenWidth, screenHeight))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // 截取屏幕
                    graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
                }

                // 保存截图
                await SaveScreenshot(bitmap); // 使用 await
            }
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 全屏截图过程出错: {ex}");
            this.Show(); // 确保出错时窗口也能显示回来
            throw; // Re-throw so the caller knows about the error
         }
         // finally // 移除 finally 块中的 this.Show()
         // {
         //      // 确保窗口最终可见
         //      // this.Show(); // <--- 移除此行
         // }
     }

    private async void StartRegionCapture()
    {
        LogToFile("MainWindow: 开始区域截图");
        StatusText.Text = "准备区域截图中...";

        // 1. 截取全屏作为背景
        try
        {
            // 截图前隐藏窗口
            this.Hide();
            await Task.Delay(200); // 短暂延迟确保窗口完全隐藏

            var screen = this.Screens.Primary ?? this.Screens.All.FirstOrDefault();
            if (screen == null)
            {
                LogToFile("MainWindow: 无法获取屏幕信息");
                StatusText.Text = "错误: 无法获取屏幕信息";
                this.Show();
                return;
            }

            var pixelSize = screen.Bounds.Size;
            var scaling = screen.Scaling; // 使用屏幕的缩放因子
            int screenWidth = pixelSize.Width;
            int screenHeight = pixelSize.Height;

            LogToFile($"MainWindow: 屏幕信息 - 大小: {screenWidth}x{screenHeight}, 缩放: {scaling}");

            _fullScreenBitmapForRegionSelection?.Dispose(); // Dispose previous if any
            _fullScreenBitmapForRegionSelection = new System.Drawing.Bitmap(screenWidth, screenHeight);
            using (var graphics = Graphics.FromImage(_fullScreenBitmapForRegionSelection))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
            }
            LogToFile("MainWindow: 全屏背景已捕获");

            // 2. 将截图设置为背景并显示覆盖层
            using (var ms = new MemoryStream())
            {
                _fullScreenBitmapForRegionSelection.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                var avaloniaBgBitmap = new AvaloniaBitmap(ms);
                BackgroundCaptureImage.Source = avaloniaBgBitmap;
                BackgroundCaptureImage.IsVisible = true;
            }

            // 3. 设置状态并显示覆盖层
            _isCapturing = true;
            _startPoint = new Avalonia.Point();
            _endPoint = new Avalonia.Point();

            if (Overlay != null)
            {
                Overlay.ZIndex = 100;
                Overlay.IsVisible = true;

                // 添加鼠标事件处理
                Overlay.PointerPressed += Overlay_PointerPressed;
                Overlay.PointerMoved += Overlay_PointerMoved;
                Overlay.PointerReleased += Overlay_PointerReleased;

                // 将窗口置于全屏
                this.WindowState = WindowState.FullScreen;
                this.Show(); // 显示窗口（之前隐藏了）
                this.Activate();
                StatusText.Text = "请拖动鼠标选择区域";
                LogToFile("MainWindow: 区域截图模式启动，窗口已全屏/激活");
            }
            else
            {
                LogToFile("MainWindow: Overlay 控件未找到!");
                StatusText.Text = "错误: 无法启动区域截图";
                this.Show(); // 确保窗口显示
            }
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: StartRegionCapture 出错: {ex}");
            StatusText.Text = "错误: 无法启动区域截图";
            _isCapturing = false;
            if (_fullScreenBitmapForRegionSelection != null)
            {
                _fullScreenBitmapForRegionSelection.Dispose();
                _fullScreenBitmapForRegionSelection = null;
            }
            this.Show(); // 确保窗口显示
        }
    }

    private void Overlay_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_isCapturing)
        {
            LogToFile("MainWindow: 区域选择开始点");
            _startPoint = e.GetPosition(Overlay);
            // 重置并显示选框
            Canvas.SetLeft(_selectionRectangle, _startPoint.X);
            Canvas.SetTop(_selectionRectangle, _startPoint.Y);
            _selectionRectangle.Width = 0;
            _selectionRectangle.Height = 0;
            _selectionRectangle.IsVisible = true;
            e.Handled = true; // 标记事件已处理
        }
    }

    private void Overlay_PointerMoved(object sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_isCapturing && e.GetCurrentPoint(Overlay).Properties.IsLeftButtonPressed) // 确保鼠标左键按下时才更新
        {
            _endPoint = e.GetPosition(Overlay);
            // 更新选框位置和大小
            var x = Math.Min(_startPoint.X, _endPoint.X);
            var y = Math.Min(_startPoint.Y, _endPoint.Y);
            var width = Math.Abs(_startPoint.X - _endPoint.X);
            var height = Math.Abs(_startPoint.Y - _endPoint.Y);

            Canvas.SetLeft(_selectionRectangle, x);
            Canvas.SetTop(_selectionRectangle, y);
            _selectionRectangle.Width = width;
            _selectionRectangle.Height = height;

            // LogToFile($"Drawing selection rectangle: {x},{y} {width}x{height}");
            e.Handled = true; // 标记事件已处理
        }
    }

    private async void Overlay_PointerReleased(object sender, Avalonia.Input.PointerReleasedEventArgs e) // 改为 async void
    {
        if (_isCapturing)
        {
            LogToFile("MainWindow: 区域选择结束点");
            _endPoint = e.GetPosition(Overlay);

            // 清理事件处理
            Overlay.PointerPressed -= Overlay_PointerPressed;
            Overlay.PointerMoved -= Overlay_PointerMoved;
            Overlay.PointerReleased -= Overlay_PointerReleased;

            // 隐藏覆盖层和选框
            Overlay.IsVisible = false;
            Overlay.ZIndex = 0; // 恢复 ZIndex
            _selectionRectangle.IsVisible = false;

            // 恢复窗口状态
            this.WindowState = WindowState.Normal;

            // 重置状态
            _isCapturing = false;

            // 截取选定区域
            await CaptureRegion(); // 使用 await
            e.Handled = true; // 标记事件已处理
        }
    }

    private async Task CaptureRegion() // 改为 async Task
    {
        LogToFile("MainWindow: 截取选定区域");

        try
        {
            // 计算选择区域 (相对于Overlay)
            int x = (int)Math.Min(_startPoint.X, _endPoint.X);
            int y = (int)Math.Min(_startPoint.Y, _endPoint.Y);
            int width = (int)Math.Abs(_endPoint.X - _startPoint.X);
            int height = (int)Math.Abs(_endPoint.Y - _startPoint.Y);

            // 确保有效的区域
            if (width <= 0 || height <= 0)
            {
                LogToFile("MainWindow: 无效的选择区域");
                StatusText.Text = "请选择有效的区域";
                BackgroundCaptureImage.Source = null; // Clear background image
                BackgroundCaptureImage.IsVisible = false;
                _fullScreenBitmapForRegionSelection?.Dispose();
                _fullScreenBitmapForRegionSelection = null;
                return;
            }

            // 使用之前捕获的全屏截图进行裁剪
            if (_fullScreenBitmapForRegionSelection == null)
            {
                LogToFile("MainWindow: 错误 - 背景截图丢失");
                StatusText.Text = "截图失败: 背景丢失";
                BackgroundCaptureImage.Source = null; // Clear background image
                BackgroundCaptureImage.IsVisible = false;
                return;
            }

            // 获取正确的屏幕缩放因子
            var screen = this.Screens.Primary ?? this.Screens.All.FirstOrDefault();
            double scaling = screen?.Scaling ?? 1.0;

            // 计算裁剪区域 (使用缩放因子)
            int cropX = (int)(x * scaling);
            int cropY = (int)(y * scaling);
            int cropWidth = (int)(width * scaling);
            int cropHeight = (int)(height * scaling);

            // 确保裁剪区域在背景图范围内
            cropX = Math.Max(0, cropX);
            cropY = Math.Max(0, cropY);
            cropWidth = Math.Min(_fullScreenBitmapForRegionSelection.Width - cropX, cropWidth);
            cropHeight = Math.Min(_fullScreenBitmapForRegionSelection.Height - cropY, cropHeight);

            if (cropWidth <= 0 || cropHeight <= 0)
            {
                 LogToFile("MainWindow: 无效的裁剪区域");
                 StatusText.Text = "请选择有效的区域";
                 _fullScreenBitmapForRegionSelection.Dispose(); // 清理背景图
                 _fullScreenBitmapForRegionSelection = null;
                 BackgroundCaptureImage.Source = null; // 清理背景显示
                 BackgroundCaptureImage.IsVisible = false;
                 return;
            }


            LogToFile($"MainWindow: 裁剪区域 (背景图坐标): {cropX},{cropY} {cropWidth}x{cropHeight} (Scaling: {scaling})");

            // 创建最终截图的位图
            using (var finalBitmap = new System.Drawing.Bitmap(cropWidth, cropHeight))
            {
                using (var graphics = Graphics.FromImage(finalBitmap))
                {
                    // 从背景图中裁剪出选定区域
                    graphics.DrawImage(_fullScreenBitmapForRegionSelection,
                                       new System.Drawing.Rectangle(0, 0, cropWidth, cropHeight), // 目标矩形 (最终位图) - 使用 System.Drawing.Rectangle
                                       new System.Drawing.Rectangle(cropX, cropY, cropWidth, cropHeight), // 源矩形 (背景图) - 使用 System.Drawing.Rectangle
                                       GraphicsUnit.Pixel);
                }

                // 保存最终截图
                await SaveScreenshot(finalBitmap); // 使用 await
            }

            // 清理背景截图资源
            _fullScreenBitmapForRegionSelection.Dispose();
            _fullScreenBitmapForRegionSelection = null;
            BackgroundCaptureImage.Source = null;
            BackgroundCaptureImage.IsVisible = false;
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 区域截图过程出错: {ex}");
            StatusText.Text = "截图失败: " + ex.Message;
            this.Show(); // 确保出错时窗口也能显示回来
         }
         // finally // 移除 finally 块中的 this.Show()
         // {
         //      // 确保窗口最终可见
         //      // this.Show(); // <--- 移除此行
         // }
     }

    private async Task SaveScreenshot(System.Drawing.Bitmap bitmap) // 改为 async Task
    {
        LogToFile("MainWindow: 保存截图");
        string? finalSavePath = null; // Store the final path for status update

        try
        {
            // Check if default path is enabled and valid
            if (_config.UseDefaultSavePath && !string.IsNullOrEmpty(_config.DefaultSavePath) && Directory.Exists(_config.DefaultSavePath))
            {
                LogToFile($"MainWindow: 使用默认路径保存: {_config.DefaultSavePath}");
                string filename;
                string filePath;
                int currentFileNumber = _config.NextSaveFileNumber;

                // Find the next available filename
                do
                {
                    filename = $"{currentFileNumber}.png"; // Always save as PNG for simplicity now
                    filePath = System.IO.Path.Combine(_config.DefaultSavePath, filename);
                    currentFileNumber++;
                } while (System.IO.File.Exists(filePath));

                LogToFile($"MainWindow: 自动保存到: {filePath}");
                bitmap.Save(filePath, ImageFormat.Png); // Save directly
                finalSavePath = filePath;

                // Update and save the next file number
                _config.NextSaveFileNumber = currentFileNumber;
                SaveConfig(); // Save config immediately after successful save

                StatusText.Text = "截图已自动保存到: " + finalSavePath;
                ShowPreview(bitmap); // Show preview after saving
            }
            else
            {
                 if (_config.UseDefaultSavePath)
                 {
                      LogToFile($"MainWindow: 默认路径无效或未设置 (Path='{_config.DefaultSavePath}'), 回退到另存为对话框");
                 }
                 else
                 {
                      LogToFile($"MainWindow: 未启用默认路径，显示另存为对话框");
                 }

                // Fallback to Save File Dialog
                var dialog = new SaveFileDialog
                {
                    Title = "保存截图",
                    DefaultExtension = ".png",
                    Filters = new System.Collections.Generic.List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "PNG图片", Extensions = { "png" } },
                        new FileDialogFilter { Name = "JPEG图片", Extensions = { "jpg", "jpeg" } },
                        new FileDialogFilter { Name = "BMP图片", Extensions = { "bmp" } }
                    }
                };

                // 异步显示对话框
                var result = await dialog.ShowAsync(this); // 使用 await

                if (result != null)
                {
                    LogToFile($"MainWindow: 保存截图到: {result}");
                    finalSavePath = result;

                    // 根据文件扩展名选择格式
                    ImageFormat format = ImageFormat.Png;
                    if (result.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        result.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        format = ImageFormat.Jpeg;
                    }
                    else if (result.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    {
                        format = ImageFormat.Bmp;
                    }

                    // 保存文件
                    bitmap.Save(result, format);

                    // 显示预览
                    ShowPreview(bitmap);

                    StatusText.Text = "截图已保存到: " + result;
                }
                else
                {
                    LogToFile("MainWindow: 用户取消保存");
                    StatusText.Text = "已取消保存";
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 保存截图出错: {ex}");
            StatusText.Text = "保存失败: " + ex.Message;
        }
    }


    private void ShowPreview(System.Drawing.Bitmap bitmap)
    {
         LogToFile("MainWindow: 显示预览");

         try
         {
             // // 确保主窗口可见 // <--- 移除此行
             // this.IsVisible = true; // <--- 移除此行

             // 转换为Avalonia位图
             using (var memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;

                var avaloniaBitmap = new AvaloniaBitmap(memoryStream);

                // 显示预览 (如果需要的话，可以在主窗口显示)
                // PreviewImage.Source = avaloniaBitmap;
                // PreviewImage.IsVisible = true;

                // 显示置顶窗口
                var topWindow = new TopmostWindow(avaloniaBitmap);
                topWindow.Closed += (s, e) => {
                    LogToFile("TopmostWindow closed.");
                    // 清理资源 - 不能直接访问 MainWindow 的 PreviewImage
                    // PreviewImage.Source = null;
                    // _capturedBitmap?.Dispose();
                    // avaloniaBitmap.Dispose();
                };
                LogToFile("Showing TopmostWindow...");
                topWindow.Show(); // 使用 Show() 而不是 ShowDialog()，避免阻塞主窗口
            }
        }
        catch (Exception ex)
        {
            LogToFile($"MainWindow: 显示预览出错: {ex}");
            StatusText.Text = "显示预览失败: " + ex.Message;
        }
    }

    // --- Hotkey Logic ---
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LogToFile("MainWindow: 窗口已打开");
        Console.WriteLine("MainWindow: 窗口已打开");
        // 稍微延迟热键注册，确保窗口句柄可用
        Dispatcher.UIThread.Post(InitializeHotKeys, DispatcherPriority.Background); // Call plural version
    }

    // 添加窗口关闭事件处理以注销热键
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        LogToFile("MainWindow: 窗口正在关闭，注销热键");
        UnregisterHotKeys(); // Call plural version
        SaveConfig(); // Save config on closing
        base.OnClosing(e);
    }

    private void InitializeHotKeys() // Renamed to plural
    {
        var platformImpl = this.PlatformImpl;
        if (platformImpl == null || !platformImpl.TryGetFeature<IPlatformHandle>(out var platformHandle) || platformHandle.Handle == IntPtr.Zero)
        {
            LogToFile("MainWindow: 获取窗口句柄失败，无法注册热键");
            Console.WriteLine("Error: Could not get window handle to register hotkey.");
            StatusText.Text = "无法注册热键";
             LogToFile("MainWindow: 获取窗口句柄失败，将尝试注册全局热键 (hWnd=IntPtr.Zero)");
             // StatusText.Text = "无法注册窗口热键，尝试全局注册"; // Optional status update
             // return; // Don't return, try global registration
         }
         // var handle = platformHandle?.Handle ?? IntPtr.Zero; // Use IntPtr.Zero for global hotkeys
         var handle = IntPtr.Zero; // Explicitly use IntPtr.Zero for global hotkeys
         LogToFile($"MainWindow: 使用句柄 {handle} 注册热键 (IntPtr.Zero 表示全局)");
         bool fsSuccess = false;
         bool regionSuccess = false;

        // --- Register FullScreen Hotkey ---
        LogToFile($"MainWindow: 尝试注册全屏热键 {_config.FullScreenHotkeyString}");
        NativeMethods.UnregisterHotKey(handle, HOTKEY_ID_FULLSCREEN); // Unregister previous first
        if (!NativeMethods.RegisterHotKey(handle, HOTKEY_ID_FULLSCREEN, _config.FullScreenModifiers, _config.FullScreenVirtualKeyCode))
        {
             int errorCode = Marshal.GetLastWin32Error();
             LogToFile($"MainWindow: 注册全屏热键失败，Win32 错误码: {errorCode}");
             Console.WriteLine($"Error registering fullscreen hotkey {_config.FullScreenHotkeyString}. Win32 Error Code: {errorCode}");
        }
        else
        {
             LogToFile($"MainWindow: 全屏热键 {_config.FullScreenHotkeyString} 注册成功");
             Console.WriteLine($"Fullscreen Hotkey {_config.FullScreenHotkeyString} registered successfully.");
             fsSuccess = true;
        }

        // --- Register Region Hotkey ---
        LogToFile($"MainWindow: 尝试注册区域热键 {_config.RegionHotkeyString}");
        NativeMethods.UnregisterHotKey(handle, HOTKEY_ID_REGION); // Unregister previous first
        if (!NativeMethods.RegisterHotKey(handle, HOTKEY_ID_REGION, _config.RegionModifiers, _config.RegionVirtualKeyCode))
        {
             int errorCode = Marshal.GetLastWin32Error();
             LogToFile($"MainWindow: 注册区域热键失败，Win32 错误码: {errorCode}");
             Console.WriteLine($"Error registering region hotkey {_config.RegionHotkeyString}. Win32 Error Code: {errorCode}");
        }
        else
        {
             LogToFile($"MainWindow: 区域热键 {_config.RegionHotkeyString} 注册成功");
             Console.WriteLine($"Region Hotkey {_config.RegionHotkeyString} registered successfully.");
             regionSuccess = true;
        }

        // Update Status Text based on success
        if (fsSuccess && regionSuccess)
        {
             UpdateStatusTextWithHotkeys();
        }
        else if (fsSuccess)
        {
             StatusText.Text = $"全屏: {_config.FullScreenHotkeyString} (区域注册失败)";
        }
        else if (regionSuccess)
        {
             StatusText.Text = $"区域: {_config.RegionHotkeyString} (全屏注册失败)";
        }
        else
        {
             StatusText.Text = "热键注册失败";
        }
    }

    private void UnregisterHotKeys() // Renamed to plural
    {
         LogToFile("MainWindow: 尝试注销全局热键 (hWnd=IntPtr.Zero)");
         // var platformImpl = this.PlatformImpl;
         // IntPtr handle = IntPtr.Zero;
         // if (platformImpl != null && platformImpl.TryGetFeature<IPlatformHandle>(out var platformHandle) && platformHandle.Handle != IntPtr.Zero)
         // {
         //     handle = platformHandle.Handle; // Keep handle if needed for window-specific unregister, but we registered globally
         // }
         // else
         // {
         //      LogToFile("MainWindow: 获取窗口句柄失败，但仍尝试注销全局热键");
         // }

         // Always try to unregister with IntPtr.Zero as we registered with it
         NativeMethods.UnregisterHotKey(IntPtr.Zero, HOTKEY_ID_FULLSCREEN);
         NativeMethods.UnregisterHotKey(IntPtr.Zero, HOTKEY_ID_REGION);
         LogToFile("MainWindow: 全局热键已尝试注销");
         Console.WriteLine("Global hotkeys unregistered.");
     }

    private void SetFullScreenHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        StartSettingHotkey(HotkeySettingMode.FullScreen);
    }

     private void SetRegionHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        StartSettingHotkey(HotkeySettingMode.Region);
    }

    private void StartSettingHotkey(HotkeySettingMode mode)
    {
        string type = mode == HotkeySettingMode.FullScreen ? "全屏" : "区域";
        LogToFile($"MainWindow: 设置 {type} 热键按钮点击");
        StatusText.Text = $"请按下新的 {type} 快捷键组合 (或按 Esc 取消)...";
        _settingHotkeyMode = mode;
        // 暂时禁用按钮
        SetFullScreenHotkeyButton.IsEnabled = false;
        SetRegionHotkeyButton.IsEnabled = false;
        this.Focus(); // 尝试将焦点设置到窗口以捕获按键
        LogToFile("MainWindow: 进入热键设置模式，等待按键...");
    }

    // --- Default Path Settings Logic ---
    private void UseDefaultPathCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_config != null && UseDefaultPathCheckBox != null)
        {
            _config.UseDefaultSavePath = UseDefaultPathCheckBox.IsChecked ?? false;
            LogToFile($"MainWindow: UseDefaultSavePath 设置为 {_config.UseDefaultSavePath}");
            UpdateDefaultPathDisplay(); // Update UI state (enable/disable textbox/button)
            SaveConfig(); // Save change immediately
        }
    }

    private async void BrowseDefaultPathButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: 浏览默认路径按钮点击");
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanPickFolder)
        {
            LogToFile("MainWindow: 存储提供者不支持选择文件夹");
            StatusText.Text = "错误: 无法选择文件夹";
            return;
        }

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择默认保存文件夹",
            AllowMultiple = false
        });

        if (result != null && result.Count > 0)
        {
            var folder = result[0];
            // Use the Path property which returns a Uri
            var folderUri = folder.Path; 
            if (folderUri != null && folderUri.IsAbsoluteUri && folderUri.IsFile)
            {
                 _config.DefaultSavePath = folderUri.LocalPath;
                 LogToFile($"MainWindow: 选择的默认路径: {_config.DefaultSavePath}");
            }
            else
            {
                 LogToFile($"MainWindow: 获取文件夹路径失败或路径无效: {folderUri}");
                 StatusText.Text = "错误: 无法获取有效的文件夹路径";
                 _config.DefaultSavePath = null; // Clear invalid path
            }
            
            UpdateDefaultPathDisplay();
            SaveConfig();
        }
        else
        {
             LogToFile("MainWindow: 用户取消选择文件夹或选择无效");
        }
    }


    // --- Keyboard Event Handling ---
    protected override void OnKeyDown(KeyEventArgs e)
    {
        LogToFile($"MainWindow: OnKeyDown - Key={e.Key}, Modifiers={e.KeyModifiers}, SettingMode={_settingHotkeyMode}, IsCapturing={_isCapturing}");
        bool handled = true; // Assume handled unless explicitly set otherwise

        if (_settingHotkeyMode != HotkeySettingMode.None)
        {
            LogToFile("MainWindow: 处理热键设置...");
            if (e.Key == Key.Escape)
            {
                 StatusText.Text = "已取消设置热键";
                 LogToFile("MainWindow: 取消设置热键 (Esc)");
            }
            else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl &&
                e.Key != Key.LeftShift && e.Key != Key.RightShift && e.Key != Key.LeftAlt &&
                e.Key != Key.RightAlt && e.Key != Key.LWin && e.Key != Key.RWin && e.Key != Key.System) // 忽略纯修饰键和系统键
            {
                uint modifiers = 0;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= NativeMethods.MOD_CONTROL;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= NativeMethods.MOD_ALT;

                uint vkCode = KeyInterop.VirtualKeyFromKey(e.Key);

                if (vkCode != 0 && modifiers != 0) // 需要至少一个修饰键 + 一个普通键
                {
                    string newHotkeyString = KeyGestureConverter.KeyGestureToString(e.Key, e.KeyModifiers);
                    LogToFile($"MainWindow: 捕获到新热键: {newHotkeyString} (Modifiers={modifiers}, VK={vkCode}) for {_settingHotkeyMode}");

                    UnregisterHotKeys(); // 注销旧热键

                    // Update the correct config property
                    if (_settingHotkeyMode == HotkeySettingMode.FullScreen)
                    {
                        _config.FullScreenModifiers = modifiers;
                        _config.FullScreenVirtualKeyCode = vkCode;
                        _config.FullScreenHotkeyString = newHotkeyString;
                    }
                    else if (_settingHotkeyMode == HotkeySettingMode.Region)
                    {
                         _config.RegionModifiers = modifiers;
                         _config.RegionVirtualKeyCode = vkCode;
                         _config.RegionHotkeyString = newHotkeyString;
                    }

                    InitializeHotKeys(); // 尝试注册新热键 (InitializeHotKeys会更新StatusText)
                    SaveConfig(); // 保存配置
                    UpdateHotkeyDisplay(); // 更新界面显示
                }
                else
                {
                     StatusText.Text = "无效的热键组合 (需包含Ctrl/Shift/Alt + 普通键)";
                     LogToFile("MainWindow: 无效的热键组合");
                }
            }
            else // 用户按了纯修饰键或 Esc
            {
                 LogToFile("MainWindow: 按下纯修饰键或Esc，等待或取消...");
                 if (e.Key != Key.Escape) handled = false; // 允许继续处理修饰键本身
            }

            // 如果不是只按了修饰键，则退出设置模式
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl &&
                e.Key != Key.LeftShift && e.Key != Key.RightShift && e.Key != Key.LeftAlt &&
                e.Key != Key.RightAlt && e.Key != Key.LWin && e.Key != Key.RWin && e.Key != Key.System)
            {
                _settingHotkeyMode = HotkeySettingMode.None;
                SetFullScreenHotkeyButton.IsEnabled = true; // 重新启用按钮
                SetRegionHotkeyButton.IsEnabled = true;
                LogToFile("MainWindow: 退出热键设置模式");
                // 恢复默认状态文本 (如果热键注册成功，InitializeHotKeys会更新)
                if (StatusText != null && !StatusText.Text.Contains("注册成功") && !StatusText.Text.Contains("注册失败"))
                {
                     Dispatcher.UIThread.Post(UpdateStatusTextWithHotkeys, DispatcherPriority.Background);
                }
            }
        }
        else if (_isCapturing)
        {
            // 处理截图取消逻辑 (Esc)
            if (e.Key == Key.Escape)
            {
                LogToFile("MainWindow: Esc按下，取消截图");
                _isCapturing = false;
                if (Overlay != null)
                {
                    Overlay.IsVisible = false;
                    Overlay.ZIndex = 0; // 恢复 ZIndex
                    // 清理事件处理
                    Overlay.PointerPressed -= Overlay_PointerPressed;
                    Overlay.PointerMoved -= Overlay_PointerMoved;
                    Overlay.PointerReleased -= Overlay_PointerReleased;
                }
                if (_selectionRectangle != null) _selectionRectangle.IsVisible = false; // 隐藏选框
                this.WindowState = WindowState.Normal; // 恢复窗口状态
                BackgroundCaptureImage.Source = null; // 清理背景显示
                BackgroundCaptureImage.IsVisible = false;
                if (_fullScreenBitmapForRegionSelection != null) // 清理背景图
                {
                     _fullScreenBitmapForRegionSelection.Dispose();
                     _fullScreenBitmapForRegionSelection = null;
                }
                StatusText.Text = "截图已取消";
            }
            else
            {
                 handled = false; // 如果不是Esc，取消处理标记
            }
        }
        else
        {
            // 处理截图触发逻辑 (窗口内快捷键)
            uint currentModifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) currentModifiers |= NativeMethods.MOD_CONTROL;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) currentModifiers |= NativeMethods.MOD_SHIFT;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) currentModifiers |= NativeMethods.MOD_ALT;
            uint currentVKCode = KeyInterop.VirtualKeyFromKey(e.Key);

            if (currentVKCode != 0)
            {
                if (currentVKCode == _config.FullScreenVirtualKeyCode && currentModifiers == _config.FullScreenModifiers)
                {
                    LogToFile($"MainWindow: 检测到配置的全屏热键 {_config.FullScreenHotkeyString} (窗口内)");
                    FullScreenButton_Click(this, new RoutedEventArgs());
                }
                else if (currentVKCode == _config.RegionVirtualKeyCode && currentModifiers == _config.RegionModifiers)
                {
                     LogToFile($"MainWindow: 检测到配置的区域热键 {_config.RegionHotkeyString} (窗口内)");
                     RegionButton_Click(this, new RoutedEventArgs());
                }
                else
                {
                     handled = false; // 如果不是我们的热键，取消处理标记
                }
            }
            else
            {
                 handled = false; // 无效按键码
            }
        }

        // 如果事件未被处理，调用基类方法
        if (!handled)
        {
            base.OnKeyDown(e);
        }
    }

    private void UpdateStatusTextWithHotkeys()
    {
         if (StatusText != null && _settingHotkeyMode == HotkeySettingMode.None)
         {
              // Check registration status? For now, just display configured keys.
              StatusText.Text = $"全屏: {_config.FullScreenHotkeyString} | 区域: {_config.RegionHotkeyString}";
         }
    }
    
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        LogToFile("MainWindow: 帮助按钮点击");
        var helpWindow = new HelpWindow();
        helpWindow.ShowDialog(this); // Show as modal dialog relative to main window
    }

    // TODO: 实现全局热键消息监听 (这部分比较复杂，暂时依赖窗口焦点)
}

// --- Config Class ---
public class Config
{
    // Hotkeys
    public string FullScreenHotkeyString { get; set; } = "Ctrl+Shift+U";
    public string RegionHotkeyString { get; set; } = "Ctrl+Shift+A"; // Default for region

    // Default Save Path Settings
    public bool UseDefaultSavePath { get; set; } = false;
    public string? DefaultSavePath { get; set; } = null; // Nullable string
    public int NextSaveFileNumber { get; set; } = 1;

    // Internal representation for registration
    [JsonIgnore]
    public uint FullScreenModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    [JsonIgnore]
    public uint FullScreenVirtualKeyCode { get; set; } = KeyInterop.VirtualKeyFromKey(Key.U);

    [JsonIgnore]
    public uint RegionModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    [JsonIgnore]
    public uint RegionVirtualKeyCode { get; set; } = KeyInterop.VirtualKeyFromKey(Key.A);

    // Method to update internal values from strings after loading or setting
    public void UpdateModifiersAndVKCodeFromStrings()
    {
        // Fullscreen
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
            else { ResetToDefaultFullScreenHotkey(); }
        }
        else { ResetToDefaultFullScreenHotkey(); }

        // Region
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
            else { ResetToDefaultRegionHotkey(); }
        }
        else { ResetToDefaultRegionHotkey(); }
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

     // Static log method for use in Config class if needed
     private static void LogToFile(string message) => MainWindow.LogToFile(message);
}

// Helper class for KeyGesture conversion (Simplified)
public static class KeyGestureConverter
{
    public static string KeyGestureToString(Key key, KeyModifiers modifiers)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win"); // Or Meta
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    // Basic string to KeyGesture parsing (Needs improvement for robustness)
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

// Helper for Virtual Key Codes (Simplified)
public static class KeyInterop
{
     // Basic mapping - Needs to be more comprehensive for a real app
     public static uint VirtualKeyFromKey(Key key) => key switch
     {
         Key.A => 0x41, Key.B => 0x42, Key.C => 0x43, Key.D => 0x44, Key.E => 0x45,
         Key.F => 0x46, Key.G => 0x47, Key.H => 0x48, Key.I => 0x49, Key.J => 0x4A,
         Key.K => 0x4B, Key.L => 0x4C, Key.M => 0x4D, Key.N => 0x4E, Key.O => 0x4F,
         Key.P => 0x50, Key.Q => 0x51, Key.R => 0x52, Key.S => 0x53, Key.T => 0x54,
         Key.U => 0x55, Key.V => 0x56, Key.W => 0x57, Key.X => 0x58, Key.Y => 0x59, Key.Z => 0x5A,
         Key.D0 => 0x30, Key.D1 => 0x31, Key.D2 => 0x32, Key.D3 => 0x33, Key.D4 => 0x34,
         Key.D5 => 0x35, Key.D6 => 0x36, Key.D7 => 0x37, Key.D8 => 0x38, Key.D9 => 0x39,
         Key.F1 => 0x70, Key.F2 => 0x71, Key.F3 => 0x72, Key.F4 => 0x73, Key.F5 => 0x74,
         Key.F6 => 0x75, Key.F7 => 0x76, Key.F8 => 0x77, Key.F9 => 0x78, Key.F10 => 0x79,
         Key.F11 => 0x7A, Key.F12 => 0x7B,
         Key.PrintScreen => 0x2C, // VK_SNAPSHOT
         _ => 0 // Default or unknown
     };
}
