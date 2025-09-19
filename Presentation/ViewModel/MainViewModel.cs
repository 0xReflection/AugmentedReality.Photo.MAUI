using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Presentation.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        //private readonly CapturePhotoUseCase _capture;

        //[ObservableProperty] private ImageSource photo;

        //public MainViewModel(CapturePhotoUseCase capture) => _capture = capture;

        //[RelayCommand]
        //private async Task CaptureAsync()
        //{
        //    Photo? result = await _capture.ExecuteAsync();
        //    if (result == null) return;
        //    using var stream = File.OpenRead(result.FilePath);
        //    Photo = ImageSource.FromStream(() => stream);
        //}
    }
}
