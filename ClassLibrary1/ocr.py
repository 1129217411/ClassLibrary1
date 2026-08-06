"""
PaddleOCR 文字识别脚本（轻量版）
用法: python ocr.py <图片路径>
输出: JSON 格式的识别结果
"""
import sys
import os
import json
import warnings
warnings.filterwarnings("ignore")
os.environ["FLAGS_use_mkldnn"] = "0"

from paddleocr import PaddleOCR

# 使用轻量级模型 (mobile版本，加载更快)
ocr = PaddleOCR(
    use_angle_cls=True,
    lang='ch',
    show_log=False,
    # 使用轻量级检测和识别模型
    det_model_dir=None,  # 使用默认的轻量级检测模型
    rec_model_dir=None,  # 使用默认的轻量级识别模型
    use_gpu=False,
    enable_mkldnn=False,
)

def recognize(image_path):
    """识别图片中的文字"""
    result = ocr.ocr(image_path, cls=True)
    
    texts = []
    if result and result[0]:
        for line in result[0]:
            box = line[0]
            text = line[1][0]
            confidence = line[1][1]
            cx = int(sum(p[0] for p in box) / 4)
            cy = int(sum(p[1] for p in box) / 4)
            texts.append({
                "text": text,
                "confidence": round(confidence, 3),
                "center": {"x": cx, "y": cy},
                "box": [[int(p[0]), int(p[1])] for p in box]
            })
    return texts

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"error": "请提供图片路径"}, ensure_ascii=False))
        sys.exit(1)
    
    image_path = sys.argv[1]
    try:
        texts = recognize(image_path)
        print(json.dumps({"texts": texts}, ensure_ascii=False))
    except Exception as e:
        print(json.dumps({"error": str(e)}, ensure_ascii=False))
        sys.exit(1)
