using Domain.Interfaces;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            var photo = await _camera.CaptureAsync(ct);
            if (photo == null) return null;

            photo.FilePath = await _storage.SaveAsync(photo);
            return photo;
        }
    }
}