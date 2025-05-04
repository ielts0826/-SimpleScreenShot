using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System; // Add System namespace for Console and Exception
using System.IO;
using System.Text;

namespace ScreenCaptureTool;

public partial class App : Application
{
    public override void Initialize()
    {
        try
        {
            LogToFile("App: Initialize 开始");
            AvaloniaXamlLoader.Load(this);
            LogToFile("App: Initialize 完成");
        }
        catch (Exception ex)
        {
            LogToFile($"App: Initialize 出错: {ex}");
            throw;
        }
    }
    
    // 添加文件日志方法
    private static void LogToFile(string message)
    {
        try
        {
            string logPath = "avalonia_app_log.txt";
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, logMessage, Encoding.UTF8);
        }
        catch
        {
            // 如果日志记录失败，忽略异常
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LogToFile("App: OnFrameworkInitializationCompleted 开始");
        Console.WriteLine("App: OnFrameworkInitializationCompleted started.");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            LogToFile("App: 创建 MainWindow...");
            Console.WriteLine("App: Creating MainWindow...");
            
            try
            {
                 desktop.MainWindow = new MainWindow();
                 LogToFile("App: MainWindow 创建成功");
                 Console.WriteLine("App: MainWindow created successfully.");
            }
            catch (Exception ex)
            {
                  LogToFile($"App: 创建 MainWindow 出错: {ex}");
                  Console.WriteLine($"App: Error creating MainWindow: {ex}");
             }

             // Subscribe to the ShutdownRequested event to clean up resources
             desktop.ShutdownRequested += OnShutdownRequested;
             LogToFile("App: 已订阅 ShutdownRequested 事件");
         }
         else
        {
             LogToFile("App: ApplicationLifetime 不是 IClassicDesktopStyleApplicationLifetime");
             Console.WriteLine("App: ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime.");
        }

        try
        {
             LogToFile("App: 调用 base.OnFrameworkInitializationCompleted()");
             base.OnFrameworkInitializationCompleted();
             LogToFile("App: base.OnFrameworkInitializationCompleted 完成");
             Console.WriteLine("App: base.OnFrameworkInitializationCompleted completed.");
        }
        catch (Exception ex)
        {
             LogToFile($"App: base.OnFrameworkInitializationCompleted 出错: {ex}");
             Console.WriteLine($"App: Error in base.OnFrameworkInitializationCompleted: {ex}");
        }
        
         LogToFile("App: OnFrameworkInitializationCompleted 结束");
         Console.WriteLine("App: OnFrameworkInitializationCompleted finished.");
     }

    // Event handler for application shutdown
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        LogToFile("App: ShutdownRequested 事件触发，执行清理...");
        Console.WriteLine("App: ShutdownRequested event triggered, performing cleanup...");
        User32Hotkey.Cleanup(); // Call the cleanup method for the hotkey resources
        LogToFile("App: User32Hotkey 清理完成");
        Console.WriteLine("App: User32Hotkey cleanup finished.");
    }
 }
