# -*- coding: utf-8 -*-
import subprocess, time, os

ADB = r"O:\app\雷电\leidian\LDPlayer14\adb.exe"
SERIAL = "emulator-5554"
OUT = r"Z:\interest\ClassLibrary1\_bench_pull.png"

def best(label, cmd, n=3, capture=False):
    ts = []
    size = 0
    for _ in range(n):
        t0 = time.perf_counter()
        r = subprocess.run(cmd, stdout=subprocess.PIPE if capture else subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL)
        ts.append((time.perf_counter() - t0) * 1000)
        if capture and r.stdout:
            size = len(r.stdout)
    print(f"{label}: best {min(ts):.0f} ms / last {ts[-1]:.0f} ms" + (f" / {size} bytes" if size else ""))

# 先看设备列表
r = subprocess.run([ADB, "devices"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
print("devices:", r.stdout.decode(errors="ignore").strip().replace("\r", "").split("\n")[1:])

best("1) screencap -p (设备端PNG编码+写盘)", [ADB, "-s", SERIAL, "shell", "screencap", "-p", "/sdcard/ld_auto.png"])
best("2) adb pull (PNG传输)", [ADB, "-s", SERIAL, "pull", "/sdcard/ld_auto.png", OUT])
best("3) exec-out screencap -p (PNG直出,免设备端写盘)", [ADB, "-s", SERIAL, "exec-out", "screencap", "-p"], capture=True)
best("4) exec-out screencap (原始RGBA直出)", [ADB, "-s", SERIAL, "exec-out", "screencap"], capture=True)
best("5) exec-out screencap 无后缀(JPEG直出)", [ADB, "-s", SERIAL, "exec-out", "screencap", "/x.jpg"], capture=True)
best("6) shell screencap -p /sdcard/x.png 写盘", [ADB, "-s", SERIAL, "shell", "screencap", "-p", "/sdcard/x.png"])
