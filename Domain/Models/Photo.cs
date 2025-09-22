using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Photo
    {
        public SKBitmap Bitmap { get; }
        public string? FilePath { get; set; } 

        public Photo(SKBitmap bitmap) => Bitmap = bitmap;
    }
}
