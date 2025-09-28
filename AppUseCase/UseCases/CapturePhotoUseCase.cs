using Domain.Interfaces;
using Domain.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppUseCase.UseCases
{
    public class CapturePhotoUseCase
    {
        private readonly ICameraService _camera;
        private readonly IStorageService _storage;

        public CapturePhotoUseCase(ICameraService camera, IStorageService storage)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task<Photo?> ExecuteAsync(CancellationToken ct = default)
        {
            if (!_camera.IsInitialized)
            {
                throw new InvalidOperationException("Camera service is not initialized");
            }

            var photo = await _camera.CaptureAsync(ct);
            if (photo == null)
            {
                throw new InvalidOperationException("Failed to capture photo");
            }

            try
            {
                photo.FilePath = await _storage.SaveAsync(photo);
                return photo;
            }
            catch
            {
                photo.Dispose();
                throw;
            }
        }
    }
}