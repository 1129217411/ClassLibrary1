// 诊断脚本 - 模拟程序的模拟器检测逻辑
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

class Diagnostic
{
    static void Main()
    {
        Console.WriteLine("=== 模拟器检测诊断 ===\n");
        
        var allPaths = new List<string>();
        
        // 1. 从 pathconfig.ini 解析
        string configPath = @"O:\app\雷电\ldmutiplayer\pathconfig.ini";
        Console.WriteLine("1. pathconfig.ini: " + (File.Exists(configPath) ? "存在" : "不存在"));
        if (File.Exists(configPath))
        {
            foreach (string line in File.ReadAllLines(configPath))
            {
                if (line.StartsWith("player"))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        string dir = line.Substring(eq + 1).Trim();
                        string lc = Path.Combine(dir, "ldconsole.exe");
                        string dc = Path.Combine(dir, "dnconsole.exe");
                        Console.WriteLine("  " + line);
                        Console.WriteLine("    ldconsole: " + (File.Exists(lc) ? "存在" : "不存在"));
                        Console.WriteLine("    dnconsole: " + (File.Exists(dc) ? "存在" : "不存在"));
                        if (File.Exists(lc)) allPaths.Add(lc);
                        else if (File.Exists(dc)) allPaths.Add(dc);
                    }
                }
            }
        }
        
        // 2. 回退路径
        string fb1 = @"O:\app\雷电\新建文件夹\leidian\LDPlayer9\ldconsole.exe";
        string fb2 = @"O:\app\雷电\leidian\LDPlayer14\ldconsole.exe";
        Console.WriteLine("\n2. 回退路径:");
        Console.WriteLine("  LDPlayer9: " + (File.Exists(fb1) ? "存在" : "不存在"));
        Console.WriteLine("  LDPlayer14: " + (File.Exists(fb2) ? "存在" : "不存在"));
        if (File.Exists(fb1) && !allPaths.Contains(fb1)) allPaths.Add(fb1);
        if (File.Exists(fb2) && !allPaths.Contains(fb2)) allPaths.Add(fb2);
        
        Console.WriteLine("\n共找到 " + allPaths.Count + " 个控制台:");
        foreach (var p in allPaths) Console.WriteLine("  " + p);
        
        // 3. 对每个控制台调用 list2
        Console.WriteLine("\n3. list2 结果:");
        foreach (var cp in allPaths)
        {
            Console.WriteLine("  调用: " + cp);
            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = cp;
                psi.Arguments = "list2";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = Encoding.GetEncoding("gb2312");
                psi.CreateNoWindow = true;
                
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    Console.WriteLine("  输出: [" + output + "]");
                    
                    foreach (string line in output.Split(new[] { '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            int index;
                            if (int.TryParse(parts[0], out index))
                                Console.WriteLine("    -> 实例: index=" + index + " name=" + parts[1]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  错误: " + ex.Message);
            }
        }
    }
}
