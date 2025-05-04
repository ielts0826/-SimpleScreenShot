using Avalonia;
using Avalonia.Win32; // 添加Win32平台支持
// 移除不存在的命名空间引用
using Avalonia.Controls.ApplicationLifetimes;
using System; // Keep System for Console
using System.IO;
using System.Text;
using System.Reflection;

namespace ScreenCaptureTool;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 添加文件日志
        LogToFile("Program: Main method started.");
        Console.WriteLine("Program: Main method started.");
        
        try
        {
             LogToFile("Program: 准备启动Avalonia应用...");
             BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
             LogToFile("Program: StartWithClassicDesktopLifetime finished.");
             Console.WriteLine("Program: StartWithClassicDesktopLifetime finished.");
        }
        catch (Exception ex)
        {
             LogToFile($"Program: Unhandled exception in Main: {ex}");
             Console.WriteLine($"Program: Unhandled exception in Main: {ex}");
        }
        
        LogToFile("Program: Main method finished.");
        Console.WriteLine("Program: Main method finished.");
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


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        LogToFile("BuildAvaloniaApp: 配置Avalonia应用...");
        try
        {
            // 显式添加所有可能的渲染后端
            var builder = AppBuilder.Configure<App>();
            
            // 记录可用的渲染后端
            LogToFile("BuildAvaloniaApp: 添加平台检测");
            builder = builder.UsePlatformDetect();
            
            // 不尝试显式配置Skia，依赖UsePlatformDetect自动选择合适的渲染器
            LogToFile("BuildAvaloniaApp: 使用默认渲染器配置");
            
            // 添加其他配置
            LogToFile("BuildAvaloniaApp: 添加字体和日志配置");
            builder = builder.WithInterFont().LogToTrace();
            
            LogToFile("BuildAvaloniaApp: 应用配置完成");
            return builder;
        }
        catch (Exception ex)
        {
            LogToFile($"BuildAvaloniaApp: 配置过程中出错: {ex}");
            throw;
        }
    }
}
