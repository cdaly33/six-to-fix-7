using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SixToFix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyHubDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pillar_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pillar = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    body_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pillar_contents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "playbook_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pillar = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    popularity = table.Column<int>(type: "integer", nullable: false),
                    last_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playbook_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_pillar_progresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pillar = table.Column<int>(type: "integer", nullable: false),
                    percent_complete = table.Column<int>(type: "integer", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_pillar_progresses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pillar_contents_tenant_id_pillar",
                table: "pillar_contents",
                columns: new[] { "tenant_id", "pillar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_playbook_templates_tenant_id_status",
                table: "playbook_templates",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_user_pillar_progresses_tenant_id_pillar",
                table: "user_pillar_progresses",
                columns: new[] { "tenant_id", "pillar" });

            migrationBuilder.CreateIndex(
                name: "ix_user_pillar_progresses_user_id_pillar",
                table: "user_pillar_progresses",
                columns: new[] { "user_id", "pillar" },
                unique: true);

            // Role migration: introduce "Client" role and migrate Reviewer/Viewer users to it.
            // Old role rows stay intact — they are cleaned up in Phase 6.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    v_client_role_id uuid;
                BEGIN
                    -- Insert "Client" role if it does not already exist.
                    IF NOT EXISTS (SELECT 1 FROM asp_net_roles WHERE normalized_name = 'CLIENT') THEN
                        v_client_role_id := gen_random_uuid();
                        INSERT INTO asp_net_roles (id, name, normalized_name, concurrency_stamp)
                        VALUES (v_client_role_id, 'Client', 'CLIENT', gen_random_uuid()::text);
                    ELSE
                        SELECT id INTO v_client_role_id FROM asp_net_roles WHERE normalized_name = 'CLIENT';
                    END IF;

                    -- Add all users currently in "Reviewer" or "Viewer" to "Client" (skip duplicates).
                    INSERT INTO asp_net_user_roles (user_id, role_id)
                    SELECT DISTINCT ur.user_id, v_client_role_id
                    FROM asp_net_user_roles ur
                    JOIN asp_net_roles r ON r.id = ur.role_id
                    WHERE r.normalized_name IN ('REVIEWER', 'VIEWER')
                      AND NOT EXISTS (
                          SELECT 1 FROM asp_net_user_roles x
                          WHERE x.user_id = ur.user_id AND x.role_id = v_client_role_id
                      );
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pillar_contents");

            migrationBuilder.DropTable(
                name: "playbook_templates");

            migrationBuilder.DropTable(
                name: "user_pillar_progresses");
        }
    }
}
