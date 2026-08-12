using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Persistence.Seed;

public static class RoleSeedData
{
    public static List<Permission> GetPermissions()
    {
        return Permissions.PermissionDefinitions.Select(p => new Permission(
            p.Key,
            p.Value.Name,
            p.Key,
            p.Value.Category
        )).ToList();
    }

    public static List<Role> GetDefaultRoles()
    {
        return new List<Role>
        {
            new Role("Administrador", "Acesso total ao sistema", isSystemRole: true, isDefault: false),
            new Role("Supervisor", "Supervisão e monitoramento", isSystemRole: false, isDefault: false),
            new Role("Auditor", "Apenas visualização e auditoria", isSystemRole: false, isDefault: false),
            new Role("Operador", "Operações básicas do sistema", isSystemRole: false, isDefault: false),
            new Role("Somente Leitura", "Apenas visualização", isSystemRole: false, isDefault: true),
        };
    }

    public static Dictionary<string, List<string>> GetRolePermissions()
    {
        return new Dictionary<string, List<string>>
        {
            ["Administrador"] = Permissions.GetAll(),

            ["Supervisor"] = new()
            {
                Permissions.DashboardView,
                Permissions.MachinesView,
                Permissions.MachinesEdit,
                Permissions.IncidentsView,
                Permissions.IncidentsCreate,
                Permissions.IncidentsEdit,
                Permissions.IncidentsClose,
                Permissions.TransfersView,
                Permissions.ScreenshotsView,
                Permissions.RemoteView,
                Permissions.RemoteStart,
                Permissions.RemoteStop,
                Permissions.SecurityView,
                Permissions.ReportsView,
                Permissions.ReportsExport,
                Permissions.UsersView,
                Permissions.AuditView,
                Permissions.SettingsView,
            },

            ["Auditor"] = new()
            {
                Permissions.DashboardView,
                Permissions.MachinesView,
                Permissions.IncidentsView,
                Permissions.TransfersView,
                Permissions.ScreenshotsView,
                Permissions.RemoteView,
                Permissions.SecurityView,
                Permissions.ReportsView,
                Permissions.ReportsExport,
                Permissions.UsersView,
                Permissions.RolesView,
                Permissions.AuditView,
                Permissions.SettingsView,
            },

            ["Operador"] = new()
            {
                Permissions.DashboardView,
                Permissions.MachinesView,
                Permissions.IncidentsView,
                Permissions.IncidentsCreate,
                Permissions.IncidentsEdit,
                Permissions.TransfersView,
                Permissions.ScreenshotsView,
                Permissions.RemoteView,
                Permissions.RemoteStart,
                Permissions.RemoteStop,
                Permissions.SecurityView,
                Permissions.ReportsView,
            },

            ["Somente Leitura"] = new()
            {
                Permissions.DashboardView,
                Permissions.MachinesView,
                Permissions.IncidentsView,
                Permissions.TransfersView,
                Permissions.ScreenshotsView,
                Permissions.RemoteView,
                Permissions.SecurityView,
                Permissions.ReportsView,
                Permissions.UsersView,
                Permissions.AuditView,
            },
        };
    }
}
