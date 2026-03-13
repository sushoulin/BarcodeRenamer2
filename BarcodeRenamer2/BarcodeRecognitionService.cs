using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZXing;
using ZXing.Windows.Compatibility;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 条形码识别服务类
    /// 策略：ZXing识别 + 严格的结果验证
    /// </summary>
    public class BarcodeRecognitionService
    {
        private readonly BarcodeReader reader;
        private string cropOutputFolder;

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
        /// 设置裁剪图片输出文件夹
        /// </summary>
        public void SetCropOutputFolder(string folder)
        {
            cropOutputFolder = folder;
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        /// <summary>
        /// 识别图片中的条形码
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
                        // 裁剪右上角区域：高度20%，宽度50%
                        int cropHeight = noBlank.Height / 5;
                        int cropWidth = (int)(noBlank.Width * 0.5);
                        using (var cropped = CropTopRightRegion(noBlank, cropHeight, cropWidth))
                        {
                            // 保存裁剪图片
                            SaveCropImage(cropped, Path.GetFileNameWithoutExtension(imagePath));
                            
                            // 多策略识别
                            var result = MultiStrategyRecognition(cropped);
                            
                            if (result != null)
                            {
                                // 严格验证：必须有条形码的几何特征
                                if (ValidateBarcodeGeometry(cropped, result))
                                {
                                    System.Diagnostics.Debug.WriteLine($"条形码识别成功: {result.Text}");
                                    return CreateSuccessResult(result);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"几何验证失败，可能无条形码: {result.Text}");
                                }
                            }
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
        /// 多策略识别
        /// </summary>
        private Result MultiStrategyRecognition(Bitmap bitmap)
        {
            // 策略1: 原始图像识别
            var result = reader.Decode(bitmap);
            if (result != null && ValidateResultContent(result))
                return result;

            // 策略2: 灰度化识别
            using (var gray = ConvertToGrayscale(bitmap))
            {
                result = reader.Decode(gray);
                if (result != null && ValidateResultContent(result))
                    return result;

                // 策略3: 二值化识别
                foreach (int thresh in new[] { 128, 100, 150, 80, 180 })
                {
                    using (var binary = Binarize(gray, thresh))
                    {
                        result = reader.Decode(binary);
                        if (result != null && ValidateResultContent(result))
                            return result;
                    }
                }
            }

            // 策略4: 放大识别
            using (var scaled = new Bitmap(bitmap, bitmap.Width * 2, bitmap.Height * 2))
            {
                result = reader.Decode(scaled);
                if (result != null && ValidateResultContent(result))
                    return result;

                using (var gray = ConvertToGrayscale(scaled))
                {
                    result = reader.Decode(gray);
                    if (result != null && ValidateResultContent(result))
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 验证识别结果内容的有效性
        /// </summary>
        private bool ValidateResultContent(Result result)
        {
            if (result == null || string.IsNullOrEmpty(result.Text))
                return false;

            string text = result.Text.Trim();

            // 长度检查：条形码通常8-20字符
            if (text.Length < 6 || text.Length > 30)
            {
                System.Diagnostics.Debug.WriteLine($"内容长度不符: {text.Length}");
                return false;
            }

            // 数字占比检查：条形码应主要是数字
            int digitCount = text.Count(c => char.IsDigit(c));
            double digitRatio = (double)digitCount / text.Length;
            if (digitRatio < 0.6)
            {
                System.Diagnostics.Debug.WriteLine($"数字占比不足: {digitRatio:P0} - {text}");
                return false;
            }

            // 检查是否全是相同字符
            if (text.Distinct().Count() == 1)
            {
                System.Diagnostics.Debug.WriteLine($"全相同字符: {text}");
                return false;
            }

            // 检查简单重复模式
            if (IsSimpleRepeatingPattern(text))
            {
                System.Diagnostics.Debug.WriteLine($"简单重复模式: {text}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查是否是简单重复模式
        /// </summary>
        private bool IsSimpleRepeatingPattern(string text)
        {
            if (text.Length < 6) return false;

            // 检查2字符重复 (ABABAB...)
            if (text.Length >= 6 && text.Length % 2 == 0)
            {
                bool isRepeat = true;
                for (int i = 2; i < text.Length; i += 2)
                {
                    if (text.Substring(i, 2) != text.Substring(0, 2))
                    {
                        isRepeat = false;
                        break;
                    }
                }
                if (isRepeat) return true;
            }

            // 检查3字符重复 (ABCABC...)
            if (text.Length >= 6 && text.Length % 3 == 0)
            {
                bool isRepeat = true;
                for (int i = 3; i < text.Length; i += 3)
                {
                    if (text.Substring(i, 3) != text.Substring(0, 3))
                    {
                        isRepeat = false;
                        break;
                    }
                }
                if (isRepeat) return true;
            }

            return false;
        }

        /// <summary>
        /// 验证条形码几何特征 - 关键方法，防止误识别
        /// </summary>
        private bool ValidateBarcodeGeometry(Bitmap bitmap, Result result)
        {
            // 必须有识别点坐标
            if (result.ResultPoints == null || result.ResultPoints.Length < 2)
            {
                System.Diagnostics.Debug.WriteLine("无识别点坐标");
                return false;
            }

            // 计算识别区域
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            int validPointCount = 0;

            foreach (var point in result.ResultPoints)
            {
                if (point == null) continue;
                validPointCount++;
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }

            if (validPointCount < 2)
            {
                System.Diagnostics.Debug.WriteLine("有效识别点不足");
                return false;
            }

            float width = maxX - minX;
            float height = maxY - minY;

            // 条形码必须是横向的（宽度大于高度）
            if (width < height)
            {
                System.Diagnostics.Debug.WriteLine($"非横向条形码: W={width}, H={height}");
                return false;
            }

            // 宽度应该足够大
            if (width < 50)
            {
                System.Diagnostics.Debug.WriteLine($"宽度太小: {width}");
                return false;
            }

            // 验证识别区域的条纹特征
            int padding = 5;
            int startX = Math.Max(0, (int)minX - padding);
            int endX = Math.Min(bitmap.Width - 1, (int)maxX + padding);
            int startY = Math.Max(0, (int)minY - padding);
            int endY = Math.Min(bitmap.Height - 1, (int)maxY + padding);

            return HasBarcodeStripePattern(bitmap, startX, endX, startY, endY);
        }

        /// <summary>
        /// 检测图像区域是否具有条形码条纹特征
        /// </summary>
        private bool HasBarcodeStripePattern(Bitmap bitmap, int startX, int endX, int startY, int endY)
        {
            int width = endX - startX + 1;
            int height = endY - startY + 1;

            if (width < 50 || height < 5)
                return false;

            // 二值化
            int threshold = 128;
            int[,] binary = new int[height, width];
            int blackCount = 0;

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    binary[y - startY, x - startX] = gray < threshold ? 0 : 1;
                    if (gray < threshold) blackCount++;
                }
            }

            // 1. 黑白像素比例检查（条形码应该有明显黑白对比）
            double blackRatio = (double)blackCount / (width * height);
            if (blackRatio < 0.1 || blackRatio > 0.9)
            {
                System.Diagnostics.Debug.WriteLine($"黑白比例不符: {blackRatio:P0}");
                return false;
            }

            // 2. 条纹交替检测（每一行应该有多次黑白交替）
            int validRows = 0;
            for (int y = 0; y < height; y++)
            {
                int transitions = 0;
                for (int x = 1; x < width; x++)
                {
                    if (binary[y, x] != binary[y, x - 1])
                        transitions++;
                }
                // 条形码每行应该有足够的交替次数
                if (transitions >= 8)
                    validRows++;
            }

            double validRowRatio = (double)validRows / height;
            if (validRowRatio < 0.6)
            {
                System.Diagnostics.Debug.WriteLine($"有效行比例不足: {validRowRatio:P0}");
                return false;
            }

            // 3. 行间一致性检测（条形码各行应该相似）
            if (height >= 2)
            {
                int consistentPairs = 0;
                for (int y = 0; y < height - 1; y++)
                {
                    int sameCount = 0;
                    for (int x = 0; x < width; x++)
                    {
                        if (binary[y, x] == binary[y + 1, x])
                            sameCount++;
                    }
                    if ((double)sameCount / width > 0.6)
                        consistentPairs++;
                }

                double consistency = (double)consistentPairs / (height - 1);
                if (consistency < 0.6)
                {
                    System.Diagnostics.Debug.WriteLine($"行间一致性不足: {consistency:P0}");
                    return false;
                }
            }

            System.Diagnostics.Debug.WriteLine($"几何验证通过: 黑白比例{blackRatio:P0}, 有效行{validRows}/{height}");
            return true;
        }

        /// <summary>
        /// 保存裁剪图片
        /// </summary>
        private void SaveCropImage(Bitmap cropImage, string originalFileName)
        {
            if (string.IsNullOrEmpty(cropOutputFolder))
                return;

            try
            {
                string fileName = $"{originalFileName}_crop_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string filePath = Path.Combine(cropOutputFolder, fileName);
                cropImage.Save(filePath, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存裁剪图片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 去除顶部空白区域
        /// </summary>
        private Bitmap RemoveTopBlankArea(Bitmap original)
        {
            int w = original.Width;
            int h = original.Height;

            int firstNonBlankRow = h;

            // 多策略检测
            var results = new[] {
                DetectBlankByBlock(original, 250, 0.005, 0.1),
                DetectBlankByBlock(original, 240, 0.01, 0.1),
                DetectBlankByBlockBrightness(original, 240, 0.01, 0.1)
            };

            foreach (var r in results)
            {
                if (r > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, r);
            }

            if (firstNonBlankRow == h) firstNonBlankRow = 0;
            if (firstNonBlankRow == 0) return new Bitmap(original);

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
                            nonBlankPixels++;
                    }
                }

                if (nonBlankPixels > maxNoisePixels)
                    return blockStart;
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
                            nonBlankPixels++;
                    }
                }

                if (nonBlankPixels > maxNoisePixels)
                    return blockStart;
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
        /// 转换为灰度图
        /// </summary>
        private Bitmap ConvertToGrayscale(Bitmap original)
        {
            var grayscale = new Bitmap(original.Width, original.Height);
            using (var g = Graphics.FromImage(grayscale))
            {
                var colorMatrix = new ColorMatrix(new float[][]
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
                    binary.SetPixel(x, y, gray < threshold ? Color.Black : Color.White);
                }
            }
            return binary;
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
