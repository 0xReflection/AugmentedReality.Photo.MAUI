using Domain.Interfaces;
using Domain.Models;
using Java.Nio;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.TensorFlow.Lite;
using Android.Runtime;

namespace Presentation.Platforms.Android.Services
{
    public sealed class ObjectDetectionService : IObjectDetectionService, IDisposable
    {
        private readonly ILogger<ObjectDetectionService> _logger;
        private IntPtr _interpreterHandle = IntPtr.Zero;
        private IntPtr _modelHandle = IntPtr.Zero;
        private readonly string[] _labels;
        private bool _disposed;

        private const int InputSize = 300;
        private const float ConfidenceThreshold = 0.6f;
        private const int NumDetections = 10;

        // TensorFlow Lite Native Methods
        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteModelCreate")]
        private static extern IntPtr TfLiteModelCreate(byte[] model_data, int model_size);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteModelDelete")]
        private static extern void TfLiteModelDelete(IntPtr model);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterOptionsCreate")]
        private static extern IntPtr TfLiteInterpreterOptionsCreate();

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterOptionsDelete")]
        private static extern void TfLiteInterpreterOptionsDelete(IntPtr options);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterCreate")]
        private static extern IntPtr TfLiteInterpreterCreate(IntPtr model, IntPtr options);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterDelete")]
        private static extern void TfLiteInterpreterDelete(IntPtr interpreter);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterAllocateTensors")]
        private static extern int TfLiteInterpreterAllocateTensors(IntPtr interpreter);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterInvoke")]
        private static extern int TfLiteInterpreterInvoke(IntPtr interpreter);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterGetInputTensor")]
        private static extern IntPtr TfLiteInterpreterGetInputTensor(IntPtr interpreter, int input_index);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteInterpreterGetOutputTensor")]
        private static extern IntPtr TfLiteInterpreterGetOutputTensor(IntPtr interpreter, int output_index);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteTensorData")]
        private static extern IntPtr TfLiteTensorData(IntPtr tensor);

        [DllImport("libtensorflowlite_jni.so", EntryPoint = "TfLiteTensorByteSize")]
        private static extern int TfLiteTensorByteSize(IntPtr tensor);

        public ObjectDetectionService(ILogger<ObjectDetectionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                _logger.LogInformation("Initializing TensorFlow Lite with real model...");

                // Load model from Android assets
                LoadModel();

                // Load labels
                _labels = LoadLabels();
                _logger.LogInformation($"Loaded {_labels.Length} labels for object detection");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize TensorFlow Lite interpreter");
                throw new InvalidOperationException("Failed to initialize object detection service", ex);
            }
        }

        private unsafe void LoadModel()
        {
            try
            {
                // Загрузка модели из embedded resources MAUI
                var assembly = GetType().Assembly;
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("detect.tflite"));

                if (string.IsNullOrEmpty(resourceName))
                    throw new FileNotFoundException("Model file detect.tflite not found in embedded resources");

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new FileNotFoundException("Failed to open model stream");

                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                var modelBytes = memoryStream.ToArray();

                fixed (byte* modelPtr = modelBytes)
                {
                    _modelHandle = TfLiteModelCreate(modelBytes, modelBytes.Length);
                    if (_modelHandle == IntPtr.Zero)
                        throw new InvalidOperationException("Failed to create TFLite model");
                }

                // Create interpreter options
                var options = TfLiteInterpreterOptionsCreate();
                if (options == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create interpreter options");

                try
                {
                    // Create interpreter
                    _interpreterHandle = TfLiteInterpreterCreate(_modelHandle, options);
                    if (_interpreterHandle == IntPtr.Zero)
                        throw new InvalidOperationException("Failed to create interpreter");

                    // Allocate tensors
                    var allocateResult = TfLiteInterpreterAllocateTensors(_interpreterHandle);
                    if (allocateResult != 0)
                        throw new InvalidOperationException("Failed to allocate tensors");
                }
                finally
                {
                    TfLiteInterpreterOptionsDelete(options);
                }

                _logger.LogInformation("TensorFlow Lite model loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load TensorFlow Lite model");
                Cleanup();
                throw;
            }
        }

        private string[] LoadLabels()
        {
            try
            {
                // Загрузка меток из embedded resources MAUI
                var assembly = GetType().Assembly;
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("labelmap.txt"));

                if (!string.IsNullOrEmpty(resourceName))
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        return reader.ReadToEnd()
                            .Split('\n')
                            .Select(label => label.Trim())
                            .Where(label => !string.IsNullOrEmpty(label))
                            .ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load labels file labelmap.txt");
            }

            // Fallback labels
            return new string[] { "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light" };
        }


        public async Task<HumanDetectionResult> DetectPersonAsync(Stream imageStream, CancellationToken ct = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ObjectDetectionService));
            if (imageStream == null) throw new ArgumentNullException(nameof(imageStream));

            try
            {
                return await Task.Run(() => DetectPersonInternal(imageStream, ct), ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Object detection operation was cancelled");
                return HumanDetectionResult.NoPerson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during object detection");
                return HumanDetectionResult.NoPerson;
            }
        }

        private HumanDetectionResult DetectPersonInternal(Stream imageStream, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            using var skBitmap = SKBitmap.Decode(imageStream);
            if (skBitmap == null)
            {
                _logger.LogWarning("Failed to decode image stream");
                return HumanDetectionResult.NoPerson;
            }

            // Preprocess image
            var inputTensor = PreprocessImage(skBitmap);
            if (inputTensor == null)
            {
                return HumanDetectionResult.NoPerson;
            }

            // Run inference
            var detectionResult = RunInference(inputTensor, skBitmap.Width, skBitmap.Height, ct);
            return detectionResult;
        }

        private float[] PreprocessImage(SKBitmap bitmap)
        {
            try
            {
                using var resized = bitmap.Resize(new SKImageInfo(InputSize, InputSize), SKFilterQuality.High);
                if (resized == null) return null;

                var input = new float[InputSize * InputSize * 3];
                int index = 0;

                for (int y = 0; y < InputSize; y++)
                {
                    for (int x = 0; x < InputSize; x++)
                    {
                        var color = resized.GetPixel(x, y);
                        // Normalize for MobileNet: [-1, 1]
                        input[index++] = (color.Red / 127.5f) - 1.0f;
                        input[index++] = (color.Green / 127.5f) - 1.0f;
                        input[index++] = (color.Blue / 127.5f) - 1.0f;
                    }
                }

                return input;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to preprocess image");
                return null;
            }
        }

        private unsafe HumanDetectionResult RunInference(float[] input, int originalWidth, int originalHeight, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Get input tensor
                var inputTensor = TfLiteInterpreterGetInputTensor(_interpreterHandle, 0);
                if (inputTensor == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to get input tensor");

                // Copy input data to tensor
                var tensorData = TfLiteTensorData(inputTensor);
                var tensorByteSize = TfLiteTensorByteSize(inputTensor);

                // Копируем данные напрямую через Marshal
                var handle = GCHandle.Alloc(input, GCHandleType.Pinned);
                try
                {
                    System.Buffer.MemoryCopy(
                        handle.AddrOfPinnedObject().ToPointer(),
                        tensorData.ToPointer(),
                        tensorByteSize,
                        input.Length * sizeof(float)
                    );
                }
                finally
                {
                    handle.Free();
                }

                // Run inference
                var invokeResult = TfLiteInterpreterInvoke(_interpreterHandle);
                if (invokeResult != 0)
                    throw new InvalidOperationException("Inference failed");

                // Get output tensors - исправленный метод
                var outputLocations = GetOutputTensorData<float>(0);
                var outputClasses = GetOutputTensorData<float>(1);
                var outputScores = GetOutputTensorData<float>(2);
                var numDetections = GetOutputTensorData<float>(3);

                var detectionsCount = Math.Min((int)numDetections[0], NumDetections);

                for (int i = 0; i < detectionsCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var confidence = outputScores[i];
                    if (confidence < ConfidenceThreshold) continue;

                    var classId = (int)outputClasses[i];
                    if (classId >= _labels.Length) continue;

                    var label = _labels[classId];
                    if (!label.Contains("person", StringComparison.OrdinalIgnoreCase)) continue;

                    // Denormalize coordinates
                    var ymin = outputLocations[i * 4 + 0] * originalHeight;
                    var xmin = outputLocations[i * 4 + 1] * originalWidth;
                    var ymax = outputLocations[i * 4 + 2] * originalHeight;
                    var xmax = outputLocations[i * 4 + 3] * originalWidth;

                    // Validate coordinates
                    xmin = Math.Max(0, Math.Min(xmin, originalWidth));
                    ymin = Math.Max(0, Math.Min(ymin, originalHeight));
                    xmax = Math.Max(0, Math.Min(xmax, originalWidth));
                    ymax = Math.Max(0, Math.Min(ymax, originalHeight));

                    var width = xmax - xmin;
                    var height = ymax - ymin;

                    if (width <= 0 || height <= 0) continue;

                    _logger.LogDebug($"Detected person with confidence: {confidence:F2}");

                    var human = new HumanESP(
                        (float)xmin,
                        (float)ymin,
                        (float)width,
                        (float)height,
                        (float)confidence
                    );

                    return new HumanDetectionResult(human, new Domain.Interfaces.Rect(0, 0, 0, 0), true);
                }

                return HumanDetectionResult.NoPerson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run inference");
                return HumanDetectionResult.NoPerson;
            }
        }

        private unsafe T[] GetOutputTensorData<T>(int outputIndex) where T : struct
        {
            var tensor = TfLiteInterpreterGetOutputTensor(_interpreterHandle, outputIndex);
            if (tensor == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to get output tensor {outputIndex}");

            var dataPtr = TfLiteTensorData(tensor);
            var byteSize = TfLiteTensorByteSize(tensor);
            var elementSize = Marshal.SizeOf<T>();
            var elementCount = byteSize / elementSize;

            var result = new T[elementCount];

            // Копируем данные напрямую через Marshal
            var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
            try
            {
                System.Buffer.MemoryCopy(
                    dataPtr.ToPointer(),
                    handle.AddrOfPinnedObject().ToPointer(),
                    byteSize,
                    byteSize
                );
            }
            finally
            {
                handle.Free();
            }

            return result;
        }

        private void Cleanup()
        {
            if (_interpreterHandle != IntPtr.Zero)
            {
                TfLiteInterpreterDelete(_interpreterHandle);
                _interpreterHandle = IntPtr.Zero;
            }

            if (_modelHandle != IntPtr.Zero)
            {
                TfLiteModelDelete(_modelHandle);
                _modelHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            Cleanup();
            _disposed = true;
            _logger.LogInformation("RealObjectDetectionService disposed");
        }
    }
}

