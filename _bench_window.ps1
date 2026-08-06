Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$code = @'
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public static class CapBench {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
    public static void Run() {
        var pids = new HashSet<int>();
        foreach (var p in Process.GetProcessesByName("dnplayer")) pids.Add(p.Id);
        IntPtr hwnd = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            int pid; GetWindowThreadProcessId(h, out pid);
            if (pids.Contains(pid) && IsWindowVisible(h)) {
                var cls = new System.Text.StringBuilder(256);
                GetClassName(h, cls, 256);
                if (cls.ToString() == "LDPlayerMainFrame") { hwnd = h; return false; }
            }
            return true;
        }, IntPtr.Zero);
        if (hwnd == IntPtr.Zero) { Console.WriteLine("no window"); return; }
        RECT rc; GetClientRect(hwnd, out rc);
        int w = rc.R - rc.L, hgt = rc.B - rc.T;
        Console.WriteLine("window client: " + w + "x" + hgt);
        var sw = new Stopwatch();
        int n = 5; long total = 0; bool black = true;
        for (int i = 0; i < n; i++) {
            using (var bmp = new Bitmap(w, hgt)) {
                sw.Restart();
                using (var g = Graphics.FromImage(bmp)) {
                    IntPtr hdc = g.GetHdc();
                    bool ok = PrintWindow(hwnd, hdc, 3);
                    g.ReleaseHdc(hdc);
                    if (!ok) { Console.WriteLine("PrintWindow failed"); return; }
                }
                sw.Stop();
                total += sw.ElapsedMilliseconds;
                if (i == n - 1) {
                    int sx = 64 * w / 720, sy = 268 * hgt / 1280;
                    int nonblack = 0, cnt = 0;
                    for (int y = sy; y < sy + 30 && y < hgt; y += 3)
                        for (int x = sx; x < sx + 90 && x < w; x += 3) {
                            var c = bmp.GetPixel(x, y);
                            if (c.R + c.G + c.B > 30) nonblack++;
                            cnt++;
                        }
                    black = cnt > 0 && nonblack * 100 / cnt < 5;
                    bmp.Save(@"Z:\interest\ClassLibrary1\_bench_window.png", ImageFormat.Png);
                }
            }
        }
        Console.WriteLine("PrintWindow avg: " + (total / n) + " ms, regionMostlyBlack=" + black);
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Windows.Forms,System.Drawing
[CapBench]::Run()
