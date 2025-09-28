
#if ANDROID
using Domain.Interfaces;
using Domain.Models;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Android.Provider;

namespace Presentation.Services
{
    public sealed class StorageService : IStorageService
    {
        public async Task<string> SaveAsync(Photo photo)
        {
            return await SaveAsync(photo.Bitmap);
        }

        public async Task<string> SaveAsync(SKBitmap bitmap)
        {
            if (bitmap == null || bitmap.IsNull) throw new ArgumentException("Invalid bitmap");

            var context = Android.App.Application.Context;
            var filename = $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            var values = new ContentValues();
            values.Put(MediaStore.MediaColumns.DisplayName, filename);
            values.Put(MediaStore.MediaColumns.MimeType, "image/png");
            values.Put(MediaStore.MediaColumns.RelativePath, Android.OS.Environment.DirectoryPictures);

            var uri = context.ContentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);
            if (uri == null) throw new IOException("Failed to create MediaStore entry");

            using var stream = context.ContentResolver.OpenOutputStream(uri);
            await bitmap.Encode(SKEncodedImageFormat.Png, 100).AsStream().CopyToAsync(stream);
            await stream.FlushAsync();

            return uri.ToString();
        }
    }
}
#endif

