using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Vortice.Direct3D11;
using Windows.Graphics;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.DXGI;

namespace epicro_wpf.Helpers
{
    public class CaptureHelper
    {
        private readonly IDirect3DDevice _d3dDevice;
        private readonly GraphicsCaptureItem _captureItem;
        private readonly SizeInt32 _size;

        public CaptureHelper(GraphicsCaptureItem item)
        {
            _captureItem = item;
            // 네이티브 Direct3D 디바이스와 컨텍스트 생성 (두 번째 요소는 d3dContext이므로 사용하지 않음)
            var (d3dDevice, _) = Direct3D11Helper.CreateDevice();

            // d3dDevice를 기반으로 WinRT IDirect3DDevice 생성
            var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
            IntPtr winrtDevicePtr;
            Direct3D11Helper.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out winrtDevicePtr);
            _d3dDevice = Marshal.GetObjectForIUnknown(winrtDevicePtr) as IDirect3DDevice;

            _size = item.Size;
        }

        public async Task<SoftwareBitmap> CaptureToBitmapAsync()
        {
            var tcs = new TaskCompletionSource<SoftwareBitmap>();

            using var framePool = Direct3D11CaptureFramePool.Create(
                _d3dDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                _size);

            using var session = framePool.CreateCaptureSession(_captureItem);

            void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
            {
                using var frame = sender.TryGetNextFrame();
                SoftwareBitmap bitmap = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface).AsTask().Result;
                tcs.SetResult(bitmap);
                sender.FrameArrived -= OnFrameArrived;
            }

            framePool.FrameArrived += OnFrameArrived;
            session.StartCapture();

            return await tcs.Task;
        }

        public static async Task SaveBitmapAsync(SoftwareBitmap bitmap, string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Create);
            var randomStream = stream.AsRandomAccessStream();

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomStream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
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
    }
}
