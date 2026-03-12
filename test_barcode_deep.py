#!/usr/bin/env python3
"""
条形码图像分析和识别工具
"""

import cv2
import numpy as np
from PIL import Image
import os

def analyze_image(image_path):
    """分析图片特征"""
    print(f"\n{'='*60}")
    print(f"分析图片: {image_path}")
    print(f"{'='*60}")

    img = cv2.imread(image_path)
    if img is None:
        print("❌ 无法读取图片")
        return None

    h, w = img.shape[:2]
    print(f"尺寸: {w} x {h}")
    print(f"通道: {img.shape[2] if len(img.shape) > 2 else 1}")

    # 转换为灰度图
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

    # 分析灰度值分布
    print(f"灰度值范围: {gray.min()} - {gray.max()}")
    print(f"灰度值均值: {gray.mean():.2f}")
    print(f"灰度值标准差: {gray.std():.2f}")

    # 统计黑白像素比例
    _, binary = cv2.threshold(gray, 128, 255, cv2.THRESH_BINARY)
    white_pixels = np.sum(binary == 255)
    black_pixels = np.sum(binary == 0)
    total_pixels = w * h
    print(f"白色像素: {white_pixels} ({white_pixels/total_pixels*100:.2f}%)")
    print(f"黑色像素: {black_pixels} ({black_pixels/total_pixels*100:.2f}%)")

    # 分析右上角区域
    right_top = img[0:h//2, w//2:w]
    gray_rt = cv2.cvtColor(right_top, cv2.COLOR_BGR2GRAY)
    print(f"\n右上角区域分析:")
    print(f"区域尺寸: {right_top.shape[1]} x {right_top.shape[0]}")
    print(f"灰度值范围: {gray_rt.min()} - {gray_rt.max()}")
    print(f"灰度值均值: {gray_rt.mean():.2f}")

    # 尝试使用不同的库识别
    print(f"\n尝试识别:")

    # 尝试使用 pyzbar
    try:
        from pyzbar import pyzbar
        barcodes = pyzbar.decode(img)
        if barcodes:
            for barcode in barcodes:
                print(f"✅ pyzbar 原始图片: {barcode.data.decode('utf-8')} ({barcode.type})")
        else:
            print("❌ pyzbar 原始图片: 未识别")

        # 灰度化后识别
        barcodes = pyzbar.decode(gray)
        if barcodes:
            for barcode in barcodes:
                print(f"✅ pyzbar 灰度图: {barcode.data.decode('utf-8')} ({barcode.type})")
        else:
            print("❌ pyzbar 灰度图: 未识别")

        # 右上角区域
        barcodes = pyzbar.decode(right_top)
        if barcodes:
            for barcode in barcodes:
                print(f"✅ pyzbar 右上角: {barcode.data.decode('utf-8')} ({barcode.type})")
        else:
            print("❌ pyzbar 右上角: 未识别")

        # 右上角灰度
        barcodes = pyzbar.decode(gray_rt)
        if barcodes:
            for barcode in barcodes:
                print(f"✅ pyzbar 右上角灰度: {barcode.data.decode('utf-8')} ({barcode.type})")
        else:
            print("❌ pyzbar 右上角灰度: 未识别")

    except Exception as e:
        print(f"pyzbar 错误: {e}")

    return img, gray

def test_with_opencv_preprocessing(image_path, expected):
    """使用 OpenCV 预处理后识别"""
    print(f"\n{'='*60}")
    print(f"OpenCV 预处理测试: {image_path}")
    print(f"预期: {expected}")
    print(f"{'='*60}")

    img = cv2.imread(image_path)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

    # 测试不同的阈值
    thresholds = [50, 70, 90, 110, 128, 150, 170, 190, 210]

    from pyzbar import pyzbar

    # 1. 全局二值化
    for thresh in thresholds:
        _, binary = cv2.threshold(gray, thresh, 255, cv2.THRESH_BINARY)
        barcodes = pyzbar.decode(binary)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                if result == expected:
                    print(f"✅ 全局二值化(阈值{thresh}): {result} ✓✓✓")
                    return True

    # 2. 自适应阈值
    for block_size in [11, 15, 21, 31, 41]:
        for C in [2, 5, 10]:
            adaptive = cv2.adaptiveThreshold(gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
                                            cv2.THRESH_BINARY, block_size, C)
            barcodes = pyzbar.decode(adaptive)
            if barcodes:
                for barcode in barcodes:
                    result = barcode.data.decode('utf-8')
                    if result == expected:
                        print(f"✅ 自适应阈值(block={block_size},C={C}): {result} ✓✓✓")
                        return True

    # 3. Otsu 二值化
    _, otsu = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    barcodes = pyzbar.decode(otsu)
    if barcodes:
        for barcode in barcodes:
            result = barcode.data.decode('utf-8')
            if result == expected:
                print(f"✅ Otsu二值化: {result} ✓✓✓")
                return True

    # 4. 右上角区域 + 所有策略
    h, w = gray.shape[:2]
    right_top_gray = gray[0:h//2, w//2:w]

    # 右上角二值化
    for thresh in thresholds:
        _, binary_rt = cv2.threshold(right_top_gray, thresh, 255, cv2.THRESH_BINARY)
        barcodes = pyzbar.decode(binary_rt)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                if result == expected:
                    print(f"✅ 右上角二值化(阈值{thresh}): {result} ✓✓✓")
                    return True

    # 右上角自适应阈值
    for block_size in [11, 15, 21, 31]:
        for C in [2, 5, 10]:
            adaptive_rt = cv2.adaptiveThreshold(right_top_gray, 255,
                                                cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
                                                cv2.THRESH_BINARY, block_size, C)
            barcodes = pyzbar.decode(adaptive_rt)
            if barcodes:
                for barcode in barcodes:
                    result = barcode.data.decode('utf-8')
                    if result == expected:
                        print(f"✅ 右上角自适应(block={block_size},C={C}): {result} ✓✓✓")
                        return True

    # 5. 放大 + 二值化
    for scale in [2, 3, 4]:
        resized = cv2.resize(gray, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
        for thresh in thresholds:
            _, binary_scaled = cv2.threshold(resized, thresh, 255, cv2.THRESH_BINARY)
            barcodes = pyzbar.decode(binary_scaled)
            if barcodes:
                for barcode in barcodes:
                    result = barcode.data.decode('utf-8')
                    if result == expected:
                        print(f"✅ 放大{scale}倍+二值化(阈值{thresh}): {result} ✓✓✓")
                        return True

    # 6. 右上角放大 + 二值化
    for scale in [2, 3, 4, 5]:
        resized_rt = cv2.resize(right_top_gray, None, fx=scale, fy=scale,
                                interpolation=cv2.INTER_CUBIC)
        for thresh in thresholds:
            _, binary_scaled_rt = cv2.threshold(resized_rt, thresh, 255, cv2.THRESH_BINARY)
            barcodes = pyzbar.decode(binary_scaled_rt)
            if barcodes:
                for barcode in barcodes:
                    result = barcode.data.decode('utf-8')
                    if result == expected:
                        print(f"✅ 右上角放大{scale}倍+二值化(阈值{thresh}): {result} ✓✓✓")
                        return True

    # 7. 腐蚀和膨胀
    kernel = np.ones((3, 3), np.uint8)
    for thresh in thresholds:
        _, binary = cv2.threshold(gray, thresh, 255, cv2.THRESH_BINARY)
        erosion = cv2.erode(binary, kernel, iterations=1)
        barcodes = pyzbar.decode(erosion)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                if result == expected:
                    print(f"✅ 腐蚀+二值化(阈值{thresh}): {result} ✓✓✓")
                    return True

        dilation = cv2.dilate(binary, kernel, iterations=1)
        barcodes = pyzbar.decode(dilation)
        if barcodes:
            for barcode in barcodes:
                result = barcode.data.decode('utf-8')
                if result == expected:
                    print(f"✅ 膨胀+二值化(阈值{thresh}): {result} ✓✓✓")
                    return True

    print("❌ 所有策略均失败")
    return False

def main():
    print("=== 条形码识别深度测试 ===")

    test_cases = [
        ("test_barcodes/01137590.png", "40260300081"),
        ("test_barcodes/11264071.png", "40260300085")
    ]

    for image_path, expected in test_cases:
        analyze_image(image_path)

    print(f"\n{'='*60}")
    print("开始深度识别测试...")
    print(f"{'='*60}")

    success_count = 0
    for image_path, expected in test_cases:
        if test_with_opencv_preprocessing(image_path, expected):
            success_count += 1

    print(f"\n{'='*60}")
    print(f"最终结果: {success_count}/{len(test_cases)} 成功")
    print(f"{'='*60}")

if __name__ == "__main__":
    main()
