#if ANDROID
using Android.OS;
using Android.Content;
using Android.Hardware.Camera2;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Presentation.Services
{
    public static class CameraAccessManager
    {
        private static readonly SemaphoreSlim _cameraLock = new SemaphoreSlim(1, 1);
        private static string _currentCameraUser;

        public static bool IsCameraInUse => _currentCameraUser != null;

        public static async Task<bool> AcquireExclusiveAccess(string requester, int timeoutMs = 8000)
        {
            if (Android.OS.Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
                return false;

            if (!await _cameraLock.WaitAsync(timeoutMs))
                return false;

            _currentCameraUser = requester;

         
            await Task.Delay(100);
            return true;
        }

        public static void ReleaseExclusiveAccess(string requester)
        {
            if (_currentCameraUser == requester)
            {
                _currentCameraUser = null;
                _cameraLock.Release();
            }
        }

        public static bool IsCamera2Supported(Context context)
        {
            if (Android.OS.Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
                return false;

            try
            {
                var cameraManager = context.GetSystemService(Context.CameraService) as CameraManager;
                return cameraManager?.GetCameraIdList()?.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif