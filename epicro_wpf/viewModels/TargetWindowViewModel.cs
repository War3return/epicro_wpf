using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using epicro_wpf.Models;
using epicro_wpf.Helpers;

namespace epicro_wpf.viewModels
{
    public class TargetWindowViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<WindowInfo> WindowList { get; } = new();

        private WindowInfo _selectedWindow;
        public WindowInfo SelectedWindow
        {
            get => _selectedWindow;
            set
            {
                _selectedWindow = value;
                OnPropertyChanged(nameof(SelectedWindow));
            }
        }

        public ICommand ApplyCommand { get; }
        public ICommand CloseCommand { get; }

        public TargetWindowViewModel()
        {
            ApplyCommand = new RelayCommand<Window>(Apply);
            CloseCommand = new RelayCommand<Window>(Close);
            LoadWindows();
        }

        private void Apply(Window window)
        {
            if (SelectedWindow != null)
            {
                window.DialogResult = true;
                window.Close();
            }
        }

        private void Close(Window window)
        {
            window.Close();
        }

        private void LoadWindows()
        {
            WindowList.Clear();
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    var length = GetWindowTextLength(hWnd);
                    if (length == 0) return true;
                    var builder = new System.Text.StringBuilder(length + 1);
                    GetWindowText(hWnd, builder, builder.Capacity);
                    string title = builder.ToString();
                    if (title.ToLower().Contains("warcraft")) // 🔍 warcraft 포함 여부 체크
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);
                        WindowList.Add(new WindowInfo 
                        { 
                            Handle = hWnd, 
                            Title = title, 
                            ProcessId = (int)pid 
                        });
                    }
                }
                return true;
            }, IntPtr.Zero);
        }

        // Win32 API 선언
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

}
