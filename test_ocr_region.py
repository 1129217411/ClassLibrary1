import urllib.request, json

img = r"z:\interest\ClassLibrary1\test_new.png"
url = "http://127.0.0.1:18900/ocr"

# 测试1: 精确区域 (原始)
body = json.dumps({"image": img, "x1": 70, "y1": 274, "x2": 148, "y2": 299}).encode("utf-8")
req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"})
r = urllib.request.urlopen(req, timeout=30)
data = json.loads(r.read().decode("utf-8"))
print("Exact region (70,274,148,299):", len(data["texts"]), "texts")
for t in data["texts"]:
    print("  ", t["text"])

# 测试2: 扩大区域 (+padding)
body = json.dumps({"image": img, "x1": 50, "y1": 254, "x2": 170, "y2": 320}).encode("utf-8")
req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"})
r = urllib.request.urlopen(req, timeout=30)
data = json.loads(r.read().decode("utf-8"))
print("\nExpanded region (50,254,170,320):", len(data["texts"]), "texts")
for t in data["texts"]:
    print("  ", t["text"], "at", t["center"])

# 测试3: 全图
body = json.dumps({"image": img}).encode("utf-8")
req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"})
r = urllib.request.urlopen(req, timeout=30)
data = json.loads(r.read().decode("utf-8"))
print("\nFull image:", len(data["texts"]), "texts")
for t in data["texts"]:
    print("  ", t["text"], "at", t["center"])
