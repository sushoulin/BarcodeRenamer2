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
    /// 条形码识别服务类 - 专注于右上角区域快速识别
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
                    PureBarcode = false,
                    ReturnCodabarStartEnd = true,
                    TryInverted = true
                }
            };
        }

        /// <summary>
        /// 识别图片中的条形码（专注于右上角区域）
        /// </summary>
        public RecognitionResult Recognize(string imagePath)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    // 快速识别右上角区域
                    var result = TryTopRightRegion(bitmap);
                    if (result != null)
                    {
                        return CreateSuccessResult(result);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"识别异常: {ex.Message}");
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
        /// 右上角区域快速识别
        /// </summary>
        private Result? TryTopRightRegion(Bitmap original)
        {
            int w = original.Width;
            int h = original.Height;

            // 右上角区域：宽度的50%-100%，高度的0%-50%
            int startX = w / 2;
            int startY = 0;
            int regionWidth = w / 2;
            int regionHeight = h / 2;

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

                // 策略1: 原始区域识别
                var result = reader.Decode(region);
                if (result != null) return result;

                // 策略2: 灰度化识别
                using (var gray = ConvertToGrayscale(region))
                {
                    result = reader.Decode(gray);
                    if (result != null) return result;

                    // 策略3: 二值化识别（尝试几个关键阈值）
                    int[] thresholds = { 128, 100, 150 };
                    foreach (int thresh in thresholds)
                    {
                        using (var binary = Binarize(gray, thresh))
                        {
                            result = reader.Decode(binary);
                            if (result != null) return result;
                        }
                    }

                    // 策略4: 放大2倍后识别
                    int scale = 2;
                    int newW = regionWidth * scale;
                    int newH = regionHeight * scale;

                    using (var scaledGray = new Bitmap(gray, newW, newH))
                    {
                        result = reader.Decode(scaledGray);
                        if (result != null) return result;

                        // 放大后二值化
                        foreach (int thresh in thresholds)
                        {
                            using (var binary = Binarize(scaledGray, thresh))
                            {
                                result = reader.Decode(binary);
                                if (result != null) return result;
                            }
                        }
                    }
                }
            }

            return null;
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
        /// 转换为灰度图
        /// </summary>
        private Bitmap ConvertToGrayscale(Bitmap original)
        {
            var grayscale = new Bitmap(original.Width, original.Height);

            using (var g = Graphics.FromImage(grayscale))
            {
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
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    var newColor = gray < threshold ? Color.Black : Color.White;
                    binary.SetPixel(x, y, newColor);
                }
            }

            return binary;
        }
    }

    /// <summary>
    /// 识别结果类
    /// </summary>
    public class RecognitionResult
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? Format { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
