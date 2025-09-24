namespace Domain.Models
{
    public sealed class HumanESP
    {
        public float Confidence { get; }

        public HumanESP(float confidence)
        {
            Confidence = confidence;
        }
    }
}