using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ClassLibrary1;

class TestWinDiag
{
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool rep);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);

    static IntPtr FindWin(System.Reflection.Assembly asm, object emu)
    {
        var mi = asm.GetType("ClassLibrary1.EmulatorHelper").GetMethod("FindEmulatorWindow",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (IntPtr)mi.Invoke(emu, null);
    }

    static void Main()
    {
        var asm = typeof(EmulatorHelper).Assembly;
        var emu = new EmulatorHelper(@"O:\app\雷电\leidian\LDPlayer14\adb.exe", 0);
        // 预热 GetDeviceSize 缓存
        emu.OCRText(0, 0, 1, 1);

        IntPtr hwnd = FindWin(asm, emu);
        Console.WriteLine("hwnd=" + hwnd);
        if (hwnd == IntPtr.Zero) return;

        RECT cr, wr;
        GetClientRect(hwnd, out cr); GetWindowRect(hwnd, out wr);
        int cw = cr.R - cr.L, ch = cr.B - cr.T;
        Console.WriteLine("client=" + cw + "x" + ch + " window=" + (wr.R - wr.L) + "x" + (wr.B - wr.T));

        int frameW = (wr.R - wr.L) - cw, frameH = (wr.B - wr.T) - ch;
        bool ok = MoveWindow(hwnd, wr.L, wr.T, 720 + frameW, 1280 + frameH, true);
        Console.WriteLine("MoveWindow to 1:1 -> " + ok);
        System.Threading.Thread.Sleep(60);
        GetClientRect(hwnd, out cr);
        Console.WriteLine("client after resize=" + (cr.R - cr.L) + "x" + (cr.B - cr.T));

        // PrintWindow 多次采样，检查重绘是否完成
        for (int i = 0; i < 5; i++)
        {
            var bmp = new Bitmap(cr.R - cr.L, cr.B - cr.T);
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                bool pok = PrintWindow(hwnd, hdc, 1);
                g.ReleaseHdc(hdc);
                if (!pok) { Console.WriteLine("iter " + i + ": PrintWindow false"); bmp.Dispose(); System.Threading.Thread.Sleep(30); continue; }
            }
            // 采样区域 (64,268)-(148,292) 统计非黑像素
            int nonblack = 0, cnt = 0;
            for (int y = 268; y < 292; y += 2)
                for (int x = 64; x < 148; x += 2)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R > 8 || c.G > 8 || c.B > 8) nonblack++;
                    cnt++;
                }
            Console.WriteLine("iter " + i + ": region nonblack=" + nonblack + "/" + cnt);
            if (i == 4)
            {
                using (var crop = bmp.Clone(new Rectangle(58, 262, 96, 36), PixelFormat.Format24bppRgb))
                    crop.Save(@"Z:\interest\ClassLibrary1\_diag_crop.png", ImageFormat.Png);
                bmp.Save(@"Z:\interest\ClassLibrary1\_diag_full.png", ImageFormat.Png);
                Console.WriteLine("saved _diag_crop.png / _diag_full.png");
            }
            bmp.Dispose();
            System.Threading.Thread.Sleep(30);
        }

        // 恢复窗口
        bool ok2 = MoveWindow(hwnd, wr.L, wr.T, wr.R - wr.L, wr.B - wr.T, true);
        Console.WriteLine("restore -> " + ok2);
        GetClientRect(hwnd, out cr);
        Console.WriteLine("client after restore=" + (cr.R - cr.L) + "x" + (cr.B - cr.T));
    }
}
