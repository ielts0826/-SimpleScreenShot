using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using System;

namespace ScreenCaptureTool
{
    public partial class RegionSelectionWindow : Window
    {
        private Point _startPoint;
        private bool _isSelecting = false;
        private Rectangle _selectionRectangle;
        private Canvas _selectionCanvas;
        private Image _backgroundImage;

        // Property to hold the selected region result
        public Rect? SelectedRegion { get; private set; } = null;

        // Constructor accepting the background bitmap
        public RegionSelectionWindow(Bitmap background)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            _backgroundImage = this.FindControl<Image>("BackgroundImage")!;
            _selectionCanvas = this.FindControl<Canvas>("SelectionCanvas")!;

            _backgroundImage.Source = background; // Set the background

            _selectionRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent, // Make the rectangle itself transparent
                IsVisible = false
            };
            _selectionCanvas.Children.Add(_selectionRectangle);

            // Attach event handlers to the canvas
            _selectionCanvas.PointerPressed += Canvas_PointerPressed;
            _selectionCanvas.PointerMoved += Canvas_PointerMoved;
            _selectionCanvas.PointerReleased += Canvas_PointerReleased;
            this.KeyDown += Window_KeyDown; // Handle Esc key

            MainWindow.LogToFile("RegionSelectionWindow: Initialized and background set.");
        }

        // Parameterless constructor for XAML designer
        public RegionSelectionWindow()
        {
             InitializeComponent();
             // Designer specific setup if needed
             _backgroundImage = this.FindControl<Image>("BackgroundImage")!;
             _selectionCanvas = this.FindControl<Canvas>("SelectionCanvas")!;
             _selectionRectangle = new Rectangle { Stroke = Brushes.Red, StrokeThickness = 2, Fill = Brushes.Transparent, IsVisible = true, Width=100, Height=100 };
             _selectionCanvas.Children.Add(_selectionRectangle);
             Canvas.SetLeft(_selectionRectangle, 50);
             Canvas.SetTop(_selectionRectangle, 50);
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(_selectionCanvas);
            _selectionRectangle.Width = 0;
            _selectionRectangle.Height = 0;
            Canvas.SetLeft(_selectionRectangle, _startPoint.X);
            Canvas.SetTop(_selectionRectangle, _startPoint.Y);
            _selectionRectangle.IsVisible = true;
            e.Pointer.Capture(_selectionCanvas); // Capture pointer on the canvas
            MainWindow.LogToFile($"RegionSelectionWindow: PointerPressed at {_startPoint}");
            e.Handled = true;
        }

        private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isSelecting && e.GetCurrentPoint(_selectionCanvas).Properties.IsLeftButtonPressed)
            {
                Point currentPoint = e.GetPosition(_selectionCanvas);
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(_startPoint.X - currentPoint.X);
                var height = Math.Abs(_startPoint.Y - currentPoint.Y);

                Canvas.SetLeft(_selectionRectangle, x);
                Canvas.SetTop(_selectionRectangle, y);
                _selectionRectangle.Width = width;
                _selectionRectangle.Height = height;
                // LogToFile($"RegionSelectionWindow: PointerMoved, Rect: {x},{y} {width}x{height}"); // Can be very verbose
                e.Handled = true;
            }
        }

        private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                Point endPoint = e.GetPosition(_selectionCanvas);
                e.Pointer.Capture(null); // Release pointer capture
                MainWindow.LogToFile($"RegionSelectionWindow: PointerReleased at {endPoint}");

                // Calculate the selected region
                var x = Math.Min(_startPoint.X, endPoint.X);
                var y = Math.Min(_startPoint.Y, endPoint.Y);
                var width = Math.Abs(_startPoint.X - endPoint.X);
                var height = Math.Abs(_startPoint.Y - endPoint.Y);

                if (width > 0 && height > 0)
                {
                    SelectedRegion = new Rect(x, y, width, height);
                    MainWindow.LogToFile($"RegionSelectionWindow: SelectedRegion calculated: {SelectedRegion}");
                }
                else
                {
                     SelectedRegion = null; // No valid region selected
                     MainWindow.LogToFile("RegionSelectionWindow: Invalid region selected (width or height is 0).");
                }

                this.Close(); // Close the window after selection
                e.Handled = true;
            }
        }

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                MainWindow.LogToFile("RegionSelectionWindow: Escape key pressed, cancelling selection.");
                SelectedRegion = null; // Ensure region is null on cancel
                _isSelecting = false; // Ensure state is reset
                this.Close(); // Close the window
                e.Handled = true;
            }
        }
    }
}
