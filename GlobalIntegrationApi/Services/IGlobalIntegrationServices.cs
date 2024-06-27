
using GlobalIntegrationApi.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GlobalIntegrationApi.Services
{
    public interface IGlobalIntegrationServices
    {
        Task<bool> StopNamedCosumer(string consumerId);

        Task<bool> RestartNamedCosumer(string consumerId);

        Task<List<StatusDto>> GetStatusesAsync();
    }
}