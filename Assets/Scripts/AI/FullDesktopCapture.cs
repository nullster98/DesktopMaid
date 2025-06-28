using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class FullDesktopCapture
{
    // Win32 API 선언
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(
        IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, System.Int32 dwRop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("kernel32.dll")] private static extern uint GetLastError(); // 에러 코드 확인용 (선택 사항)


    private const int SRCCOPY = 0x00CC0020; // BitBlt에서 원본을 그대로 복사하는 옵션
    private const uint DIB_RGB_COLORS = 0;  // GetDIBits에서 컬러 테이블이 없음을 의미 (RGB 값 직접 사용)

    /// <summary>
    /// 현재 모니터(해상도)의 전체 화면을 캡처하여 Unity Texture2D로 반환합니다.
    /// 반드시 Windows에서만 작동합니다.
    /// </summary>
    public static Texture2D CaptureEntireDesktop()
    {
        // 1) 데스크탑 핸들, 해상도 구하기
        IntPtr desktopWnd = GetDesktopWindow();
        if (desktopWnd == IntPtr.Zero)
        {
            Debug.LogError("[FullDesktopCapture] GetDesktopWindow 실패");
            return null;
        }

        IntPtr desktopDC = GetWindowDC(desktopWnd);
        if (desktopDC == IntPtr.Zero)
        {
            Debug.LogError("[FullDesktopCapture] GetWindowDC 실패");
            // GetDesktopWindow는 성공했으나 DC를 못 얻는 경우는 드물지만, 방어 코드
            // ReleaseDC는 desktopWnd가 유효할 때만 호출해야 하므로, 여기서는 바로 null 반환
            return null;
        }

        int width = Screen.currentResolution.width;
        int height = Screen.currentResolution.height;

        // 2) 메모리 DC, 호환 비트맵 생성
        IntPtr memDC = CreateCompatibleDC(desktopDC);
        if (memDC == IntPtr.Zero)
        {
            Debug.LogError("[FullDesktopCapture] CreateCompatibleDC 실패");
            ReleaseDC(desktopWnd, desktopDC);
            return null;
        }

        IntPtr hBitmap = CreateCompatibleBitmap(desktopDC, width, height);
        if (hBitmap == IntPtr.Zero)
        {
            Debug.LogError("[FullDesktopCapture] CreateCompatibleBitmap 실패");
            DeleteDC(memDC);
            ReleaseDC(desktopWnd, desktopDC);
            return null;
        }

        IntPtr oldBitmap = SelectObject(memDC, hBitmap);
        if (oldBitmap == IntPtr.Zero)
        {
            Debug.LogError("[FullDesktopCapture] SelectObject 실패 (hBitmap 선택)");
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(desktopWnd, desktopDC);
            return null;
        }

        // 3) BitBlt로 데스크탑 전부를 메모리 DC로 복사
        bool blitResult = BitBlt(memDC, 0, 0, width, height, desktopDC, 0, 0, SRCCOPY);
        if (!blitResult)
        {
            Debug.LogWarning("[FullDesktopCapture] BitBlt 실패. Error Code: " + GetLastError());
            // 실패해도 리소스 정리는 계속 진행
        }

        // 4) Unity Texture2D 객체 생성
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        // 5) 비트맵에서 픽셀 데이터를 읽어오기 위해 BITMAPINFOHEADER와 GetDIBits를 사용
        //    (GDI+ 없이 Raw 픽셀을 읽기 위한 Win32 API 호출)
        var bitmapData = new BITMAPINFO();
        bitmapData.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)); // biSize는 uint 타입이므로 캐스팅
        bitmapData.bmiHeader.biWidth = width;
        bitmapData.bmiHeader.biHeight = -height; // top-down DIB (픽셀 순서가 위에서 아래로)
        bitmapData.bmiHeader.biPlanes = 1;
        bitmapData.bmiHeader.biBitCount = 24;     // RGB24
        bitmapData.bmiHeader.biCompression = 0;   // BI_RGB (압축 없음)
        // biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant는 0으로 초기화되거나 GetDIBits에서 무시됨 (BI_RGB의 경우)

        // 스캔 라인 당 바이트 수 계산 (DWORD(4바이트) 경계로 패딩됨)
        int scanLineSize = ((width * bitmapData.bmiHeader.biBitCount + 31) / 32) * 4;
        int totalBytes = scanLineSize * height; // 실제 이미지 데이터 크기
        byte[] pixelData = new byte[totalBytes];

        // GetDIBits 호출
        // 성공 시 복사된 스캔 라인 수를 반환, 실패 시 0을 반환
        int linesCopied = GetDIBits( // 반환 타입을 int로 변경하여 직접 사용
            memDC,
            hBitmap,
            0U,                 // uStartScan (uint 타입이므로 'U' 접미사 또는 (uint)0 사용)
            (uint)height,       // cScanLines (height는 양수이므로 캐스팅)
            pixelData,
            ref bitmapData,
            DIB_RGB_COLORS      // uUsage (DIB_RGB_COLORS는 0이므로 0U 또는 (uint)0 사용)
        );

        if (linesCopied == 0) // 실패 시
        {
            Debug.LogWarning("[FullDesktopCapture] GetDIBits 실패. 복사된 라인 수: 0. Error Code: " + GetLastError());
            // 실패 시 pixelData는 유효하지 않으므로, tex는 빈 상태(검은색 등)로 남을 수 있음
            // 필요시 여기서 tex = null; 또는 tex.EncodeToPNG() 등을 호출하지 않도록 처리
        }
        else
        {
            // 6) pixelData를 Unity Texture2D에 복사
            //    Win32 DIB는 BGR 순서이므로, RGB로 바꿔줘야 함
            //    biHeight가 음수(top-down)이므로, 픽셀 데이터의 첫 번째 바이트가 이미지의 왼쪽 상단 픽셀임.
            Color32[] colors = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * scanLineSize; // DIB 데이터는 top-down이므로 y 인덱스 그대로 사용
                for (int x = 0; x < width; x++)
                {
                    int pixelIndexInDIB = rowStart + x * 3; // 3 bytes per pixel (B,G,R)
                    byte b = pixelData[pixelIndexInDIB + 0];
                    byte g = pixelData[pixelIndexInDIB + 1];
                    byte r = pixelData[pixelIndexInDIB + 2];
                    colors[y * width + x] = new Color32(r, g, b, 255); // Unity는 RGB 순서
                }
            }
            tex.SetPixels32(colors);
            tex.Apply();
        }

        // 7) GDI 개체 정리
        SelectObject(memDC, oldBitmap); // 원래 비트맵으로 되돌림
        DeleteObject(hBitmap);          // 생성한 비트맵 삭제
        DeleteDC(memDC);                // 생성한 메모리 DC 삭제
        ReleaseDC(desktopWnd, desktopDC); // 데스크탑 DC 해제

        return tex;
    }

    #region Win32 구조체 & GetDIBits 선언
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

    // RGBQUAD는 BITMAPINFO 내에 포함될 때 사용되지만, 24비트 DIB에서는 GetDIBits가 bmiColors 필드를 직접 참조하지 않음
    // GetDIBits 호출 시 ref BITMAPINFO를 전달할 때 정확한 구조체 레이아웃을 위해 정의는 필요
    [StructLayout(LayoutKind.Sequential)]
    private struct RGBQUAD
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        // 24bpp DIB의 경우, bmiColors는 사용되지 않거나 하나의 더미 RGBQUAD만 있어도 무방.
        // GetDIBits는 bmiHeader.biSize를 보고 헤더의 끝을 판단함.
        public RGBQUAD bmiColors; // 단일 RGBQUAD (GetDIBits가 24bpp에서 직접 사용 안함)
    }

    // GetDIBits의 반환 타입은 int (복사된 스캔 라인 수 또는 에러 시 0)
    [DllImport("gdi32.dll", SetLastError = true)] // SetLastError = true 추가하여 Marshal.GetLastWin32Error() 사용 가능하도록
    private static extern int GetDIBits( // 반환 타입을 IntPtr에서 int로 변경
        IntPtr hdc,
        IntPtr hbmp,
        uint uStartScan,
        uint cScanLines,
        [Out] byte[] lpvBits,
        ref BITMAPINFO lpbi,
        uint uUsage
    );
    #endregion
}