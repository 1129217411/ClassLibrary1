using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace ClassLibrary1
{
    public class EmulatorHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern void OutputDebugString(string lpOutputString);

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;
        private static bool _consoleAllocated = false;
        private string consolePath;
        private int realIndex;

        // ======================== 截图守护进程（方案 C） ========================
        private const int SCREENSHOT_DAEMON_PORT_BASE = 19001; // PC端端口 = base + realIndex
        private const int SCREENSHOT_DAEMON_DEVICE_PORT = 19000; // 模拟器内监听端口
        private bool _screenshotServiceReady = false;
        private bool _shellDaemonReady = false;
        // ======================== 持久化 adb shell（方案 D） ========================
        private Process _persistentShellProcess;
        private Stream _shellInputStream;
        private Stream _shellOutputStream;
        private bool _persistentShellReady = false;
        private static readonly byte[] SHELL_MARKER = new byte[] { 0x53, 0x53, 0x42, 0x47, 0x0A }; // "SSBG\n"

        public EmulatorHelper(string consolePath, int realIndex)
        {
            this.consolePath = consolePath;
            this.realIndex = realIndex;
        }

        /// <summary>
        /// 是否优先使用宿主机窗口截图（PrintWindow，约 20ms，远快于 adb 截图 350ms+）。
        /// 找不到窗口 / 窗口最小化 / 截到黑屏时会自动回退到 adb 截图。
        /// 注意：窗口截图的坐标可能与 adb 不一致（模拟器窗口含 UI 元素），默认关闭以保证坐标准确。
        /// </summary>
        public static bool UseWindowCapture = false;

        /// <summary>
        /// 截图模拟器屏幕，返回截图本地路径
        /// </summary>
        public string Screenshot()
        {
            string localPath = Path.Combine(
                Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),
                "screenshot_" + realIndex + ".png");

            // 方式一：宿主机窗口截图（需开启开关）
            if (UseWindowCapture)
            {
                try
                {
                    if (ScreenshotByWindow(localPath))
                    {
                        Log("[截图] 窗口截图成功: " + localPath);
                        return localPath;
                    }
                }
                catch (Exception ex) { Log("[截图] 窗口截图异常: " + ex.Message); }
            }

            // 方式二：adb exec-out 直出 PNG 流（免 sdcard 写盘+pull，比两步式快约160ms）
            try
            {
                if (ScreenshotByAdbStream(localPath))
                {
                    long len = new FileInfo(localPath).Length;
                    if (len > 100)
                    {
                        Log(string.Format("[截图] adb流截图成功: {0} ({1} bytes)", localPath, len));
                        return localPath;
                    }
                    Log(string.Format("[截图] adb流截图文件太小: {0} bytes", len));
                }
            }
            catch (Exception ex) { Log("[截图] adb流截图异常: " + ex.Message); }

            // 回退：旧的两步式
            Log("[截图] 使用两步式 adb 截图回退");
            string remotePath = "/sdcard/ld_auto.png";
            RunAdb("shell screencap -p " + remotePath);
            RunAdb("pull " + remotePath + " " + localPath);
            if (File.Exists(localPath) && new FileInfo(localPath).Length > 100)
            {
                Log(string.Format("[截图] 两步式截图完成: {0} ({1} bytes)", localPath, new FileInfo(localPath).Length));
                return localPath;
            }
            Log("[截图] 两步式截图失败，尝试窗口截图回退");

            // 最后兑底：宿主机窗口截图（含标题栏、坐标可能不准，仅作兑底）
            try
            {
                if (ScreenshotByWindow(localPath))
                {
                    Log("[截图] 窗口截图兑底成功: " + localPath);
                    return localPath;
                }
            }
            catch (Exception ex) { Log("[截图] 窗口截图异常: " + ex.Message); }
            return localPath;
        }

        /// <summary>
        /// 通过 exec-out screencap -p 把 PNG 流直接写入本地文件
        /// </summary>
        private bool ScreenshotByAdbStream(string localPath)
        {
            byte[] data = GetAdbPngBytes();
            if (data == null) return false;
            File.WriteAllBytes(localPath, data);
            return true;
        }

        /// <summary>
        /// 通过 exec-out screencap -p 直出 PNG 流，返回 PNG 字节（失败返回 null）
        /// </summary>
        private byte[] GetAdbPngBytes()
        {
            string adbExe = ResolveAdbExe();
            if (adbExe == null) return null;

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = adbExe;
            psi.Arguments = "-s " + DeviceSerial() + " exec-out screencap -p";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            byte[] data;
            using (Process p = Process.Start(psi))
            {
                using (var ms = new MemoryStream())
                {
                    p.StandardOutput.BaseStream.CopyTo(ms);
                    data = ms.ToArray();
                }
                p.StandardError.ReadToEnd();
                if (!p.WaitForExit(10000))
                {
                    try { p.Kill(); } catch { }
                    return null;
                }
            }

            // Windows 上 adb 会把二进制流中的 0A 扩展成 0D 0A，导致 PNG 损坏；先校验，不合法则修复
            if (!IsPng(data))
            {
                data = UndoCrlfExpansion(data);
                if (!IsPng(data)) return null;
            }
            return data;
        }

        private static bool IsPng(byte[] data)
        {
            if (data == null || data.Length < 8) return false;
            return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
                && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
        }

        /// <summary>
        /// 撤销换行扩展：每个 0A 前的插入式 0D 移除（0D 0A -> 0A）
        /// </summary>
        private static byte[] UndoCrlfExpansion(byte[] data)
        {
            var result = new byte[data.Length];
            int n = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x0D && i + 1 < data.Length && data[i + 1] == 0x0A)
                    continue;
                result[n++] = data[i];
            }
            var trimmed = new byte[n];
            Array.Copy(result, trimmed, n);
            return trimmed;
        }

        /// <summary>
        /// 通过 adb exec-out screencap 获取 raw 像素数据并直接转为 Bitmap（~150ms）。
        /// exec-out 专为二进制输出设计，不会做 CR/LF 转换，比 adb PNG 快 ~2-3 倍（省去 PNG 编码）。
        /// 输出格式：12 字节头 (w:i32le + h:i32le + format:i32le) + RGBA 像素数据 (w*h*4 字节)
        /// </summary>
        private Bitmap CaptureBitmapRaw()
        {
            string adbExe = ResolveAdbExe();
            if (adbExe == null) return null;

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = adbExe;
            psi.Arguments = "-s " + DeviceSerial() + " exec-out screencap";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process p = Process.Start(psi))
            {
                var stream = p.StandardOutput.BaseStream;

                // 读取 12 字节头: width(4) + height(4) + format(4)
                byte[] header = new byte[12];
                int headerRead = 0;
                while (headerRead < 12)
                {
                    int r = stream.Read(header, headerRead, 12 - headerRead);
                    if (r <= 0) break;
                    headerRead += r;
                }
                if (headerRead < 12)
                {
                    Log("[截图服务] raw: 无法读取头部 (" + headerRead + " bytes)");
                    try { p.Kill(); } catch { }
                    return null;
                }

                int w = BitConverter.ToInt32(header, 0);
                int h = BitConverter.ToInt32(header, 4);

                if (w <= 0 || w > 4096 || h <= 0 || h > 4096)
                {
                    Log(string.Format("[截图服务] raw: 无效分辨率 {0}x{1}", w, h));
                    try { p.Kill(); } catch { }
                    return null;
                }

                // 读取 RGBA 像素数据
                int pixelDataSize = w * h * 4;
                byte[] rgba = new byte[pixelDataSize];
                int totalRead = 0;
                while (totalRead < pixelDataSize)
                {
                    int r = stream.Read(rgba, totalRead, pixelDataSize - totalRead);
                    if (r <= 0) break;
                    totalRead += r;
                }

                p.StandardError.ReadToEnd();
                if (!p.WaitForExit(10000))
                {
                    try { p.Kill(); } catch { }
                }

                if (totalRead < pixelDataSize)
                {
                    Log(string.Format("[截图服务] raw: 像素不完整 {0}/{1}", totalRead, pixelDataSize));
                    return null;
                }

                // RGBA → ARGB32 Bitmap
                Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, w, h),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                byte[] argb = new byte[pixelDataSize];
                for (int i = 0; i < pixelDataSize; i += 4)
                {
                    argb[i]     = rgba[i + 3]; // A
                    argb[i + 1] = rgba[i];     // R
                    argb[i + 2] = rgba[i + 1]; // G
                    argb[i + 3] = rgba[i + 2]; // B
                }

                Marshal.Copy(argb, 0, bmpData.Scan0, pixelDataSize);
                bmp.UnlockBits(bmpData);
                return bmp;
            }
        }

        // ======================== 宿主机窗口截图 ========================

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        /// <summary>
        /// 从宿主机直接截取模拟器窗口客户区（不走 adb，约20ms）。
        /// 失败（找不到窗口/最小化/截到黑屏）时返回 false，由调用方回退 adb 方式。
        /// </summary>
        private bool ScreenshotByWindow(string localPath)
        {
            IntPtr hwnd = FindEmulatorWindow();
            if (hwnd == IntPtr.Zero || IsIconic(hwnd)) return false;

            RECT rc;
            if (!GetClientRect(hwnd, out rc)) return false;
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            if (w <= 0 || h <= 0) return false;

            Bitmap bmp = null;
            try
            {
                bmp = new Bitmap(w, h);
                // PW_CLIENTONLY=1；若截到黑屏再用 PW_CLIENTONLY|PW_RENDERFULLCONTENT=3 重试
                if (!PrintWindowToBitmap(hwnd, bmp, 1) || IsMostlyBlack(bmp))
                {
                    if (!PrintWindowToBitmap(hwnd, bmp, 3) || IsMostlyBlack(bmp))
                        return false;
                }

                // 以实际 bitmap 尺寸为准，检查宽高比是否与设备一致
                int bw = bmp.Width, bh = bmp.Height;
                Size dev = GetDeviceSize();
                if (dev.Width > 0 && dev.Height > 0)
                {
                    // 宽高比偏差超过 5% 说明窗口有额外 UI 元素，缩放会扭曲内容，回退 adb
                    double bmpRatio = (double)bw / bh;
                    double devRatio = (double)dev.Width / dev.Height;
                    double ratioDiff = Math.Abs(bmpRatio - devRatio) / devRatio;
                    if (ratioDiff > 0.05)
                        return false;
                    // 宽高比一致但尺寸不同（窗口被等比缩放），缩放到设备分辨率保证坐标准确
                    if (bw != dev.Width || bh != dev.Height)
                    {
                        var scaled = new Bitmap(bmp, dev.Width, dev.Height);
                        bmp.Dispose();
                        bmp = scaled;
                    }
                }
                bmp.Save(localPath, System.Drawing.Imaging.ImageFormat.Png);
                return true;
            }
            finally
            {
                if (bmp != null) bmp.Dispose();
            }
        }

        private static bool PrintWindowToBitmap(IntPtr hwnd, Bitmap bmp, uint flags)
        {
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try
                {
                    return PrintWindow(hwnd, hdc, flags);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }
        }

        /// <summary>
        /// 查找当前模拟器序号对应的窗口句柄（按类名 LDPlayerMainFrame 匹配，排除"新通知"等干扰窗口）
        /// </summary>
        private IntPtr FindEmulatorWindow()
        {
            // 收集所有 dnplayer 进程 PID
            var pids = new HashSet<int>();
            foreach (var p in Process.GetProcessesByName("dnplayer"))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
            if (pids.Count == 0) return IntPtr.Zero;

            // 枚举属于 dnplayer 的可见顶层窗口，只保留模拟器主窗口类
            var candidates = new List<KeyValuePair<IntPtr, string>>();
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                int pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pids.Contains(pid) && IsWindowVisible(hWnd))
                {
                    var cls = new System.Text.StringBuilder(256);
                    GetClassName(hWnd, cls, cls.Capacity);
                    if (cls.ToString() != "LDPlayerMainFrame") return true;

                    var sb = new System.Text.StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    candidates.Add(new KeyValuePair<IntPtr, string>(hWnd, sb.ToString()));
                }
                return true;
            }, IntPtr.Zero);
            if (candidates.Count == 0) return IntPtr.Zero;

            // 优先按标题匹配：新版雷电命名为"雷电模拟器N"（从1开始），旧版为"雷电模拟器"/"雷电模拟器-N"
            string[] wants = {
                "雷电模拟器" + (realIndex + 1),
                "雷电模拟器-" + realIndex,
                realIndex == 0 ? "雷电模拟器" : null
            };
            foreach (var want in wants)
            {
                if (want == null) continue;
                foreach (var kv in candidates)
                    if (kv.Value == want) return kv.Key;
            }

            // 回退：只有一个窗口直接用；否则按枚举顺序取（不严格保证对应关系）
            if (candidates.Count == 1) return candidates[0].Key;
            return realIndex < candidates.Count ? candidates[realIndex].Key : IntPtr.Zero;
        }

        /// <summary>
        /// 判断截图是否几乎全黑（D3D 渲染内容可能无法被 GDI 截取，需回退 adb）
        /// </summary>
        private static bool IsMostlyBlack(Bitmap bmp)
        {
            int black = 0, total = 0;
            int stepX = Math.Max(1, bmp.Width / 10), stepY = Math.Max(1, bmp.Height / 10);
            for (int y = stepY / 2; y < bmp.Height; y += stepY)
            {
                for (int x = stepX / 2; x < bmp.Width; x += stepX)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R < 8 && c.G < 8 && c.B < 8) black++;
                    total++;
                }
            }
            return total > 0 && black * 100 / total >= 95;
        }

        /// <summary>
        /// 设备分辨率（缓存，窗口截图缩放用）
        /// </summary>
        private Size? _deviceSize = null;

        private Size GetDeviceSize()
        {
            if (_deviceSize.HasValue) return _deviceSize.Value;
            Size size = new Size(0, 0);
            try
            {
                // 输出示例: Physical size: 720x1280
                string output = RunAdb("shell wm size");
                int idx = output.LastIndexOf(':');
                if (idx >= 0)
                {
                    string[] parts = output.Substring(idx + 1).Trim().Split('x');
                    if (parts.Length == 2)
                        size = new Size(int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
                }
            }
            catch { }
            _deviceSize = size;
            return size;
        }

        /// <summary>
        /// 读取图片中指定坐标的像素颜色
        /// </summary>
        public static Color GetPixel(string imagePath, int x, int y)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(imagePath))
                {
                    if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height)
                        return bmp.GetPixel(x, y);
                }
            }
            catch { }
            return Color.Empty;
        }

        /// <summary>
        /// 点击屏幕坐标
        /// </summary>
        public void Tap(int x, int y)
        {
            RunAdb("shell input tap " + x + " " + y);
        }

        /// <summary>
        /// 滑动操作
        /// </summary>
        public void Swipe(int x1, int y1, int x2, int y2, int durationMs = 300)
        {
            RunAdb("shell input swipe " + x1 + " " + y1 + " " + x2 + " " + y2 + " " + durationMs);
        }

        /// <summary>
        /// 判断颜色是否近似匹配（绝对容差）
        /// </summary>
        public static bool IsColorMatch(Color actual, Color expected, int tolerance = 20)
        {
            return Math.Abs(actual.R - expected.R) <= tolerance
                && Math.Abs(actual.G - expected.G) <= tolerance
                && Math.Abs(actual.B - expected.B) <= tolerance;
        }

        /// <summary>
        /// 快速截取屏幕为 Bitmap：通过 adb exec-out screencap -p 获取 PNG 并解码
        /// </summary>
        private Bitmap CaptureBitmapFast()
        {
            byte[] png = GetAdbPngBytes();
            if (png != null && png.Length > 100)
            {
                try
                {
                    using (var ms = new MemoryStream(png))
                        return new Bitmap(ms);
                }
                catch { }
            }
            return null;
        }

        // ======================== 截图守护进程（方案 C） ========================

        /// <summary>
        /// 初始化截图守护进程：推送脚本到模拟器、启动守护进程、建立端口转发。
        /// 调用一次即可，之后所有截图走 TCP socket（~100ms/次）。
        /// </summary>
        public void InitScreenshotDaemon()
        {
            // 已简化为 adb PNG 方式，无需初始化守护进程
        }

        /// <summary>
        /// 通过守护进程 TCP socket 截取屏幕，返回 Bitmap（~100ms，raw 像素无 PNG 编码）
        /// </summary>
        private Bitmap CaptureBitmapViaDaemon()
        {
            int pcPort = SCREENSHOT_DAEMON_PORT_BASE + realIndex;

            using (var client = new TcpClient("127.0.0.1", pcPort))
            {
                client.SendTimeout = 3000;
                client.ReceiveTimeout = 10000;

                using (var stream = client.GetStream())
                {
                    // 发送截图命令
                    byte[] cmd = System.Text.Encoding.ASCII.GetBytes("shot");
                    stream.Write(cmd, 0, cmd.Length);

                    // 读取文本头行（格式: "width height\n"）
                    // 逐字节读取直到换行符（避免缓冲吃掉后续二进制数据）
                    var headerBuf = new List<byte>();
                    while (true)
                    {
                        int b = stream.ReadByte();
                        if (b == -1 || b == '\n') break;
                        headerBuf.Add((byte)b);
                    }
                    string header = System.Text.Encoding.ASCII.GetString(headerBuf.ToArray()).Trim();
                    if (header.StartsWith("ERR") || header.Length == 0)
                    {
                        Log("[截图服务] 守护进程返回错误");
                        return null;
                    }

                    string[] parts = header.Split(' ');
                    if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h))
                    {
                        Log("[截图服务] 无效头: " + header);
                        return null;
                    }

                    // 读取 RGBA 像素数据
                    int pixelDataSize = w * h * 4;
                    byte[] rgba = new byte[pixelDataSize];
                    int totalRead = 0;
                    while (totalRead < pixelDataSize)
                    {
                        int read = stream.Read(rgba, totalRead, pixelDataSize - totalRead);
                        if (read <= 0) break;
                        totalRead += read;
                    }

                    if (totalRead < pixelDataSize)
                    {
                        Log(string.Format("[截图服务] 像素数据不完整: 期望 {0} 字节，实际 {1} 字节", pixelDataSize, totalRead));
                        return null;
                    }

                    // RGBA → ARGB32 Bitmap
                    Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var bmpData = bmp.LockBits(
                        new Rectangle(0, 0, w, h),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    byte[] argb = new byte[pixelDataSize];
                    for (int i = 0; i < pixelDataSize; i += 4)
                    {
                        argb[i]     = rgba[i + 3]; // A
                        argb[i + 1] = rgba[i];     // R
                        argb[i + 2] = rgba[i + 1]; // G
                        argb[i + 3] = rgba[i + 2]; // B
                    }

                    Marshal.Copy(argb, 0, bmpData.Scan0, pixelDataSize);
                    bmp.UnlockBits(bmpData);
                    return bmp;
                }
            }
        }

        /// <summary>
        /// 执行 adb 命令并返回输出（内部工具方法）
        /// </summary>
        private string RunAdbShell(string adbExe, string serial, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = adbExe;
            psi.Arguments = "-s " + serial + " " + args;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit(10000);
                return output;
            }
        }

        /// <summary>
        /// 初始化持久化 adb shell 进程（方案 D）：启动一个长生命周期的 adb shell，
        /// 通过 stdin 发送 screencap 命令，从 stdout 读取 raw 像素。
        /// 无需模拟器内安装任何东西，每次截图无需新建进程。
        /// </summary>
        private void InitPersistentShell(string adbExe, string serial)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = adbExe;
                psi.Arguments = "-s " + serial + " shell";
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = System.Text.Encoding.UTF8;

                _persistentShellProcess = Process.Start(psi);
                _shellInputStream = _persistentShellProcess.StandardInput.BaseStream;
                _shellOutputStream = _persistentShellProcess.StandardOutput.BaseStream;

                // 关闭回显和输出处理，避免 PTY 干扰二进制数据
                byte[] sttyCmd = System.Text.Encoding.ASCII.GetBytes("stty -echo -opost raw\n");
                _shellInputStream.Write(sttyCmd, 0, sttyCmd.Length);
                _shellInputStream.Flush();
                System.Threading.Thread.Sleep(300);

                // 丢弃 stty 命令本身的输出（回显、提示符等）
                DrainShellOutput(500);

                _persistentShellReady = true;
                Log("[截图服务] 持久化 adb shell 就绪");
            }
            catch (Exception ex)
            {
                Log("[截图服务] 持久化 shell 初始化失败: " + ex.Message);
                CleanupPersistentShell();
            }
        }

        /// <summary>
        /// 通过持久化 adb shell 截取屏幕（方案 D，~100ms）
        /// 复用已建立的 adb shell 进程，每次截图无需新建 adb 进程，也无需 PNG 编码。
        /// </summary>
        private Bitmap CaptureBitmapViaPersistentShell()
        {
            if (_persistentShellProcess == null || _persistentShellProcess.HasExited)
            {
                _persistentShellReady = false;
                return null;
            }

            // 发送带标记的 screencap 命令
            // printf 输出二进制标记 \x53\x53\x42\x47\n，然后执行 screencap
            string cmd = "printf '\\x53\\x53\\x42\\x47\\n' && /system/bin/screencap && printf '\\x53\\x53\\x45\\x44\\n'\n";
            byte[] cmdBytes = System.Text.Encoding.ASCII.GetBytes(cmd);
            _shellInputStream.Write(cmdBytes, 0, cmdBytes.Length);
            _shellInputStream.Flush();

            // 1. 扫描起始标记 SS\x42\x47\n（处理可选的 \r）
            int matchLen = 0;
            bool found = false;
            while (true)
            {
                int b = _shellOutputStream.ReadByte();
                if (b == -1)
                {
                    _persistentShellReady = false;
                    return null;
                }
                if (b == SHELL_MARKER[matchLen])
                {
                    matchLen++;
                    if (matchLen >= SHELL_MARKER.Length) { found = true; break; }
                }
                else if (matchLen == 4 && b == 0x0D)
                {
                    // \r\n 而不是 \n，接受并继续
                    int next = _shellOutputStream.ReadByte();
                    if (next == 0x0A) { found = true; break; }
                    matchLen = 0;
                }
                else
                {
                    matchLen = 0;
                }
            }
            if (!found)
            {
                Log("[截图服务] ps: 未找到起始标记");
                return null;
            }

            // 2. 读取 12 字节头部 (width + height + format)
            byte[] header = ReadExactFromShell(12);
            if (header == null)
            {
                Log("[截图服务] ps: 无法读取头部");
                return null;
            }

            // 处理可能的 CR/LF 扩展
            header = UndoCrlfIfCorrupted(header);
            if (header.Length < 12)
            {
                Log("[截图服务] ps: CR/LF 修复后头部太短");
                return null;
            }

            int w = header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24);
            int h = header[4] | (header[5] << 8) | (header[6] << 16) | (header[7] << 24);

            if (w <= 0 || w > 4096 || h <= 0 || h > 4096)
            {
                Log(string.Format("[截图服务] ps: 无效分辨率 {0}x{1}", w, h));
                return null;
            }

            // 3. 读取 RGBA 像素数据
            int pixelDataSize = w * h * 4;
            byte[] raw = ReadExactFromShell(pixelDataSize);
            if (raw == null)
            {
                Log(string.Format("[截图服务] ps: 像素读取失败 ({0} bytes)", pixelDataSize));
                return null;
            }

            // 处理可能的 CR/LF 扩展
            raw = UndoCrlfIfCorrupted(raw);
            if (raw.Length != pixelDataSize)
            {
                // CR/LF 扩展导致数据大小变化，尝试重新读取
                Log(string.Format("[截图服务] ps: CR/LF 修复后像素大小不匹配 {0} vs {1}，尝试继续", raw.Length, pixelDataSize));
                if (raw.Length < pixelDataSize) return null;
            }

            // 4. RGBA → ARGB32 Bitmap
            Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            byte[] argb = new byte[pixelDataSize];
            for (int i = 0; i < pixelDataSize; i += 4)
            {
                argb[i]     = raw[i + 3]; // A
                argb[i + 1] = raw[i];     // R
                argb[i + 2] = raw[i + 1]; // G
                argb[i + 3] = raw[i + 2]; // B
            }

            Marshal.Copy(argb, 0, bmpData.Scan0, pixelDataSize);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        /// <summary>
        /// 从 shell 输出流精确读取 count 字节（带缓冲，高效）
        /// </summary>
        private byte[] ReadExactFromShell(int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;
            while (totalRead < count)
            {
                int r = _shellOutputStream.Read(buffer, totalRead, count - totalRead);
                if (r <= 0) return null;
                totalRead += r;
            }
            return buffer;
        }

        /// <summary>
        /// 非阻塞地丢弃 shell 输出缓冲中的数据，用于初始化时清除提示符等垃圾数据
        /// </summary>
        private void DrainShellOutput(int timeoutMs)
        {
            byte[] drain = new byte[4096];
            try
            {
                // 使用 Task + Wait 实现带超时的读取
                var task = System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try { _shellOutputStream.Read(drain, 0, drain.Length); } catch { }
                });
                task.Wait(timeoutMs);
                // 如果 task 还在运行，不管它（下次截图时 marker 扫描会跳过垃圾数据）
            }
            catch { }
        }

        /// <summary>
        /// 检测并修复 CR/LF 扩展：如果数据中包含 0D 0A 但原始数据应只有 0A
        /// </summary>
        private static byte[] UndoCrlfIfCorrupted(byte[] data)
        {
            // 快速检测：是否包含 0D 0A 序列
            bool hasCrlf = false;
            for (int i = 0; i < data.Length - 1; i++)
            {
                if (data[i] == 0x0D && data[i + 1] == 0x0A)
                {
                    hasCrlf = true;
                    break;
                }
            }
            if (!hasCrlf) return data;
            return UndoCrlfExpansion(data);
        }

        /// <summary>
        /// 清理持久化 shell 进程
        /// </summary>
        public void CleanupPersistentShell()
        {
            _persistentShellReady = false;
            try { if (_persistentShellProcess != null && !_persistentShellProcess.HasExited) _persistentShellProcess.Kill(); } catch { }
            try { _persistentShellProcess?.Dispose(); } catch { }
            _persistentShellProcess = null;
            _shellInputStream = null;
            _shellOutputStream = null;
        }

        /// <summary>
        /// 初始化 Shell 守护进程（nc + screencap，无需 Python）
        /// </summary>
        private void InitShellDaemon(string adbExe, string serial, int pcPort)
        {
            try
            {
                // 检查 nc 可用性
                string ncCheck = RunAdbShell(adbExe, serial, "shell which nc").Trim();
                if (string.IsNullOrEmpty(ncCheck) || ncCheck.Contains("not found"))
                {
                    Log("[截图服务] 模拟器内无 nc，shell 守护进程不可用");
                    return;
                }
                Log("[截图服务] 检测到 nc: " + ncCheck);

                // 推送 shell 脚本
                string scriptDir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                string script = Path.Combine(scriptDir, "screenshot_daemon.sh");
                if (!File.Exists(script))
                {
                    Log("[截图服务] 脚本不存在: " + script);
                    return;
                }

                string remotePath = "/data/local/tmp/screenshot_daemon.sh";
                RunAdbShell(adbExe, serial, "push \"" + script + "\" " + remotePath);
                RunAdbShell(adbExe, serial, "shell chmod 755 " + remotePath);

                // 杀掉可能残留的旧进程
                RunAdbShell(adbExe, serial, "shell pkill -f screenshot_daemon.sh || true");
                System.Threading.Thread.Sleep(300);

                // 在后台启动守护进程
                RunAdbShell(adbExe, serial,
                    "shell \"nohup sh " + remotePath + " " +
                    SCREENSHOT_DAEMON_DEVICE_PORT + " > /dev/null 2>&1 &\"");
                Log("[截图服务] 已启动 shell 守护进程 (设备端口 " + SCREENSHOT_DAEMON_DEVICE_PORT + ")");

                // 端口转发（可能已在 Python 守护进程中设置过，重复设置无影响）
                RunAdbShell(adbExe, serial,
                    "forward tcp:" + pcPort + " tcp:" + SCREENSHOT_DAEMON_DEVICE_PORT);

                // 等待守护进程就绪（短暂等待，不做 TCP ping 以避免触发 nc 连接导致竞争条件）
                System.Threading.Thread.Sleep(2000);
                // 验证守护进程是否在运行
                string psResult = RunAdbShell(adbExe, serial, "shell ps 2>/dev/null");
                if (psResult != null && psResult.Contains("screenshot_daemon"))
                {
                    _shellDaemonReady = true;
                    Log("[截图服务] shell 守护进程就绪 (PC端口 " + pcPort + ")");
                    return;
                }
                // 尝试通过 TCP 连接确认（作为备选）
                try
                {
                    using (var client = new TcpClient())
                    {
                        var result = client.BeginConnect("127.0.0.1", pcPort, null, null);
                        bool connected = result.AsyncWaitHandle.WaitOne(2000);
                        if (connected)
                        {
                            client.EndConnect(result);
                            _shellDaemonReady = true;
                            Log("[截图服务] shell 守护进程就绪 (PC端口 " + pcPort + ")");
                            return;
                        }
                    }
                }
                catch { }

                Log("[截图服务] shell 守护进程启动超时");
            }
            catch (Exception ex)
            {
                Log("[截图服务] shell 守护进程异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 通过 Shell 守护进程 TCP socket 截取屏幕（nc + screencap raw，~120ms）
        /// screencap 输出 raw 格式：12 字节头 (w,h,format) + RGBA 像素数据
        /// </summary>
        private Bitmap CaptureBitmapViaShellDaemon()
        {
            int pcPort = SCREENSHOT_DAEMON_PORT_BASE + realIndex;

            using (var client = new TcpClient("127.0.0.1", pcPort))
            {
                client.ReceiveTimeout = 15000;
                var stream = client.GetStream();

                // 读取 12 字节头: width(4) + height(4) + format(4)
                byte[] header = new byte[12];
                int headerRead = 0;
                while (headerRead < 12)
                {
                    int r = stream.Read(header, headerRead, 12 - headerRead);
                    if (r <= 0) break;
                    headerRead += r;
                }
                if (headerRead < 12)
                {
                    Log("[截图服务] shell: 无法读取头部 (" + headerRead + " bytes)");
                    return null;
                }

                int w = BitConverter.ToInt32(header, 0);
                int h = BitConverter.ToInt32(header, 4);
                // int format = BitConverter.ToInt32(header, 8); // 通常为 1 (RGBA_8888)

                if (w <= 0 || w > 4096 || h <= 0 || h > 4096)
                {
                    Log(string.Format("[截图服务] shell: 无效分辨率 {0}x{1}", w, h));
                    return null;
                }

                // 读取 RGBA 像素数据
                int pixelDataSize = w * h * 4;
                byte[] rgba = new byte[pixelDataSize];
                int totalRead = 0;
                while (totalRead < pixelDataSize)
                {
                    int r = stream.Read(rgba, totalRead, pixelDataSize - totalRead);
                    if (r <= 0) break;
                    totalRead += r;
                }

                if (totalRead < pixelDataSize)
                {
                    Log(string.Format("[截图服务] shell: 像素不完整 {0}/{1}", totalRead, pixelDataSize));
                    return null;
                }

                // RGBA → ARGB32 Bitmap
                Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, w, h),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                byte[] argb = new byte[pixelDataSize];
                for (int i = 0; i < pixelDataSize; i += 4)
                {
                    argb[i]     = rgba[i + 3]; // A
                    argb[i + 1] = rgba[i];     // R
                    argb[i + 2] = rgba[i + 1]; // G
                    argb[i + 3] = rgba[i + 2]; // B
                }

                Marshal.Copy(argb, 0, bmpData.Scan0, pixelDataSize);
                bmp.UnlockBits(bmpData);
                return bmp;
            }
        }

        /// <summary>
        /// 多点颜色匹配检测：优先 PrintWindow 快速截图（~20ms），回退 adb PNG。
        /// 全程内存操作，不写磁盘。逐一检查每个坐标点的像素颜色是否与期望颜色匹配。
        /// </summary>
        /// <param name="points">坐标与期望颜色列表，格式: (x, y, 0xRRGGBB)</param>
        /// <param name="tolerance">匹配阈值 (0~1)，默认 0.9，越大越严格；1.0 表示必须完全一致</param>
        /// <returns>所有点颜色均匹配返回 true，否则返回 false</returns>
        public bool IsColorMatch(List<(int, int, int)> points, double tolerance = 0.9)
        {
            if (points == null || points.Count == 0) return false;

            var sw = Stopwatch.StartNew();
            using (Bitmap bmp = CaptureBitmapFast())
            {
                sw.Stop();
                if (bmp == null) return false;

                double maxRatio = 1.0 - tolerance;
                int matched = 0;

                foreach (var (x, y, expectedColor) in points)
                {
                    if (x < 0 || x >= bmp.Width || y < 0 || y >= bmp.Height) continue;

                    int expR = (expectedColor >> 16) & 0xFF;
                    int expG = (expectedColor >> 8) & 0xFF;
                    int expB = expectedColor & 0xFF;

                    Color actual = bmp.GetPixel(x, y);

                    double rDiff = expR == 0 ? (actual.R == 0 ? 0 : 1) : (double)Math.Abs(actual.R - expR) / expR;
                    double gDiff = expG == 0 ? (actual.G == 0 ? 0 : 1) : (double)Math.Abs(actual.G - expG) / expG;
                    double bDiff = expB == 0 ? (actual.B == 0 ? 0 : 1) : (double)Math.Abs(actual.B - expB) / expB;

                    if (rDiff <= maxRatio && gDiff <= maxRatio && bDiff <= maxRatio)
                        matched++;
                    else
                    { }
                }

                bool result = matched == points.Count;
                return result;
            }
        }

        /// <summary>
        /// 等待指定毫秒
        /// </summary>
        public void Wait(int ms)
        {
            System.Threading.Thread.Sleep(ms);
        }

        /// <summary>
        /// 写入日志文件（程序目录下的 log.txt）
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="toConsole">是否同时输出到控制台</param>
        public void Log(string message, bool toConsole = false)
        {
            try
            {
                string line = DateTime.Now.ToString("[HH:mm:ss] ") + message;
                string logFile = Path.Combine(
                    Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "log.txt");
                File.AppendAllText(logFile, line + Environment.NewLine, System.Text.Encoding.UTF8);
                if (toConsole)
                {
                    if (!_consoleAllocated)
                    {
                        AttachConsole(ATTACH_PARENT_PROCESS);
                        Console.OutputEncoding = System.Text.Encoding.UTF8;
                        _consoleAllocated = true;
                    }
                    Console.WriteLine(line);
                }
            }
            catch { }
        }

        // ======================== OCR 文字识别 ========================

        private const int OCR_PORT = 18900;
        private static bool _ocrServiceStarted = false;

        /// <summary>
        /// OCR 识别结果
        /// </summary>
        public class OcrResult
        {
            public string Text { get; set; }
            public double Confidence { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }

        /// <summary>
        /// 确保 OCR 服务已启动（首次调用时自动启动）
        /// </summary>
        private static void EnsureOcrService()
        {
            if (_ocrServiceStarted) return;
            try
            {
                // 检查服务是否已在运行
                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + OCR_PORT + "/health");
                req.Timeout = 1000;
                var resp = (HttpWebResponse)req.GetResponse();
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    _ocrServiceStarted = true;
                    return;
                }
            }
            catch { }

            // 启动 OCR 服务
            string scriptDir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            string script = Path.Combine(scriptDir, "ocr_server.py");
            if (!File.Exists(script)) return;

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "python";
            psi.Arguments = "\"" + script + "\" " + OCR_PORT;
            psi.UseShellExecute = true;
            psi.CreateNoWindow = false;
            psi.WindowStyle = ProcessWindowStyle.Minimized;
            Process.Start(psi);

            // 等待服务就绪
            for (int i = 0; i < 30; i++)
            {
                System.Threading.Thread.Sleep(1000);
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + OCR_PORT + "/health");
                    req.Timeout = 1000;
                    var resp = (HttpWebResponse)req.GetResponse();
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        _ocrServiceStarted = true;
                        return;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 识别屏幕上的所有文字（通过 HTTP 调用常驻服务）
        /// </summary>
        public List<OcrResult> OCR()
        {
            return OcrCore(null, 0, 0, 0, 0, false, false);
        }

        /// <summary>
        /// 识别屏幕指定区域内的文字
        /// </summary>
        public List<OcrResult> OCR(int x1, int y1, int x2, int y2)
        {
            return OcrCore(null, x1, y1, x2, y2, true, false);
        }

        /// <summary>
        /// 识别屏幕指定区域内的文字
        /// </summary>
        /// <param name="skipDet">跳过文本检测只做识别（适合已知单行小区域，略快）</param>
        public List<OcrResult> OCR(int x1, int y1, int x2, int y2, bool skipDet)
        {
            return OcrCore(null, x1, y1, x2, y2, true, skipDet);
        }

        /// <summary>
        /// 从已有图片识别所有文字（复用截图，避免重复截图）
        /// </summary>
        public List<OcrResult> OCRFromImage(string imagePath)
        {
            return OcrCore(imagePath, 0, 0, 0, 0, false, false);
        }

        /// <summary>
        /// 从已有图片识别指定区域内的文字（复用截图，避免重复截图）
        /// </summary>
        public List<OcrResult> OCRFromImage(string imagePath, int x1, int y1, int x2, int y2)
        {
            return OcrCore(imagePath, x1, y1, x2, y2, true, false);
        }

        /// <summary>
        /// 从已有图片识别指定区域内的文字（复用截图，避免重复截图）
        /// </summary>
        public List<OcrResult> OCRFromImage(string imagePath, int x1, int y1, int x2, int y2, bool skipDet)
        {
            return OcrCore(imagePath, x1, y1, x2, y2, true, skipDet);
        }

        private List<OcrResult> OcrCore(string imagePath, int x1, int y1, int x2, int y2, bool hasRegion, bool skipDet, bool preCropped = false)
        {
            var results = new List<OcrResult>();
            try
            {
                EnsureOcrService();
                string screenshot = imagePath ?? Screenshot();
                if (!File.Exists(screenshot)) return results;

                string escapedPath = screenshot.Replace("\\", "\\\\").Replace("\"", "\\\"");
                string jsonBody;
                if (hasRegion)
                {
                    jsonBody = "{\"image\":\"" + escapedPath + "\",\"x1\":" + x1 + ",\"y1\":" + y1 + ",\"x2\":" + x2 + ",\"y2\":" + y2;
                    if (skipDet) jsonBody += ",\"skip_det\":true";
                    if (preCropped) jsonBody += ",\"pre_cropped\":true";
                    jsonBody += "}";
                }
                else
                    jsonBody = "{\"image\":\"" + escapedPath + "\"}";

                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + OCR_PORT + "/ocr");
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.Timeout = 30000;

                byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                req.ContentLength = bodyBytes.Length;
                using (var stream = req.GetRequestStream())
                    stream.Write(bodyBytes, 0, bodyBytes.Length);

                var resp = (HttpWebResponse)req.GetResponse();
                string output;
                using (var reader = new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8))
                    output = reader.ReadToEnd();

                if (!string.IsNullOrEmpty(output))
                {
                    var serializer = new JavaScriptSerializer();
                    var json = serializer.Deserialize<Dictionary<string, object>>(output);
                    if (json != null && json.ContainsKey("texts"))
                    {
                        var textsList = json["texts"] as System.Collections.IList;
                        if (textsList != null)
                        {
                            foreach (var item in textsList)
                            {
                                var dict = item as Dictionary<string, object>;
                                if (dict != null)
                                {
                                    var center = dict["center"] as Dictionary<string, object>;
                                    results.Add(new OcrResult
                                    {
                                        Text = dict["text"] as string ?? "",
                                        Confidence = Convert.ToDouble(dict["confidence"]),
                                        X = Convert.ToInt32(center["x"]),
                                        Y = Convert.ToInt32(center["y"])
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (WebException wex)
            {
                // 读取 HTTP 错误响应的具体内容（Python 端返回的 error 字段）
                string errorDetail = "";
                if (wex.Response != null)
                {
                    try
                    {
                        using (var reader = new StreamReader(wex.Response.GetResponseStream(), System.Text.Encoding.UTF8))
                            errorDetail = reader.ReadToEnd();
                    }
                    catch { }
                }
                Log("OCR 异常: " + wex.Message + (string.IsNullOrEmpty(errorDetail) ? "" : " | 详情: " + errorDetail));
            }
            catch (Exception ex)
            {
                Log("OCR 异常: " + ex.Message);
            }
            return results;
        }

        /// <summary>
        /// 查找屏幕上的指定文字，返回位置（未找到返回null）
        /// </summary>
        public OcrResult OCRFindText(string keyword)
        {
            var results = OCR();
            foreach (var r in results)
            {
                if (r.Text.Contains(keyword))
                    return r;
            }
            return null;
        }

        /// <summary>
        /// 在指定区域内查找文字并返回位置
        /// </summary>
        public OcrResult OCRFindText(string keyword, int x1, int y1, int x2, int y2)
        {
            return OCRFindText(keyword, x1, y1, x2, y2, false);
        }

        /// <summary>
        /// 在指定区域内查找文字并返回位置
        /// </summary>
        /// <param name="skipDet">跳过文本检测只做识别（适合已知单行小区域）</param>
        public OcrResult OCRFindText(string keyword, int x1, int y1, int x2, int y2, bool skipDet)
        {
            var results = OCR(x1, y1, x2, y2, skipDet);
            foreach (var r in results)
            {
                if (r.Text.Contains(keyword))
                    return r;
            }
            return null;
        }

        /// <summary>
        /// 点击屏幕上的指定文字（未找到返回false）
        /// </summary>
        public bool OCRTapText(string keyword)
        {
            var result = OCRFindText(keyword);
            if (result != null)
            {
                Tap(result.X, result.Y);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 识别全图文字，返回文字列表（不含坐标）
        /// </summary>
        public List<string> OCRText()
        {
            var texts = new List<string>();
            foreach (var r in OCR())
                texts.Add(r.Text);
            return texts;
        }

        /// <summary>
        /// 识别指定区域文字，返回文字列表（不含坐标）。
        /// 快速路径：C# 端预裁剪小图 + 跳过文本检测，小区域耗时大幅降低。
        /// </summary>
        public List<string> OCRText(int x1, int y1, int x2, int y2)
        {
            return OCRTextFast(x1, y1, x2, y2);
        }

        /// <summary>
        /// 识别指定区域文字，返回文字列表（不含坐标）
        /// </summary>
        /// <param name="skipDet">跳过文本检测只做识别（适合已知单行小区域）</param>
        public List<string> OCRText(int x1, int y1, int x2, int y2, bool skipDet)
        {
            if (skipDet)
                return OCRTextFast(x1, y1, x2, y2);
            var texts = new List<string>();
            foreach (var r in OCR(x1, y1, x2, y2))
                texts.Add(r.Text);
            return texts;
        }

        // ======================== 后台截图流水线（OCRText 加速） ========================

        /// <summary>
        /// OCRText 直接复用最新帧的最大帧龄（毫秒）：帧比这新则直接使用（OCRText 总耗时约 60ms）；
        /// 帧太旧时会短暂等待新帧以保证画面新鲜。调大更不易等待，调小画面更新鲜。
        /// </summary>
        public static int OCRFrameMaxAgeMs = 150;

        /// <summary>
        /// OCRText 等新帧的最长时间（毫秒），超时后回退同步 adb 截图
        /// </summary>
        public static int OCRFrameMaxWaitMs = 400;

        private readonly object _pipeLock = new object();
        private byte[] _pipePng;
        private long _pipeTicks;
        private static volatile bool _ocrWarmedUp;

        /// <summary>
        /// 首次调用时异步预热 OCR 服务（后续调用不再触发）
        /// </summary>
        private void EnsureFrameCache()
        {
            if (!_ocrWarmedUp) WarmupOcrAsync();
        }

        /// <summary>
        /// 通过 PrintWindow 截取模拟器窗口客户区，返回 PNG 字节（失败返回 null）。
        /// 自动检测并裁剪掉 LDPlayer 工具栏/控制栏，只保留 Android 画面部分后缩放到设备分辨率。
        /// </summary>
        private byte[] GetWindowPngBytes()
        {
            IntPtr hwnd = FindEmulatorWindow();
            if (hwnd == IntPtr.Zero || IsIconic(hwnd)) return null;

            RECT rc;
            if (!GetClientRect(hwnd, out rc)) return null;
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            if (w <= 0 || h <= 0) return null;

            Bitmap bmp = null;
            try
            {
                bmp = new Bitmap(w, h);
                if (!PrintWindowToBitmap(hwnd, bmp, 1) || IsMostlyBlack(bmp))
                {
                    if (!PrintWindowToBitmap(hwnd, bmp, 3) || IsMostlyBlack(bmp))
                        return null;
                }

                // 裁剪 Android 画面区域（去除 LDPlayer 工具栏/控制栏）
                int bw = bmp.Width, bh = bmp.Height;
                bmp = CropAndroidDisplay(bmp, ref bw, ref bh);

                Size dev = GetDeviceSize();
                if (dev.Width > 0 && dev.Height > 0)
                {
                    double bmpRatio = (double)bw / bh;
                    double devRatio = (double)dev.Width / dev.Height;
                    double ratioDiff = Math.Abs(bmpRatio - devRatio) / devRatio;
                    if (ratioDiff > 0.05) return null;
                    if (bw != dev.Width || bh != dev.Height)
                    {
                        var scaled = new Bitmap(bmp, dev.Width, dev.Height);
                        bmp.Dispose();
                        bmp = scaled;
                    }
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            finally
            {
                if (bmp != null) bmp.Dispose();
            }
        }

        /// <summary>
        /// 检测 LDPlayer 窗口客户区中的 Android 画面区域（去除工具栏/控制栏），返回裁剪后的 Bitmap。
        /// 若工具栏很小（≤ 5px）则不裁剪，直接返回原位图。
        /// </summary>
        private Bitmap CropAndroidDisplay(Bitmap bmp, ref int bw, ref int bh)
        {
            int toolbarH = 0;
            // 从顶部向下扫描：连续低方差行 = LDPlayer 工具栏
            for (int y = 0; y < bh / 4; y++)
            {
                int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
                for (int x = 0; x < bw; x += 4)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R < minR) minR = c.R; if (c.R > maxR) maxR = c.R;
                    if (c.G < minG) minG = c.G; if (c.G > maxG) maxG = c.G;
                    if (c.B < minB) minB = c.B; if (c.B > maxB) maxB = c.B;
                }
                if (maxR - minR > 20 || maxG - minG > 20 || maxB - minB > 20)
                {
                    toolbarH = y;
                    break;
                }
            }
            // 从底部向上扫描：连续低方差行 = LDPlayer 底部控制栏
            int bottomBarY = bh;
            for (int y = bh - 1; y > bh * 3 / 4; y--)
            {
                int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
                for (int x = 0; x < bw; x += 4)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R < minR) minR = c.R; if (c.R > maxR) maxR = c.R;
                    if (c.G < minG) minG = c.G; if (c.G > maxG) maxG = c.G;
                    if (c.B < minB) minB = c.B; if (c.B > maxB) maxB = c.B;
                }
                if (maxR - minR > 20 || maxG - minG > 20 || maxB - minB > 20)
                {
                    bottomBarY = y + 1;
                    break;
                }
            }
            int androidH = bottomBarY - toolbarH;
            // 工具栏/控制栏很小时不裁剪
            if (toolbarH <= 5 && bh - bottomBarY <= 5) return bmp;
            if (androidH < bh / 2) return bmp; // 安全检查：裁剪后太小说明检测异常
            var cropped = bmp.Clone(
                new Rectangle(0, toolbarH, bw, androidH),
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            bmp.Dispose();
            bw = cropped.Width;
            bh = cropped.Height;
            return cropped;
        }

        /// <summary>
        /// 获取一帧画面（按需截图 + 帧缓存）：
        /// 帧龄 ≤ OCRFrameMaxAgeMs 则直接复用缓存；否则通过 adb exec-out 截一帧（~280ms，不闪屏）。
        /// 不调用就不截图，无后台线程空转。
        /// </summary>
        private byte[] GetFrame()
        {
            lock (_pipeLock)
            {
                if (_pipePng != null)
                {
                    long ageMs = (DateTime.UtcNow.Ticks - _pipeTicks) / TimeSpan.TicksPerMillisecond;
                    if (ageMs <= OCRFrameMaxAgeMs) return _pipePng;
                }
            }
            // 帧过期或无帧，通过 adb 截图（不用 PrintWindow，避免闪屏）
            byte[] png = GetAdbPngBytes();
            if (png != null && png.Length > 100)
                lock (_pipeLock) { _pipePng = png; _pipeTicks = DateTime.UtcNow.Ticks; }
            return png;
        }

        /// <summary>
        /// 异步预热 OCR 服务首次推理（生成 8x32 白底黑字小图 POST 一次，结果丢弃）
        /// </summary>
        private static void WarmupOcrAsync()
        {
            if (_ocrWarmedUp) return;
            _ocrWarmedUp = true; // 只发一次，失败也不重试（正式调用会再走一遍）
            var t = new System.Threading.Thread(delegate()
            {
                try
                {
                    string warmPath = Path.Combine(
                        Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),
                        "ocr_warm.png");
                    using (var bmp = new Bitmap(8, 32, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.White);
                            g.DrawString("一", new Font("宋体", 8), Brushes.Black, 0, 4);
                        }
                        bmp.Save(warmPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    string jsonBody = "{\"image\":\"" + warmPath.Replace("\\", "\\\\") +
                        "\",\"x1\":0,\"y1\":0,\"x2\":8,\"y2\":32,\"skip_det\":true,\"pre_cropped\":true}";
                    var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + OCR_PORT + "/ocr");
                    req.Method = "POST";
                    req.ContentType = "application/json; charset=utf-8";
                    req.Timeout = 30000;
                    byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    req.ContentLength = bodyBytes.Length;
                    using (var stream = req.GetRequestStream())
                        stream.Write(bodyBytes, 0, bodyBytes.Length);
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                        reader.ReadToEnd();
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }



        /// <summary>
        /// 从流水线帧（内存 PNG）裁剪小区域到 cropPath，成功返回 true
        /// </summary>
        private bool CropFromFrame(byte[] png, int x1, int y1, int x2, int y2, string cropPath)
        {
            try
            {
                using (var ms = new MemoryStream(png))
                using (var bmp = new Bitmap(ms))
                {
                    int cx1 = Math.Max(0, x1);
                    int cy1 = Math.Max(0, y1);
                    int cx2 = Math.Min(bmp.Width, x2);
                    int cy2 = Math.Min(bmp.Height, y2);
                    if (cx2 <= cx1 || cy2 <= cy1) return false;
                    using (var crop = bmp.Clone(new Rectangle(cx1, cy1, cx2 - cx1, cy2 - cy1), System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                        crop.Save(cropPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                return true;
            }
            catch (Exception ex) { Log("流水线帧裁剪异常: " + ex.Message); return false; }
        }

        /// <summary>
        /// OCRText 快速路径：按需 adb 截图（~280ms，不闪屏）+ 帧缓存复用 +
        /// C# 端裁剪小区域 + skip_det 识别；连续调用时帧未过期直接复用，不调用则不截图。
        /// </summary>
        private List<string> OCRTextFast(int x1, int y1, int x2, int y2)
        {
            var texts = new List<string>();
            try
            {
                EnsureOcrService();
                string cropPath = Path.Combine(
                    Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),
                    "ocr_crop_" + realIndex + ".png");

                EnsureFrameCache();
                byte[] frame = GetFrame();
                if (frame == null) return texts;
                bool cropped = CropFromFrame(frame, x1, y1, x2, y2, cropPath);
                if (!cropped) return texts;

                // 传原区域坐标用于返回中心点，skip_det+preCropped 表示图片已是裁剪好的区域
                foreach (var r in OcrCore(cropPath, x1, y1, x2, y2, true, true, true))
                    texts.Add(r.Text);
            }
            catch (Exception ex)
            {
                Log("OCRText 异常: " + ex.Message);
            }
            return texts;
        }

        /// <summary>
        /// 启动模拟器中指定包名的APP。
        /// 优先通过 resolve-activity 查找启动 Activity 再用 am start 启动；
        /// 查找失败时回退 monkey 方式。
        /// </summary>
        /// <param name="packageName">应用包名，如 com.gof.china</param>
        public void LaunchApp(string packageName)
        {
            try
            {
                Log("[LaunchApp] 启动应用: " + packageName);

                // 方式一：查询启动 Activity 后用 am start
                string resolveOutput = RunAdb("shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER " + packageName);
                // 输出示例: "priority=0 ...\ncom.gof.china/.MainActivity"
                string activityName = "";
                foreach (string line in resolveOutput.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Contains("/")) // 格式: com.gof.china/.MainActivity
                    {
                        activityName = trimmed;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(activityName))
                {
                    string amOutput = RunAdb("shell am start -n " + activityName);
                    Log("[LaunchApp] am start 结果: " + amOutput.Trim());
                    if (amOutput.Contains("Error"))
                        Log("[LaunchApp] am start 失败，尝试 monkey 回退");
                    else
                    {
                        Log("[LaunchApp] 启动成功: " + activityName);
                        return;
                    }
                }
                else
                {
                    Log("[LaunchApp] 未解析到启动Activity，尝试 monkey 回退");
                }

                // 方式二：monkey 回退
                string monkeyOutput = RunAdb("shell monkey -p " + packageName + " -c android.intent.category.LAUNCHER 1");
                if (monkeyOutput.Contains("Events injected"))
                    Log("[LaunchApp] monkey 启动成功: " + packageName);
                else
                    Log("[LaunchApp] monkey 启动可能失败: " + monkeyOutput.Trim());
            }
            catch (Exception ex)
            {
                Log("[LaunchApp] 启动异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 启动模拟器中应用ID为 com.gof.china 的APP
        /// </summary>
        public void LaunchGofChina()
        {
            LaunchApp("com.gof.china");
        }

        /// <summary>
        /// 在模拟器底部显示气泡提示（自动消失）
        /// </summary>
        public void Notify(string message)
        {
            try
            {
                string b64msg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message));
                RunAdb("shell am start -n com.emu.toast/.ToastActivity --es msg " + b64msg);
            }
            catch { }
        }

        /// <summary>
        /// adb.exe 路径（优先 consolePath 同目录）
        /// </summary>
        private string ResolveAdbExe()
        {
            string dir = Path.GetDirectoryName(consolePath);
            string adbExe = Path.Combine(dir, "adb.exe");
            if (!File.Exists(adbExe)) adbExe = consolePath;
            return adbExe;
        }

        /// <summary>
        /// 设备序列号: emulator-5554, emulator-5556, ...
        /// </summary>
        private string DeviceSerial()
        {
            return "emulator-" + (5554 + realIndex * 2);
        }

        /// <summary>
        /// 执行ADB命令并返回输出（直接使用adb.exe，比ldconsole adb更可靠）
        /// </summary>
        public string RunAdb(string command)
        {
            try
            {
                string adbExe = ResolveAdbExe();
                string serial = DeviceSerial();

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = adbExe;
                psi.Arguments = "-s " + serial + " " + command;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(5000);
                    return output;
                }
            }
            catch { }
            return "";
        }
    }
}
