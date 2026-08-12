-- Sentinela Multi-Tenant Migration
-- Data: 06/08/2026

-- 1. Criar tabela tenants
CREATE TABLE IF NOT EXISTS tenants (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    cnpj VARCHAR(18),
    plan VARCHAR(20) NOT NULL DEFAULT 'Starter',
    status VARCHAR(20) NOT NULL DEFAULT 'Active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS ix_tenants_cnpj ON tenants(cnpj);
CREATE INDEX IF NOT EXISTS ix_tenants_status ON tenants(status);

-- 2. Criar tenant padrão para dados existentes
INSERT INTO tenants (id, name, plan, status, created_at)
VALUES ('00000000-0000-0000-0000-000000000001', 'Sentinela Default', 'Enterprise', 'Active', NOW())
ON CONFLICT (id) DO NOTHING;

-- 3. Adicionar tenant_id em todas as tabelas (ignorar erro se já existir)
DO $$
DECLARE
    t TEXT;
BEGIN
    FOR t IN
        SELECT table_name
        FROM information_schema.columns
        WHERE column_name = 'tenant_id'
        AND table_schema = 'public'
    LOOP
        RAISE NOTICE 'Table % already has tenant_id', t;
    END LOOP;
END $$;

-- computers
ALTER TABLE computers ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_computers_tenant_id ON computers(tenant_id);

-- heartbeats
ALTER TABLE heartbeats ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_heartbeats_tenant_id ON heartbeats(tenant_id);

-- timeline_entries
ALTER TABLE timeline_entries ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_timeline_entries_tenant_id ON timeline_entries(tenant_id);

-- application_usages
ALTER TABLE application_usages ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_application_usages_tenant_id ON application_usages(tenant_id);

-- usb_events
ALTER TABLE usb_events ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_usb_events_tenant_id ON usb_events(tenant_id);

-- security_events
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_security_events_tenant_id ON security_events(tenant_id);

-- security_alerts
ALTER TABLE security_alerts ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_security_alerts_tenant_id ON security_alerts(tenant_id);

-- vulnerability_events
ALTER TABLE vulnerability_events ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_vulnerability_events_tenant_id ON vulnerability_events(tenant_id);

-- correlation_rules
ALTER TABLE correlation_rules ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_correlation_rules_tenant_id ON correlation_rules(tenant_id);

-- alerts
ALTER TABLE alerts ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_alerts_tenant_id ON alerts(tenant_id);

-- alert_rules
ALTER TABLE alert_rules ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_alert_rules_tenant_id ON alert_rules(tenant_id);

-- alert_comments
ALTER TABLE alert_comments ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_alert_comments_tenant_id ON alert_comments(tenant_id);

-- workflows
ALTER TABLE workflows ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_workflows_tenant_id ON workflows(tenant_id);

-- workflow_conditions
ALTER TABLE workflow_conditions ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_workflow_conditions_tenant_id ON workflow_conditions(tenant_id);

-- workflow_execution_logs
ALTER TABLE workflow_execution_logs ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_workflow_execution_logs_tenant_id ON workflow_execution_logs(tenant_id);

-- audit_trails (já pode ter tenant_id)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'audit_trails'
        AND column_name = 'tenant_id'
    ) THEN
        ALTER TABLE audit_trails ADD COLUMN tenant_id UUID REFERENCES tenants(id);
        CREATE INDEX ix_audit_trails_tenant_id ON audit_trails(tenant_id);
    END IF;
END $$;

-- screen_captures
ALTER TABLE screen_captures ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_screen_captures_tenant_id ON screen_captures(tenant_id);

-- screenshots
ALTER TABLE screenshots ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_screenshots_tenant_id ON screenshots(tenant_id);

-- screen_capture_records
ALTER TABLE screen_capture_records ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_screen_capture_records_tenant_id ON screen_capture_records(tenant_id);

-- software_inventory
ALTER TABLE software_inventory ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_software_inventory_tenant_id ON software_inventory(tenant_id);

-- endpoint_security_status
ALTER TABLE endpoint_security_status ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_endpoint_security_status_tenant_id ON endpoint_security_status(tenant_id);

-- remote_sessions
ALTER TABLE remote_sessions ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_remote_sessions_tenant_id ON remote_sessions(tenant_id);

-- file_transfers
ALTER TABLE file_transfers ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_file_transfers_tenant_id ON file_transfers(tenant_id);

-- 4. Atualizar dados existentes com o tenant padrão
UPDATE computers SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE heartbeats SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE timeline_entries SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE application_usages SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE usb_events SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE security_events SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE security_alerts SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE vulnerability_events SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE correlation_rules SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE alerts SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE alert_rules SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE alert_comments SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE workflows SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE workflow_conditions SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE workflow_execution_logs SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE audit_trails SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE screen_captures SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE screenshots SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE screen_capture_records SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE software_inventory SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE endpoint_security_status SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE remote_sessions SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;
UPDATE file_transfers SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;

-- 5. Adicionar tenant_id na tabela users (Identity)
ALTER TABLE users ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
CREATE INDEX IF NOT EXISTS ix_users_tenant_id ON users(tenant_id);

-- 6. Atualizar usuário Admin existente com o tenant padrão
UPDATE users SET tenant_id = '00000000-0000-0000-0000-000000000001' WHERE tenant_id IS NULL;

-- 7. Tornar tenant_id obrigatório após migração de dados
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT table_name
        FROM information_schema.columns
        WHERE column_name = 'tenant_id'
        AND table_schema = 'public'
        AND is_nullable = 'YES'
        AND table_name != 'tenants'
    LOOP
        EXECUTE format('ALTER TABLE %I ALTER COLUMN tenant_id SET NOT NULL', r.table_name);
        RAISE NOTICE 'Set tenant_id NOT NULL on %', r.table_name;
    END LOOP;
END $$;
