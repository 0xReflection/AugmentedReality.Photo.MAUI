using Domain.Interfaces;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCases.UseCases
{
    public class CapturePhotoUseCase
    {
        private readonly ICameraService _camera;
        private readonly IStorageService _storage;

        public CapturePhotoUseCase(ICameraService camera, IStorageService storage)
        {
            _camera = camera; _storage = storage;
        }

        public async Task<Photo?> ExecuteAsync()
        {
            var photo = await _camera.CaptureAsync();
            if (photo == null) return null;
            await _storage.SaveAsync(photo);
            return photo;
        }
    }
}
