# -*- coding: utf-8 -*-
"""验证窗口裁剪小图经放大+增强后能否被 OCR 正确识别"""
from PIL import Image, ImageOps, ImageFilter
import urllib.request, json

im = Image.open(r'Z:\interest\ClassLibrary1\_bench_window.png').convert('RGB')
w, h = im.size
# 设备区域 (64,268)-(148,292) 映射到窗口
sx, sy = w / 720.0, h / 1280.0
box = (int(64 * sx) - 4, int(268 * sy) - 4, int(148 * sx) + 4, int(292 * sy) + 4)
crop = im.crop(box)
print('crop size:', crop.size)

def ocr(path):
    body = json.dumps({'image': path, 'x1': 64, 'y1': 268, 'x2': 148, 'y2': 292,
                       'skip_det': True, 'pre_cropped': True}).encode()
    r = urllib.request.urlopen(
        urllib.request.Request('http://127.0.0.1:18900/ocr', data=body), timeout=30)
    d = json.loads(r.read())
    return [t['text'] for t in d['texts']]

for zoom in (2, 3, 4):
    big = crop.resize((crop.width * zoom, crop.height * zoom), Image.LANCZOS)
    big = ImageOps.autocontrast(big)
    big = big.filter(ImageFilter.SHARPEN)
    p = r'Z:\interest\ClassLibrary1\_win_e%d.png' % zoom
    big.save(p)
    print('zoom', zoom, '->', ocr(p))
