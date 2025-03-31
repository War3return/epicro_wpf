using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace epicro_wpf.Helpers
{
    public static class WinRTHelper
    {
        // RoGetActivationFactory는 combase.dll에 있습니다.
        [DllImport("combase.dll", ExactSpelling = true)]
        private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

        // WindowsCreateString와 WindowsDeleteString은 WinRT HSTRING 관리용입니다.
        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string sourceString, uint length, out IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        /// <summary>
        /// 주어진 WinRT 타입(예, GraphicsCaptureItem)의 활성화 팩토리를 반환합니다.
        /// 이 함수는 WindowsRuntimeMarshal.GetActivationFactory 대신 사용할 수 있습니다.
        /// </summary>
        /// <param name="type">활성화 팩토리를 얻고자 하는 WinRT 타입</param>
        /// <returns>활성화 팩토리 객체</returns>
        /// 
        public static IGraphicsCaptureItemInterop GetInterop()
        {
            string runtimeClassName = "Windows.Graphics.Capture.GraphicsCaptureItem";

            // HSTRING 생성
            WindowsCreateString(runtimeClassName, (uint)runtimeClassName.Length, out var hstring);

            // IActivationFactory 얻기
            var iid = new Guid("00000035-0000-0000-C000-000000000046"); // IActivationFactory GUID
            RoGetActivationFactory(hstring, ref iid, out var factoryPtr);
            WindowsDeleteString(hstring);

            // QueryInterface로 IGraphicsCaptureItemInterop 얻기
            var interopGuid = new Guid("79C3F95B-31F7-4ec2-A464-632EF5D30760");
            Marshal.QueryInterface(factoryPtr, ref interopGuid, out var interopPtr);
            Marshal.Release(factoryPtr);

            return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(interopPtr);
        }

        public static Windows.Graphics.Capture.GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr hwnd)
        {
            var interop = GetInterop();
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
        public static object GetActivationFactory(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            // WinRT 런타임 클래스 이름은 보통 해당 타입의 전체 이름을 사용합니다.
            // 중첩 타입의 경우 '+' 대신 '.'로 변경합니다.
            string className = type.FullName.Replace('+', '.');

            // HSTRING 생성
            IntPtr hstring;
            int hr = WindowsCreateString(className, (uint)className.Length, out hstring);
            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);

            // 활성화 팩토리는 IActivationFactory 인터페이스를 구현합니다.
            Guid iid = typeof(IActivationFactory).GUID;
            hr = RoGetActivationFactory(hstring, ref iid, out IntPtr factory);
            // HSTRING은 더 이상 필요 없으므로 삭제
            WindowsDeleteString(hstring);
            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);

            object result = Marshal.GetObjectForIUnknown(factory);
            Marshal.Release(factory);
            return result;
        }
    }

    // IActivationFactory는 활성화 팩토리의 기본 인터페이스(마커 인터페이스)입니다.
    [ComImport]
    [Guid("00000035-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IActivationFactory
    {
        // 마커 인터페이스이므로 멤버를 정의하지 않습니다.
    }
}