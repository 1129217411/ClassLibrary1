# -*- coding: utf-8 -*-
"""验证 1:1 窗口截图(_diag_full.png)的区域经增强后能否被 OCR 正确识别"""
from PIL import Image, ImageOps, ImageFilter
import urllib.request, json, io, time

im = Image.open(r'Z:\interest\ClassLibrary1\_diag_full.png').convert('RGB')
print('full size:', im.size)

def ocr_bytes(im):
    buf = io.BytesIO()
    im.save(buf, 'PNG')
    body = json.dumps({'image_b64': __import__('base64').b64encode(buf.getvalue()).decode(),
                       'x1': 64, 'y1': 268, 'x2': 148, 'y2': 292,
                       'skip_det': True, 'pre_cropped': True}).encode()
    t0 = time.perf_counter()
    try:
        r = urllib.request.urlopen(
            urllib.request.Request('http://127.0.0.1:18900/ocr', data=body), timeout=30)
        d = json.loads(r.read())
        ms = (time.perf_counter() - t0) * 1000
        return [t['text'] for t in d['texts']], ms
    except Exception as e:
        return ['ERR:' + str(e)[:80]], 0

# 先试服务端是否支持 image_b64；不行就用文件路径
txt, ms = ocr_bytes(im.crop((58, 262, 154, 298)))
print('b64 test ->', txt, '%.0f ms' % ms)
USE_B64 = not str(txt).startswith("['ERR")

def ocr(im2, name):
    if USE_B64:
        r, ms = ocr_bytes(im2)
        print(name, '->', r, '%.0f ms' % ms)
    else:
        p = r'Z:\interest\ClassLibrary1\_diag_' + name + '.png'
        im2.save(p)
        body = json.dumps({'image': p, 'x1': 64, 'y1': 268, 'x2': 148, 'y2': 292,
                           'skip_det': True, 'pre_cropped': True}).encode()
        t0 = time.perf_counter()
        d = json.loads(urllib.request.urlopen(
            urllib.request.Request('http://127.0.0.1:18900/ocr', data=body), timeout=30).read())
        print(name, '->', [t['text'] for t in d['texts']],
              '%.0f ms' % ((time.perf_counter() - t0) * 1000))

crop = im.crop((58, 262, 154, 298))
print('crop size:', crop.size)
ocr(crop, 'raw')
for z in (2, 3):
    big = crop.resize((crop.width * z, crop.height * z), Image.LANCZOS)
    big = ImageOps.autocontrast(big)
    ocr(big, 'ac_z%d' % z)
    big2 = big.filter(ImageFilter.SHARPEN)
    ocr(big2, 'acsh_z%d' % z)

# 对照组：adb 全图截图的同区域
try:
    adb = Image.open(r'Z:\interest\ClassLibrary1\ClassLibrary1\bin\Release\screenshot_0.png').convert('RGB')
    adb.crop((58, 262, 154, 298)).save(r'Z:\interest\ClassLibrary1\_diag_adb_crop.png')
    body = json.dumps({'image': r'Z:\interest\ClassLibrary1\_diag_adb_crop.png',
                       'x1': 64, 'y1': 268, 'x2': 148, 'y2': 292,
                       'skip_det': True, 'pre_cropped': True}).encode()
    d = json.loads(urllib.request.urlopen(
        urllib.request.Request('http://127.0.0.1:18900/ocr', data=body), timeout=30).read())
    print('adb crop ->', [t['text'] for t in d['texts']])
except Exception as e:
    print('adb ref failed:', e)
