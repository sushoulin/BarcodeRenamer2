#!/usr/bin/env python3
"""
条形码图像可视化调试工具
"""

import cv2
import numpy as np
from pyzbar import pyzbar
import os

def visualize_and_test(image_path, expected):
    """可视化处理过程并测试"""
    print(f"\n{'='*60}")
    print(f"处理: {image_path}")
    print(f"预期: {expected}")
    print(f"{'='*60}")

    # 读取图片
    img = cv2.imread(image_path)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    h, w = gray.shape[:2]

    print(f"原始尺寸: {w} x {h}")

    # 尝试不同的放大倍数
    for scale in [4, 5, 6, 8, 10]:
        resized = cv2.resize(gray, None, fx=scale, fy=scale,
                            interpolation=cv2.INTER_CUBIC)

        # 尝试不同的二值化阈值
        for thresh in range(40, 220, 10):
            _, binary = cv2.threshold(resized, thresh, 255, cv2.THRESH_BINARY)

            # 保存处理后的图片
            output_dir = "test_barcodes/processed"
            os.makedirs(output_dir, exist_ok=True)
            output_path = f"{output_dir}/{os.path.basename(image_path).replace('.png', f'_scale{scale}_thresh{thresh}.png')}"
            cv2.imwrite(output_path, binary)

            # 尝试识别
            barcodes = pyzbar.decode(binary)
            if barcodes:
                for barcode in barcodes:
                    result = barcode.data.decode('utf-8')
                    print(f"✅ 发现条形码! 放大{scale}x, 阈值{thresh}: {result}")
                    if result == expected:
                        print(f"   ✅✅✅ 匹配成功! 保存到: {output_path}")
                        return True, output_path

    # 尝试右上角区域
    print("\n尝试右上角区域...")
    right_top = gray[0:h//2, w//2:w]

    for scale in [4, 5, 6, 8, 10, 12]:
        resized_rt = cv2.resize(right_top, None, fx=scale, fy=scale,
                               interpolation=cv2.INTER_CUBIC)

        for thresh in range(40, 220, 10):
            _, binary = cv2.threshold(resized_rt, thresh, 255, cv2.THRESH_BINARY)

            output_path = f"{output_dir}/{os.path.basename(image_path).replace('.png', f'_RT_scale{scale}_thresh{thresh}.png')}"
            cv2.imwrite(output_path, binary)

            barcodes = pyzbar.decode(binary)
            if barcodes:
                for barcode in barcodes:
                    result = barcode.data.decode('utf-8')
                    print(f"✅ 发现条形码! 右上角放大{scale}x, 阈值{thresh}: {result}")
                    if result == expected:
                        print(f"   ✅✅✅ 匹配成功! 保存到: {output_path}")
                        return True, output_path

    # 尝试四个角落
    print("\n尝试四个角落...")
    corners = {
        'TopLeft': gray[0:h//2, 0:w//2],
        'TopRight': gray[0:h//2, w//2:w],
        'BottomLeft': gray[h//2:h, 0:w//2],
        'BottomRight': gray[h//2:h, w//2:w]
    }

    for corner_name, corner_img in corners.items():
        for scale in [4, 5, 6, 8, 10, 12]:
            resized_corner = cv2.resize(corner_img, None, fx=scale, fy=scale,
                                       interpolation=cv2.INTER_CUBIC)

            for thresh in range(40, 220, 20):
                _, binary = cv2.threshold(resized_corner, thresh, 255, cv2.THRESH_BINARY)

                output_path = f"{output_dir}/{os.path.basename(image_path).replace('.png', f'_{corner_name}_scale{scale}_thresh{thresh}.png')}"
                cv2.imwrite(output_path, binary)

                barcodes = pyzbar.decode(binary)
                if barcodes:
                    for barcode in barcodes:
                        result = barcode.data.decode('utf-8')
                        print(f"✅ 发现条形码! {corner_name}放大{scale}x, 阈值{thresh}: {result}")
                        if result == expected:
                            print(f"   ✅✅✅ 匹配成功! 保存到: {output_path}")
                            return True, output_path

    print("❌ 所有策略失败")
    return False, None

def main():
    print("=== 条形码识别可视化调试 ===")

    test_cases = [
        ("test_barcodes/01137590.png", "40260300081"),
        ("test_barcodes/11264071.png", "40260300085")
    ]

    results = []
    for image_path, expected in test_cases:
        success, output = visualize_and_test(image_path, expected)
        results.append((image_path, success, output))

    print(f"\n{'='*60}")
    print("测试结果汇总:")
    print(f"{'='*60}")
    for image_path, success, output in results:
        status = "✅ 成功" if success else "❌ 失败"
        print(f"{image_path}: {status}")
        if output:
            print(f"   最佳处理图片: {output}")

if __name__ == "__main__":
    main()
