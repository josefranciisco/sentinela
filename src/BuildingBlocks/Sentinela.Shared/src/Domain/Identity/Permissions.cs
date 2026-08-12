namespace Sentinela.Shared.Domain.Identity;

public static class Permissions
{
    public const string DashboardView = "dashboard.view";
    public const string DashboardEdit = "dashboard.edit";

    public const string MachinesView = "machines.view";
    public const string MachinesEdit = "machines.edit";
    public const string MachinesDelete = "machines.delete";

    public const string IncidentsView = "incidents.view";
    public const string IncidentsCreate = "incidents.create";
    public const string IncidentsEdit = "incidents.edit";
    public const string IncidentsClose = "incidents.close";

    public const string TransfersView = "transfers.view";

    public const string ScreenshotsView = "screenshots.view";
    public const string ScreenshotsDelete = "screenshots.delete";

    public const string RemoteView = "remote.view";
    public const string RemoteStart = "remote.start";
    public const string RemoteStop = "remote.stop";

    public const string SecurityView = "security.view";
    public const string SecurityManage = "security.manage";

    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersEdit = "users.edit";
    public const string UsersDelete = "users.delete";

    public const string RolesView = "roles.view";
    public const string RolesCreate = "roles.create";
    public const string RolesEdit = "roles.edit";
    public const string RolesDelete = "roles.delete";

    public const string AuditView = "audit.view";

    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";

    public const string CompaniesView = "companies.view";
    public const string CompaniesManage = "companies.manage";

    public const string LicensesView = "licenses.view";
    public const string LicensesManage = "licenses.manage";

    public static readonly Dictionary<string, (string Name, string Category)> PermissionDefinitions = new()
    {
        [DashboardView] = ("Visualizar", "Dashboard"),
        [DashboardEdit] = ("Editar", "Dashboard"),

        [MachinesView] = ("Visualizar", "Máquinas"),
        [MachinesEdit] = ("Editar", "Máquinas"),
        [MachinesDelete] = ("Excluir", "Máquinas"),

        [IncidentsView] = ("Visualizar", "Incidentes"),
        [IncidentsCreate] = ("Criar", "Incidentes"),
        [IncidentsEdit] = ("Editar", "Incidentes"),
        [IncidentsClose] = ("Encerrar", "Incidentes"),

        [TransfersView] = ("Visualizar", "Transferência de Dados"),

        [ScreenshotsView] = ("Visualizar", "Capturas"),
        [ScreenshotsDelete] = ("Excluir", "Capturas"),

        [RemoteView] = ("Visualizar", "Acesso Remoto"),
        [RemoteStart] = ("Iniciar", "Acesso Remoto"),
        [RemoteStop] = ("Encerrar", "Acesso Remoto"),

        [SecurityView] = ("Visualizar", "Segurança"),
        [SecurityManage] = ("Gerenciar", "Segurança"),

        [ReportsView] = ("Visualizar", "Relatórios"),
        [ReportsExport] = ("Exportar", "Relatórios"),

        [UsersView] = ("Visualizar", "Usuários"),
        [UsersCreate] = ("Criar", "Usuários"),
        [UsersEdit] = ("Editar", "Usuários"),
        [UsersDelete] = ("Excluir", "Usuários"),

        [RolesView] = ("Visualizar", "Perfis"),
        [RolesCreate] = ("Criar", "Perfis"),
        [RolesEdit] = ("Editar", "Perfis"),
        [RolesDelete] = ("Excluir", "Perfis"),

        [AuditView] = ("Visualizar", "Auditoria"),

        [SettingsView] = ("Visualizar", "Configurações"),
        [SettingsManage] = ("Alterar", "Configurações"),

        [CompaniesView] = ("Visualizar", "Empresas"),
        [CompaniesManage] = ("Gerenciar", "Empresas"),

        [LicensesView] = ("Visualizar", "Licenças"),
        [LicensesManage] = ("Gerenciar", "Licenças"),
    };

    public static List<string> GetAll() => PermissionDefinitions.Keys.ToList();

    public static Dictionary<string, List<string>> GetByCategory()
    {
        return PermissionDefinitions
            .GroupBy(p => p.Value.Category)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => p.Key).ToList()
            );
    }
}
