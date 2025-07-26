using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Win32 API를 사용하여 Windows 데스크톱 전체 화면을 캡처하는 정적 유틸리티 클래스.
/// </summary>
public static class FullDesktopCapture
{
    #region Public Capture Method

    /// <summary>
    /// 현재 모니터의 전체 데스크톱 화면을 캡처하여 Unity Texture2D로 반환합니다.
    /// 이 함수는 Windows 운영체제에서만 작동합니다.
    /// </summary>
    /// <returns>캡처된 화면의 Texture2D. 실패 시 null을 반환합니다.</returns>
    public static Texture2D CaptureEntireDesktop()
    {
        // 1. 데스크톱 핸들 및 디바이스 컨텍스트(DC) 가져오기
        IntPtr desktopWnd = GetDesktopWindow();
        IntPtr desktopDC = GetWindowDC(desktopWnd);
        
        // 2. 캡처할 이미지의 크기 결정 (주 모니터 해상도)
        int width = Screen.currentResolution.width;
        int height = Screen.currentResolution.height;

        // 3. 메모리에 캡처 이미지를 담을 GDI 비트맵 생성
        IntPtr memDC = CreateCompatibleDC(desktopDC);
        IntPtr hBitmap = CreateCompatibleBitmap(desktopDC, width, height);
        IntPtr oldBitmap = SelectObject(memDC, hBitmap);

        try
        {
            // 4. 실제 화면(desktopDC)을 메모리 비트맵(memDC)으로 복사
            bool success = BitBlt(memDC, 0, 0, width, height, desktopDC, 0, 0, SRCCOPY);
            if (!success)
            {
                Debug.LogWarning($"[FullDesktopCapture] BitBlt failed with error code: {GetLastError()}");
                return null;
            }

            // 5. GDI 비트맵 데이터를 Unity Texture2D로 변환
            return CreateTextureFromBitmap(memDC, hBitmap, width, height);
        }
        finally
        {
            // 6. 사용한 모든 GDI 리소스를 반드시 해제하여 메모리 누수 방지
            SelectObject(memDC, oldBitmap); // 원래 비트맵으로 복원
            DeleteObject(hBitmap);          // 생성한 비트맵 삭제
            DeleteDC(memDC);                // 생성한 메모리 DC 삭제
            ReleaseDC(desktopWnd, desktopDC); // 데스크톱 DC 해제
        }
    }

    /// <summary>
    /// GDI 비트맵 핸들로부터 픽셀 데이터를 추출하여 Texture2D를 생성합니다.
    /// </summary>
    private static Texture2D CreateTextureFromBitmap(IntPtr memDC, IntPtr hBitmap, int width, int height)
    {
        // 비트맵 정보를 담을 구조체 설정
        var bmpInfo = new BITMAPINFO();
        bmpInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
        bmpInfo.bmiHeader.biWidth = width;
        bmpInfo.bmiHeader.biHeight = -height; // Top-Down 형식으로 데이터 요청
        bmpInfo.bmiHeader.biPlanes = 1;
        bmpInfo.bmiHeader.biBitCount = 24;    // 픽셀당 24비트 (RGB)
        bmpInfo.bmiHeader.biCompression = 0;  // BI_RGB (압축 없음)

        // 픽셀 데이터를 담을 바이트 배열 할당
        int scanLineSize = ((width * bmpInfo.bmiHeader.biBitCount + 31) / 32) * 4; // 4바이트 경계 패딩
        byte[] pixelData = new byte[scanLineSize * height];

        // GetDIBits를 호출하여 비트맵에서 바이트 배열로 픽셀 데이터 추출
        int linesCopied = GetDIBits(memDC, hBitmap, 0, (uint)height, pixelData, ref bmpInfo, DIB_RGB_COLORS);
        if (linesCopied == 0)
        {
            Debug.LogWarning($"[FullDesktopCapture] GetDIBits failed with error code: {GetLastError()}");
            return null;
        }

        // Win32 DIB(BGR) 순서를 Unity(RGB) 순서로 변환
        var colors = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * scanLineSize;
            for (int x = 0; x < width; x++)
            {
                int pixelIndexInDIB = rowStart + x * 3; // 3 bytes per pixel (B, G, R)
                byte b = pixelData[pixelIndexInDIB + 0];
                byte g = pixelData[pixelIndexInDIB + 1];
                byte r = pixelData[pixelIndexInDIB + 2];
                // Y축은 이미 Top-Down 형식이므로 뒤집을 필요 없음
                colors[y * width + x] = new Color32(r, g, b, 255);
            }
        }
        
        // 최종 Texture2D 생성 및 픽셀 데이터 적용
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.SetPixels32(colors);
        tex.Apply();
        
        return tex;
    }

    #endregion

    #region Win32 API Imports & Structures

    // 캡처 작업에 필요한 Win32 API 함수들을 DllImport로 선언
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);
    [DllImport("kernel32.dll")] private static extern uint GetLastError();

    private const uint SRCCOPY = 0x00CC0020;
    private const uint DIB_RGB_COLORS = 0;

    // 비트맵 정보를 담기 위한 Win32 구조체 선언
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public RGBQUAD bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RGBQUAD { public byte rgbBlue, rgbGreen, rgbRed, rgbReserved; }

    #endregion
}