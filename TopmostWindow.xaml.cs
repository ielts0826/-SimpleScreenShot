using Avalonia;
using Avalonia.Controls;
using Avalonia.Input; // Add for PointerEventArgs
using Avalonia.Interactivity;
using Avalonia.Media; // Add for Transforms
using Avalonia.Media.Imaging;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap; // Add alias
using Avalonia.Platform.Storage;
using Avalonia.Controls.Platform;
using Avalonia.Markup.Xaml; // Required for AvaloniaXamlLoader
using System;
using System.IO;
using System.Threading.Tasks;

namespace ScreenCaptureTool;

public partial class TopmostWindow : Window
{
    private readonly AvaloniaBitmap? _bitmap; // Make nullable
    private Avalonia.Controls.Image PreviewImage => this.FindControl<Avalonia.Controls.Image>("PreviewImage");
    private Panel ImageContainer => this.FindControl<Panel>("ImageContainer"); // Get reference to the Panel

    // Zoom and Pan state
    private Point _panStartPoint;
    private bool _isPanning = false;
    private double _currentScale = 1.0;
    private const double MinScale = 1.0; // 不允许缩小到小于原图
    private const double MaxScale = 10.0; // 限制最大放大倍数 (可调整)
    private const double ZoomFactor = 1.1;
    private TranslateTransform _translateTransform = new TranslateTransform();
    private ScaleTransform _scaleTransform = new ScaleTransform(1, 1);
    private TransformGroup _transformGroup = new TransformGroup();

    public TopmostWindow()
    {
        AvaloniaXamlLoader.Load(this);
        InitializeTransforms();
    }

    public TopmostWindow(AvaloniaBitmap bitmap) : this()
    {
        _bitmap = bitmap;
        if (PreviewImage != null) // Check if control is found
        {
            PreviewImage.Source = _bitmap;
        }
        // Adjust window size based on image initially? Maybe later.
    }

    private void InitializeTransforms()
    {
        _transformGroup.Children.Add(_scaleTransform);
        _transformGroup.Children.Add(_translateTransform);
        if (PreviewImage != null)
        {
            PreviewImage.RenderTransform = _transformGroup;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmap == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var storageProvider = topLevel.StorageProvider;
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存截图",
            SuggestedFileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg" } }
            },
            DefaultExtension = "png"
        });

        if (file != null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                var extension = Path.GetExtension(file.Name).ToLowerInvariant();

                if (extension == ".png")
                {
                    _bitmap.Save(stream); // Default save is PNG
                }
                else if (extension == ".jpg" || extension == ".jpeg")
                {
                    // Avalonia doesn't have a built-in JPEG encoder in the core library.
                    // Saving as PNG regardless of extension choice for simplicity here.
                    // For JPEG, a third-party library like SkiaSharp.Extended would be needed.
                    _bitmap.Save(stream);
                    Console.WriteLine("Warning: JPEG saving not fully implemented, saved as PNG.");
                }
                else
                {
                     _bitmap.Save(stream); // Default to PNG if extension is unknown
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存截图时出错: {ex.Message}");
                // Optionally show an error message to the user
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // --- Zoom Logic ---
    private void ImageContainer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (PreviewImage == null || ImageContainer == null) return;

        var point = e.GetPosition(ImageContainer); // Zoom towards cursor position relative to the container
        var delta = e.Delta.Y; // Vertical scroll for zoom

        double oldScale = _currentScale;
        if (delta > 0) // Zoom In
        {
            _currentScale *= ZoomFactor;
        }
        else if (delta < 0) // Zoom Out
        {
            _currentScale /= ZoomFactor;
        }

        _currentScale = Math.Clamp(_currentScale, MinScale, MaxScale);

        if (Math.Abs(oldScale - _currentScale) < 1e-5) // Avoid tiny changes
            return;

        // Calculate the offset needed to keep the point under the cursor fixed
        double dx = (point.X - _translateTransform.X) * (_currentScale / oldScale - 1);
        double dy = (point.Y - _translateTransform.Y) * (_currentScale / oldScale - 1);

        // Apply scale
        _scaleTransform.ScaleX = _currentScale;
        _scaleTransform.ScaleY = _currentScale;

        // Apply translation adjustment for zoom-to-cursor
        _translateTransform.X -= dx;
        _translateTransform.Y -= dy;


        ClampTranslation(); // Ensure panning stays within bounds after zoom
        e.Handled = true;
    }

    // --- Pan Logic ---
    private void ImageContainer_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (PreviewImage == null || ImageContainer == null) return;

        // Check if left or right button is pressed (configurable) - Using Left for Pan
        if (e.GetCurrentPoint(ImageContainer).Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(ImageContainer);
            ImageContainer.Cursor = new Cursor(StandardCursorType.Hand); // Change cursor
            e.Pointer.Capture(ImageContainer); // Capture pointer for smooth dragging
            e.Handled = true;
        }
    }

    private void ImageContainer_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isPanning && PreviewImage != null && ImageContainer != null)
        {
            var currentPoint = e.GetPosition(ImageContainer);
            var delta = currentPoint - _panStartPoint;

            // Update translation based on delta
            _translateTransform.X += delta.X;
            _translateTransform.Y += delta.Y;

            // Update start point for next move delta calculation
            _panStartPoint = currentPoint; // Update start point for continuous panning delta

            ClampTranslation(); // Clamp after applying delta
            e.Handled = true;
        }
    }

    private void ImageContainer_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ImageContainer.Cursor = Cursor.Default; // Restore cursor
            e.Pointer.Capture(null); // Release pointer capture
            e.Handled = true;
        }
    }

    // --- Clamping Logic ---
    private void ClampTranslation()
    {
        if (PreviewImage == null || ImageContainer == null || _bitmap == null) return;

        // Use the actual bitmap size for calculations, not PreviewImage.Bounds which might not be updated yet
        var imageSize = new Size(_bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
        var containerBounds = ImageContainer.Bounds;

        // Calculate scaled dimensions
        var scaledWidth = imageSize.Width * _currentScale;
        var scaledHeight = imageSize.Height * _currentScale;

        // Calculate maximum allowed translation offsets
        double minTranslateX, maxTranslateX, minTranslateY, maxTranslateY;

        // If image is wider than container
        if (scaledWidth > containerBounds.Width)
        {
            // The origin is 0.5, 0.5. Translation moves the center.
            // Max distance the center can move left/right from the container center.
            maxTranslateX = (scaledWidth - containerBounds.Width) / 2;
            minTranslateX = -maxTranslateX;
        }
        else // Image is narrower than or equal to container
        {
            // Center the image horizontally
             minTranslateX = 0;
             maxTranslateX = 0;
        }

        // If image is taller than container
        if (scaledHeight > containerBounds.Height)
        {
            maxTranslateY = (scaledHeight - containerBounds.Height) / 2;
            minTranslateY = -maxTranslateY;
        }
        else // Image is shorter than or equal to container
        {
            // Center the image vertically
             minTranslateY = 0;
             maxTranslateY = 0;
        }

        // Apply immediate centering if image is smaller than container
        if (scaledWidth <= containerBounds.Width)
        {
             _translateTransform.X = 0; // Center X
        }
         if (scaledHeight <= containerBounds.Height)
        {
             _translateTransform.Y = 0; // Center Y
        }

        // Clamp the current translation within calculated bounds
        _translateTransform.X = Math.Clamp(_translateTransform.X, minTranslateX, maxTranslateX);
        _translateTransform.Y = Math.Clamp(_translateTransform.Y, minTranslateY, maxTranslateY);

        // Console.WriteLine($"Scale: {_currentScale}, TransX: {_translateTransform.X} (Min: {minTranslateX}, Max: {maxTranslateX}), TransY: {_translateTransform.Y} (Min: {minTranslateY}, Max: {maxTranslateY})");
    }
} // <-- Added missing closing brace for the class
