using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Photo
    {
        public string FilePath { get; }
        public Photo(string filePath) => FilePath = filePath;
    }
}
