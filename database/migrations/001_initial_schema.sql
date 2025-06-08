-- Initial schema for Permafrost Identity Management System
-- Version: 1.0.0
-- Created: 2025-06-08

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create enum types
CREATE TYPE user_status AS ENUM ('active', 'disabled', 'locked', 'deleted');
CREATE TYPE group_type AS ENUM ('security', 'distribution', 'builtin');
CREATE TYPE event_type AS ENUM ('user_created', 'user_modified', 'user_deleted', 'group_created', 'group_modified', 'group_deleted', 'login', 'logout', 'permission_changed');

-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    domain_controller_id VARCHAR(255) NOT NULL,
    ad_object_guid UUID NOT NULL UNIQUE,
    sam_account_name VARCHAR(255) NOT NULL,
    user_principal_name VARCHAR(255),
    display_name VARCHAR(255),
    given_name VARCHAR(255),
    surname VARCHAR(255),
    email VARCHAR(255),
    department VARCHAR(255),
    title VARCHAR(255),
    manager_dn TEXT,
    status user_status NOT NULL DEFAULT 'active',
    last_logon TIMESTAMP WITH TIME ZONE,
    password_last_set TIMESTAMP WITH TIME ZONE,
    account_expires TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    last_seen_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Groups table
CREATE TABLE groups (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    domain_controller_id VARCHAR(255) NOT NULL,
    ad_object_guid UUID NOT NULL UNIQUE,
    sam_account_name VARCHAR(255) NOT NULL,
    display_name VARCHAR(255),
    description TEXT,
    group_type group_type NOT NULL DEFAULT 'security',
    distinguished_name TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    last_seen_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Group memberships table
CREATE TABLE group_memberships (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    group_id UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, group_id)
);

-- Events table for audit logging
CREATE TABLE events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    domain_controller_id VARCHAR(255) NOT NULL,
    event_type event_type NOT NULL,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    group_id UUID REFERENCES groups(id) ON DELETE SET NULL,
    event_data JSONB,
    source_ip INET,
    user_agent TEXT,
    occurred_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Domain controllers table
CREATE TABLE domain_controllers (
    id VARCHAR(255) PRIMARY KEY,
    hostname VARCHAR(255) NOT NULL,
    domain_name VARCHAR(255) NOT NULL,
    last_heartbeat TIMESTAMP WITH TIME ZONE,
    version VARCHAR(50),
    status VARCHAR(50) NOT NULL DEFAULT 'unknown',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create indexes for performance
CREATE INDEX idx_users_sam_account_name ON users(sam_account_name);
CREATE INDEX idx_users_upn ON users(user_principal_name);
CREATE INDEX idx_users_status ON users(status);
CREATE INDEX idx_users_domain_controller ON users(domain_controller_id);
CREATE INDEX idx_users_last_seen ON users(last_seen_at);

CREATE INDEX idx_groups_sam_account_name ON groups(sam_account_name);
CREATE INDEX idx_groups_type ON groups(group_type);
CREATE INDEX idx_groups_domain_controller ON groups(domain_controller_id);
CREATE INDEX idx_groups_last_seen ON groups(last_seen_at);

CREATE INDEX idx_group_memberships_user ON group_memberships(user_id);
CREATE INDEX idx_group_memberships_group ON group_memberships(group_id);

CREATE INDEX idx_events_type ON events(event_type);
CREATE INDEX idx_events_occurred_at ON events(occurred_at);
CREATE INDEX idx_events_user ON events(user_id);
CREATE INDEX idx_events_group ON events(group_id);
CREATE INDEX idx_events_domain_controller ON events(domain_controller_id);

CREATE INDEX idx_domain_controllers_status ON domain_controllers(status);
CREATE INDEX idx_domain_controllers_last_heartbeat ON domain_controllers(last_heartbeat);

-- Create updated_at trigger function
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Apply updated_at triggers
CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_groups_updated_at BEFORE UPDATE ON groups
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_domain_controllers_updated_at BEFORE UPDATE ON domain_controllers
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
