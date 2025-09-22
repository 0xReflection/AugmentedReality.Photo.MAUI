using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
