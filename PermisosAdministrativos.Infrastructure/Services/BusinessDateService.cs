using Microsoft.Extensions.Configuration;
using PermisosAdministrativos.Application.Interfaces;

namespace PermisosAdministrativos.Infrastructure.Services;

public class BusinessDateService : IBusinessDateService
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public BusinessDateService(
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _timeProvider = timeProvider;

        var timeZoneId = configuration["BusinessTimeZone"];

        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new InvalidOperationException(
                "No se configuró la zona horaria del negocio (BusinessTimeZone).");

        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateOnly Today
    {
        get
        {
            var businessTime = TimeZoneInfo.ConvertTime(
                _timeProvider.GetUtcNow(),
                _timeZone);

            return DateOnly.FromDateTime(businessTime.DateTime);
        }
    }
}
