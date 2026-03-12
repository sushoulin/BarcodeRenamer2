using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ZXing;
using ZXing.Windows.Compatibility;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 条形码识别服务类
    /// </summary>
    public class BarcodeRecognitionService
    {
        private readonly BarcodeReader reader;

        public BarcodeRecognitionService()
        {
            reader = new BarcodeReader
            {
                AutoRotate = true,
                TryInverted = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        // 只保留一维条形码格式，移除二维码
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.CODE_93,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                        BarcodeFormat.ITF,
                        BarcodeFormat.CODABAR
                    },
                    // 优化识别参数
                    PureBarcode = false,
                    ReturnCodabarStartEnd = true,
                    // 添加更多识别选项
                    TryInverted = true
                }
            };

            // 设置更高的容错率
            reader.Options.TryHarder = true;
        }

        /// <summary>
        /// 识别图片中的条形码
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>识别结果</returns>
        public RecognitionResult Recognize(string imagePath)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    // 策略0: 尝试右上角区域识别（条形码通常在右上角）
                    var result = TryTopRightRegion(bitmap);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }

                    // 策略1: 尝试原始图片识别
                    result = reader.Decode(bitmap);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }

                    // 策略2: 强力二值化识别（黑白图片优化）
                    result = TryAggressiveBinarization(bitmap);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }

                    // 策略3: 尝试多个角度旋转识别
                    result = TryMultipleRotations(bitmap);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }

                    // 策略4: 尝试不同尺寸缩放识别
                    result = TryDifferentScales(imagePath);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }

                    // 策略5: 尝试图像预处理（灰度化、对比度增强）
                    result = TryWithPreprocessing(bitmap);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }

                    // 策略6: 组合策略 - 预处理 + 旋转 + 缩放
                    result = TryCombinedStrategies(imagePath);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }

                    // 策略7: 四个角落区域识别
                    result = TryCornerRegions(bitmap);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }
                }
            }
            catch (Exception ex)
            {
                return new RecognitionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }

            return new RecognitionResult
            {
                Success = false,
                ErrorMessage = "未能识别条形码"
            };
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        private RecognitionResult CreateSuccessResult(Result result)
        {
            return new RecognitionResult
            {
                Success = true,
                Content = result.Text,
                Format = result.BarcodeFormat.ToString()
            };
        }

        /// <summary>
        /// 尝试右上角区域识别
        /// </summary>
        private Result? TryTopRightRegion(Bitmap original)
        {
            try
            {
                // 右上角区域：宽度的50%-100%，高度的0%-50%
                int startX = original.Width / 2;
                int startY = 0;
                int regionWidth = original.Width / 2;
                int regionHeight = original.Height / 2;

                // 裁剪右上角区域
                using (var region = new Bitmap(regionWidth, regionHeight))
                {
                    using (var g = Graphics.FromImage(region))
                    {
                        g.DrawImage(original,
                            new Rectangle(0, 0, regionWidth, regionHeight),
                            new Rectangle(startX, startY, regionWidth, regionHeight),
                            GraphicsUnit.Pixel);
                    }

                    var result = reader.Decode(region);
                    if (result != null)
                    {
                        return result;
                    }

                    // 右上角区域放大2-4倍识别
                    for (int scale = 2; scale <= 4; scale++)
                    {
                        using (var scaled = new Bitmap(region, regionWidth * scale, regionHeight * scale))
                        {
                            result = reader.Decode(scaled);
                            if (result != null)
                            {
                                return result;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 强力二值化识别（针对黑白条形码优化）
        /// </summary>
        private Result? TryAggressiveBinarization(Bitmap original)
        {
            // 针对黑白条形码，尝试多种二值化策略
            int[] thresholds = { 50, 70, 90, 110, 128, 150, 170, 190, 210 };

            foreach (int threshold in thresholds)
            {
                try
                {
                    using (var binary = Binarize(original, threshold))
                    {
                        var result = reader.Decode(binary);
                        if (result != null)
                        {
                            return result;
                        }

                        // 二值化后放大识别
                        for (int scale = 2; scale <= 3; scale++)
                        {
                            using (var scaled = new Bitmap(binary, original.Width * scale, original.Height * scale))
                            {
                                result = reader.Decode(scaled);
                                if (result != null)
                                {
                                    return result;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略错误
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试四个角落区域识别
        /// </summary>
        private Result? TryCornerRegions(Bitmap original)
        {
            var regions = new[]
            {
                new { Name = "TopLeft", X = 0, Y = 0 },
                new { Name = "TopRight", X = original.Width / 2, Y = 0 },
                new { Name = "BottomLeft", X = 0, Y = original.Height / 2 },
                new { Name = "BottomRight", X = original.Width / 2, Y = original.Height / 2 }
            };

            int regionWidth = original.Width / 2;
            int regionHeight = original.Height / 2;

            foreach (var regionInfo in regions)
            {
                try
                {
                    using (var region = new Bitmap(regionWidth, regionHeight))
                    {
                        using (var g = Graphics.FromImage(region))
                        {
                            g.DrawImage(original,
                                new Rectangle(0, 0, regionWidth, regionHeight),
                                new Rectangle(regionInfo.X, regionInfo.Y, regionWidth, regionHeight),
                                GraphicsUnit.Pixel);
                        }

                        var result = reader.Decode(region);
                        if (result != null)
                        {
                            return result;
                        }

                        // 区域放大识别
                        for (int scale = 2; scale <= 4; scale++)
                        {
                            using (var scaled = new Bitmap(region, regionWidth * scale, regionHeight * scale))
                            {
                                result = reader.Decode(scaled);
                                if (result != null)
                                {
                                    return result;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略错误
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试多个角度旋转识别
        /// </summary>
        private Result? TryMultipleRotations(Bitmap bitmap)
        {
            int[] angles = { 90, 180, 270 };

            foreach (int angle in angles)
            {
                try
                {
                    using (var rotated = RotateImage(bitmap, angle))
                    {
                        var result = reader.Decode(rotated);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
                catch
                {
                    // 忽略旋转失败的情况
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试不同尺寸缩放识别
        /// </summary>
        private Result? TryDifferentScales(string imagePath)
        {
            // 增加更多缩放比例，包括更大的倍数
            double[] scales = { 0.2, 0.3, 0.4, 0.5, 0.6, 0.75, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 };

            foreach (double scale in scales)
            {
                try
                {
                    using (var original = new Bitmap(imagePath))
                    {
                        int newWidth = (int)(original.Width * scale);
                        int newHeight = (int)(original.Height * scale);

                        if (newWidth < 10 || newHeight < 10 || newWidth > 10000 || newHeight > 10000)
                        {
                            continue;
                        }

                        using (var scaled = new Bitmap(original, newWidth, newHeight))
                        {
                            var result = reader.Decode(scaled);
                            if (result != null)
                            {
                                return result;
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略缩放失败的情况
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试图像预处理（灰度化、对比度增强）
        /// </summary>
        private Result? TryWithPreprocessing(Bitmap original)
        {
            try
            {
                // 转换为灰度图
                using (var grayscale = ConvertToGrayscale(original))
                {
                    var result = reader.Decode(grayscale);
                    if (result != null)
                    {
                        return result;
                    }

                    // 增强对比度
                    using (var enhanced = EnhanceContrast(grayscale))
                    {
                        result = reader.Decode(enhanced);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }
            catch
            {
                // 忽略预处理失败的情况
            }

            return null;
        }

        /// <summary>
        /// 尝试二值化处理
        /// </summary>
        private Result? TryWithBinarization(Bitmap original)
        {
            // 尝试不同的二值化阈值
            int[] thresholds = { 80, 100, 128, 150, 180 };

            foreach (int threshold in thresholds)
            {
                try
                {
                    using (var binary = Binarize(original, threshold))
                    {
                        var result = reader.Decode(binary);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
                catch
                {
                    // 忽略二值化失败的情况
                }
            }

            return null;
        }

        /// <summary>
        /// 组合策略 - 预处理 + 旋转 + 缩放
        /// </summary>
        private Result? TryCombinedStrategies(string imagePath)
        {
            try
            {
                using (var original = new Bitmap(imagePath))
                {
                    // 灰度化 + 旋转
                    using (var grayscale = ConvertToGrayscale(original))
                    {
                        var result = TryMultipleRotations(grayscale);
                        if (result != null)
                        {
                            return result;
                        }
                    }

                    // 二值化 + 旋转
                    using (var binary = Binarize(original, 128))
                    {
                        var result = TryMultipleRotations(binary);
                        if (result != null)
                        {
                            return result;
                        }
                    }

                    // 灰度化 + 缩放
                    using (var grayscale = ConvertToGrayscale(original))
                    {
                        int[] scales = { 2, 3 };
                        foreach (int scale in scales)
                        {
                            int newWidth = grayscale.Width * scale;
                            int newHeight = grayscale.Height * scale;
                            using (var scaled = new Bitmap(grayscale, newWidth, newHeight))
                            {
                                var scaledResult = reader.Decode(scaled);
                                if (scaledResult != null)
                                {
                                    return scaledResult;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略组合策略失败的情况
            }

            return null;
        }

        /// <summary>
        /// 转换为灰度图
        /// </summary>
        private Bitmap ConvertToGrayscale(Bitmap original)
        {
            var grayscale = new Bitmap(original.Width, original.Height);

            using (var g = Graphics.FromImage(grayscale))
            {
                // 使用灰度颜色矩阵
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                        new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                        new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                g.DrawImage(original,
                    new Rectangle(0, 0, original.Width, original.Height),
                    0, 0, original.Width, original.Height,
                    GraphicsUnit.Pixel, attributes);
            }

            return grayscale;
        }

        /// <summary>
        /// 增强对比度
        /// </summary>
        private Bitmap EnhanceContrast(Bitmap original)
        {
            var enhanced = new Bitmap(original.Width, original.Height);

            using (var g = Graphics.FromImage(enhanced))
            {
                // 增强对比度的颜色矩阵
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {1.5f, 0, 0, 0, 0},
                        new float[] {0, 1.5f, 0, 0, 0},
                        new float[] {0, 0, 1.5f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {-0.25f, -0.25f, -0.25f, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                g.DrawImage(original,
                    new Rectangle(0, 0, original.Width, original.Height),
                    0, 0, original.Width, original.Height,
                    GraphicsUnit.Pixel, attributes);
            }

            return enhanced;
        }

        /// <summary>
        /// 二值化处理
        /// </summary>
        private Bitmap Binarize(Bitmap original, int threshold)
        {
            var binary = new Bitmap(original.Width, original.Height);

            for (int x = 0; x < original.Width; x++)
            {
                for (int y = 0; y < original.Height; y++)
                {
                    var pixel = original.GetPixel(x, y);
                    // 计算灰度值
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    // 二值化
                    var newColor = gray < threshold ? Color.Black : Color.White;
                    binary.SetPixel(x, y, newColor);
                }
            }

            return binary;
        }

        /// <summary>
        /// 旋转图片
        /// </summary>
        private Bitmap RotateImage(Bitmap bitmap, float angle)
        {
            var rotated = new Bitmap(bitmap.Height, bitmap.Width);
            using (var g = Graphics.FromImage(rotated))
            {
                g.TranslateTransform(bitmap.Height / 2f, bitmap.Width / 2f);
                g.RotateTransform(angle);
                g.TranslateTransform(-bitmap.Width / 2f, -bitmap.Height / 2f);
                g.DrawImage(bitmap, 0, 0);
            }
            return rotated;
        }
    }

    /// <summary>
    /// 识别结果类
    /// </summary>
    public class RecognitionResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 条形码内容
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// 条形码格式
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
