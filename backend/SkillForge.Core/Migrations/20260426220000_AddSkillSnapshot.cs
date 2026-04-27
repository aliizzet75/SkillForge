using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SkillForge.Core.Data;

#nullable disable

namespace SkillForge.Core.Migrations
{
    [DbContext(typeof(SkillForgeDbContext))]
    [Migration("20260426220000_AddSkillSnapshot")]
    public partial class AddSkillSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    XP = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Percentile = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillSnapshots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillSnapshots_UserId_SkillType_RecordedAt",
                table: "SkillSnapshots",
                columns: new[] { "UserId", "SkillType", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SkillSnapshots");
        }
    }
}
