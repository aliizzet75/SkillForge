using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillForge.Core.Migrations
{
    public partial class AddLeaderboardView : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_leaderboard AS
SELECT
    us.""Id"",
    us.""UserId"",
    u.""Username"",
    u.""Avatar"",
    u.""CountryCode"",
    us.""SkillType"",
    us.""Level"",
    us.""XP"",
    u.""TotalXp"",
    us.""Percentile"",
    us.""GamesPlayed"",
    us.""GamesWon"",
    RANK() OVER (PARTITION BY us.""SkillType"" ORDER BY us.""XP"" DESC, us.""GamesPlayed"" DESC) AS ""GlobalRank"",
    RANK() OVER (PARTITION BY us.""SkillType"", u.""CountryCode"" ORDER BY us.""XP"" DESC, us.""GamesPlayed"" DESC) AS ""CountryRank""
FROM ""UserSkills"" us
JOIN ""Users"" u ON u.""Id"" = us.""UserId"";

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_leaderboard_id ON mv_leaderboard (""Id"");
CREATE INDEX IF NOT EXISTS idx_mv_leaderboard_skilltype_xp ON mv_leaderboard (""SkillType"", ""XP"" DESC);
CREATE INDEX IF NOT EXISTS idx_mv_leaderboard_skilltype_country ON mv_leaderboard (""SkillType"", ""CountryCode"", ""XP"" DESC);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP MATERIALIZED VIEW IF EXISTS mv_leaderboard;
");
        }
    }
}
