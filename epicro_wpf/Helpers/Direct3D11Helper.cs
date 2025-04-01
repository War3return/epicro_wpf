using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.DirectX.Direct3D11;
using System.Runtime.InteropServices;
using WinRT;
using Vortice.Direct3D;

namespace epicro_wpf.Helpers
{
    public static class Direct3D11Helper
    {
        [DllImport("d3d11.dll")]
        public static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int DriverType,
            IntPtr Software,
            int Flags,
            IntPtr pFeatureLevels,
            int FeatureLevels,
            int SDKVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        [DllImport("Windows.Graphics.Capture.dll")]
        public static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        public static (ID3D11Device d3dDevice, ID3D11DeviceContext d3dContext) CreateDevice()
        {
            // 디바이스 생성 플래그 설정
            DeviceCreationFlags creationFlags = DeviceCreationFlags.BgraSupport;

            // 지원할 피처 레벨 배열 정의
            FeatureLevel[] featureLevels = new[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1
    };

            // Direct3D 11 디바이스 및 디바이스 컨텍스트 생성
            D3D11.D3D11CreateDevice(
                null, // 기본 어댑터 사용
                DriverType.Hardware,
                creationFlags,
                featureLevels,
                out ID3D11Device d3dDevice,
                out ID3D11DeviceContext d3dContext);

            return (d3dDevice, d3dContext);
        }
    }
}
