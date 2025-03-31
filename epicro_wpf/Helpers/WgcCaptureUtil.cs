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
