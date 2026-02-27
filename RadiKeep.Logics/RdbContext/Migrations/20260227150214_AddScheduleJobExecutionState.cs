using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadiKeep.Logics.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleJobExecutionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ActualStartUtc",
                table: "ScheduleJob",
                type: "INTEGER",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 22);

            migrationBuilder.AddColumn<long>(
                name: "CompletedUtc",
                table: "ScheduleJob",
                type: "INTEGER",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 23);

            migrationBuilder.AddColumn<int>(
                name: "LastErrorCode",
                table: "ScheduleJob",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Relational:ColumnOrder", 25);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorDetail",
                table: "ScheduleJob",
                type: "TEXT",
                maxLength: 500,
                nullable: true)
                .Annotation("Relational:ColumnOrder", 26);

            migrationBuilder.AddColumn<long>(
                name: "PrepareStartUtc",
                table: "ScheduleJob",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Relational:ColumnOrder", 20);

            migrationBuilder.AddColumn<long>(
                name: "QueuedAtUtc",
                table: "ScheduleJob",
                type: "INTEGER",
                nullable: true)
                .Annotation("Relational:ColumnOrder", 21);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "ScheduleJob",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Relational:ColumnOrder", 24);

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "ScheduleJob",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Relational:ColumnOrder", 19);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleJob_Active_Unique",
                table: "ScheduleJob",
                columns: new[] { "ProgramId", "ServiceKind", "StartDateTime" },
                unique: true,
                filter: "State IN (0, 1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleJob_SchedulerScan",
                table: "ScheduleJob",
                columns: new[] { "IsEnabled", "State", "PrepareStartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleJob_Active_Unique",
                table: "ScheduleJob");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleJob_SchedulerScan",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "ActualStartUtc",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "CompletedUtc",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "LastErrorDetail",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "PrepareStartUtc",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "QueuedAtUtc",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "ScheduleJob");

            migrationBuilder.DropColumn(
                name: "State",
                table: "ScheduleJob");
        }
    }
}
