using Domain.Interfaces;
using Domain.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        public async Task<string> SaveAsync(Photo photo)
        {
            return await SaveAsync(photo.Bitmap);
        }

        public async Task<string> SaveAsync(SKBitmap bitmap)
        {
            var fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmssfff}.png";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            await data.AsStream().CopyToAsync(stream);

            return filePath;
        }
    }
}