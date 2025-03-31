using System;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Foundation;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using WinRT;
using Windows.Storage.Streams;
using System.IO;
using Windows.Graphics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

namespace epicro_wpf.Helpers
{
    // WinRT 인터롭 인터페이스 정의
    [ComImport]
    [Guid("79C3F95B-31F7-4ec2-A464-632EF5D30760")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IGraphicsCaptureItemInterop
    {
        /// <summary>
        /// 주어진 window 핸들을 사용하여 GraphicsCaptureItem을 생성합니다.
        /// </summary>
        /// <param name="window">캡처할 창의 핸들</param>
        /// <param name="iid">반환될 인터페이스의 GUID (여기서는 GraphicsCaptureItem의 GUID)</param>
        /// <param name="result">생성된 GraphicsCaptureItem</param>
        /// <returns>HRESULT 코드</returns>
        [PreserveSig]
        int CreateForWindow(IntPtr window, ref Guid iid, out object result);
    }

    public class CaptureHelper : IDisposable
    {
        private GraphicsCaptureItem? _captureItem;
        private IDirect3DDevice? _d3dDevice;
        private Direct3D11CaptureFramePool? _framePool;
        private GraphicsCaptureSession? _session;
        private SizeInt32 _captureSize;
        private Action<SoftwareBitmap>? _onFrameCaptured;


        public CaptureHelper(GraphicsCaptureItem item)
        {
            _captureItem = item ?? throw new ArgumentNullException(nameof(item));
            Initialize();
        }

        private void Initialize()
        {
            // SharpDX를 이용하여 DXGI Factory, Adapter, Device 생성
            var dxgiFactory = new SharpDX.DXGI.Factory1();
            var adapter = dxgiFactory.GetAdapter1(0);
            var device = new SharpDX.Direct3D11.Device(adapter);
            // WinRT용 Direct3D 디바이스 생성
            _d3dDevice = Direct3D11Helper.CreateDirect3DDevice(device.NativePointer);
            _captureSize = _captureItem?.Size ?? throw new InvalidOperationException("Capture item is null.");
        }

        public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
        {
            // GraphicsCaptureItem의 활성화 팩토리를 가져옵니다.
       object factory = WinRTHelper.GetActivationFactory(typeof(Windows.Graphics.Capture.GraphicsCaptureItem));
            IGraphicsCaptureItemInterop interop = (IGraphicsCaptureItemInterop)factory;

            // GraphicsCaptureItem의 인터페이스 GUID
            Guid iid = typeof(GraphicsCaptureItem).GUID;

            // 캡처 아이템 생성
            int hr = interop.CreateForWindow(hwnd, ref iid, out object result);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return (GraphicsCaptureItem)result;
        }

        /// <summary>
        /// 캡처를 시작하고, 프레임이 도착할 때마다 콜백을 호출합니다.
        /// </summary>
        public void StartCapture(Action<SoftwareBitmap> onFrameCaptured)
        {
            _onFrameCaptured = onFrameCaptured;
            _framePool = Direct3D11CaptureFramePool.Create(
                _d3dDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                _captureSize);
            _session = _framePool.CreateCaptureSession(_captureItem);
            _framePool.FrameArrived += FramePool_FrameArrived;
            _session.StartCapture();
        }

        private async void FramePool_FrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using (var frame = sender.TryGetNextFrame())
            {
                // 비동기적으로 SoftwareBitmap 생성 (캡처된 전체 이미지)
                SoftwareBitmap bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                _onFrameCaptured?.Invoke(bitmap);
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
            _framePool?.Dispose();
        }
    }

}
