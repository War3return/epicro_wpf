using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Diagnostics;
using epicro_wpf.Models;
using epicro_wpf.views;
using System.Windows;
// Microsoft 배포 헬퍼 네임스페이스 (실제 네임스페이스로 수정 필요)
using epicro_wpf.Helpers;
using System.Windows.Interop;
using Windows.Graphics.Capture;
using WinRT.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace epicro_wpf.viewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _targetWindowDisplay = "대상 없음";
        public string TargetWindowDisplay
        {
            get => _targetWindowDisplay;
            set
            {
                if (_targetWindowDisplay != value)
                {
                    _targetWindowDisplay = value;
                    OnPropertyChanged();
                }
            }
        }

        private GraphicsCaptureItem _selectedCaptureItem;
        public GraphicsCaptureItem SelectedCaptureItem
        {
            get => _selectedCaptureItem;
            set
            {
                if (_selectedCaptureItem != value)
                {
                    _selectedCaptureItem = value;
                    TargetWindowDisplay = value?.DisplayName ?? "대상 없음";
                    OnPropertyChanged();
                }
            }
        }

        public ICommand OpenTargetSelectCommand { get; } = null!;
        public ICommand OpenROISelectCommand { get; } = null!;
        public MainViewModel()
        {
            OpenTargetSelectCommand = new RelayCommand(async () => await SelectTargetWindow());
            OpenROICommand = new RelayCommand(OpenROIWindow);
        }

        private async Task SelectTargetWindow()
        {
            try
            {
                var picker = new GraphicsCapturePicker();
                var hwnd = new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
                InitializeWithWindow.Initialize(picker, hwnd);

                var item = await picker.PickSingleItemAsync();
                if (item != null)
                {
                    SelectedCaptureItem = item;
                }
            }
            catch (Exception ex)
            {
                // 로그 출력용 (원하면 txtLog에 연결 가능)
                System.Diagnostics.Debug.WriteLine($"[에러] {ex.Message}");
            }
        }

        public ICommand OpenROICommand { get; }

        private async void OpenROIWindow()
        {
            if (SelectedCaptureItem == null)
                return;

            var helper = new CaptureHelper(SelectedCaptureItem);
            var bitmap = await helper.CaptureToBitmapAsync();
            var filePath = Path.Combine(Environment.CurrentDirectory, "capture.png");

            await CaptureHelper.SaveSoftwareBitmapToFileAsync(bitmap, filePath);

            // 2. ROI Window Open
            var roiWindow = new ROIWindow(filePath);
            var result = roiWindow.ShowDialog();
            if (result == true && roiWindow.SelectedROI.HasValue)
            {
                var roi = roiWindow.SelectedROI.Value;
                System.Diagnostics.Debug.WriteLine($"ROI: {roi.X}, {roi.Y}, {roi.Width}, {roi.Height}");
                // 여기에 저장 처리 추가
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
