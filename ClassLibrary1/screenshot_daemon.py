#!/usr/bin/env python3
"""
截图守护进程 - 运行在 Android 模拟器内部
TCP 服务，监听截图请求，返回 screencap raw 数据（跳过 PNG 编码，~100ms）
启动: python3 screenshot_daemon.py [端口号]
默认端口: 19000
协议:
  客户端发送: b"shot"
  服务端返回: b"<width> <height>\n" + raw RGBA 像素数据 (w*h*4 字节)
"""
import socket
import subprocess
import sys
import struct
import os

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 19000
SCREENCAP = "/system/bin/screencap"

def capture_screen():
    """调用 screencap 获取 raw 数据，返回 (width, height, rgba_bytes)"""
    raw = subprocess.check_output([SCREENCAP])
    # raw 格式: 12 字节头 (width:i32le, height:i32le, format:i32le) + 像素数据
    if len(raw) < 12:
        return None
    w, h = struct.unpack_from('<II', raw, 0)
    pixels = raw[12:]
    return w, h, pixels

def handle_client(conn):
    """处理单个客户端请求"""
    try:
        data = conn.recv(64)
        if data.strip() == b"shot":
            result = capture_screen()
            if result is None:
                conn.sendall(b"ERR\n")
                return
            w, h, pixels = result
            # 发送文本头 + 二进制像素数据
            header = "{} {}\n".format(w, h).encode()
            conn.sendall(header)
            conn.sendall(pixels)
        elif data.strip() == b"ping":
            conn.sendall(b"pong\n")
    except Exception as e:
        try:
            conn.sendall(b"ERR\n")
        except:
            pass

def main():
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", PORT))
    srv.listen(4)
    print("Screenshot daemon listening on port {}".format(PORT), flush=True)
    while True:
        conn, addr = srv.accept()
        try:
            handle_client(conn)
        finally:
            conn.close()

if __name__ == "__main__":
    main()
