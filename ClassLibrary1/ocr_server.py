"""
PaddleOCR 常驻服务
启动: python ocr_server.py [端口号]
默认端口: 18900
POST /ocr {"image": "图片路径"} -> JSON
GET /health -> 200
"""
import sys
import os
import json
import warnings
import logging
warnings.filterwarnings("ignore")
os.environ["FLAGS_use_mkldnn"] = "0"
# 全局禁用 WARNING 及以下级别的日志
logging.disable(logging.WARNING)

from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 18900

print(f"[OCR] 正在加载模型...", flush=True)
from paddleocr import PaddleOCR
from PIL import Image, ImageDraw
import numpy as np
ocr = PaddleOCR(use_angle_cls=False, lang='ch', show_log=False, use_gpu=False, enable_mkldnn=False)

# 预热：用合成文字图跑一遍 det+rec，消除首次请求的推理初始化毛刺（~200ms）
try:
    _warm_img = Image.new("RGB", (200, 48), "white")
    ImageDraw.Draw(_warm_img).text((8, 14), "OCR 1234567890", fill="black")
    _warm_arr = np.array(_warm_img.resize((400, 96)))
    ocr.ocr(_warm_arr)
    if hasattr(ocr, "text_recognizer"):
        ocr.text_recognizer([_warm_arr])
    del _warm_img, _warm_arr
except Exception:
    pass
print(f"[OCR] 模型加载并预热完成，监听端口 {PORT}", flush=True)

class Handler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass  # 静默日志

    def do_GET(self):
        if self.path == "/health":
            self._json_response(200, {"status": "ok"})
        else:
            self._json_response(404, {"error": "not found"})

    def do_POST(self):
        if self.path == "/ocr":
            try:
                length = int(self.headers.get("Content-Length", 0))
                body = json.loads(self.rfile.read(length).decode("utf-8"))
                image_path = body.get("image", "")
                if not image_path:
                    self._json_response(400, {"error": "missing image path"})
                    return
                # 可选区域裁剪参数
                x1 = body.get("x1")
                y1 = body.get("y1")
                x2 = body.get("x2")
                y2 = body.get("y2")
                # 仅识别模式：已知单行文字区域时跳过文本检测，只做识别
                skip_det = bool(body.get("skip_det", False))
                # 图片已由 C# 端预裁剪，无需再次裁剪
                pre_cropped = bool(body.get("pre_cropped", False))
                texts = self._recognize(image_path, x1, y1, x2, y2, skip_det, pre_cropped)
                self._json_response(200, {"texts": texts})
            except Exception as e:
                self._json_response(500, {"error": str(e)})
        else:
            self._json_response(404, {"error": "not found"})

    def _recognize(self, image_path, x1=None, y1=None, x2=None, y2=None, skip_det=False, pre_cropped=False):
        # 如果指定了区域，裁剪后直接传内存数组（不写磁盘）
        crop = x1 is not None and y1 is not None and x2 is not None and y2 is not None
        try:
            if crop and skip_det and pre_cropped:
                # 图片已在 C# 端裁剪好，直接识别（免二次裁剪），坐标仅用于中心点换算
                img_arr = np.array(Image.open(image_path).convert("RGB"))
                texts = self._recognize_only(img_arr, x1, y1, x2, y2)
                if texts is not None:
                    return texts
                # 直连识别失败时回退完整流水线（对小图跑 det+rec）
                result = ocr.ocr(img_arr)
            elif crop:
                img = Image.open(image_path).convert("RGB")
                cropped = np.array(img.crop((x1, y1, x2, y2)))
                # 仅识别模式：跳过 DB 文本检测，直接对区域做识别（适合已知单行小区域）
                if skip_det:
                    texts = self._recognize_only(cropped, x1, y1, x2, y2)
                    if texts is not None:
                        return texts
                    # 直连识别失败时回退完整流水线
                result = ocr.ocr(cropped)
            else:
                # 全图 OCR：先加载图片验证
                img = Image.open(image_path).convert("RGB")
                img_array = np.array(img)
                print(f"[OCR] 处理图片: {image_path}, 尺寸: {img_array.shape}", flush=True)
                result = ocr.ocr(img_array)

            texts = []
            if result and result[0]:
                for line in result[0]:
                    box = line[0]
                    text = line[1][0]
                    confidence = line[1][1]
                    cx = int(sum(p[0] for p in box) / 4)
                    cy = int(sum(p[1] for p in box) / 4)
                    # 裁剪时坐标要加回偏移量
                    if crop:
                        cx += x1
                        cy += y1
                        box = [[p[0] + x1, p[1] + y1] for p in box]
                    texts.append({
                        "text": text,
                        "confidence": round(confidence, 3),
                        "center": {"x": cx, "y": cy},
                        "box": [[int(p[0]), int(p[1])] for p in box]
                    })
            return texts
        except Exception as e:
            print(f"[OCR] 处理图片失败: {image_path}, 错误: {e}", flush=True)
            raise

    def _recognize_only(self, cropped, x1, y1, x2, y2):
        """跳过文本检测，直接调用识别模型。失败返回 None 由调用方回退。"""
        try:
            res = ocr.text_recognizer([cropped])
            rec_res = res[0] if isinstance(res, tuple) else res
            texts = []
            for item in rec_res:
                text, confidence = item[0], item[1]
                if not text:
                    continue
                texts.append({
                    "text": text,
                    "confidence": round(float(confidence), 3),
                    "center": {"x": int((x1 + x2) / 2), "y": int((y1 + y2) / 2)},
                    "box": [[x1, y1], [x2, y1], [x2, y2], [x1, y2]]
                })
            return texts
        except Exception:
            return None

    def _json_response(self, code, data):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

if __name__ == "__main__":
    server = HTTPServer(("127.0.0.1", PORT), Handler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    server.server_close()
