namespace Domain.Models
{
    public record HumanDetectionResult(
        HumanESP? Human,
        bool HasPerson
    )
    {
        public static HumanDetectionResult NoPerson =>
            new HumanDetectionResult(null, false);
    }
}