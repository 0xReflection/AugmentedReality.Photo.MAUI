using Presentation.Views;

namespace Presentation
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("Main", typeof(MainPage));

            //this.HomeTab.Icon = ImageSource.FromResource("Presentation.Resources.Images.home.png", this.GetType().Assembly);
            //this.CharacterTab.Icon = ImageSource.FromResource("Presentation.Resources.Images.categories.png", this.GetType().Assembly);
        }
    }
}
