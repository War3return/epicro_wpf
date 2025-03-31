using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using System;
using System.Windows.Controls;

namespace epicro_wpf.views
{
    public partial class ROIWindow : Window
    {
        private Point _startPoint;
        public Rect? SelectedROI { get; private set; }

        public ROIWindow(string imagePath)
        {
            InitializeComponent();
            TargetImage.Source = new BitmapImage(new Uri(imagePath));
            TargetImage.MouseLeftButtonDown += Image_MouseLeftButtonDown;
            TargetImage.MouseMove += Image_MouseMove;
            TargetImage.MouseLeftButtonUp += Image_MouseLeftButtonUp;
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(TargetImage);
            SelectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(TargetImage);
                var x = Math.Min(pos.X, _startPoint.X);
                var y = Math.Min(pos.Y, _startPoint.Y);
                var w = Math.Abs(pos.X - _startPoint.X);
                var h = Math.Abs(pos.Y - _startPoint.Y);
                Canvas.SetLeft(SelectionRect, x);
                Canvas.SetTop(SelectionRect, y);
                SelectionRect.Width = w;
                SelectionRect.Height = h;
            }
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var x = Canvas.GetLeft(SelectionRect);
            var y = Canvas.GetTop(SelectionRect);
            var w = SelectionRect.Width;
            var h = SelectionRect.Height;
            SelectedROI = new Rect(x, y, w, h);
            DialogResult = true;
            Close();
        }
    }
}
