using Domain.Interfaces;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.UseCases
{
    public class LaunchArUseCase
    {
        private readonly IArService _arService;
        public LaunchArUseCase(IArService arService) => _arService = arService;

        public Task ExecuteAsync(CharacterModel character) =>
            _arService.LaunchArAsync(character);
    }

}
