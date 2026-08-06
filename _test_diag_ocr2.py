# -*- coding: utf-8 -*-
"""对比窗口1:1截图与adb截图的区域像素，尝试gamma/阈值等校正后OCR"""
from PIL import Image, ImageOps
import urllib.request, json, time

win = Image.open(r'Z:\interest\ClassLibrary1\_diag_full.png').convert('RGB')
adb = Image.open(r'Z:\interest\ClassLibrary1\ClassLibrary1\bin\Release\screenshot_0.png').convert('RGB')
print('win size:', win.size, 'adb size:', adb.size)

sy = win.size[1] / float(adb.size[1])
# adb 坐标 (58,262,154,298) 映射到窗口
box_w = (58, int(262 * sy), 154, int(298 * sy))
wc = win.crop(box_w)
ac = adb.crop((58, 262, 154, 298))
wc = wc.resize(ac.size, Image.LANCZOS)
print('crop size:', ac.size)

def stats(im, name):
    px = list(im.getdata())
    n = len(px)
    mr = sum(p[0] for p in px) // n
    mg = sum(p[1] for p in px) // n
    mb = sum(p[2] for p in px) // n
    hi = sum(1 for p in px if max(p) > 128)
    print('%s mean=(%d,%d,%d) bright_px=%d/%d' % (name, mr, mg, mb, hi, n))

stats(ac, 'adb  ')
stats(wc, 'win  ')

def ocr(im2, name):
    p = r'Z:\interest\ClassLibrary1\_d2_' + name + '.png'
    im2.save(p)
    body = json.dumps({'image': p, 'x1': 64, 'y1': 268, 'x2': 148, 'y2': 292,
                       'skip_det': True, 'pre_cropped': True}).encode()
    t0 = time.perf_counter()
    try:
        d = json.loads(urllib.request.urlopen(
            urllib.request.Request('http://127.0.0.1:18900/ocr', data=body), timeout=30).read())
        print('%-10s ->' % name, [t['text'] for t in d['texts']],
              '%.0f ms' % ((time.perf_counter() - t0) * 1000))
    except Exception as e:
        print('%-10s -> ERR' % name, str(e)[:60])

def gamma(im, g):
    lut = [int(((i / 255.0) ** g) * 255) for i in range(256)]
    return im.point(lut * 3)

def z2(im):
    return im.resize((im.width * 2, im.height * 2), Image.LANCZOS)

ocr(ac, 'adb_raw')
ocr(wc, 'win_raw')
for g in (0.3, 0.45, 0.6, 2.2):
    ocr(z2(ImageOps.autocontrast(gamma(wc, g))), 'g%.2f' % g)
# 阈值二值化（文字亮于背景？或暗于背景？两种都试）
gray = wc.convert('L')
for th in (60, 90, 120):
    binv = gray.point(lambda v: 255 if v > th else 0)   # 亮字
    binn = gray.point(lambda v: 0 if v > th else 255)   # 暗字
    ocr(z2(binv), 'th%d_inv' % th)
    ocr(z2(binn), 'th%d_nor' % th)
