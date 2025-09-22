using Domain.Interfaces;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        public Task<string> SaveAsync(Photo photo)
        {
            var name = Path.GetFileName(photo.FilePath);
            var dest = Path.Combine(FileSystem.AppDataDirectory, name);
            if (!File.Exists(dest)) File.Copy(photo.FilePath, dest, true);
            return Task.FromResult(dest);
        }
    }
}
