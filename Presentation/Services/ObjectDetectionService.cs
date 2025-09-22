using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;




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
using Xamarin.TensorFlow.Lite;
using Java.Nio;
#endif
using Domain.Models;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public sealed class ObjectDetectionService : IObjectDetectionService, IDisposable
    {
        private readonly ILogger<ObjectDetectionService> _logger;
        private bool _disposed;

#if ANDROID
        private Interpreter _tflite;
        private ByteBuffer _modelBuffer;
        private float[] _reusableInputArray;
#endif

        private readonly string[] _labels;
        private const int InputSize = 224;
        private const float ConfidenceThreshold = 0.6f; 
        private const int NumClasses = 1001;

        public ObjectDetectionService(ILogger<ObjectDetectionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

#if ANDROID
            InitializeTensorFlowLite();
            _labels = LoadLabels();
            _reusableInputArray = new float[InputSize * InputSize * 3];
#else
            _labels = new[] { "person" };
#endif
        }

#if ANDROID
        private void InitializeTensorFlowLite()
        {
            try
            {
                var ctx = Android.App.Application.Context;
                using var assetStream = ctx.Assets.Open("mobilenet_v1_1.0_224.tflite");
                using var ms = new MemoryStream();
                assetStream.CopyTo(ms);

                var modelBytes = ms.ToArray();
                _modelBuffer = ByteBuffer.AllocateDirect(modelBytes.Length);
                _modelBuffer.Order(ByteOrder.NativeOrder());
                _modelBuffer.Put(modelBytes);
                _modelBuffer.Rewind();

                var options = new Interpreter.Options();
                options.SetNumThreads(2);      // многопоток
                options.SetUseNNAPI(false);    // ОТКЛЮЧАЕМ NNAPI

                _tflite = new Interpreter(_modelBuffer, options);
                _logger.LogInformation("MobileNet TensorFlow Lite initialized (CPU mode)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize TensorFlow Lite");
                throw;
            }
        }

        private string[] LoadLabels()
        {
            try
            {
                var ctx = Android.App.Application.Context;
                using var s = ctx.Assets.Open("labelmap.txt");
                using var reader = new StreamReader(s);
                return reader.ReadToEnd()
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .ToArray();
            }
            catch
            {
                return new[] { "person" };
            }
        }
#endif

        public async Task<HumanDetectionResult> DetectPersonAsync(SKBitmap frame, CancellationToken ct = default)
        {
#if ANDROID
            return await Task.Run(() => DetectPersonAndroid(frame, ct), ct);
#else
            await Task.Delay(16, ct);
            return new Random().NextDouble() > 0.5
                ? new HumanDetectionResult(new HumanESP(0.9f), true)
                : HumanDetectionResult.NoPerson;
#endif
        }

#if ANDROID
        private HumanDetectionResult DetectPersonAndroid(SKBitmap bitmap, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var input = PreprocessImage(bitmap);
                return RunInference(input);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in classification");
                return HumanDetectionResult.NoPerson;
            }
        }

        private float[] PreprocessImage(SKBitmap bitmap)
        {
            using var resized = bitmap.Resize(new SKImageInfo(InputSize, InputSize), SKFilterQuality.Low);

            int idx = 0;
            for (int y = 0; y < InputSize; y++)
            {
                for (int x = 0; x < InputSize; x++)
                {
                    var c = resized.GetPixel(x, y);
                    _reusableInputArray[idx++] = c.Red / 127.5f - 1f;
                    _reusableInputArray[idx++] = c.Green / 127.5f - 1f;
                    _reusableInputArray[idx++] = c.Blue / 127.5f - 1f;
                }
            }
            return _reusableInputArray;
        }

        private HumanDetectionResult RunInference(float[] input)
        {
            // Float -> ByteBuffer
            var inputBuffer = ByteBuffer.AllocateDirect(input.Length * sizeof(float));
            inputBuffer.Order(ByteOrder.NativeOrder());
            inputBuffer.AsFloatBuffer().Put(input);
            inputBuffer.Rewind();

            // ByteBuffer для результата
            var outputBuffer = ByteBuffer.AllocateDirect(NumClasses * sizeof(float));
            outputBuffer.Order(ByteOrder.NativeOrder());
            outputBuffer.Rewind();
            _tflite.Run(inputBuffer, outputBuffer);
            // Преобразуем ByteBuffer обратно в float[]
            outputBuffer.Rewind();
            var outputFlat = new float[NumClasses];
            outputBuffer.AsFloatBuffer().Get(outputFlat);
            int personIndex = Array.FindIndex(_labels, l => l.Contains("person", StringComparison.OrdinalIgnoreCase));
            if (personIndex < 0) return HumanDetectionResult.NoPerson;
            float confidence = outputFlat[personIndex];
            if (confidence < ConfidenceThreshold) return HumanDetectionResult.NoPerson;
            for (int i = 0; i < _labels.Length; i++)
                _logger.LogDebug($"{_labels[i]}: {outputFlat[i]:F3}");
            return new HumanDetectionResult(new HumanESP(confidence), true);
        }
#endif

        public void Dispose()
        {
            if (_disposed) return;
#if ANDROID
            _tflite?.Close();
            _tflite?.Dispose();
            _modelBuffer?.Clear();
#endif
            _disposed = true;
        }
    }
}
