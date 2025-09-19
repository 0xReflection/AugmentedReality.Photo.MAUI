using Domain.Interfaces;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ArService : IArService
    {
        public Task LaunchArAsync(CharacterModel character)
        {
#if ANDROID
            var intent = new Android.Content.Intent(Android.App.Application.Context, 
                typeof(ArActivity));
            intent.PutExtra("model", character.ModelPath);
            intent.PutExtra("effect", character.Effect);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
#endif
            return Task.CompletedTask;
        }
    }
}
