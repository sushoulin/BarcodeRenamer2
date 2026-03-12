using System;
using IronBarCode;

namespace BarcodeRenamer2.Tests
{
    /// <summary>
    /// 使用 IronBarcode 进行条形码识别测试
    /// IronBarcode 是一个商业条形码识别库，识别率远高于 ZXing
    /// 官网: https://ironsoftware.com/csharp/barcode/
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== IronBarcode 条形码识别测试 ===\n");

            var testCases = new[]
            {
                new { File = "test_barcodes/01137590.png", Expected = "40260300081" },
                new { File = "test_barcodes/11264071.png", Expected = "40260300085" }
            };

            foreach (var test in testCases)
            {
                Console.WriteLine($"测试文件: {test.File}");
                Console.WriteLine($"预期结果: {test.Expected}");

                try
                {
                    // IronBarcode 基本识别
                    var result = BarcodeReader.Read(test.File);

                    if (result != null && result.Count > 0)
                    {
                        foreach (var barcode in result)
                        {
                            Console.WriteLine($"✅ 识别成功: {barcode.Value}");
                            Console.WriteLine($"   格式: {barcode.BarcodeType}");
                            Console.WriteLine($"   匹配: {(barcode.Value == test.Expected ? "✅ 正确" : "❌ 错误")}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ 基本识别失败: 未找到条形码");

                        // 尝试更激进的识别 - 使用正确的 API
                        Console.WriteLine("\n尝试激进识别参数...");

                        // 使用 BarcodeReaderOptions 配置参数
                        var options = new BarcodeReaderOptions
                        {
                            Speed = ReadingSpeed.Detailed,
                            ExpectMultipleBarcodes = false,
                            TryHarder = true,
                            TryInverted = true
                        };

                        var aggressiveResult = BarcodeReader.Read(test.File, options);

                        if (aggressiveResult != null && aggressiveResult.Count > 0)
                        {
                            foreach (var barcode in aggressiveResult)
                            {
                                Console.WriteLine($"✅ 激进识别成功: {barcode.Value}");
                                Console.WriteLine($"   格式: {barcode.BarcodeType}");
                                Console.WriteLine($"   匹配: {(barcode.Value == test.Expected ? "✅ 正确" : "❌ 错误")}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"❌ 激进识别也失败");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 错误: {ex.Message}");
                    Console.WriteLine($"堆栈: {ex.StackTrace}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("=== 测试完成 ===");
            Console.ReadKey();
        }
    }
}
