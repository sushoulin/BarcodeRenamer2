using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ZXing;
using ZXing.Windows.Compatibility;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.Barcode;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 条形码识别服务类
    /// 使用 Emgu.CV WeChatQRCode 深度学习模型检测和解码条形码
    /// </summary>
    public class BarcodeRecognitionService
    {
        private readonly BarcodeReader reader;
        private string cropOutputFolder;
        private WeChatQRCode weChatQRCode;
        private bool detectorInitialized;

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

            // 初始化 WeChatQRCode 检测器
            detectorInitialized = InitializeWeChatQRCode();
        }

        /// <summary>
        /// 初始化 WeChatQRCode 检测器
        /// 使用 wechat_qrcode 文件夹中的模型文件
        /// </summary>
        private bool InitializeWeChatQRCode()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string wechatDir = Path.Combine(baseDir, "wechat_qrcode");

                string detectProto = Path.Combine(wechatDir, "detect.prototxt");
                string detectModel = Path.Combine(wechatDir, "detect.caffemodel");
                string srProto = Path.Combine(wechatDir, "sr.prototxt");
                string srModel = Path.Combine(wechatDir, "sr.caffemodel");

                // 检查模型文件是否存在
                if (!File.Exists(detectProto) || !File.Exists(detectModel))
                {
                    System.Diagnostics.Debug.WriteLine($"WeChatQRCode 检测模型文件不存在: {detectProto}");
                    return false;
                }

                // sr 模型是可选的（用于超分辨率增强）
                if (File.Exists(srProto) && File.Exists(srModel))
                {
                    weChatQRCode = new WeChatQRCode(
                        detectProto, detectModel,
                        srProto, srModel);
                    System.Diagnostics.Debug.WriteLine("WeChatQRCode 初始化成功（包含超分辨率）");
                }
                else
                {
                    // 不使用超分辨率模型
                    weChatQRCode = new WeChatQRCode(detectProto, detectModel, "", "");
                    System.Diagnostics.Debug.WriteLine("WeChatQRCode 初始化成功（不含超分辨率）");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WeChatQRCode 初始化失败: {ex.Message}");
                weChatQRCode = null;
                return false;
            }
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
                    // 方法1: 使用 WeChatQRCode 深度学习检测和解码
                    if (detectorInitialized && weChatQRCode != null)
                    {
                        var result = DetectAndDecodeWithWeChatQRCode(bitmap);
                        if (result != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"WeChatQRCode 识别成功: {result.Content}");
                            return result;
                        }
                    }

                    // 方法2: 回退到 ZXing 直接解码
                    var zxingResult = MultiStrategyRecognition(bitmap);
                    if (zxingResult != null && IsValidBarcodeContent(zxingResult.Text))
                    {
                        System.Diagnostics.Debug.WriteLine($"ZXing 识别成功: {zxingResult.Text}");
                        return CreateSuccessResult(zxingResult);
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
        /// 使用 WeChatQRCode 深度学习模型检测和解码条形码
        /// </summary>
        private RecognitionResult DetectAndDecodeWithWeChatQRCode(Bitmap bitmap)
        {
            try
            {
                // 将 Bitmap 转换为 Mat
                using (var mat = BitmapToMat(bitmap))
                using (var gray = new Mat())
                {
                    // 转换为灰度图
                    CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);

                    // 使用 WeChatQRCode 检测和解码
                    using (var points = new VectorOfVectorOfPointF())
                    {
                        // WeChatQRCode.DetectAndDecode 同时检测位置并解码
                        var results = weChatQRCode.DetectAndDecode(gray, points);

                        if (results != null && results.Length > 0)
                        {
                            // 遍历所有检测到的结果
                            foreach (var decodedString in results)
                            {
                                if (!string.IsNullOrEmpty(decodedString) && IsValidBarcodeContent(decodedString))
                                {
                                    return new RecognitionResult
                                    {
                                        Success = true,
                                        Content = decodedString,
                                        Format = "Unknown"
                                    };
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WeChatQRCode 检测异常: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 将 Bitmap 转换为 Mat (Emgu.CV 格式)
        /// </summary>
        private Mat BitmapToMat(Bitmap bitmap)
        {
            // 确保位图格式正确
            Bitmap bmp = bitmap;
            bool needDispose = false;

            if (bitmap.PixelFormat != PixelFormat.Format24bppRgb &&
                bitmap.PixelFormat != PixelFormat.Format32bppArgb &&
                bitmap.PixelFormat != PixelFormat.Format32bppRgb)
            {
                bmp = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(bitmap, 0, 0);
                }
                needDispose = true;
            }

            try
            {
                // 锁定位图数据
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    bmp.PixelFormat);

                try
                {
                    // 计算通道数
                    int channels = System.Drawing.Bitmap.GetPixelFormatSize(bmp.PixelFormat) / 8;

                    // 创建 Mat
                    var mat = new Mat(bmp.Height, bmp.Width, DepthType.Cv8U, channels, bmpData.Scan0, bmpData.Stride);

                    // 克隆以避免引用被释放的内存
                    var result = mat.Clone();
                    mat.Dispose();
                    return result;
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }
            }
            finally
            {
                if (needDispose && bmp != null)
                {
                    bmp.Dispose();
                }
            }
        }

        /// <summary>
        /// 多策略识别（ZXing）
        /// </summary>
        private Result MultiStrategyRecognition(Bitmap bitmap)
        {
            // 策略1: 原始图像
            var result = reader.Decode(bitmap);
            if (result != null)
                return result;

            // 策略2: 灰度化
            using (var gray = ConvertToGrayscale(bitmap))
            {
                result = reader.Decode(gray);
                if (result != null)
                    return result;

                // 策略3: 二值化
                foreach (int thresh in new[] { 128, 100, 150 })
                {
                    using (var binary = Binarize(gray, thresh))
                    {
                        result = reader.Decode(binary);
                        if (result != null)
                            return result;
                    }
                }
            }

            // 策略4: 放大
            using (var scaled = new Bitmap(bitmap, bitmap.Width * 2, bitmap.Height * 2))
            {
                result = reader.Decode(scaled);
                if (result != null)
                    return result;
            }

            return null;
        }

        private bool IsValidBarcodeContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            text = text.Trim();

            // 长度检查
            if (text.Length < 8 || text.Length > 20)
                return false;

            // 数字占比
            int digitCount = text.Count(c => char.IsDigit(c));
            double digitRatio = (double)digitCount / text.Length;
            if (digitRatio < 0.7)
                return false;

            // 不能全是相同字符
            if (text.Distinct().Count() == 1)
                return false;

            // 不能是简单重复模式
            if (IsSimpleRepeatingPattern(text))
                return false;

            return true;
        }

        private bool IsSimpleRepeatingPattern(string text)
        {
            if (text.Length < 6) return false;

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
