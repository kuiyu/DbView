using DbView.Application.Users.DTOs;

namespace DbView.Application
{
    public interface IUserAppService
    {
        Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    }
}