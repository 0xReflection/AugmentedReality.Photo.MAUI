using Domain.Models;

namespace Domain.Interfaces
{
    public interface IRealTimeDetectionService : IDisposable
    {
        event EventHandler<HumanDetectionResult> OnPersonDetected;
        event EventHandler<string> OnDetectionError;
        event EventHandler<string> OnStatusChanged;

        Task StartRealTimeDetectionAsync(CancellationToken ct = default);
        Task StopRealTimeDetectionAsync();

        bool IsDetecting { get; }
        double CurrentFps { get; }
        int TargetFps { get; set; }
        HumanDetectionResult LastDetectionResult { get; }
    }
}