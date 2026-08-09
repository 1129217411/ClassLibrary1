using System;
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
            
            
            
            
            // emu.InitScreenshotDaemon();
            // emu.Log("自动化开始执行", true);
            
            // 启动游戏并进入游戏首页
            // LaunchGameAndEnterHome(emu);

            // // 执行采矿差异分析：读取设置 -> 截图识别 -> 比较差异
            // Dictionary<string, int> diffResult = AnalyzeMiningDiff(emu);
            // if (diffResult.Count == 0)
            // {
            //     emu.Log("采矿差异: 全部满足", true);
            // }
            // else
            // {
            //     foreach (var kv in diffResult)
            //         emu.Log("采矿差异: " + kv.Key + "缺少" + kv.Value, true);
            //     
            //     
            //     // 获取采矿等级（1~9）
            //     int miningLevel = MainForm.GetMiningLevel();
            //     emu.Log("采矿等级: " + miningLevel, true);
            //     
            // }

            // 前往城镇
            // CheckAndNavigate(emu, "城镇");
            

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
            // EmulatorHelper.OcrResult btn = emu.OCRFindText(name);
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






            // var 欢迎回来 = new List<(int, int, int)> { (1, 1, 0x2a4263),(503, 1184, 0x5275ad) };
            // var isColorMatch = emu.IsColorMatch(欢迎回来);
            // if (isColorMatch)
            // {
            //     emu.Log("欢迎回来-确认", true);
            // } else
            // {
            //     emu.Log("未找到欢迎回来按钮", true);
            // } 



        }
        /// <summary>
        /// 启动游戏并进入游戏首页
        /// 等待游戏启动后，检测"欢迎回来"界面并自动点击确认
        /// </summary>
        private void LaunchGameAndEnterHome(EmulatorHelper emu)
        {
            emu.Wait(2000);
            emu.Log("启动游戏", true);
            emu.LaunchGofChina();
            
            emu.Wait(10000);
            // 判断是否进入游戏首页
            for (int i = 0; i < 10; i++)
            {
                // 判断是否在欢迎回来界面
                var 欢迎回来 = new List<(int, int, int)> { (209, 246, 0x638ac6), (370, 250, 0xf6f6f7), (374, 250, 0x5074ac), (102, 409, 0xc35f5a) };
                var isColorMatch = emu.IsColorMatch(欢迎回来);
                if (isColorMatch)
                {
                    emu.Log("欢迎回来-确认", true);
                    emu.Tap(359, 1025);
                }
                
                
                
                
                emu.Wait(1000);
            }

        }

        /// <summary>
        /// 采矿差异分析：读取设置 -> 截图识别 -> 比较差异
        /// 返回缺少的矿产及数量，例如: {"铁": 2} 表示铁缺少2个
        /// 全部满足时返回空字典
        /// </summary>
        private Dictionary<string, int> AnalyzeMiningDiff(EmulatorHelper emu)
        {
            // ===== 第一步：从功能设置中读取采矿配置 =====

            // 获取各队的采矿资源设置
            // 返回长度为6的数组，未启用的队伍为空字符串
            // 例如: ["肉", "", "煤", "", "铁", "木"] 表示启用1/3/5/6队
            string[] resources = MainForm.GetMiningResources();
            for (int i = 0; i < resources.Length; i++)
            {
                if (string.IsNullOrEmpty(resources[i])) continue;
                // emu.Log("第 " + (i + 1) + " 队采集: " + resources[i], true);
            }

            // ===== 第二步：定义屏幕上6个矿产位置的识别区域 =====
            // 每个区域格式: {x1, y1, x2, y2}
            // 对应屏幕上第1~6队的矿产显示位置
            int[][] ranges = new int[][] {
                new int[] {114, 345, 335, 368},  // 第1队位置
                new int[] {114, 417, 361, 444},  // 第2队位置
                new int[] {114, 492, 368, 519},  // 第3队位置
                new int[] {114, 562, 361, 590},  // 第4队位置
                new int[] {114, 636, 371, 659},  // 第5队位置
                new int[] {114, 709, 353, 734}   // 第6队位置
            };

            // ===== 第三步：截图并识别各区域的矿产 =====
            // 只截一次图，然后复用这张图识别6个区域，避免重复截图
            string screenshot = emu.Screenshot();
            string[] screenResources = new string[ranges.Length];
            for (int i = 0; i < ranges.Length; i++)
            {
                // OCR 识别指定区域的文字
                List<EmulatorHelper.OcrResult> results = emu.OCRFromImage(screenshot, ranges[i][0], ranges[i][1], ranges[i][2], ranges[i][3]);
                string rawText = results.Count > 0 ? results[0].Text : "";
                // 将 OCR 文字映射为标准资源名（畜牧->肉, 木材->木, 煤矿->煤, 炼铁->铁）
                string normalized = NormalizeResource(rawText);
                screenResources[i] = normalized;
                // emu.Log("范围" + (i + 1) + ": " + (string.IsNullOrEmpty(rawText) ? "(空)" : rawText + " -> " + (string.IsNullOrEmpty(normalized) ? "(未识别)" : normalized)), true);
            }

            // ===== 第四步：统计设置需求的各矿产数量 =====
            // 例如: 设置需要 ["肉", "", "煤", "", "铁", "木"]
            // 统计结果: {肉:1, 煤:1, 铁:1, 木:1}
            var needCount = new Dictionary<string, int>();
            foreach (string r in resources)
            {
                if (string.IsNullOrEmpty(r)) continue; // 跳过未启用的队
                if (!needCount.ContainsKey(r)) needCount[r] = 0;
                needCount[r]++;
            }

            // ===== 第五步：统计屏幕上已识别的各矿产数量 =====
            // 例如: 屏幕识别到 ["肉", "木", "煤", "", "", "肉"]
            // 统计结果: {肉:2, 木:1, 煤:1}
            var foundCount = new Dictionary<string, int>();
            foreach (string r in screenResources)
            {
                if (string.IsNullOrEmpty(r)) continue; // 跳过未识别的
                if (!foundCount.ContainsKey(r)) foundCount[r] = 0;
                foundCount[r]++;
            }

            // ===== 第六步：比较差异，生成结果 =====
            // 遍历需求，计算每种矿产的缺口
            // 例如: 需要铁3个，已有1个 -> {"铁": 2}
            var diff = new Dictionary<string, int>();
            foreach (var kv in needCount)
            {
                string res = kv.Key;
                int need = kv.Value;
                int have = foundCount.ContainsKey(res) ? foundCount[res] : 0;
                int shortage = need - have;
                if (shortage > 0)
                    diff[res] = shortage;
            }

            // 返回缺少的矿产及数量，全部满足时返回空字典
            return diff;
        }

        /// <summary>
        /// 将 OCR 识别的文字映射为标准资源名称
        /// 映射规则: 畜牧->肉, 木材->木, 煤矿->煤, 炼铁->铁
        /// 无法识别时返回空字符串
        /// </summary>
        private static string NormalizeResource(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains("畜牧")) return "肉";
            if (text.Contains("木材")) return "木";
            if (text.Contains("煤矿")) return "煤";
            if (text.Contains("炼铁")) return "铁";
            return "";
        }

        /// <summary>
        /// 检查屏幕指定区域(617,1240,680,1271)的文字，根据传入的目标位置进行导航
        /// 最多循环5次，当识别到目标并点击后跳出循环
        /// - 如果识别到"城镇"且传入目标是"城镇"，点击(648,1222)进入城镇并跳出
        /// - 如果识别到"野外"且传入目标是"野外"，点击(648,1222)进入野外并跳出
        /// - 如果识别到的既不是"城镇"也不是"野外"，按返回键继续循环
        /// </summary>
        /// <param name="emu">模拟器帮助对象</param>
        /// <param name="target">目标位置，"城镇"或"野外"</param>
        private void CheckAndNavigate(EmulatorHelper emu, string target)
        {
            // 最多循环5次
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                // emu.Log("第 " + attempt + " 次尝试识别位置...", true);

                // 识别指定区域的文字
                var ocrText = emu.OCRText(617, 1240, 680, 1271);
                string currentText = string.Join(" ", ocrText);
                // emu.Log("识别结果: " + (string.IsNullOrEmpty(currentText) ? "(空)" : currentText), true);

                // 判断当前是城镇还是野外
                if (currentText == "城镇")
                {
                    if (target == "城镇")
                    {
                        // 当前在城镇，目标也是城镇，点击进入并跳出循环
                        emu.Log("当前在城镇，目标也是城镇，点击进入", true);
                        TapWithOffset(emu, 648, 1222);
                        break;
                    }
                }
                else if (currentText == "野外")
                {
                    if (target == "野外")
                    {
                        // 当前在野外，目标也是野外，点击进入并跳出循环
                        emu.Log("当前在野外，目标也是野外，点击进入", true);
                        TapWithOffset(emu, 648, 1222);
                        break;
                    }
                }
                else
                {
                    // 识别到的既不是城镇也不是野外，按返回键继续循环
                    emu.Log("未识别到城镇或野外，按返回键重试", true);
                    emu.RunAdb("shell input keyevent 4"); // 4 = KEYCODE_BACK
                    emu.Wait(500); // 等待返回动画
                }
            }
        }

        /// <summary>
        /// 点击坐标并添加随机偏移（默认±10像素），点击后随机等待300~600ms
        /// </summary>
        /// <param name="emu">模拟器帮助对象</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="offset">偏移量，默认10</param>
        private void TapWithOffset(EmulatorHelper emu, int x, int y, int offset = 10)
        {
            var rng = new Random();
            // x和y各随机加减offset
            int offsetX = x + rng.Next(-offset, offset + 1);
            int offsetY = y + rng.Next(-offset, offset + 1);
            emu.Tap(offsetX, offsetY);
            // 点击后随机等待300~600ms
            int waitMs = rng.Next(300, 601);
            emu.Wait(waitMs);
        }
    }
    // ================================================
}
