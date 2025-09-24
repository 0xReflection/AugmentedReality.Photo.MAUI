using Domain.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IStorageService
    {
        Task<string> SaveAsync(Photo photo);
        Task<string> SaveAsync(SKBitmap bitmap);
    }
}