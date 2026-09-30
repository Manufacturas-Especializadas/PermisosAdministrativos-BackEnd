namespace PermisosAdministrativos.Application.Common.Models;

public record PagedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);