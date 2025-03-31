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
using epicro_wpf.Helpers;


namespace epicro_wpf.Helpers
{
    // WinRT 인터롭 인터페이스 정의
    [ComImport]
    [Guid("79C3F95B-31F7-4ec2-A464-632EF5D30760")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphicsCaptureItemInterop
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
        static readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        private GraphicsCaptureItem? _captureItem;
        private IDirect3DDevice? _d3dDevice;
        private Direct3D11CaptureFramePool? _framePool;
        private GraphicsCaptureSession? _session;
        private SizeInt32 _captureSize;
        private Action<SoftwareBitmap>? _onFrameCaptured;
        [DllImport("d3d11.dll")]
        private static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int DriverType,
            IntPtr Software,
            uint Flags,
            IntPtr pFeatureLevels,
            uint FeatureLevels,
            uint SDKVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext
);

        [DllImport("windows.graphics.capture.interop.dll")]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice
        );

        /// <summary>
        /// D3D11 Device를 생성하고 WinRT IDirect3DDevice로 변환
        /// </summary>
        /// 

        [DllImport("windows.graphics.directx.direct3d11.dll", ExactSpelling = true)]
        private static extern IDirect3DDevice CreateDirect3DDevice(IntPtr dxgiDevice);

        public static IDirect3DDevice CreateD3DDevice()
        {
            // 1. SharpDX로 D3D11 Device 생성
            var dxgiFactory = new SharpDX.DXGI.Factory1();
            var adapter = dxgiFactory.GetAdapter1(0);
            var d3dDevice = new SharpDX.Direct3D11.Device(adapter);

            // 2. DXGI Device로 변환
            var dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device>();

            // 3. WinRT IDirect3DDevice로 변환
            IntPtr graphicsDevicePtr;
            int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out graphicsDevicePtr);
            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);

            var d3dInteropDevice = Marshal.GetObjectForIUnknown(graphicsDevicePtr) as IDirect3DDevice;
            Marshal.Release(graphicsDevicePtr);

            return d3dInteropDevice ?? throw new InvalidOperationException("D3D Device 생성 실패");
        }

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
            // WinRT 디바이스로 변환
            _d3dDevice = CreateD3DDevice();
            _captureSize = _captureItem.Size;
        }



        public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
        {
            var interop = WinRTHelper.GetInterop();
            var iid = typeof(GraphicsCaptureItem).GUID;
            interop.CreateForWindow(hwnd, ref iid, out object itemObj);

            try
            {
                IntPtr itemPtr = Marshal.GetIUnknownForObject(itemObj);
                var item = Marshal.GetObjectForIUnknown(itemPtr) as GraphicsCaptureItem;
                return item;
            }
            finally
            {
                if (itemObj != null)
                    Marshal.Release(Marshal.GetIUnknownForObject(itemObj));
            }
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

        public async Task<SoftwareBitmap> CaptureToBitmapAsync()
        {
            var tcs = new TaskCompletionSource<SoftwareBitmap>();

            var framePool = Direct3D11CaptureFramePool.Create(
                _d3dDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                _captureSize);
            var session = framePool.CreateCaptureSession(_captureItem);

            TypedEventHandler<Direct3D11CaptureFramePool, object> handler = null;
            handler = async (s, e) =>
            {
                using (var frame = s.TryGetNextFrame())
                {
                    var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                    tcs.TrySetResult(bitmap);
                }

                // 반드시 FrameArrived 핸들러에서 바로 해제하지 말 것 (AccessViolation 원인)
                // 해제 요청만 예약
                framePool.FrameArrived -= handler;
            };

            framePool.FrameArrived += handler;
            session.StartCapture();

            var result = await tcs.Task;

            // 안전하게 캡처 완료 후 Dispose
            session.Dispose();
            framePool.Dispose();

            return result;
        }


        public static async Task SaveSoftwareBitmapToFileAsync(SoftwareBitmap bitmap, string filePath)
        {
            using (var stream = File.OpenWrite(filePath))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
            _framePool?.Dispose();
        }
    }

}
