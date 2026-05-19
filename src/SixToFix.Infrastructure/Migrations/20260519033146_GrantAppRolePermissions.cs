using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SixToFix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GrantAppRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prerequisite: the sf_app login role must already exist in PostgreSQL.
            // Create it via the deploy workflow or infra setup script before applying migrations:
            //   CREATE ROLE sf_app WITH LOGIN PASSWORD '<from-KV DefaultConnection>';
            // This migration only codifies the permission grants — it does not manage secrets.

            // Fail fast if sf_app does not exist so the error is obvious.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'sf_app') THEN
                        RAISE EXCEPTION 'Migration pre-condition failed: PostgreSQL role sf_app does not exist. '
                            'Create it before applying migrations.';
                    END IF;
                END
                $$;
                """);

            // Grant DML on all existing tables and sequences.
            migrationBuilder.Sql(
                "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sf_app;");
            migrationBuilder.Sql(
                "GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO sf_app;");

            // category_result_versions is append-only — revoke mutation to enforce at DB layer.
            migrationBuilder.Sql(
                "REVOKE UPDATE, DELETE ON TABLE category_result_versions FROM sf_app;");

            // Future tables created by subsequent migrations also inherit these grants.
            migrationBuilder.Sql(
                "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sf_app;");
            migrationBuilder.Sql(
                "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO sf_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM sf_app;");
            migrationBuilder.Sql(
                "REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public FROM sf_app;");
        }
    }
}
