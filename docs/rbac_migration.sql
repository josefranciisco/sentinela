-- RBAC Migration for Sentinela
-- Run this script against the sentinela database

-- ============================================================
-- 1. Create permissions table
-- ============================================================
CREATE TABLE IF NOT EXISTS permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(100) NOT NULL,
    name VARCHAR(200) NOT NULL,
    description VARCHAR(500),
    category VARCHAR(100) NOT NULL,
    tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_permissions_code ON permissions(code);
CREATE INDEX IF NOT EXISTS ix_permissions_category ON permissions(category);

-- ============================================================
-- 2. Create app_roles table (using app_ prefix to avoid conflict with ASP.NET Identity)
-- ============================================================
CREATE TABLE IF NOT EXISTS app_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    description VARCHAR(500),
    is_system_role BOOLEAN NOT NULL DEFAULT FALSE,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    tenant_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_app_roles_tenant_name ON app_roles(tenant_id, name) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS ix_app_roles_tenant_id ON app_roles(tenant_id);

-- ============================================================
-- 3. Create app_role_permissions table
-- ============================================================
CREATE TABLE IF NOT EXISTS app_role_permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    role_id UUID NOT NULL REFERENCES app_roles(id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_app_role_permissions_role_permission ON app_role_permissions(role_id, permission_id) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS ix_app_role_permissions_role_id ON app_role_permissions(role_id);
CREATE INDEX IF NOT EXISTS ix_app_role_permissions_permission_id ON app_role_permissions(permission_id);

-- ============================================================
-- 4. Create app_user_roles table
-- ============================================================
CREATE TABLE IF NOT EXISTS app_user_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    role_id UUID NOT NULL REFERENCES app_roles(id) ON DELETE CASCADE,
    tenant_id UUID DEFAULT '00000000-0000-0000-0000-000000000001',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_app_user_roles_user_role ON app_user_roles(user_id, role_id) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS ix_app_user_roles_user_id ON app_user_roles(user_id);
CREATE INDEX IF NOT EXISTS ix_app_user_roles_role_id ON app_user_roles(role_id);

-- ============================================================
-- 5. Seed permissions
-- ============================================================
INSERT INTO permissions (code, name, description, category) VALUES
    ('dashboard.view', 'Visualizar', 'dashboard.view', 'Dashboard'),
    ('dashboard.edit', 'Editar', 'dashboard.edit', 'Dashboard'),
    ('machines.view', 'Visualizar', 'machines.view', 'Máquinas'),
    ('machines.edit', 'Editar', 'machines.edit', 'Máquinas'),
    ('machines.delete', 'Excluir', 'machines.delete', 'Máquinas'),
    ('incidents.view', 'Visualizar', 'incidents.view', 'Incidentes'),
    ('incidents.create', 'Criar', 'incidents.create', 'Incidentes'),
    ('incidents.edit', 'Editar', 'incidents.edit', 'Incidentes'),
    ('incidents.close', 'Encerrar', 'incidents.close', 'Incidentes'),
    ('transfers.view', 'Visualizar', 'transfers.view', 'Transferência de Dados'),
    ('screenshots.view', 'Visualizar', 'screenshots.view', 'Capturas'),
    ('screenshots.delete', 'Excluir', 'screenshots.delete', 'Capturas'),
    ('remote.view', 'Visualizar', 'remote.view', 'Acesso Remoto'),
    ('remote.start', 'Iniciar', 'remote.start', 'Acesso Remoto'),
    ('remote.stop', 'Encerrar', 'remote.stop', 'Acesso Remoto'),
    ('security.view', 'Visualizar', 'security.view', 'Segurança'),
    ('security.manage', 'Gerenciar', 'security.manage', 'Segurança'),
    ('reports.view', 'Visualizar', 'reports.view', 'Relatórios'),
    ('reports.export', 'Exportar', 'reports.export', 'Relatórios'),
    ('users.view', 'Visualizar', 'users.view', 'Usuários'),
    ('users.create', 'Criar', 'users.create', 'Usuários'),
    ('users.edit', 'Editar', 'users.edit', 'Usuários'),
    ('users.delete', 'Excluir', 'users.delete', 'Usuários'),
    ('roles.view', 'Visualizar', 'roles.view', 'Perfis'),
    ('roles.create', 'Criar', 'roles.create', 'Perfis'),
    ('roles.edit', 'Editar', 'roles.edit', 'Perfis'),
    ('roles.delete', 'Excluir', 'roles.delete', 'Perfis'),
    ('audit.view', 'Visualizar', 'audit.view', 'Auditoria'),
    ('settings.view', 'Visualizar', 'settings.view', 'Configurações'),
    ('settings.manage', 'Alterar', 'settings.manage', 'Configurações'),
    ('companies.view', 'Visualizar', 'companies.view', 'Empresas'),
    ('companies.manage', 'Gerenciar', 'companies.manage', 'Empresas'),
    ('licenses.view', 'Visualizar', 'licenses.view', 'Licenças'),
    ('licenses.manage', 'Gerenciar', 'licenses.manage', 'Licenças')
ON CONFLICT (code) DO NOTHING;

-- ============================================================
-- 6. Seed default roles for default tenant
-- ============================================================
DO $$
DECLARE
    v_tenant_id UUID := '00000000-0000-0000-0000-000000000001';
    v_admin_role_id UUID;
    v_supervisor_role_id UUID;
    v_auditor_role_id UUID;
    v_operator_role_id UUID;
    v_readonly_role_id UUID;
BEGIN
    -- Create roles
    INSERT INTO app_roles (name, description, is_system_role, is_default, tenant_id)
    VALUES ('Administrador', 'Acesso total ao sistema', TRUE, FALSE, v_tenant_id)
    ON CONFLICT DO NOTHING;

    SELECT id INTO v_admin_role_id FROM app_roles WHERE name = 'Administrador' AND tenant_id = v_tenant_id AND is_deleted = FALSE LIMIT 1;

    INSERT INTO app_roles (name, description, is_system_role, is_default, tenant_id)
    VALUES ('Supervisor', 'Supervisão e monitoramento', FALSE, FALSE, v_tenant_id)
    ON CONFLICT DO NOTHING;

    SELECT id INTO v_supervisor_role_id FROM app_roles WHERE name = 'Supervisor' AND tenant_id = v_tenant_id AND is_deleted = FALSE LIMIT 1;

    INSERT INTO app_roles (name, description, is_system_role, is_default, tenant_id)
    VALUES ('Auditor', 'Apenas visualização e auditoria', FALSE, FALSE, v_tenant_id)
    ON CONFLICT DO NOTHING;

    SELECT id INTO v_auditor_role_id FROM app_roles WHERE name = 'Auditor' AND tenant_id = v_tenant_id AND is_deleted = FALSE LIMIT 1;

    INSERT INTO app_roles (name, description, is_system_role, is_default, tenant_id)
    VALUES ('Operador', 'Operações básicas do sistema', FALSE, FALSE, v_tenant_id)
    ON CONFLICT DO NOTHING;

    SELECT id INTO v_operator_role_id FROM app_roles WHERE name = 'Operador' AND tenant_id = v_tenant_id AND is_deleted = FALSE LIMIT 1;

    INSERT INTO app_roles (name, description, is_system_role, is_default, tenant_id)
    VALUES ('Somente Leitura', 'Apenas visualização', FALSE, TRUE, v_tenant_id)
    ON CONFLICT DO NOTHING;

    SELECT id INTO v_readonly_role_id FROM app_roles WHERE name = 'Somente Leitura' AND tenant_id = v_tenant_id AND is_deleted = FALSE LIMIT 1;

    -- Assign all permissions to Administrador
    INSERT INTO app_role_permissions (role_id, permission_id, tenant_id)
    SELECT v_admin_role_id, id, v_tenant_id FROM permissions WHERE is_deleted = FALSE
    ON CONFLICT DO NOTHING;

    -- Supervisor permissions
    INSERT INTO app_role_permissions (role_id, permission_id, tenant_id)
    SELECT v_supervisor_role_id, id, v_tenant_id FROM permissions
    WHERE code IN ('dashboard.view', 'machines.view', 'machines.edit', 'incidents.view', 'incidents.create', 'incidents.edit', 'incidents.close', 'transfers.view', 'screenshots.view', 'remote.view', 'remote.start', 'remote.stop', 'security.view', 'reports.view', 'reports.export', 'users.view', 'audit.view', 'settings.view')
    AND is_deleted = FALSE
    ON CONFLICT DO NOTHING;

    -- Auditor permissions
    INSERT INTO app_role_permissions (role_id, permission_id, tenant_id)
    SELECT v_auditor_role_id, id, v_tenant_id FROM permissions
    WHERE code IN ('dashboard.view', 'machines.view', 'incidents.view', 'transfers.view', 'screenshots.view', 'remote.view', 'security.view', 'reports.view', 'reports.export', 'users.view', 'roles.view', 'audit.view', 'settings.view')
    AND is_deleted = FALSE
    ON CONFLICT DO NOTHING;

    -- Operator permissions
    INSERT INTO app_role_permissions (role_id, permission_id, tenant_id)
    SELECT v_operator_role_id, id, v_tenant_id FROM permissions
    WHERE code IN ('dashboard.view', 'machines.view', 'incidents.view', 'incidents.create', 'incidents.edit', 'transfers.view', 'screenshots.view', 'remote.view', 'remote.start', 'remote.stop', 'security.view', 'reports.view')
    AND is_deleted = FALSE
    ON CONFLICT DO NOTHING;

    -- Read-only permissions
    INSERT INTO app_role_permissions (role_id, permission_id, tenant_id)
    SELECT v_readonly_role_id, id, v_tenant_id FROM permissions
    WHERE code IN ('dashboard.view', 'machines.view', 'incidents.view', 'transfers.view', 'screenshots.view', 'remote.view', 'security.view', 'reports.view', 'users.view', 'audit.view')
    AND is_deleted = FALSE
    ON CONFLICT DO NOTHING;

END $$;

-- ============================================================
-- 7. Link existing Admin user to Administrador role
-- ============================================================
DO $$
DECLARE
    v_user_id UUID;
    v_role_id UUID;
    v_tenant_id UUID := '00000000-0000-0000-0000-000000000001';
BEGIN
    -- Find the Admin user (from asp_net_users)
    SELECT id INTO v_user_id FROM asp_net_users WHERE "UserName" = 'Admin' LIMIT 1;

    -- Find the Administrador role
    SELECT id INTO v_role_id FROM app_roles WHERE name = 'Administrador' AND tenant_id = v_tenant_id AND is_deleted = FALSE LIMIT 1;

    IF v_user_id IS NOT NULL AND v_role_id IS NOT NULL THEN
        INSERT INTO app_user_roles (user_id, role_id, tenant_id)
        VALUES (v_user_id, v_role_id, v_tenant_id)
        ON CONFLICT DO NOTHING;
    END IF;
END $$;
