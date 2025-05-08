# 简易截图与录制工具 (Simple Screen Capture & GIF Record Tool)

[![CSharp](https://img.shields.io/badge/C%23-11-blueviolet)](https://docs.microsoft.com/en-us/dotnet/csharp/) [![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/) [![Avalonia UI](https://img.shields.io/badge/Avalonia%20UI-11.x-orange)](https://avaloniaui.net/) [![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE) <!-- 您需要添加一个 LICENSE 文件 -->

一款基于 C#、.NET 8 和 Avalonia UI 构建的现代化、轻量级 Windows 截图与GIF录制工具，采用 NativeAOT 技术发布，旨在提供流畅的用户体验。

![主界面截图](https://i.imgur.com/Wj18JGu.png) <!-- 提示：请替换为您的实际主界面截图链接 -->

## ✨ 功能特性

- **全屏截图**: 一键捕捉整个屏幕。
- **区域截图**: 自由选择屏幕上的任意矩形区域进行捕捉。
- **区域GIF录制**: 
    - 自由选择屏幕上的任意矩形区域进行GIF录制。
    - 开始录制前有3秒倒计时，方便准备。
    - 录制帧率默认为10 FPS (后续可考虑自定义)。
- **截图后预览**:
  - 截图完成后自动弹出置顶预览窗口。
  - 支持鼠标滚轮**缩放**和鼠标左键**平移**预览图像。
  - 在预览窗口上**右键单击**即可快速关闭。
  - **上传到 Imgur**: 在预览窗口，可一键将截图匿名上传到 Imgur 并自动复制链接到剪贴板 (需在设置中配置Imgur Client ID)。
- **自定义快捷键**:
  - 可分别为“全屏截图”、“区域截图”和“GIF录制”设置独立的**全局快捷键**。
  - 在主界面点击“设置”按钮，然后点击各功能旁的快捷键设置按钮即可录制新快捷键（需包含 Ctrl/Shift/Alt）。
  - 快捷键全局生效，即使窗口最小化也能触发相应操作。
- **后台运行**: 操作后主窗口保持在后台或任务栏，不打断您的工作流程。
- **默认保存路径** (截图功能):
  - 可选择启用默认保存路径功能。
  - 启用后，截图将自动保存到指定文件夹，文件名从 `1.png` 开始递增。
  - 跳过"另存为"对话框，提高效率。
- **Imgur Client ID 配置**: 
  - 在独立的"设置中心"窗口中，用户可以配置自己的 Imgur Client ID，以启用匿名上传功能。
- **现代化 UI**: 使用 Avalonia UI 构建，界面简洁美观。
- **NativeAOT 发布**: 编译为本地代码，无需外部 .NET 运行时依赖（注意：由于包含 UI 框架，最终体积可能仍较大）。

## 🚀 安装

1.  前往本项目的 [GitHub Releases](https://github.com/ielts0826/-SimpleScreenShot/releases) 页面下载最新的 `简易截图工具Setup.exe` 安装包。 <!-- 提示：请替换为您的实际 GitHub 仓库链接 -->
2.  运行安装包，按照提示完成安装。
3.  由于使用了 NativeAOT 技术，您**无需**在电脑上预先安装 .NET 运行时。

## 💡 使用方法

1.  **启动**: 运行安装后的"简易截图工具"。
2.  **截图**:
    - 点击主界面的"全屏截图"或"区域截图"按钮。
    - 或使用您为截图功能设置的全局快捷键。
3.  **GIF录制**:
    - 点击主界面的"录制GIF"按钮，或使用您为GIF录制功能设置的全局快捷键。
    - 屏幕会变暗，鼠标指针变为十字形。按住鼠标左键拖动选择录制区域，松开即可选定。
    - 选定区域后，主界面状态栏会显示3秒倒计时。
    - 倒计时结束后开始录制。再次点击"录制GIF"按钮（此时会显示为"停止录制"）或再次按下GIF录制快捷键可停止录制。
    - 停止后会弹出对话框让您选择GIF文件的保存位置。
4.  **区域选择 (截图/GIF录制)**: 如果选择区域截图或GIF录制，屏幕会变暗，鼠标指针变为十字形。按住鼠标左键拖动选择区域，松开即可完成选择。按 `Esc` 键可中途取消选择。
5.  **预览 (截图后)**: 
    - 滚动鼠标滚轮缩放。
    - 按住鼠标左键拖动（需先放大）。
    - 右键单击预览窗口关闭。
    - 点击预览窗口的"保存"按钮可手动选择保存位置。
    - 点击预览窗口的"上传到 Imgur"按钮，可将当前截图上传 (需预先在设置中配置 Client ID)。成功后链接会自动复制到剪贴板。
    - 点击"关闭"按钮关闭预览。
6.  **设置中心**: 
    - 点击主界面上方的"设置中心"按钮，打开设置窗口。
    - **快捷键设置**: 在主界面原位置（全屏、区域、GIF录制功能按钮的下方区域）点击各功能旁的"设置"按钮，然后按下新的组合键来更改快捷键。
    - **默认保存路径**: (同原有逻辑) 在主界面勾选"启用默认保存路径"，点击"浏览..."选择文件夹，即可开启截图自动保存功能。
    - **Imgur Client ID**: 在"设置中心"窗口中，输入您从 Imgur 获取的 Client ID。此ID用于启用截图预览窗口中的"上传到 Imgur"功能。如果为空或无效，上传功能将不可用。

<!-- 提示：建议添加GIF演示录制功能和Imgur上传流程 -->
![区域选择演示](images/region_select.gif) <!-- 提示：请替换为您的实际 GIF 链接 -->
![预览窗口演示](images/preview_window.gif) <!-- 提示：请替换为您的实际 GIF 链接 -->


## 🛠️ 技术栈

- **语言**: C# 11
- **框架**: .NET 8
- **UI**: Avalonia UI 11.x (Fluent Theme)
- **编译**: NativeAOT (用于 Release 发布)
- **核心依赖**:
  - `System.Drawing.Common` (用于 Bitmap 处理和屏幕捕捉)
  - `AnimatedGif` (用于 GIF 动画编码)
  - P/Invoke (调用 Windows API 实现热键注册和屏幕捕捉)
  - `System.Text.Json` (用于配置文件的加载和保存)

## 🔧 从源码编译运行

**环境要求**:

- .NET 8 SDK
- Visual Studio 2022 (或 Visual Studio Build Tools) 并安装 **"使用 C++ 的桌面开发"** 工作负载 (NativeAOT 发布必需)

**步骤**:

1.  克隆仓库: `git clone https://github.com/YOUR_USERNAME/YOUR_REPO.git` <!-- 提示：请替换为您的实际 GitHub 仓库链接 -->
2.  进入项目目录: `cd YOUR_REPO`
3.  构建项目: `dotnet build` (会自动还原 NuGet 包, 包括 `AnimatedGif`)
4.  运行 (Debug): `dotnet run --project ScreenCaptureTool.csproj`
5.  发布 (Release with NativeAOT): `dotnet publish ScreenCaptureTool.csproj -c Release -r win-x64 --self-contained true`
    - 发布的文件位于 `bin\Release\net8.0\win-x64\publish\`

## 🤝 贡献

欢迎提交 Pull Requests 或 Issues！

## 📧 联系方式

如有问题或建议，请联系：lishengwei0826@gmail.com

## 📄 许可

本项目采用 [MIT](LICENSE) 许可。 <!-- 提示：请在项目中添加一个名为 LICENSE 的文件，并写入 MIT 许可文本 -->
