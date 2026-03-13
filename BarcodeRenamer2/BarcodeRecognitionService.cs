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
    /// 核心原则：宁可漏识别，也不能误识别
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
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.ITF
                    },
                    PureBarcode = false,
                    TryInverted = true
                }
            };
        }

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
                        // 裁剪右上角区域
                        int cropHeight = noBlank.Height / 5;
                        int cropWidth = (int)(noBlank.Width * 0.5);
                        using (var cropped = CropTopRightRegion(noBlank, cropHeight, cropWidth))
                        {
                            // 保存裁剪图片
                            SaveCropImage(cropped, Path.GetFileNameWithoutExtension(imagePath));

                            // 检查图像中是否真的有条形码纹理特征
                            // 这是最关键的检查：没有条形码纹理特征直接返回失败
                            if (!HasBarcodeTexture(cropped))
                            {
                                System.Diagnostics.Debug.WriteLine("图像无条形码纹理特征");
                                return new RecognitionResult
                                {
                                    Success = false,
                                    ErrorMessage = "无条形码"
                                };
                            }

                            // 有条形码纹理特征，尝试ZXing识别
                            var result = MultiStrategyRecognition(cropped);

                            if (result != null)
                            {
                                // 再次验证：识别结果和图像特征必须匹配
                                if (ValidateRecognitionResult(cropped, result))
                                {
                                    System.Diagnostics.Debug.WriteLine($"条形码识别成功: {result.Text}");
                                    return CreateSuccessResult(result);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"识别结果验证失败: {result.Text}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"识别异常: {ex.Message}");
            }

            return new RecognitionResult
            {
                Success = false,
                ErrorMessage = "无条形码"
            };
        }

        /// <summary>
        /// 检查图像是否具有条形码纹理特征
        /// 这是第一道关卡：没有条形码纹理特征的图像直接拒绝
        /// </summary>
        private bool HasBarcodeTexture(Bitmap bitmap)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;

            // 转换为灰度并二值化
            int threshold = 128;
            int[,] binary = new int[h, w];
            int blackCount = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    binary[y, x] = gray < threshold ? 0 : 1;
                    if (gray < threshold) blackCount++;
                }
            }

            double blackRatio = (double)blackCount / (w * h);

            // 条形码的黑白比例应该在合理范围内
            if (blackRatio < 0.1 || blackRatio > 0.7)
            {
                System.Diagnostics.Debug.WriteLine($"黑白比例不符条形码特征: {blackRatio:P0}");
                return false;
            }

            // 检测是否存在密集的条纹区域
            // 条形码特征：存在一个区域，该区域内有多行密集的黑白交替条纹
            int stripeRegionCount = 0;

            // 扫描图像，寻找条形码纹理区域
            for (int startY = 0; startY < h - 10; startY += 5)
            {
                for (int startX = 0; startX < w - 50; startX += 10)
                {
                    int regionW = Math.Min(100, w - startX);
                    int regionH = Math.Min(20, h - startY);

                    // 计算该区域的条纹密度
                    int totalTransitions = 0;
                    int rowsWithStripes = 0;

                    for (int y = startY; y < startY + regionH && y < h; y++)
                    {
                        int transitions = 0;
                        for (int x = startX + 1; x < startX + regionW && x < w; x++)
                        {
                            if (binary[y, x] != binary[y, x - 1])
                            {
                                transitions++;
                            }
                        }

                        // 条形码每行应该有较多的黑白交替
                        if (transitions >= 10)
                        {
                            rowsWithStripes++;
                            totalTransitions += transitions;
                        }
                    }

                    // 如果大部分行都有密集条纹，说明可能是条形码区域
                    if (rowsWithStripes >= regionH * 0.7 && totalTransitions >= regionH * 12)
                    {
                        stripeRegionCount++;
                    }
                }
            }

            // 必须检测到至少一个条纹区域
            if (stripeRegionCount == 0)
            {
                System.Diagnostics.Debug.WriteLine("未检测到条纹区域");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"检测到 {stripeRegionCount} 个条纹区域");
            return true;
        }

        /// <summary>
        /// 多策略识别
        /// </summary>
        private Result MultiStrategyRecognition(Bitmap bitmap)
        {
            // 策略1: 原始图像识别
            var result = reader.Decode(bitmap);
            if (result != null && IsValidBarcodeContent(result.Text))
                return result;

            // 策略2: 灰度化识别
            using (var gray = ConvertToGrayscale(bitmap))
            {
                result = reader.Decode(gray);
                if (result != null && IsValidBarcodeContent(result.Text))
                    return result;

                // 策略3: 二值化识别
                foreach (int thresh in new[] { 128, 100, 150 })
                {
                    using (var binary = Binarize(gray, thresh))
                    {
                        result = reader.Decode(binary);
                        if (result != null && IsValidBarcodeContent(result.Text))
                            return result;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 验证条形码内容是否有效
        /// </summary>
        private bool IsValidBarcodeContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            text = text.Trim();

            // 长度检查：大多数条形码长度在8-20之间
            if (text.Length < 8 || text.Length > 20)
                return false;

            // 数字占比：条形码应该主要是数字
            int digitCount = text.Count(c => char.IsDigit(c));
            double digitRatio = (double)digitCount / text.Length;
            if (digitRatio < 0.8)
                return false;

            // 不能全是相同字符
            if (text.Distinct().Count() == 1)
                return false;

            // 不能是简单重复模式
            if (IsSimpleRepeatingPattern(text))
                return false;

            return true;
        }

        /// <summary>
        /// 验证识别结果是否可信
        /// </summary>
        private bool ValidateRecognitionResult(Bitmap bitmap, Result result)
        {
            // 必须有ResultPoints
            if (result.ResultPoints == null || result.ResultPoints.Length < 2)
            {
                System.Diagnostics.Debug.WriteLine("无ResultPoints");
                return false;
            }

            // 计算识别区域
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            int validPoints = 0;

            foreach (var point in result.ResultPoints)
            {
                if (point == null) continue;
                validPoints++;
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            if (validPoints < 2)
                return false;

            float width = maxX - minX;
            float height = maxY - minY;

            // 条形码必须是横向的
            if (width < height)
            {
                System.Diagnostics.Debug.WriteLine($"非横向: W={width}, H={height}");
                return false;
            }

            // 宽度必须足够
            if (width < 60)
            {
                System.Diagnostics.Debug.WriteLine($"宽度过小: {width}");
                return false;
            }

            // 纵横比检查：条形码通常是宽而扁的
            double aspectRatio = width / Math.Max(1, height);
            if (aspectRatio < 1.5 || aspectRatio > 20)
            {
                System.Diagnostics.Debug.WriteLine($"纵横比不符: {aspectRatio}");
                return false;
            }

            // 验证识别区域的条纹特征
            int padding = 5;
            int startX = Math.Max(0, (int)minX - padding);
            int endX = Math.Min(bitmap.Width - 1, (int)maxX + padding);
            int startY = Math.Max(0, (int)minY - padding);
            int endY = Math.Min(bitmap.Height - 1, (int)maxY + padding);

            return VerifyStripePattern(bitmap, startX, endX, startY, endY);
        }

        /// <summary>
        /// 验证指定区域的条纹模式
        /// </summary>
        private bool VerifyStripePattern(Bitmap bitmap, int startX, int endX, int startY, int endY)
        {
            int width = endX - startX + 1;
            int height = endY - startY + 1;

            if (width < 50 || height < 3)
                return false;

            // 二值化
            int[,] binary = new int[height, width];
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    binary[y - startY, x - startX] = gray < 128 ? 0 : 1;
                }
            }

            // 检查每一行的条纹交替次数
            int validRows = 0;
            for (int y = 0; y < height; y++)
            {
                int transitions = 0;
                for (int x = 1; x < width; x++)
                {
                    if (binary[y, x] != binary[y, x - 1])
                        transitions++;
                }

                // 条形码行应该有足够多的黑白交替
                if (transitions >= 12)
                    validRows++;
            }

            double validRatio = (double)validRows / height;
            if (validRatio < 0.8)
            {
                System.Diagnostics.Debug.WriteLine($"有效行比例不足: {validRatio:P0}");
                return false;
            }

            // 检查行间一致性
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
                    if ((double)sameCount / width > 0.7)
                        consistentPairs++;
                }

                double consistency = (double)consistentPairs / (height - 1);
                if (consistency < 0.7)
                {
                    System.Diagnostics.Debug.WriteLine($"行间一致性不足: {consistency:P0}");
                    return false;
                }
            }

            return true;
        }

        private bool IsSimpleRepeatingPattern(string text)
        {
            if (text.Length < 6) return false;

            // 2字符重复
            if (text.Length >= 6 && text.Length % 2 == 0)
            {
                bool repeat = true;
                for (int i = 2; i < text.Length; i += 2)
                {
                    if (text.Substring(i, 2) != text.Substring(0, 2))
                    {
                        repeat = false;
                        break;
                    }
                }
                if (repeat) return true;
            }

            // 3字符重复
            if (text.Length >= 6 && text.Length % 3 == 0)
            {
                bool repeat = true;
                for (int i = 3; i < text.Length; i += 3)
                {
                    if (text.Substring(i, 3) != text.Substring(0, 3))
                    {
                        repeat = false;
                        break;
                    }
                }
                if (repeat) return true;
            }

            return false;
        }

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
            catch { }
        }

        private Bitmap RemoveTopBlankArea(Bitmap original)
        {
            int w = original.Width;
            int h = original.Height;

            int firstNonBlankRow = h;
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

        private Bitmap CropTopRightRegion(Bitmap original, int cropHeight, int cropWidth)
        {
            int w = original.Width;
            int h = original.Height;

            cropHeight = Math.Min(cropHeight, h);
            cropWidth = Math.Min(cropWidth, w);

            var cropped = new Bitmap(cropWidth, cropHeight);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, cropWidth, cropHeight),
                    new Rectangle(w - cropWidth, 0, cropWidth, cropHeight),
                    GraphicsUnit.Pixel);
            }
            return cropped;
        }

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

    public class RecognitionResult
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? Format { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
