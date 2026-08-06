# -*- coding: utf-8 -*-
"""OCR 耗时分解基准测试"""
import os, time, warnings
warnings.filterwarnings("ignore")
from PIL import Image
import numpy as np

IMG = r"z:\interest\ClassLibrary1\test_new.png"
REGION = (70, 274, 148, 299)  # 与 test_ocr_region.py 一致的小区域

def t(label, fn, n=5):
    best = 1e9
    for _ in range(n):
        s = time.perf_counter()
        fn()
        best = min(best, time.perf_counter() - s)
    print(f"{label}: {best*1000:.1f} ms")
    return best

print("== 阶段1: PIL 解码整图 ==")
img = t("Image.open().convert(RGB)", lambda: Image.open(IMG).convert("RGB"))
full = Image.open(IMG).convert("RGB")
print("整图尺寸:", full.size)

print("== 阶段2: 裁剪 ==")
crop = t("crop 78x25", lambda: np.array(full.crop(REGION)))
crop_arr = np.array(full.crop(REGION))
print("crop shape:", crop_arr.shape)

print("== 阶段3: PaddleOCR (当前服务端配置: mkldnn关闭) ==")
from paddleocr import PaddleOCR
ocr = PaddleOCR(use_angle_cls=False, lang='ch', show_log=False, use_gpu=False, enable_mkldnn=False)
# 预热
ocr.ocr(crop_arr)
t("ocr.ocr(crop) 完整流水线", lambda: ocr.ocr(crop_arr))

# 检测/识别组件是否可直接访问（用于跳过检测）
print("组件属性:", [a for a in ("text_detector", "text_recognizer") if hasattr(ocr, a)])

print("== 阶段4: det_limit_side_len 影响 ==")
for side in (960, 736, 480, 320):
    o2 = PaddleOCR(use_angle_cls=False, lang='ch', show_log=False, use_gpu=False,
                   enable_mkldnn=False, det_limit_side_len=side)
    o2.ocr(crop_arr)  # 预热
    r = t(f"ocr.ocr(crop) det_limit_side_len={side}", lambda: o2.ocr(crop_arr))

print("== 阶段5: 仅识别(跳过检测) ==")
try:
    # 直接对裁剪区域做识别（适合已知单行文字的小区域）
    rec = ocr.text_recognizer
    rec([crop_arr])  # 预热
    t("text_recognizer 直连识别", lambda: rec([crop_arr]))
except Exception as e:
    print("直连识别失败:", e)

print("== 阶段6: 开启 MKLDNN ==")
try:
    os.environ["FLAGS_use_mkldnn"] = "1"
    o3 = PaddleOCR(use_angle_cls=False, lang='ch', show_log=False, use_gpu=False, enable_mkldnn=True)
    o3.ocr(crop_arr)  # 预热
    t("ocr.ocr(crop) mkldnn=True", lambda: o3.ocr(crop_arr))
except Exception as e:
    print("mkldnn 失败:", e)

print("== 阶段7: cpu_math_library_num_threads ==")
import paddle
print("当前线程数:", paddle.get_device())
for n_thr in (1, 4, 8):
    try:
        paddle.set_device("cpu")
        o4 = PaddleOCR(use_angle_cls=False, lang='ch', show_log=False, use_gpu=False,
                       enable_mkldnn=False, cpu_math_library_num_threads=n_thr)
        o4.ocr(crop_arr)
        t(f"ocr.ocr(crop) threads={n_thr}", lambda: o4.ocr(crop_arr))
    except Exception as e:
        print(f"threads={n_thr} 失败:", e)
