using Domain.Models;
using SkiaSharp;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IStorageService
    {
        Task<string> SaveAsync(Photo photo);
        Task<string> SaveAsync(SKBitmap bitmap);
    }
}
