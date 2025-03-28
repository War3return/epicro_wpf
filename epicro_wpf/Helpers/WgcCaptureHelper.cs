using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace epicro_wpf.Helpers
{
    public static class WgcCaptureHelper
    {
        #region 인터페이스/WinRT Factory 관련

        [ComImport]
        [Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            GraphicsCaptureItem CreateForWindow(IntPtr hwnd, ref Guid iid);
        }

        [DllImport("combase.dll", CharSet = CharSet.Unicode)]
        private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr hstring, ref Guid iid, out IntPtr factory);

        // DXGI 디바이스(IntPtr)를 받아서 IDirect3DDevice(WinRT에서 쓰는 디바이스)로 만들어 주는 함수
        [DllImport("Windows.Graphics.Capture.dll", CharSet = CharSet.Unicode)]
        private static extern IDirect3DDevice CreateDirect3DDeviceFromDXGIDevice(IntPtr dxgiDevice);

        #endregion

        #region Public 메서드

        /// <summary>
        /// hWnd 창을 캡처해 PNG로 저장 (비동기)
        /// </summary>
        public static async Task CaptureToPngAsync(IntPtr hwnd, string fileName)
        {
            // 1) 대상 창(Handle)에 대한 GraphicsCaptureItem 생성
            var item = CreateCaptureItemForWindow(hwnd);
            if (item == null)
            {
                throw new InvalidOperationException("해당 창은 WGC(GraphicsCaptureItem)로 캡처할 수 없습니다.");
            }

            // 2) D3D11 디바이스 생성
            var d3dDevice = CreateD3DDevice();

            // 3) d3dDevice → IDXGIDevice → IDirect3DDevice
            var wgcDevice = CreateWgcDevice(d3dDevice);

            // 4) FramePool / Session 생성
            var framePool = Direct3D11CaptureFramePool.Create(
                wgcDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size);

            var session = framePool.CreateCaptureSession(item);
            var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>();

            framePool.FrameArrived += (s, e) =>
            {
                var frame = s.TryGetNextFrame();
                tcs.TrySetResult(frame);
            };

            session.StartCapture();
            var frameResult = await tcs.Task;

            session.Dispose();
            framePool.Dispose();

            // 5) SoftwareBitmap으로 변환 후 PNG 저장
            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frameResult.Surface);
            await SaveToPngAsync(softwareBitmap, fileName);
        }

        /// <summary>
        /// hWnd 창을 캡처해 GDI+ Bitmap 반환 (동기)
        /// </summary>
        public static Bitmap Capture(IntPtr hWnd)
        {
            var item = CreateCaptureItemForWindow(hWnd);
            if (item == null)
            {
                throw new InvalidOperationException("해당 창은 WGC(GraphicsCaptureItem)로 캡처할 수 없습니다.");
            }

            var task = CaptureToBitmapAsync(item);
            task.Wait();
            return task.Result;
        }

        #endregion

        #region 내부 로직

        /// <summary>
        /// hWnd에 대응하는 GraphicsCaptureItem 생성
        /// </summary>
        private static GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr hWnd)
        {
            var hstring = IntPtr.Zero;
            var iid = typeof(IGraphicsCaptureItemInterop).GUID;
            var factoryGuid = typeof(GraphicsCaptureItem).GUID;

            // 1. HSTRING 생성
            string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
            WindowsCreateString(className, className.Length, out hstring);

            // 2. Activation Factory 가져오기
            int hr = RoGetActivationFactory(hstring, ref iid, out var factoryPtr);
            WindowsDeleteString(hstring);

            if (hr < 0 || factoryPtr == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            // 3. IGraphicsCaptureItemInterop 변환
            var factory = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            Marshal.Release(factoryPtr);

            // 4. 실제 CaptureItem 생성
            var item = factory.CreateForWindow(hWnd, ref factoryGuid);
            return item;
        }

        /// <summary>
        /// ID3D11Device 생성 (Feature Level 지정 + 하드웨어 실패 시 WARP로 폴백)
        /// </summary>
        private static ID3D11Device CreateD3DDevice()
        {
            IntPtr devicePtr = Direct3DDeviceManager.CreateDXGIDevice();
            if (devicePtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("D3D11 디바이스 생성 실패");
            }

            // COM 포인터 → C# ID3D11Device
            var d3dDevice = (ID3D11Device)Marshal.GetObjectForIUnknown(devicePtr);
            if (d3dDevice == null)
            {
                throw new InvalidOperationException("ID3D11Device 변환 실패");
            }

            return d3dDevice;
        }

        /// <summary>
        /// d3dDevice → IDXGIDevice → IDirect3DDevice
        /// </summary>
        private static IDirect3DDevice CreateWgcDevice(ID3D11Device d3dDevice)
        {
            // QueryInterface로 IDXGIDevice 얻기
            var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
            if (dxgiDevice == null)
            {
                throw new InvalidOperationException("IDXGIDevice 인터페이스를 얻을 수 없습니다.");
            }

            // 실제 COM 포인터(IntPtr) 추출
            IntPtr dxgiDevicePtr = Marshal.GetIUnknownForObject(dxgiDevice);
            if (dxgiDevicePtr == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(dxgiDevicePtr), "IDXGIDevice COM 포인터가 null입니다.");
            }

            // IDirect3DDevice 생성
            return CreateDirect3DDeviceFromDXGIDevice(dxgiDevicePtr);
        }

        /// <summary>
        /// CaptureItem → Bitmap (비동기)
        /// </summary>
        private static async Task<Bitmap> CaptureToBitmapAsync(GraphicsCaptureItem item)
        {
            var d3dDevice = CreateD3DDevice();
            var wgcDevice = CreateWgcDevice(d3dDevice);

            var framePool = Direct3D11CaptureFramePool.Create(
                wgcDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size);

            var session = framePool.CreateCaptureSession(item);
            var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>();

            framePool.FrameArrived += (s, e) =>
            {
                var frame = s.TryGetNextFrame();
                tcs.TrySetResult(frame);
            };

            session.StartCapture();
            var frameResult = await tcs.Task;

            session.Dispose();
            framePool.Dispose();

            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frameResult.Surface);
            return ConvertToBitmap(softwareBitmap);
        }

        /// <summary>
        /// SoftwareBitmap → PNG 파일
        /// </summary>
        private static async Task SaveToPngAsync(SoftwareBitmap bitmap, string fileName)
        {
            // 바탕화면 / PicturesLibrary 등 원하는 경로로 조정하세요.
            var picturesFolder = KnownFolders.PicturesLibrary;
            var file = await picturesFolder.CreateFileAsync(
                fileName,
                CreationCollisionOption.ReplaceExisting);

            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            // BGRA8로 변환
            var converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(converted);
            await encoder.FlushAsync();
        }

        /// <summary>
        /// SoftwareBitmap → GDI+ Bitmap
        /// </summary>
        private static Bitmap ConvertToBitmap(SoftwareBitmap softwareBitmap)
        {
            var converted = SoftwareBitmap.Convert(
                softwareBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            var pixels = new byte[width * height * 4];
            converted.CopyToBuffer(pixels.AsBuffer());

            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            var rect = new Rectangle(0, 0, width, height);

            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            bmp.UnlockBits(bmpData);

            return bmp;
        }

        #endregion
    }

    /// <summary>
    /// COM 객체(QueryInterface) 확장 메서드
    /// </summary>
    public static class ComExtensions
    {
        public static T QueryInterface<T>(this object comObject)
        {
            Guid iid = typeof(T).GUID;
            IntPtr pUnk = Marshal.GetIUnknownForObject(comObject);
            IntPtr ppv;
            int hr = Marshal.QueryInterface(pUnk, ref iid, out ppv);
            Marshal.Release(pUnk);

            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);

            T result = (T)Marshal.GetObjectForIUnknown(ppv);
            Marshal.Release(ppv);
            return result;
        }
    }

    /// <summary>
    /// D3D11.dll P/Invoke 및 DXGI 디바이스 생성 로직
    /// Feature Level 지정 & 하드웨어 실패 시 WARP 폴백
    /// </summary>
    public static class Direct3DDeviceManager
    {
        // 필요한 enum/struct들 정의
        private enum D3D_DRIVER_TYPE
        {
            UNKNOWN = 0,
            HARDWARE = 1,
            REFERENCE = 2,
            NULL = 3,
            SOFTWARE = 4,
            WARP = 5
        }

        private enum D3D_FEATURE_LEVEL : uint
        {
            D3D_FEATURE_LEVEL_9_1 = 0x9100,
            D3D_FEATURE_LEVEL_9_2 = 0x9200,
            D3D_FEATURE_LEVEL_9_3 = 0x9300,
            D3D_FEATURE_LEVEL_10_0 = 0xa000,
            D3D_FEATURE_LEVEL_10_1 = 0xa100,
            D3D_FEATURE_LEVEL_11_0 = 0xb000,
            D3D_FEATURE_LEVEL_11_1 = 0xb100
        }

        private static readonly D3D_FEATURE_LEVEL[] s_featureLevels =
        {
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_3,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_2,
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1,
        };

        private const uint D3D11_SDK_VERSION = 7;
        private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;  // BGRA 서피스
        private const uint D3D11_CREATE_DEVICE_DEBUG = 0x2;          // 디버그(선택)

        [DllImport("d3d11.dll", SetLastError = true)]
        private static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            D3D_DRIVER_TYPE DriverType,
            IntPtr Software,
            uint Flags,
            [In] D3D_FEATURE_LEVEL[] pFeatureLevels,
            int FeatureLevels,
            uint SDKVersion,
            out IntPtr ppDevice,
            out D3D_FEATURE_LEVEL pFeatureLevel,
            out IntPtr ppImmediateContext
        );

        /// <summary>
        /// DXGI 디바이스 포인터 생성
        /// </summary>
        public static IntPtr CreateDXGIDevice()
        {
            // BGRA + (디버그 옵션을 쓰고 싶다면 D3D11_CREATE_DEVICE_DEBUG 추가)
            uint creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

            // 1) 하드웨어 가속 시도
            IntPtr devicePtr;
            var hr = D3D11CreateDevice(
                IntPtr.Zero,
                D3D_DRIVER_TYPE.HARDWARE,
                IntPtr.Zero,
                creationFlags,
                s_featureLevels,
                s_featureLevels.Length,
                D3D11_SDK_VERSION,
                out devicePtr,
                out var chosenFeatureLevel,
                out var immediateContextPtr
            );

            // 2) 하드웨어 실패 시 WARP 폴백
            if (hr < 0)
            {
                hr = D3D11CreateDevice(
                    IntPtr.Zero,
                    D3D_DRIVER_TYPE.WARP,
                    IntPtr.Zero,
                    creationFlags,
                    s_featureLevels,
                    s_featureLevels.Length,
                    D3D11_SDK_VERSION,
                    out devicePtr,
                    out chosenFeatureLevel,
                    out immediateContextPtr
                );

                if (hr < 0)
                {
                    // 하드웨어, WARP 모두 실패
                    Marshal.ThrowExceptionForHR(hr);
                }
            }

            return devicePtr;
        }
    }
}
