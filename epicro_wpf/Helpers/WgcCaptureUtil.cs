using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices;

namespace epicro_wpf.Helpers
{
    public static class WgcCaptureUtil
    {
        public static async Task CaptureWindowToPngAsync(IntPtr hwnd, string filePath)
        {
            // 1. CaptureItem 생성
            var item = CaptureHelper.CreateItemForWindow(hwnd);
            if (item == null)
                throw new InvalidOperationException("Capture 대상 윈도우를 찾을 수 없습니다.");

            // 2. Direct3D Device 생성
            var d3dDevice = Direct3D11Helper.CreateDevice();
            //var wgcDevice = Direct3D11Helper.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer);

            // 3. FramePool 생성
            var framePool = Direct3D11CaptureFramePool.Create(
                d3dDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size);
            var session = framePool.CreateCaptureSession(item);

            var tcs = new TaskCompletionSource<Windows.Graphics.Capture.Direct3D11CaptureFrame>();

            framePool.FrameArrived += (s, e) =>
            {
                var frame = s.TryGetNextFrame();
                if (frame != null)
                    tcs.TrySetResult(frame);
            };

            session.StartCapture();

            // 프레임 도착 대기
            var result = await tcs.Task;

            // 캡처 종료
            session.Dispose();
            framePool.Dispose();

            // SoftwareBitmap 변환
            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(result.Surface);

            // PNG 저장
            await SaveSoftwareBitmapToPngAsync(softwareBitmap, filePath);
        }

        private static async Task SaveSoftwareBitmapToPngAsync(SoftwareBitmap bitmap, string path)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    stream.Seek(0);
                    await stream.AsStreamForRead().CopyToAsync(fileStream);
                }
            }
        }

    }
}
