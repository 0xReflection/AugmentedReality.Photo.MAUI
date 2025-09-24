using SkiaSharp;

namespace Domain.Models
{
    public class Photo
    {
        public SKBitmap Bitmap { get; }
        public string? FilePath { get; set; }

        public Photo(SKBitmap bitmap) => Bitmap = bitmap;
    }
}