





//namespace Presentation
//{
//    public sealed class ObjectDetectionService : IObjectDetectionService, IDisposable
//    {
//        private readonly ILogger<ObjectDetectionService> _logger;
//        private IntPtr _interpreterHandle = IntPtr.Zero;
//        private IntPtr _modelHandle = IntPtr.Zero;
//        private readonly string[] _labels;
//        private bool _disposed;

//        private const int InputSize = 300;
//        private const float ConfidenceThreshold = 0.6f;
//        private const int NumDetections = 10;

//        // TensorFlow Lite Native Methods for Android
//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteModelCreate")]
//        private static extern IntPtr TfLiteModelCreate(byte[] model_data, int model_size);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteModelDelete")]
//        private static extern void TfLiteModelDelete(IntPtr model);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterOptionsCreate")]
//        private static extern IntPtr TfLiteInterpreterOptionsCreate();

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterOptionsDelete")]
//        private static extern void TfLiteInterpreterOptionsDelete(IntPtr options);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterCreate")]
//        private static extern IntPtr TfLiteInterpreterCreate(IntPtr model, IntPtr options);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterDelete")]
//        private static extern void TfLiteInterpreterDelete(IntPtr interpreter);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterAllocateTensors")]
//        private static extern int TfLiteInterpreterAllocateTensors(IntPtr interpreter);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterInvoke")]
//        private static extern int TfLiteInterpreterInvoke(IntPtr interpreter);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterGetInputTensor")]
//        private static extern IntPtr TfLiteInterpreterGetInputTensor(IntPtr interpreter, int input_index);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterGetOutputTensor")]
//        private static extern IntPtr TfLiteInterpreterGetOutputTensor(IntPtr interpreter, int output_index);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteTensorData")]
//        private static extern IntPtr TfLiteTensorData(IntPtr tensor);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteTensorByteSize")]
//        private static extern int TfLiteTensorByteSize(IntPtr tensor);

//        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteTensorType")]
//        private static extern int TfLiteTensorType(IntPtr tensor);

//        public ObjectDetectionService(ILogger<ObjectDetectionService> logger)
//        {
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

//#if ANDROID
//            try
//            {
//                _logger.LogInformation("Initializing TensorFlow Lite for Android...");
//                LoadModelFromAndroidAssets();
//                _labels = LoadLabelsFromAndroidAssets();
//                _logger.LogInformation($"Loaded {_labels.Length} labels for object detection");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to initialize TensorFlow Lite interpreter");
//                throw new InvalidOperationException("Failed to initialize object detection service", ex);
//            }
//#else
//            _logger.LogInformation("Object detection service created for non-Android platform");
//            _labels = new string[] { "person" };
//#endif
//        }

//#if ANDROID
//        private void LoadModelFromAndroidAssets()
//        {
//            try
//            {
//                var context = Android.App.Application.Context;
//                using var stream = context.Assets?.Open("detect.tflite");
//                if (stream == null)
//                    throw new FileNotFoundException("Model file detect.tflite not found in Android assets");

//                using var memoryStream = new MemoryStream();
//                stream.CopyTo(memoryStream);
//                var modelBytes = memoryStream.ToArray();

//                _modelHandle = TfLiteModelCreate(modelBytes, modelBytes.Length);
//                if (_modelHandle == IntPtr.Zero)
//                    throw new InvalidOperationException("Failed to create TFLite model");

//                var options = TfLiteInterpreterOptionsCreate();
//                if (options == IntPtr.Zero)
//                    throw new InvalidOperationException("Failed to create interpreter options");

//                try
//                {
//                    _interpreterHandle = TfLiteInterpreterCreate(_modelHandle, options);
//                    if (_interpreterHandle == IntPtr.Zero)
//                        throw new InvalidOperationException("Failed to create interpreter");

//                    var allocateResult = TfLiteInterpreterAllocateTensors(_interpreterHandle);
//                    if (allocateResult != 0)
//                        throw new InvalidOperationException("Failed to allocate tensors");

//                    _logger.LogInformation("TensorFlow Lite model loaded successfully");
//                }
//                finally
//                {
//                    TfLiteInterpreterOptionsDelete(options);
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to load TensorFlow Lite model");
//                Cleanup();
//                throw;
//            }
//        }

//        private string[] LoadLabelsFromAndroidAssets()
//        {
//            try
//            {
//                var context = Android.App.Application.Context;
//                using var stream = context.Assets?.Open("labelmap.txt");
//                if (stream != null)
//                {
//                    using var reader = new StreamReader(stream);
//                    return reader.ReadToEnd()
//                        .Split('\n')
//                        .Select(label => label.Trim())
//                        .Where(label => !string.IsNullOrEmpty(label))
//                        .ToArray();
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to load labels file");
//            }

//            return new string[] { "person" };
//        }

//        private unsafe HumanDetectionResult DetectPersonAndroid(Stream imageStream, CancellationToken ct)
//        {
//            ct.ThrowIfCancellationRequested();

//            using var skBitmap = SKBitmap.Decode(imageStream);
//            if (skBitmap == null)
//            {
//                _logger.LogWarning("Failed to decode image stream");
//                return HumanDetectionResult.NoPerson;
//            }

//            var input = PreprocessImage(skBitmap);
//            if (input == null)
//            {
//                return HumanDetectionResult.NoPerson;
//            }

//            var detectionResult = RunInferenceAndroid(input, skBitmap.Width, skBitmap.Height, ct);
//            return detectionResult;
//        }

//        private unsafe float[] PreprocessImage(SKBitmap bitmap)
//        {
//            try
//            {
//                using var resized = bitmap.Resize(new SKImageInfo(InputSize, InputSize), SKFilterQuality.High);
//                if (resized == null) return null;

//                var input = new float[InputSize * InputSize * 3];
//                int index = 0;

//                for (int y = 0; y < InputSize; y++)
//                {
//                    for (int x = 0; x < InputSize; x++)
//                    {
//                        var color = resized.GetPixel(x, y);
//                        input[index++] = (color.Red / 127.5f) - 1.0f;
//                        input[index++] = (color.Green / 127.5f) - 1.0f;
//                        input[index++] = (color.Blue / 127.5f) - 1.0f;
//                    }
//                }

//                return input;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to preprocess image");
//                return null;
//            }
//        }

//        private unsafe HumanDetectionResult RunInferenceAndroid(float[] input, int originalWidth, int originalHeight, CancellationToken ct)
//        {
//            ct.ThrowIfCancellationRequested();

//            try
//            {
//                var inputTensor = TfLiteInterpreterGetInputTensor(_interpreterHandle, 0);
//                if (inputTensor == IntPtr.Zero)
//                    throw new InvalidOperationException("Failed to get input tensor");

//                var tensorData = TfLiteTensorData(inputTensor);
//                var tensorByteSize = TfLiteTensorByteSize(inputTensor);

//                var handle = GCHandle.Alloc(input, GCHandleType.Pinned);
//                try
//                {
//                    Buffer.MemoryCopy(
//                        handle.AddrOfPinnedObject().ToPointer(),
//                        tensorData.ToPointer(),
//                        tensorByteSize,
//                        input.Length * sizeof(float)
//                    );
//                }
//                finally
//                {
//                    handle.Free();
//                }

//                var invokeResult = TfLiteInterpreterInvoke(_interpreterHandle);
//                if (invokeResult != 0)
//                    throw new InvalidOperationException("Inference failed");

//                var outputLocations = GetOutputTensorData<float>(0);
//                var outputClasses = GetOutputTensorData<float>(1);
//                var outputScores = GetOutputTensorData<float>(2);
//                var numDetections = GetOutputTensorData<float>(3);

//                var detectionsCount = Math.Min((int)numDetections[0], NumDetections);

//                for (int i = 0; i < detectionsCount; i++)
//                {
//                    ct.ThrowIfCancellationRequested();

//                    var confidence = outputScores[i];
//                    if (confidence < ConfidenceThreshold) continue;

//                    var classId = (int)outputClasses[i];
//                    if (classId >= _labels.Length) continue;

//                    var label = _labels[classId];
//                    if (!label.Contains("person", StringComparison.OrdinalIgnoreCase)) continue;

//                    var ymin = outputLocations[i * 4 + 0] * originalHeight;
//                    var xmin = outputLocations[i * 4 + 1] * originalWidth;
//                    var ymax = outputLocations[i * 4 + 2] * originalHeight;
//                    var xmax = outputLocations[i * 4 + 3] * originalWidth;

//                    xmin = Math.Max(0, Math.Min(xmin, originalWidth));
//                    ymin = Math.Max(0, Math.Min(ymin, originalHeight));
//                    xmax = Math.Max(0, Math.Min(xmax, originalWidth));
//                    ymax = Math.Max(0, Math.Min(ymax, originalHeight));

//                    var width = xmax - xmin;
//                    var height = ymax - ymin;

//                    if (width <= 0 || height <= 0) continue;

//                    _logger.LogDebug($"Detected person with confidence: {confidence:F2}");

//                    var human = new HumanESP(
//                        (float)xmin,
//                        (float)ymin,
//                        (float)width,
//                        (float)height,
//                        (float)confidence
//                    );

//                    return new HumanDetectionResult(human, new Domain.Interfaces.Rect(0, 0, 0, 0), true);
//                }

//                return HumanDetectionResult.NoPerson;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to run inference");
//                return HumanDetectionResult.NoPerson;
//            }
//        }

//        private unsafe T[] GetOutputTensorData<T>(int outputIndex) where T : unmanaged
//        {
//            var tensor = TfLiteInterpreterGetOutputTensor(_interpreterHandle, outputIndex);
//            if (tensor == IntPtr.Zero)
//                throw new InvalidOperationException($"Failed to get output tensor {outputIndex}");

//            var dataPtr = TfLiteTensorData(tensor);
//            var byteSize = TfLiteTensorByteSize(tensor);
//            var elementSize = sizeof(T);
//            var elementCount = byteSize / elementSize;

//            var result = new T[elementCount];

//            fixed (T* resultPtr = result)
//            {
//                Buffer.MemoryCopy(
//                    dataPtr.ToPointer(),
//                    resultPtr,
//                    byteSize,
//                    byteSize
//                );
//            }

//            return result;
//        }
//#endif

//        public async Task<HumanDetectionResult> DetectPersonAsync(Stream imageStream, CancellationToken ct = default)
//        {
//            if (_disposed) throw new ObjectDisposedException(nameof(ObjectDetectionService));
//            if (imageStream == null) throw new ArgumentNullException(nameof(imageStream));

//#if ANDROID
//            try
//            {
//                return await Task.Run(() => DetectPersonAndroid(imageStream, ct), ct);
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.LogWarning("Object detection operation was cancelled");
//                return HumanDetectionResult.NoPerson;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error during object detection on Android");
//                return HumanDetectionResult.NoPerson;
//            }
//#else
//            return await DetectPersonWindowsAsync(imageStream, ct);
//#endif
//        }

//#if !ANDROID
//        private async Task<HumanDetectionResult> DetectPersonWindowsAsync(Stream imageStream, CancellationToken ct)
//        {
//            await Task.Delay(100, ct); // Имитация обработки

//            try
//            {
//                using var skBitmap = SKBitmap.Decode(imageStream);
//                if (skBitmap == null)
//                {
//                    _logger.LogWarning("Failed to decode image stream on Windows");
//                    return HumanDetectionResult.NoPerson;
//                }

//                
//                var random = new Random();
//                if (random.NextDouble() > 0.7) // 30% шанс обнаружить человека
//                {
//                    var width = skBitmap.Width;
//                    var height = skBitmap.Height;

//                   
//                    var xmin = width * 0.2f + (float)random.NextDouble() * width * 0.6f;
//                    var ymin = height * 0.2f + (float)random.NextDouble() * height * 0.6f;
//                    var bboxWidth = width * 0.1f + (float)random.NextDouble() * width * 0.3f;
//                    var bboxHeight = height * 0.2f + (float)random.NextDouble() * height * 0.4f;
//                    var confidence = 0.7f + (float)random.NextDouble() * 0.3f;

//                    var human = new HumanESP(xmin, ymin, bboxWidth, bboxHeight, confidence);
//                    _logger.LogDebug($"Simulated person detection on Windows with confidence: {confidence:F2}");

//                    return new HumanDetectionResult(human, new Domain.Interfaces.Rect(0, 0, 0, 0), true);
//                }

//                return HumanDetectionResult.NoPerson;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error during simulated detection on Windows");
//                return HumanDetectionResult.NoPerson;
//            }
//        }
//#endif

//        private void Cleanup()
//        {
//            if (_interpreterHandle != IntPtr.Zero)
//            {
//                TfLiteInterpreterDelete(_interpreterHandle);
//                _interpreterHandle = IntPtr.Zero;
//            }

//            if (_modelHandle != IntPtr.Zero)
//            {
//                TfLiteModelDelete(_modelHandle);
//                _modelHandle = IntPtr.Zero;
//            }
//        }

//        public void Dispose()
//        {
//            if (_disposed) return;

//#if ANDROID
//            Cleanup();
//#endif
//            _disposed = true;
//            _logger.LogInformation("ObjectDetectionService disposed");
//        }
//    }
//}
#if ANDROID
using Android.App;
using Domain.Interfaces;
using Domain.Models;
using Java.Nio;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.TensorFlow.Lite;

namespace Presentation.Services
{
    public sealed class ObjectDetectionService : IObjectDetectionService
    {
        private const int INPUT_SIZE = 224;
        private const int CHANNELS = 3;
        private const float CONFIDENCE_THRESHOLD = 0.6f;

        private Interpreter _interpreter;
        private string[] _labels;
        private bool _isInitialized;
        private readonly ILogger<ObjectDetectionService> _logger;

        private ByteBuffer _inputBuffer;
        private FloatBuffer _outputBuffer;

        public bool IsInitialized => _isInitialized;
        public event EventHandler<Exception> OnError;

        public ObjectDetectionService(ILogger<ObjectDetectionService> logger = null)
        {
            _logger = logger;
        }

        public async Task<bool> InitializeAsync(CancellationToken ct = default)
        {
            if (_isInitialized) return true;

            try
            {
                using var stream = Android.App.Application.Context.Assets.Open("mobilenet_v1_1.0_224.tflite");
                byte[] modelData = new byte[stream.Length];
                await stream.ReadAsync(modelData, 0, modelData.Length);

                var bb = ByteBuffer.AllocateDirect(modelData.Length);
                bb.Put(modelData);
                bb.Rewind();

                var options = new Interpreter.Options();
                options.SetNumThreads(Java.Lang.Runtime.GetRuntime().AvailableProcessors());

                _interpreter = new Interpreter(bb, options);

                using var sr = new StreamReader(Android.App.Application.Context.Assets.Open("labels.txt"));
                _labels = (await sr.ReadToEndAsync())
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .ToArray();

                _inputBuffer = ByteBuffer.AllocateDirect(INPUT_SIZE * INPUT_SIZE * CHANNELS * sizeof(float));
                _inputBuffer.Order(ByteOrder.NativeOrder());

                _outputBuffer = ByteBuffer.AllocateDirect(_labels.Length * sizeof(float)).AsFloatBuffer();

                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ObjectDetection init failed");
                OnError?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<HumanDetectionResult> DetectAsync(SKBitmap frame, CancellationToken ct = default)
        {
            if (!_isInitialized) throw new InvalidOperationException("Service not initialized");
            if (frame == null || frame.IsNull) return HumanDetectionResult.NoPerson;

            return await Task.Run(() =>
            {
                try
                {
                    using var resized = frame.Resize(new SKImageInfo(INPUT_SIZE, INPUT_SIZE), SKFilterQuality.Medium);
                    if (resized == null) return HumanDetectionResult.NoPerson;

                    _inputBuffer.Clear();
                    foreach (var c in resized.Pixels)
                    {
                        _inputBuffer.PutFloat(c.Red / 127.5f - 1f);
                        _inputBuffer.PutFloat(c.Green / 127.5f - 1f);
                        _inputBuffer.PutFloat(c.Blue / 127.5f - 1f);
                    }

                    _outputBuffer.Clear();
                    _interpreter.Run(_inputBuffer, _outputBuffer);

                    float confidence = 0f;
                    int personIdx = Array.FindIndex(_labels, x => x.ToLowerInvariant().Contains("person") || x.ToLowerInvariant().Contains("human"));
                    if (personIdx >= 0) confidence = _outputBuffer.Get(personIdx);

                    return confidence >= CONFIDENCE_THRESHOLD
                        ? new HumanDetectionResult(true, confidence)
                        : HumanDetectionResult.NoPerson;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Inference error");
                    OnError?.Invoke(this, ex);
                    return HumanDetectionResult.NoPerson;
                }
            }, ct);
        }

        public void Dispose()
        {
            try
            {
                _interpreter?.Close();
                _interpreter?.Dispose();
                _inputBuffer = null;
                _outputBuffer = null;
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Dispose error");
            }
        }
    }
}
#endif
