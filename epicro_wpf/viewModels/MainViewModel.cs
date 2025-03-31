using System.ComponentModel;
using System.Windows.Input;
using System.Diagnostics;
using epicro_wpf.Models;
using epicro_wpf.views;
using System.Windows;
// Microsoft 배포 헬퍼 네임스페이스 (실제 네임스페이스로 수정 필요)
using epicro_wpf.Helpers;

namespace epicro_wpf.viewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private WindowInfo _targetWindow;
        public WindowInfo TargetWindow
        {
            get => _targetWindow;
            set
            {
                _targetWindow = value;
                OnPropertyChanged(nameof(TargetWindowDisplay));
            }
        }

        public ICommand OpenTargetSelectCommand => new RelayCommand(OpenTargetSelect);

        private void OpenTargetSelect()
        {
            var vm = new TargetWindowViewModel();
            var win = new TargetWindowSelectWindow
            {
                DataContext = vm
            };

            bool? result = win.ShowDialog();

            if (result == true && vm.SelectedWindow != null)
            {
                TargetWindow = vm.SelectedWindow;
                Debug.WriteLine($"선택된 창: {TargetWindow.Title}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string TargetWindowDisplay =>
            TargetWindow != null ? $"{TargetWindow.Title} ({TargetWindow.ProcessId})" : "선택된 프로세스 없음";

        public ICommand CaptureCommand => new RelayCommand(CaptureTargetWindow);

        private async void CaptureTargetWindow()
        {
            if (TargetWindow == null)
            {
                MessageBox.Show("먼저 인식대상을 설정해주세요.");
                return;
            }

            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "capture.png");
                // Microsoft 배포 라이브러리에서 제공하는 캡처 헬퍼 호출
                // 예시로 CaptureHelper 클래스의 Capture 메서드를 사용합니다.
                await WgcCaptureUtil.CaptureWindowToPngAsync(TargetWindow.Handle, path);
                MessageBox.Show($"캡처 완료!\n{path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"캡처 실패: {ex.Message}");
            }
        }

    }
}
