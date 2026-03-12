#!/usr/bin/env python3
"""
条形码识别测试工具
使用 OpenCV 和 pyzbar 测试条形码识别
"""

import cv2
import numpy as np
from pyzbar import pyzbar
import os

def test_barcode_recognition(image_path, expected_barcode):
    """测试单个图片的条形码识别"""
    print(f"\n{'='*60}")
    print(f"测试文件: {image_path}")
    print(f"预期结果: {expected_barcode}")

    if not os.path.exists(image_path):
        print(f"❌ 文件不存在")
        return False

    # 读取图片
    img = cv2.imread(image_path)
    if img is None:
        print(f"❌ 无法读取图片")
        return False

    print(f"图片尺寸: {img.shape[1]} x {img.shape[0]}")
    print(f"图片通道: {img.shape[2] if len(img.shape) > 2 else 1}")

    # 策略1: 原始图片识别
    barcodes = pyzbar.decode(img)
    if barcodes:
        for barcode in barcodes:
            result = barcode.data.decode('utf-8')
            print(f"✅ 策略1-原始图片识别成功: {result}")
            if result == expected_barcode:
                print(f"   ✅ 匹配成功!")
                return True

    # 策略2: 灰度化
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    barcodes = pyzbar.decode(gray)
    if barcodes:
        for barcode in barcodes:
            result = barcode.data.decode('utf-8')
            print(f"✅ 策略2-灰度化识别成功: {result}")
            if result == expected_barcode:
                print(f"   ✅ 匹配成功!")
                return True

    # 策略3: 二值化 (多种阈值)
    for threshold in [50, 70, 90, 110, 128, 150, 170, 190, 210]:
        _, binary = cv2.threshold(gray, threshold, 255, cv2.THRESH_BINARY)
        barcodes = pyzbar.decode(binary)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                print(f"✅ 策略3-二值化识别成功 (阈值{threshold}): {result}")
                if result == expected_barcode:
                    print(f"   ✅ 匹配成功!")
                    return True

    # 策略4: 右上角区域
    h, w = img.shape[:2]
    right_top = img[0:h//2, w//2:w]
    barcodes = pyzbar.decode(right_top)
    if barcodes:
        for barcode in barcodes:
            result = barcode.data.decode('utf-8')
            print(f"✅ 策略4-右上角区域识别成功: {result}")
            if result == expected_barcode:
                print(f"   ✅ 匹配成功!")
                return True

    # 策略5: 右上角灰度化
    gray_rt = cv2.cvtColor(right_top, cv2.COLOR_BGR2GRAY)
    barcodes = pyzbar.decode(gray_rt)
    if barcodes:
        for barcode in barcodes:
            result = barcode.data.decode('utf-8')
            print(f"✅ 策略5-右上角灰度化识别成功: {result}")
            if result == expected_barcode:
                print(f"   ✅ 匹配成功!")
                return True

    # 策略6: 右上角二值化
    for threshold in [50, 70, 90, 110, 128, 150, 170, 190, 210]:
        _, binary_rt = cv2.threshold(gray_rt, threshold, 255, cv2.THRESH_BINARY)
        barcodes = pyzbar.decode(binary_rt)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                print(f"✅ 策略6-右上角二值化识别成功 (阈值{threshold}): {result}")
                if result == expected_barcode:
                    print(f"   ✅ 匹配成功!")
                    return True

    # 策略7: 放大识别
    for scale in [2, 3, 4]:
        resized = cv2.resize(img, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
        barcodes = pyzbar.decode(resized)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                print(f"✅ 策略7-放大{scale}倍识别成功: {result}")
                if result == expected_barcode:
                    print(f"   ✅ 匹配成功!")
                    return True

    # 策略8: 右上角放大识别
    for scale in [2, 3, 4]:
        resized_rt = cv2.resize(right_top, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
        barcodes = pyzbar.decode(resized_rt)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                print(f"✅ 策略8-右上角放大{scale}倍识别成功: {result}")
                if result == expected_barcode:
                    print(f"   ✅ 匹配成功!")
                    return True

    # 策略9: 四角落区域识别
    corners = {
        '左上': img[0:h//2, 0:w//2],
        '右上': img[0:h//2, w//2:w],
        '左下': img[h//2:h, 0:w//2],
        '右下': img[h//2:h, w//2:w]
    }

    for corner_name, corner_img in corners.items():
        gray_corner = cv2.cvtColor(corner_img, cv2.COLOR_BGR2GRAY)
        for threshold in [90, 110, 128, 150]:
            _, binary_corner = cv2.threshold(gray_corner, threshold, 255, cv2.THRESH_BINARY)
            for scale in [2, 3]:
                resized_corner = cv2.resize(binary_corner, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
                barcodes = pyzbar.decode(resized_corner)
                if barcodes:
                    for barcode in barcodes:
                        result = barcode.data.decode('utf-8')
                        print(f"✅ 策略9-{corner_name}区域二值化({threshold})放大{scale}倍识别成功: {result}")
                        if result == expected_barcode:
                            print(f"   ✅ 匹配成功!")
                            return True

    print(f"❌ 所有策略均识别失败")
    return False

def main():
    print("=== 条形码识别测试 ===")

    test_cases = [
        ("test_barcodes/01137590.png", "40260300081"),
        ("test_barcodes/11264071.png", "40260300085")
    ]

    success_count = 0
    total_count = len(test_cases)

    for image_path, expected in test_cases:
        if test_barcode_recognition(image_path, expected):
            success_count += 1

    print(f"\n{'='*60}")
    print(f"测试结果: {success_count}/{total_count} 成功")
    print(f"{'='*60}")

if __name__ == "__main__":
    main()
