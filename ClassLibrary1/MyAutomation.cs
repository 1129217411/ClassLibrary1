using System.Collections.Generic;

namespace ClassLibrary1
{
    // ========== 在这里编写你的自动化逻辑 ==========
    // 每个启动的模拟器都会自动调用 Run 方法
    // 可使用 emu 对象调用所有工具方法

    public class MyAutomation
    {
        public void Run(EmulatorHelper emu)
        {
            emu.InitScreenshotDaemon();
            emu.Log("自动化开始执行");
            // 示例：截图并检查像素颜色，匹配则点击
            // string img = emu.Screenshot();
            // Color c = EmulatorHelper.GetPixel(img, 360, 640);
            // if (EmulatorHelper.IsColorMatch(c, Color.FromArgb(255, 0, 0), 20))
            //     emu.Tap(360, 640);

            // 示例：滑动操作
            // emu.Swipe(360, 800, 360, 200, 300);

            // 示例：等待后执行
            // emu.Wait(2000);
            // emu.Tap(360, 500);

            // 示例：OCR 文字识别
            // List<EmulatorHelper.OcrResult> texts = emu.OCR();
            // foreach (var t in texts)
            //     Console.WriteLine($"{t.Text} ({t.X},{t.Y}) 置信度:{t.Confidence}");

            // 示例：查找屏幕上的文字
            // EmulatorHelper.OcrResult btn = emu.OCRFindText("确定");
            // if (btn != null) emu.Tap(btn.X, btn.Y);

            // 示例：直接点击屏幕上的文字
            // emu.OCRTapText("开始游戏");
            // emu.OCRTapText("确认");

            // 示例：识别指定区域内的文字（更快更精准）
            // var regionTexts = emu.OCR(70, 274, 148, 299);
            // foreach (var t in regionTexts)
            //     Console.WriteLine($"{t.Text} ({t.X},{t.Y})");

            // 示例：在指定区域内查找文字
            // var btn = emu.OCRFindText("确定", 0, 0, 720, 600);
            // if (btn != null) emu.Tap(btn.X, btn.Y);

            // var name = "上号器";
            // Stopwatch sw = Stopwatch.StartNew();
            // EmulatorHelper.OcrResult btn = emu.OCRFindText(name);
            // sw.Stop();
            // emu.Log($"OCRFindText 耗时: {sw.ElapsedMilliseconds} ms", true);
            // if (btn != null) {
            //     emu.Log("找到"+name+"按钮:" + btn.X + "," + btn.Y, true);
            //     emu.Tap(btn.X, btn.Y);
            // }
            // else
            // {
            //     emu.Log("未找到"+name+"按钮", true);
            // }

            // Stopwatch sw = Stopwatch.StartNew();
            // var ocrText = emu.OCRText(62,268, 149,292);
            // sw.Stop();
            // emu.Log(string.Format("OCRText 耗时: {0} ms", sw.ElapsedMilliseconds), true);
            //
            // ocrText.ForEach(t => emu.Log(t, true));
            
            // emu.Log("启动游戏", true);
            // emu.LaunchGofChina();
            
            
            // emu.Wait(10000);
            // var 欢迎回来 = new List<(int, int, int)> { (209, 246, 0x638ac6), (370, 250, 0xf6f6f7), (374, 250, 0x5074ac), (102, 409, 0xc35f5a) };
            // var isColorMatch = emu.IsColorMatch(欢迎回来);
            // if (isColorMatch)
            // {
            //     emu.Log("欢迎回来-确认", true);
            //     emu.Tap(359,1025);
            // }
            
            var 欢迎回来 = new List<(int, int, int)> { (1, 1, 0x2a4263),(503, 1184, 0x5275ad) };
            var isColorMatch = emu.IsColorMatch(欢迎回来);
            if (isColorMatch)
            {
                emu.Log("欢迎回来-确认", true);
            } else
            {
                emu.Log("未找到欢迎回来按钮", true);
            } 
            
            
        }
    }
    // ================================================
}
