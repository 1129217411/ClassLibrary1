# -*- coding: utf-8 -*-
"""实测运行中的 OCR 服务各段耗时"""
import urllib.request, json, time, os, subprocess

URL = "http://127.0.0.1:18900/ocr"
IMG = r"Z:\interest\ClassLibrary1\ClassLibrary1\bin\Debug\screenshot_0.png"

def post(payload, n=3):
    times = []
    for _ in range(n):
        t0 = time.perf_counter()
        r = urllib.request.urlopen(
            urllib.request.Request(URL, data=json.dumps(payload).encode()), timeout=30)
        r.read()
        times.append((time.perf_counter() - t0) * 1000)
    return times

print("== HTTP + 服务端总耗时(小区域 78x25) ==")
for t in post({"image": IMG, "x1": 70, "y1": 274, "x2": 148, "y2": 299}):
    print(f"  {t:.0f} ms")

print("== HTTP + 服务端总耗时(全图 720x1280) ==")
for t in post({"image": IMG}, n=2):
    print(f"  {t:.0f} ms")

# 找 adb.exe
candidates = [
    r"C:\leidian\LDPlayer9\adb.exe",
    r"D:\leidian\LDPlayer9\adb.exe",
    r"Z:\leidian\LDPlayer9\adb.exe",
    r"C:\changwan\LDPlayer\adb.exe",
]
adb = None
try:
    out = subprocess.check_output(
        ["powershell", "-Command", "(Get-Process dnplayer | Select -First 1).Path"],
        stderr=subprocess.DEVNULL).decode().strip()
    if out:
        d = os.path.dirname(out)
        p = os.path.join(d, "adb.exe")
        if os.path.exists(p):
            adb = p
            print("adb 来源: dnplayer 目录", d)
except Exception:
    pass
if not adb:
    for c in candidates:
        if os.path.exists(c):
            adb = c
            break

if adb:
    print("== adb 截图各步耗时 ==")
    for label, cmd in [
        ("screencap(设备端PNG编码)", [adb, "-s", "emulator-5554", "shell", "screencap", "-p", "/sdcard/ld_auto.png"]),
        ("adb pull(传输400KB)", [adb, "-s", "emulator-5554", "pull", "/sdcard/ld_auto.png", r"Z:\interest\ClassLibrary1\_bench_pull.png"]),
    ]:
        ts = []
        for _ in range(3):
            t0 = time.perf_counter()
            subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            ts.append((time.perf_counter() - t0) * 1000)
        print(f"  {label}: {min(ts):.0f} ms (best of 3)")
    # 对比: exec-out 直出原始数据
    t0 = time.perf_counter()
    raw = subprocess.run([adb, "-s", "emulator-5554", "exec-out", "screencap"],
                         stdout=subprocess.PIPE).stdout
    print(f"  exec-out screencap(原始RGBA): {(time.perf_counter()-t0)*1000:.0f} ms, {len(raw)} bytes")
else:
    print("未找到 adb.exe，跳过截图链路实测")
