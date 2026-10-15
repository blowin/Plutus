-- Create the reporting schema.
CREATE TABLE reports (
    id          BIGSERIAL PRIMARY KEY,
    label       TEXT NOT NULL,        -- human readable label
    pattern     TEXT NOT NULL DEFAULT 'a--b/*c*/d',
    description TEXT NOT NULL DEFAULT 'it''s -- quoted'
);

/* A block comment
   spanning lines. */
CREATE OR REPLACE FUNCTION report_label(input TEXT)
RETURNS TEXT AS $body$
BEGIN
    -- this marker is inside the function body
    RETURN input || '/* literal */';
END;
$body$ LANGUAGE plpgsql;

SELECT id, label FROM reports WHERE label <> 'x'; -- trailing comment
