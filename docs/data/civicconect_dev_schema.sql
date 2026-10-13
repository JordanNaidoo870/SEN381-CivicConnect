-- Database: civicconnect_dev

-- DROP DATABASE IF EXISTS civicconnect_dev;

CREATE DATABASE civicconnect_dev
    WITH
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'English_United States.1252'
    LC_CTYPE = 'English_United States.1252'
    LOCALE_PROVIDER = 'libc'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1
    IS_TEMPLATE = False;

GRANT TEMPORARY, CONNECT ON DATABASE civicconnect_dev TO PUBLIC;

GRANT ALL ON DATABASE civicconnect_dev TO civicconnect_user;

GRANT ALL ON DATABASE civicconnect_dev TO postgres;

-- Enable UUID generation
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- 1. REQUEST CATEGORY (reference data)
-- ============================================================
CREATE TABLE request_category (
    category_id     SERIAL          PRIMARY KEY,
    name            VARCHAR(100)    NOT NULL UNIQUE,
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at_utc  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE request_category IS 'Controlled reference data for classifying service requests.';

-- ============================================================
-- 2. APPLICATION USER (Identity aggregate)
--    Managed by ASP.NET Core Identity; simplified for schema.
-- ============================================================
CREATE TABLE application_user (
    user_id                 UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    email                   VARCHAR(256)    NOT NULL UNIQUE,
    normalized_email        VARCHAR(256)    NOT NULL,
    password_hash           TEXT            NOT NULL,
    security_stamp          TEXT,
    concurrency_stamp       TEXT,
    phone_number            VARCHAR(20),
    email_confirmed         BOOLEAN         NOT NULL DEFAULT FALSE,
    phone_number_confirmed  BOOLEAN         NOT NULL DEFAULT FALSE,
    two_factor_enabled      BOOLEAN         NOT NULL DEFAULT FALSE,
    lockout_end             TIMESTAMPTZ,
    lockout_enabled         BOOLEAN         NOT NULL DEFAULT TRUE,
    access_failed_count     INT             NOT NULL DEFAULT 0,
    display_name            VARCHAR(200)    NOT NULL,
    created_at_utc          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE application_user IS 'User accounts. Managed by ASP.NET Core Identity with role/policy extensions.';

-- ============================================================
-- 3. SERVICE REQUEST (primary aggregate root)
-- ============================================================
CREATE TYPE request_status AS ENUM (
    'Pending',
    'InProgress',
    'Resolved',
    'Closed',
    'Rejected'
);

CREATE TABLE service_request (
    request_id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    requester_id        UUID            NOT NULL,
    assigned_staff_id   UUID,
    category_id         INT             NOT NULL,
    title               VARCHAR(200)    NOT NULL,
    description         TEXT            NOT NULL,
    location            VARCHAR(300)    NOT NULL,
    status              request_status  NOT NULL DEFAULT 'Pending',
    created_at_utc      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at_utc      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    resolved_at_utc     TIMESTAMPTZ,
    resolution_notes    TEXT,
    row_version         BYTEA           NOT NULL DEFAULT '\x0000000000000001'::bytea,

    CONSTRAINT fk_service_request_requester
        FOREIGN KEY (requester_id) REFERENCES application_user(user_id) ON DELETE RESTRICT,
    CONSTRAINT fk_service_request_staff
        FOREIGN KEY (assigned_staff_id) REFERENCES application_user(user_id) ON DELETE SET NULL,
    CONSTRAINT fk_service_request_category
        FOREIGN KEY (category_id) REFERENCES request_category(category_id) ON DELETE RESTRICT
);

COMMENT ON TABLE service_request IS 'Primary aggregate root. Owns the request lifecycle and controls valid state changes.';
COMMENT ON COLUMN service_request.row_version IS 'Optimistic concurrency token.';

-- ============================================================
-- 4. REQUEST STATUS HISTORY (append-only lifecycle evidence)
-- ============================================================
CREATE TABLE request_status_history (
    history_id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    request_id          UUID            NOT NULL,
    old_status          request_status  NOT NULL,
    new_status          request_status  NOT NULL,
    changed_by_user_id  UUID            NOT NULL,
    changed_at_utc      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    comment             TEXT,

    CONSTRAINT fk_history_request
        FOREIGN KEY (request_id) REFERENCES service_request(request_id) ON DELETE CASCADE,
    CONSTRAINT fk_history_user
        FOREIGN KEY (changed_by_user_id) REFERENCES application_user(user_id) ON DELETE RESTRICT
);

COMMENT ON TABLE request_status_history IS 'Append-only record of every status change. No UPDATE or DELETE permitted.';

-- ============================================================
-- 5. REQUEST COMMENT (communication record)
-- ============================================================
CREATE TABLE request_comment (
    comment_id      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    request_id      UUID            NOT NULL,
    author_user_id  UUID            NOT NULL,
    body            TEXT            NOT NULL,
    is_official     BOOLEAN         NOT NULL DEFAULT FALSE,
    created_at_utc  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_comment_request
        FOREIGN KEY (request_id) REFERENCES service_request(request_id) ON DELETE CASCADE,
    CONSTRAINT fk_comment_author
        FOREIGN KEY (author_user_id) REFERENCES application_user(user_id) ON DELETE RESTRICT
);

COMMENT ON TABLE request_comment IS 'Supports official staff replies and request conversation.';

-- ============================================================
-- 6. REQUEST ATTACHMENT (supporting evidence metadata)
-- ============================================================
CREATE TABLE request_attachment (
    attachment_id       UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    request_id          UUID            NOT NULL,
    file_name           VARCHAR(255)    NOT NULL,
    content_type        VARCHAR(100)    NOT NULL,
    storage_reference   TEXT            NOT NULL,
    uploaded_at_utc     TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_attachment_request
        FOREIGN KEY (request_id) REFERENCES service_request(request_id) ON DELETE CASCADE
);

COMMENT ON TABLE request_attachment IS 'Basic evidence attachment metadata. Advanced media management remains future scope.';

-- ============================================================
-- 7. REQUEST UPVOTE (optional engagement — junction table)
-- ============================================================
CREATE TABLE request_upvote (
    request_id      UUID            NOT NULL,
    user_id         UUID            NOT NULL,
    created_at_utc  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    PRIMARY KEY (request_id, user_id),
    CONSTRAINT fk_upvote_request
        FOREIGN KEY (request_id) REFERENCES service_request(request_id) ON DELETE CASCADE,
    CONSTRAINT fk_upvote_user
        FOREIGN KEY (user_id) REFERENCES application_user(user_id) ON DELETE CASCADE
);

COMMENT ON TABLE request_upvote IS 'Optional upvote feature. Could-have priority; not part of initial M2 implementation.';

-- ============================================================
-- 8. AUDIT LOG (observability evidence)
-- ============================================================
CREATE TABLE audit_log (
    audit_id        UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID,
    action          VARCHAR(100)    NOT NULL,
    entity_type     VARCHAR(100)    NOT NULL,
    entity_id       VARCHAR(100)    NOT NULL,
    timestamp_utc   TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    correlation_id  VARCHAR(100),

    CONSTRAINT fk_audit_user
        FOREIGN KEY (user_id) REFERENCES application_user(user_id) ON DELETE SET NULL
);

COMMENT ON TABLE audit_log IS 'Records significant actions for observability and investigation (NFR-OBS-01).';

-- ============================================================
-- INDEXES (common access paths)
-- ============================================================

-- Service request queue filtering and reporting
CREATE INDEX idx_service_request_requester         ON service_request(requester_id);
CREATE INDEX idx_service_request_assigned_staff    ON service_request(assigned_staff_id);
CREATE INDEX idx_service_request_category          ON service_request(category_id);
CREATE INDEX idx_service_request_status            ON service_request(status);
CREATE INDEX idx_service_request_created_at        ON service_request(created_at_utc DESC);
CREATE INDEX idx_service_request_updated_at        ON service_request(updated_at_utc DESC);

-- Composite index for staff queue (most common query pattern)
CREATE INDEX idx_service_request_queue
    ON service_request(status, category_id, created_at_utc DESC);

-- Status history lookups
CREATE INDEX idx_status_history_request            ON request_status_history(request_id);
CREATE INDEX idx_status_history_changed_at         ON request_status_history(changed_at_utc DESC);

-- Comment lookups
CREATE INDEX idx_comment_request                   ON request_comment(request_id, created_at_utc DESC);

-- Attachment lookups
CREATE INDEX idx_attachment_request                ON request_attachment(request_id);

-- Audit log filtering
CREATE INDEX idx_audit_user                        ON audit_log(user_id);
CREATE INDEX idx_audit_timestamp                   ON audit_log(timestamp_utc DESC);
CREATE INDEX idx_audit_entity                      ON audit_log(entity_type, entity_id);
CREATE INDEX idx_audit_correlation                 ON audit_log(correlation_id);

-- ============================================================
-- TRIGGERS
-- ============================================================

-- Auto-update updated_at_utc on service_request changes
CREATE OR REPLACE FUNCTION update_service_request_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at_utc := NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_service_request_updated
    BEFORE UPDATE ON service_request
    FOR EACH ROW
    EXECUTE FUNCTION update_service_request_timestamp();

-- Prevent UPDATE/DELETE on request_status_history (append-only)
CREATE OR REPLACE FUNCTION prevent_history_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'request_status_history is append-only. UPDATE and DELETE are not permitted.';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_history_no_update
    BEFORE UPDATE OR DELETE ON request_status_history
    FOR EACH ROW
    EXECUTE FUNCTION prevent_history_modification();

-- ============================================================
-- SEED REFERENCE DATA (categories — not test data)
-- ============================================================
INSERT INTO request_category (name, is_active) VALUES
    ('Roads', TRUE),
    ('Water', TRUE),
    ('Garbage', TRUE),
    ('Electricity', TRUE),
    ('Parks & Recreation', TRUE),
    ('Other', TRUE);

-- ============================================================
-- END OF SCHEMA
-- ============================================================