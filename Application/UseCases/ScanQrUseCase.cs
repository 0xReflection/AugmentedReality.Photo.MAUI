using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.UseCases
{
    public class ScanQrUseCase
    {
        public Task<CharacterModel?> ExecuteAsync(string qrValue)
        {
            return Task.FromResult<CharacterModel?>(qrValue switch
            {
                "cheburashka" => new CharacterModel("Чебурашка", "Models/cheb.glb", "apples"),
                "shapoklyak" => new CharacterModel("Шапокляк", "Models/shap.glb", "lariska"),
                "wolf" => new CharacterModel("Волк", "Models/wolf.glb", "moped"),
                _ => null
            });
        }
    }
}
