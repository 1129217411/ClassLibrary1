using System;
using System.Reflection;
using ClassLibrary1;

class TestCrop
{
    static void Main()
    {
        var emu = new EmulatorHelper(@"O:\app\雷电\leidian\LDPlayer14\adb.exe", 0);
        var mi = typeof(EmulatorHelper).GetMethod("CaptureRegionByWindow", BindingFlags.NonPublic | BindingFlags.Instance);
        string path = @"Z:\interest\ClassLibrary1\_win_crop.png";
        bool ok = (bool)mi.Invoke(emu, new object[] { 64, 268, 148, 292, path });
        Console.WriteLine("CaptureRegionByWindow -> " + ok);
    }
}
