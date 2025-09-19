using Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class QrService : IQrService
    {
        public Task<string?> ScanAsync()
        {
            // норм сделать через ZXing
            return Task.FromResult<string?>(null);
        }
    }
}
