using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZXing;
using ZXing.Windows.Compatibility;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 条形码识别服务类 - 使用Emgu CV检测条形码位置
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
        /// 识别图片中的条形码
        /// 流程：Emgu CV检测条形码位置 -> 截取区域 -> ZXing识别内容
        /// </summary>
        public RecognitionResult Recognize(string imagePath)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    // 先去除顶部空白区域
                    using (var noBlank = RemoveTopBlankArea(bitmap))
                    {
                        // 裁剪右上角区域：高度20%，宽度50%（从右侧开始）
                        int cropHeight = noBlank.Height / 5; // 20%高度
                        int cropWidth = (int)(noBlank.Width * 0.5); // 50%宽度
                        using (var cropped = CropTopRightRegion(noBlank, cropHeight, cropWidth))
                        {
                            // 使用Emgu CV检测条形码位置
                            var barcodeRegions = DetectBarcodeRegions(cropped);
                            
                            if (barcodeRegions.Count == 0)
                            {
                                System.Diagnostics.Debug.WriteLine("Emgu CV未检测到条形码区域");
                                return new RecognitionResult
                                {
                                    Success = false,
                                    ErrorMessage = "未检测到条形码区域"
                                };
                            }

                            // 对每个检测到的区域尝试识别
                            foreach (var region in barcodeRegions)
                            {
                                // 扩展区域边界（确保完整条形码）
                                int padding = 5;
                                int x = Math.Max(0, region.X - padding);
                                int y = Math.Max(0, region.Y - padding);
                                int width = Math.Min(cropped.Width - x, region.Width + padding * 2);
                                int height = Math.Min(cropped.Height - y, region.Height + padding * 2);

                                // 裁剪条形码区域
                                using (var barcodeImage = CropRegion(cropped, x, y, width, height))
                                {
                                    // 调整DPI到400
                                    using (var highDpi = SetHighDPI(barcodeImage, 400))
                                    {
                                        // 使用ZXing识别
                                        var result = reader.Decode(highDpi);
                                        if (result != null && ValidateResult(result))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"条形码识别成功: {result.Text}");
                                            return CreateSuccessResult(result);
                                        }
                                    }
                                }
                            }

                            System.Diagnostics.Debug.WriteLine($"检测到{barcodeRegions.Count}个候选区域，但ZXing未能识别");
                        }
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
        /// 使用Emgu CV检测条形码区域
        /// 原理：条形码具有密集的黑白条纹，通过形态学操作可以检测
        /// </summary>
        private List<Rectangle> DetectBarcodeRegions(Bitmap bitmap)
        {
            var regions = new List<Rectangle>();

            try
            {
                // 转换为Emgu CV格式
                using (var mat = BitmapToMat(bitmap))
                using (var gray = new Mat())
                using (var gradient = new Mat())
                using (var blurred = new Mat())
                using (var threshold = new Mat())
                using (var kernel = new Mat())
                using (var closed = new Mat())
                using (var eroded = new Mat())
                using (var dilated = new Mat())
                {
                    // 1. 转换为灰度图
                    CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);

                    // 2. 计算梯度（Scharr算子）- 突出条形码的条纹特征
                    using (var gradX = new Mat())
                    using (var gradY = new Mat())
                    {
                        CvInvoke.Scharr(gray, gradX, DepthType.Cv16S, 1, 0);
                        CvInvoke.Scharr(gray, gradY, DepthType.Cv16S, 0, 1);
                        
                        // 取绝对值
                        CvInvoke.ConvertScaleAbs(gradX, gradX, 1, 0);
                        CvInvoke.ConvertScaleAbs(gradY, gradY, 1, 0);
                        
                        // 水平梯度减去垂直梯度（条形码水平条纹更明显）
                        CvInvoke.Subtract(gradX, gradY, gradient);
                        CvInvoke.ConvertScaleAbs(gradient, gradient, 1, 0);
                    }

                    // 3. 高斯模糊 - 平滑噪声
                    CvInvoke.GaussianBlur(gradient, blurred, new Size(9, 9), 0);

                    // 4. 二值化
                    CvInvoke.Threshold(blurred, threshold, 225, 255, ThresholdType.Binary);

                    // 5. 形态学操作 - 连接条形码区域
                    // 创建水平核（条形码通常是水平的）
                    kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(21, 7), new Point(-1, -1));
                    CvInvoke.MorphologyEx(threshold, closed, MorphOp.Close, kernel, new Point(-1, -1), 4, BorderType.Default, new MCvScalar());

                    // 6. 腐蚀和膨胀 - 去除小噪点
                    CvInvoke.Erode(closed, eroded, null, new Point(-1, -1), 4, BorderType.Default, new MCvScalar());
                    CvInvoke.Dilate(eroded, dilated, null, new Point(-1, -1), 4, BorderType.Default, new MCvScalar());

                    // 7. 查找轮廓
                    using (var contours = new VectorOfVectorOfPoint())
                    {
                        CvInvoke.FindContours(dilated, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                        for (int i = 0; i < contours.Size; i++)
                        {
                            var contour = contours[i];
                            var rect = CvInvoke.BoundingRectangle(contour);

                            // 过滤太小的区域
                            if (rect.Width < 80 || rect.Height < 15)
                                continue;

                            // 过滤太长的区域（宽度不应超过高度的20倍）
                            if (rect.Width > rect.Height * 25)
                                continue;

                            // 过滤太高的区域（高度不应超过宽度的1/2）
                            if (rect.Height > rect.Width / 2)
                                continue;

                            // 计算区域的纵横比
                            double aspectRatio = (double)rect.Width / rect.Height;
                            
                            // 条形码纵横比通常在2到15之间
                            if (aspectRatio < 2 || aspectRatio > 15)
                                continue;

                            regions.Add(rect);
                            System.Diagnostics.Debug.WriteLine($"检测到候选条形码区域: {rect}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Emgu CV检测异常: {ex.Message}");
            }

            return regions;
        }

        /// <summary>
        /// Bitmap转换为Mat
        /// </summary>
        private Mat BitmapToMat(Bitmap bitmap)
        {
            // 确保像素格式正确
            if (bitmap.PixelFormat != PixelFormat.Format24bppRgb &&
                bitmap.PixelFormat != PixelFormat.Format32bppArgb &&
                bitmap.PixelFormat != PixelFormat.Format32bppRgb)
            {
                var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(converted))
                {
                    g.DrawImage(bitmap, 0, 0);
                }
                return converted.ToMat();
            }
            return bitmap.ToMat();
        }

        /// <summary>
        /// 裁剪指定区域
        /// </summary>
        private Bitmap CropRegion(Bitmap original, int x, int y, int width, int height)
        {
            // 确保不超出边界
            x = Math.Max(0, Math.Min(x, original.Width - 1));
            y = Math.Max(0, Math.Min(y, original.Height - 1));
            width = Math.Min(width, original.Width - x);
            height = Math.Min(height, original.Height - y);

            var cropped = new Bitmap(width, height);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, width, height),
                    new Rectangle(x, y, width, height),
                    GraphicsUnit.Pixel);
            }
            return cropped;
        }

        /// <summary>
        /// 去除顶部空白区域
        /// </summary>
        private Bitmap RemoveTopBlankArea(Bitmap original)
        {
            int w = original.Width;
            int h = original.Height;
            
            int firstNonBlankRow1 = DetectBlankByBlock(original, 250, 0.005, 0.1);
            int firstNonBlankRow2 = DetectBlankByBlock(original, 240, 0.01, 0.1);
            int firstNonBlankRow3 = DetectBlankByBlock(original, 230, 0.02, 0.1);
            int firstNonBlankRow4 = DetectBlankByBlockBrightness(original, 240, 0.01, 0.1);
            
            int firstNonBlankRow = h;
            
            if (firstNonBlankRow1 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow1);
            if (firstNonBlankRow2 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow2);
            if (firstNonBlankRow3 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow3);
            if (firstNonBlankRow4 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow4);
            
            if (firstNonBlankRow == h)
            {
                firstNonBlankRow = 0;
            }
            
            if (firstNonBlankRow == 0)
            {
                return new Bitmap(original);
            }
            
            int newHeight = h - firstNonBlankRow;
            var cropped = new Bitmap(w, newHeight);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, w, newHeight),
                    new Rectangle(0, firstNonBlankRow, w, newHeight),
                    GraphicsUnit.Pixel);
            }
            
            return cropped;
        }
        
        private int DetectBlankByBlock(Bitmap bitmap, int threshold, double blockHeightPercent, double noiseTolerance)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            int blockHeight = Math.Max(1, (int)(h * blockHeightPercent));
            int maxNoisePixels = (int)(w * blockHeight * noiseTolerance);
            
            for (int blockStart = 0; blockStart < h; blockStart += blockHeight)
            {
                int blockEnd = Math.Min(blockStart + blockHeight, h);
                int nonBlankPixels = 0;
                
                for (int y = blockStart; y < blockEnd; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.R < threshold || pixel.G < threshold || pixel.B < threshold)
                        {
                            nonBlankPixels++;
                        }
                    }
                }
                
                if (nonBlankPixels > maxNoisePixels)
                {
                    return blockStart;
                }
            }
            
            return 0;
        }
        
        private int DetectBlankByBlockBrightness(Bitmap bitmap, int brightnessThreshold, double blockHeightPercent, double noiseTolerance)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            int blockHeight = Math.Max(1, (int)(h * blockHeightPercent));
            int maxNoisePixels = (int)(w * blockHeight * noiseTolerance);
            
            for (int blockStart = 0; blockStart < h; blockStart += blockHeight)
            {
                int blockEnd = Math.Min(blockStart + blockHeight, h);
                int nonBlankPixels = 0;
                
                for (int y = blockStart; y < blockEnd; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        int brightness = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                        
                        if (brightness < brightnessThreshold)
                        {
                            nonBlankPixels++;
                        }
                    }
                }
                
                if (nonBlankPixels > maxNoisePixels)
                {
                    return blockStart;
                }
            }
            
            return 0;
        }
        
        /// <summary>
        /// 裁剪右上角区域
        /// </summary>
        private Bitmap CropTopRightRegion(Bitmap original, int cropHeight, int cropWidth)
        {
            int w = original.Width;
            int h = original.Height;

            cropHeight = Math.Min(cropHeight, h);
            cropWidth = Math.Min(cropWidth, w);

            int startX = w - cropWidth;
            int startY = 0;

            var cropped = new Bitmap(cropWidth, cropHeight);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, cropWidth, cropHeight),
                    new Rectangle(startX, startY, cropWidth, cropHeight),
                    GraphicsUnit.Pixel);
            }
            
            return cropped;
        }
        
        /// <summary>
        /// 验证识别结果的可靠性
        /// </summary>
        private bool ValidateResult(Result result)
        {
            if (result == null || string.IsNullOrEmpty(result.Text))
            {
                return false;
            }
            
            string text = result.Text.Trim();
            
            // 检查长度
            if (text.Length < 8 || text.Length > 20)
            {
                System.Diagnostics.Debug.WriteLine($"识别结果长度不符合要求: {text}");
                return false;
            }
            
            // 检查数字占比
            int digitCount = text.Count(c => char.IsDigit(c));
            if (digitCount < text.Length * 0.8)
            {
                System.Diagnostics.Debug.WriteLine($"识别结果数字占比不足: {text}");
                return false;
            }
            
            // 检查是否全是相同字符
            if (text.Distinct().Count() == 1)
            {
                System.Diagnostics.Debug.WriteLine($"识别结果全为相同字符: {text}");
                return false;
            }
            
            // 检查重复模式
            if (IsRepeatingPattern(text))
            {
                System.Diagnostics.Debug.WriteLine($"识别结果是重复模式: {text}");
                return false;
            }
            
            return true;
        }
        
        private bool IsRepeatingPattern(string text)
        {
            if (text.Length < 6) return false;
            
            // 检查2字符重复模式
            if (text.Length >= 6 && text.Length % 2 == 0)
            {
                string pattern2 = text.Substring(0, 2);
                bool isPattern2 = true;
                for (int i = 2; i < text.Length; i += 2)
                {
                    if (text.Substring(i, 2) != pattern2)
                    {
                        isPattern2 = false;
                        break;
                    }
                }
                if (isPattern2) return true;
            }
            
            // 检查3字符重复模式
            if (text.Length >= 6 && text.Length % 3 == 0)
            {
                string pattern3 = text.Substring(0, 3);
                bool isPattern3 = true;
                for (int i = 3; i < text.Length; i += 3)
                {
                    if (text.Substring(i, 3) != pattern3)
                    {
                        isPattern3 = false;
                        break;
                    }
                }
                if (isPattern3) return true;
            }
            
            return false;
        }

        /// <summary>
        /// 设置高DPI
        /// </summary>
        private Bitmap SetHighDPI(Bitmap original, int dpi)
        {
            var highDpi = new Bitmap(original.Width, original.Height);
            highDpi.SetResolution(dpi, dpi);

            using (var g = Graphics.FromImage(highDpi))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(original, 0, 0, original.Width, original.Height);
            }

            return highDpi;
        }

        private RecognitionResult CreateSuccessResult(Result result)
        {
            return new RecognitionResult
            {
                Success = true,
                Content = result.Text,
                Format = result.BarcodeFormat.ToString()
            };
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
