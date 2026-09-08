using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CounterpartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RelatedNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSenderRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastMessagePreview = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TeacherReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CounterpartReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageThreads", x => x.Id);
                    table.UniqueConstraint("AK_MessageThreads_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_MessageThreads_Notes_RelatedNoteId",
                        column: x => x.RelatedNoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MessageThreads_Students_StudentId_TenantId",
                        columns: x => new { x.StudentId, x.TenantId },
                        principalTable: "Students",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageThreads_Teachers_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_MessageThreads_ThreadId_TenantId",
                        columns: x => new { x.ThreadId, x.TenantId },
                        principalTable: "MessageThreads",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadId_CreatedAt",
                table: "Messages",
                columns: new[] { "ThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadId_TenantId",
                table: "Messages",
                columns: new[] { "ThreadId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_RelatedNoteId",
                table: "MessageThreads",
                column: "RelatedNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_StudentId",
                table: "MessageThreads",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_StudentId_TenantId",
                table: "MessageThreads",
                columns: new[] { "StudentId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_TenantId_CounterpartRole_CounterpartId",
                table: "MessageThreads",
                columns: new[] { "TenantId", "CounterpartRole", "CounterpartId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_TenantId_LastMessageAt",
                table: "MessageThreads",
                columns: new[] { "TenantId", "LastMessageAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "MessageThreads");
        }
    }
}
