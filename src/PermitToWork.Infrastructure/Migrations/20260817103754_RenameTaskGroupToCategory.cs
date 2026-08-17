using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitToWork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTaskGroupToCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permits_TaskGroups_TaskGroupId",
                schema: "ptw",
                table: "Permits");

            migrationBuilder.DropTable(
                name: "TaskGroups",
                schema: "ptw");

            migrationBuilder.RenameColumn(
                name: "WorkPackage",
                schema: "ptw",
                table: "Permits",
                newName: "Project");

            migrationBuilder.RenameColumn(
                name: "TaskGroupId",
                schema: "ptw",
                table: "Permits",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Permits_TaskGroupId",
                schema: "ptw",
                table: "Permits",
                newName: "IX_Permits_CategoryId");

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Code",
                schema: "ptw",
                table: "Categories",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Permits_Categories_CategoryId",
                schema: "ptw",
                table: "Permits",
                column: "CategoryId",
                principalSchema: "ptw",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permits_Categories_CategoryId",
                schema: "ptw",
                table: "Permits");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "ptw");

            migrationBuilder.RenameColumn(
                name: "Project",
                schema: "ptw",
                table: "Permits",
                newName: "WorkPackage");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                schema: "ptw",
                table: "Permits",
                newName: "TaskGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_Permits_CategoryId",
                schema: "ptw",
                table: "Permits",
                newName: "IX_Permits_TaskGroupId");

            migrationBuilder.CreateTable(
                name: "TaskGroups",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskGroups_Code",
                schema: "ptw",
                table: "TaskGroups",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Permits_TaskGroups_TaskGroupId",
                schema: "ptw",
                table: "Permits",
                column: "TaskGroupId",
                principalSchema: "ptw",
                principalTable: "TaskGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
