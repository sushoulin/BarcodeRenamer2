using System;
using System.Collections.Generic;
using System.Drawing;
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
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.CODE_93,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                        BarcodeFormat.ITF,
                        BarcodeFormat.CODABAR,
                        BarcodeFormat.DATA_MATRIX,
                        BarcodeFormat.QR_CODE,
                        BarcodeFormat.PDF_417
                    }
                }
            };
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
                    // 尝试原始图片识别
                    var result = reader.Decode(bitmap);
                    if (result != null)
                    {
                        return new RecognitionResult
                        {
                            Success = true,
                            Content = result.Text,
                            Format = result.BarcodeFormat.ToString()
                        };
                    }

                    // 尝试多个角度旋转识别
                    result = TryMultipleRotations(bitmap);
                    if (result != null)
                    {
                        return new RecognitionResult
                        {
                            Success = true,
                            Content = result.Text,
                            Format = result.BarcodeFormat.ToString()
                        };
                    }

                    // 尝试不同尺寸缩放识别
                    result = TryDifferentScales(imagePath);
                    if (result != null)
                    {
                        return new RecognitionResult
                        {
                            Success = true,
                            Content = result.Text,
                            Format = result.BarcodeFormat.ToString()
                        };
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
            double[] scales = { 0.5, 1.5, 2.0 };

            foreach (double scale in scales)
            {
                try
                {
                    using (var original = new Bitmap(imagePath))
                    {
                        int newWidth = (int)(original.Width * scale);
                        int newHeight = (int)(original.Height * scale);

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
