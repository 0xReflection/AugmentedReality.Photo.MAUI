using Presentation.ViewModel;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Media;
using CommunityToolkit.Maui.ImageSources;
using Microsoft.Maui.Controls;
namespace Presentation.Views;

public partial class MainPage : ContentPage
{
    private bool _useFront = false;
    public MainPage()
	{
		InitializeComponent();
        BindingContext = new MainViewModel();
    }
    //private async void OnCaptureClicked(object sender, EventArgs e)
    //{
    //    var cameraView = _useFront ? FrontCamera : RearCamera;
    //    var vm = (Presentation.ViewModel.MainViewModel)BindingContext;
    //    await vm.CaptureImageAsync(cameraView);
    //}

    //private void OnSwitchCameraClicked(object sender, EventArgs e)
    //{
    //    _useFront = !_useFront;

    //    RearCamera.IsVisible = !_useFront;
    //    FrontCamera.IsVisible = _useFront;
    //}
}