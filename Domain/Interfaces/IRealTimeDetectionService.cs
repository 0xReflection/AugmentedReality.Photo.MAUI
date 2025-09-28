using Domain.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public enum DetectionStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }

    public interface IRealTimeDetectionService : IDisposable
    {
        int TargetFps { get; set; }
        double CurrentFps { get; }
        bool IsDetecting { get; }

        event EventHandler<HumanDetectionResult> OnPersonDetected;
        event EventHandler<Exception> OnDetectionError;
        event EventHandler<DetectionStatus> OnStatusChanged;
        event EventHandler<HumanDetectionResult> OnFrameProcessed;

        Task StartRealTimeDetectionAsync(CancellationToken ct = default);
        Task StopRealTimeDetectionAsync();
    }
}
