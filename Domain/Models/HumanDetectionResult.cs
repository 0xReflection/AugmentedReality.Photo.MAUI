namespace Domain.Models
{
    public sealed record HumanDetectionResult
    {
        public static HumanDetectionResult NoPerson => new(false, 0f);

        public HumanDetectionResult(bool isDetected, float confidence)
        {
            IsDetected = isDetected;
            Confidence = confidence;
        }

        public bool IsDetected { get; init; }
        public float Confidence { get; init; }
    }
}
