using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Domain.Entities;

namespace PermisosAdministrativos.Application.Features.Employees.Common;

public static class EmployeeValidation
{
    public static (string PayrollNumber, string FullName) ValidateFields(
        string? payrollNumber,
        string? fullName)
    {
        if (string.IsNullOrWhiteSpace(payrollNumber))
            throw new ValidationException("El número de nómina es obligatorio.");

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ValidationException("El nombre del empleado es obligatorio.");

        payrollNumber = payrollNumber.Trim();
        fullName = fullName.Trim();

        if (payrollNumber.Length > 30)
            throw new ValidationException("El número de nómina no puede exceder 30 caracteres.");

        if (fullName.Length > 200)
            throw new ValidationException("El nombre no puede exceder 200 caracteres.");

        return (payrollNumber, fullName);
    }

    public static Department ValidateDepartment(Department? department)
    {
        if (department is null)
            throw new NotFoundException("El departamento no existe.");

        if (!department.IsActive)
            throw new ValidationException("El departamento está inactivo.");

        return department;
    }
}
