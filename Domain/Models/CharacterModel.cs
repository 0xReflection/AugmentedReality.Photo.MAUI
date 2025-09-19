using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class CharacterModel
    {
        public string Name { get; }
        public string ModelPath { get; }
        public string Effect { get; }

        public CharacterModel(string name, string modelPath, string effect)
        {
            Name = name;
            ModelPath = modelPath;
            Effect = effect;
        }
    }
}
