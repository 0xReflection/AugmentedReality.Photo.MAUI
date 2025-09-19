using CommunityToolkit.Maui.Media;
using Domain.Interfaces;
using Domain.Models;

namespace Infrastructure.Services
{
    public class CameraService : ICameraService
    {
        public async Task<Photo?> CaptureAsync()
        {
            var file = await MediaPicker.CapturePhotoAsync();
            return file == null ? null : new Photo(file.FullPath);
        }
    }
}


