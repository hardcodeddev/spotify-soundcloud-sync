using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaylistSync.Infrastructure.Persistence.Migrations
{
    public partial class AddSyncSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScheduleCron",
                table: "SyncProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ScheduleEnabled",
                table: "SyncProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleTimeZone",
                table: "SyncProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ScheduleCron", table: "SyncProfiles");
            migrationBuilder.DropColumn(name: "ScheduleEnabled", table: "SyncProfiles");
            migrationBuilder.DropColumn(name: "ScheduleTimeZone", table: "SyncProfiles");
        }
    }
}
