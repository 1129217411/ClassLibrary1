using System;
using System.Diagnostics;
using ClassLibrary1;

class TestOcrText
{
    static void Main()
    {
        var emu = new EmulatorHelper(@"O:\app\雷电\leidian\LDPlayer14\adb.exe", 0);
        // 只预热 OCR 服务，不启动截图流水线，验证真实冷启动首次调用耗时
        var mi = typeof(EmulatorHelper).GetMethod("EnsureOcrService",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        mi.Invoke(null, null);
        System.Threading.Thread.Sleep(200); // 等预热日志稳定
        for (int i = 0; i < 6; i++)
        {
            var sw = Stopwatch.StartNew();
            var texts = emu.OCRText(64, 268, 148, 292);
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds + " ms -> [" + string.Join("|", texts.ToArray()) + "]");
        }
    }
}
