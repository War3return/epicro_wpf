using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Foundation;
using System.Runtime.InteropServices;

public static class Direct3D11Helper
{
    [DllImport("d3d11.dll", SetLastError = true)]
    private static extern int D3D11CreateDevice(
        IntPtr adapter,
        int driverType,
        IntPtr software,
        uint flags,
        IntPtr featureLevels,
        uint featureLevelsCount,
        uint sdkVersion,
        out IntPtr device,
        out IntPtr immediateContext,
        out IntPtr featureLevel);

    [DllImport("Windows.Graphics.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    public static IDirect3DDevice CreateD3DDevice()
    {
        IntPtr devicePtr;
        IntPtr context;
        IntPtr featureLevel;

        int hr = D3D11CreateDevice(
            IntPtr.Zero, // Default Adapter
            1,           // D3D_DRIVER_TYPE_HARDWARE
            IntPtr.Zero,
            0x20,        // D3D11_CREATE_DEVICE_BGRA_SUPPORT
            IntPtr.Zero,
            0,
            7, // D3D11_SDK_VERSION
            out devicePtr,
            out context,
            out featureLevel);

        if (hr != 0)
            Marshal.ThrowExceptionForHR(hr);

        IntPtr d3dDevice;
        hr = CreateDirect3D11DeviceFromDXGIDevice(devicePtr, out d3dDevice);

        if (hr != 0)
            Marshal.ThrowExceptionForHR(hr);

        var device = MarshalInterface<IDirect3DDevice>.FromAbi(d3dDevice);
        Marshal.Release(devicePtr);
        Marshal.Release(context);
        Marshal.Release(featureLevel);

        return device;
    }
}
