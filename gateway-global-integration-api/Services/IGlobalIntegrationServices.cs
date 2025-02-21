namespace GlobalIntegrationApi.Services
{
    public interface IGlobalIntegrationServices
    {
        Task<bool> StopNamedCosumer(string consumerId);

        Task<bool> RestartNamedCosumer(string consumerId);

        Task<bool> PostRsiMessage(RsiPostItem message);

        //Task<List<StatusDto>> GetStatusesAsync();
    }
}