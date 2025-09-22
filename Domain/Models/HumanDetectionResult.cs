using Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
