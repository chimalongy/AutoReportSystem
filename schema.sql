-- ============================================================
-- ARS Portal - PostgreSQL Database Setup Script
-- Run this script to create the required tables
-- NO Entity Framework Migrations needed
-- ============================================================

-- Create schema if it doesn't exist
CREATE SCHEMA IF NOT EXISTS public;

-- ============================================================
-- Table: app_users
-- Stores all user accounts with roles (Super Admin, Admin, Support)
-- ============================================================
CREATE TABLE IF NOT EXISTS public.app_users (
    id SERIAL PRIMARY KEY,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    department VARCHAR(100),
    last_login_date TEXT,
    profile_status VARCHAR(20) DEFAULT 'enabled',
    role VARCHAR(50) DEFAULT 'Support',
    email VARCHAR(255) NOT NULL UNIQUE,
    password TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Index for faster email lookups
CREATE INDEX IF NOT EXISTS idx_app_users_email ON public.app_users(email);

-- ============================================================
-- Table: audit_logs
-- Records all actions performed in the application
-- ============================================================
CREATE TABLE IF NOT EXISTS public.audit_logs (
    id SERIAL PRIMARY KEY,
    event TEXT,
    eventdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ipaddress VARCHAR(50),
    pageurl TEXT,
    userid TEXT
);

-- Index for faster date-based queries
CREATE INDEX IF NOT EXISTS idx_audit_logs_eventdate ON public.audit_logs(eventdate DESC);

-- ============================================================
-- Default Super Admin Account
-- This will be inserted by the application on first run
-- via GlobalFunctions.SeedSuperAdminAsync()
--
-- Default credentials (configured in appsettings.json):
--   Email:    superadmin@ars.com
--   Password: SuperAdmin@123
--
-- The application will hash the password using BCrypt
-- ============================================================

-- ============================================================
-- Table: db_connection_configs
-- Stores configured external database connections with encrypted
-- connection strings. Passwords are NEVER stored directly.
-- ============================================================
CREATE TABLE IF NOT EXISTS public.db_connection_configs (
    id SERIAL PRIMARY KEY,
    database_type VARCHAR(50) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INTEGER NOT NULL,
    database_name VARCHAR(255) NOT NULL,
    username VARCHAR(255) NOT NULL,
    encrypted_connection_string TEXT NOT NULL,
    status VARCHAR(20) DEFAULT 'active',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP
);

-- Index for faster status-based queries
CREATE INDEX IF NOT EXISTS idx_db_configs_status ON public.db_connection_configs(status);

-- ============================================================
-- Table: reports
-- Stores report configurations with automation scheduling
-- ============================================================
CREATE TABLE IF NOT EXISTS public.reports (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    db_connection_config_id INTEGER NOT NULL,
    query TEXT NOT NULL,
    output_file_name VARCHAR(200) NOT NULL,
    output_format VARCHAR(10) NOT NULL DEFAULT 'csv',
    execution_type VARCHAR(20) NOT NULL DEFAULT 'single',
    single_run_timing VARCHAR(20),
    single_run_date_time TIMESTAMP,
    schedule_frequency VARCHAR(20),
    schedule_days_of_week TEXT,
    schedule_day_of_month INTEGER,
    schedule_custom_dates TEXT,
    schedule_custom_recurring TEXT,
    schedule_time VARCHAR(10),
    status VARCHAR(20) DEFAULT 'active',
    last_run_date TIMESTAMP,
    next_run_date TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP,
    created_by_user_id INTEGER NOT NULL,
    last_error_message VARCHAR(500),
    
    -- ── Distribution Columns ──
    enable_email_distribution BOOLEAN NOT NULL DEFAULT FALSE,
    email_to_recipients VARCHAR(500),
    email_cc_recipients VARCHAR(500),
    email_bcc_recipients VARCHAR(500),
    email_subject VARCHAR(300),
    email_body_template TEXT,
    enable_file_save BOOLEAN NOT NULL DEFAULT FALSE,
    file_save_path VARCHAR(500),
	max_rows_per_sheet INTEGER,
    
    CONSTRAINT fk_reports_db_config 
        FOREIGN KEY (db_connection_config_id) 
        REFERENCES public.db_connection_configs(id) 
        ON DELETE RESTRICT
);

-- ══════════════════════════════════════════════════════════════════════
-- NEW TABLE: report_distribution_destinations
-- ══════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS public.report_distribution_destinations (
    id SERIAL PRIMARY KEY,
    report_id INTEGER NOT NULL,
    destination_type VARCHAR(20) NOT NULL DEFAULT 'email',
    email_to VARCHAR(500),
    email_cc VARCHAR(500),
    email_bcc VARCHAR(500),
    email_subject VARCHAR(300),
    email_body TEXT,
    file_path VARCHAR(500),
    
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_destinations_report 
        FOREIGN KEY (report_id) 
        REFERENCES public.reports(id) 
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS public.executions (
    id SERIAL PRIMARY KEY,
    report_id INTEGER NOT NULL,
    execution_status VARCHAR(20) NOT NULL DEFAULT 'running'
        CHECK (execution_status IN ('running', 'completed', 'failed')),
    execution_logs_path TEXT,
    execution_result_path TEXT,
    emails_sent JSONB DEFAULT '[]',
    files_sent JSONB DEFAULT '[]',
    start_time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    end_time TIMESTAMP,
    row_count INTEGER,
    error_message TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_executions_report 
        FOREIGN KEY (report_id) 
        REFERENCES public.reports(id) 
        ON DELETE CASCADE
);


-- Indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_reports_status ON public.reports(status);
CREATE INDEX IF NOT EXISTS idx_reports_execution_type ON public.reports(execution_type);
CREATE INDEX IF NOT EXISTS idx_reports_next_run_date ON public.reports(next_run_date);
CREATE INDEX IF NOT EXISTS idx_reports_created_at ON public.reports(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_destinations_report_id 
    ON public.report_distribution_destinations(report_id);

CREATE INDEX IF NOT EXISTS idx_executions_report_id 
    ON public.executions(report_id);
CREATE INDEX IF NOT EXISTS idx_executions_status 
    ON public.executions(execution_status);
CREATE INDEX IF NOT EXISTS idx_executions_start_time 
    ON public.executions(start_time DESC);


-- ============================================================
-- Optional: Verify tables were created
-- ============================================================
SELECT 'app_users table created' AS status WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'app_users');
SELECT 'audit_logs table created' AS status WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'audit_logs');
SELECT 'db_connection_configs table created' AS status WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'db_connection_configs');
SELECT 'reports table created' AS status WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'reports');
SELECT 'executions table created' AS status 
WHERE EXISTS (
    SELECT 1 FROM information_schema.tables 
    WHERE table_schema = 'public' AND table_name = 'executions'
);