using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitToWork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PermitModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ptw");

            migrationBuilder.CreateTable(
                name: "FacilityApprovers",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDecisive = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityApprovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityApprovers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "org",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityApprovers_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalSchema: "org",
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermitTypes",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskGroups",
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
                    table.PrimaryKey("PK_TaskGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermitTypeCertifications",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitTypeCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitTypeCertifications_CertificationTypes_CertificationTypeId",
                        column: x => x.CertificationTypeId,
                        principalSchema: "org",
                        principalTable: "CertificationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermitTypeCertifications_PermitTypes_PermitTypeId",
                        column: x => x.PermitTypeId,
                        principalSchema: "ptw",
                        principalTable: "PermitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permits",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PermitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkPackage = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    WorkDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permits_Employees_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "org",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Permits_Employees_ReceiverId",
                        column: x => x.ReceiverId,
                        principalSchema: "org",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Permits_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalSchema: "org",
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Permits_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "org",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Permits_PermitTypes_PermitTypeId",
                        column: x => x.PermitTypeId,
                        principalSchema: "ptw",
                        principalTable: "PermitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Permits_TaskGroups_TaskGroupId",
                        column: x => x.TaskGroupId,
                        principalSchema: "ptw",
                        principalTable: "TaskGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermitApprovals",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDecisive = table.Column<bool>(type: "bit", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecidedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitApprovals_Employees_ApproverEmployeeId",
                        column: x => x.ApproverEmployeeId,
                        principalSchema: "org",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermitApprovals_Permits_PermitId",
                        column: x => x.PermitId,
                        principalSchema: "ptw",
                        principalTable: "Permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermitCertificationRequirements",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitCertificationRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitCertificationRequirements_Permits_PermitId",
                        column: x => x.PermitId,
                        principalSchema: "ptw",
                        principalTable: "Permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermitDocuments",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitDocuments_Employees_UploadedById",
                        column: x => x.UploadedById,
                        principalSchema: "org",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermitDocuments_Permits_PermitId",
                        column: x => x.PermitId,
                        principalSchema: "ptw",
                        principalTable: "Permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermitEquipment",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitEquipment_Permits_PermitId",
                        column: x => x.PermitId,
                        principalSchema: "ptw",
                        principalTable: "Permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermitEvents",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitEvents_Permits_PermitId",
                        column: x => x.PermitId,
                        principalSchema: "ptw",
                        principalTable: "Permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermitWorkers",
                schema: "ptw",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitWorkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitWorkers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "org",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermitWorkers_Permits_PermitId",
                        column: x => x.PermitId,
                        principalSchema: "ptw",
                        principalTable: "Permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityApprovers_EmployeeId",
                schema: "org",
                table: "FacilityApprovers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityApprovers_FacilityId_EmployeeId",
                schema: "org",
                table: "FacilityApprovers",
                columns: new[] { "FacilityId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermitApprovals_ApproverEmployeeId_Decision",
                schema: "ptw",
                table: "PermitApprovals",
                columns: new[] { "ApproverEmployeeId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_PermitApprovals_PermitId_ApproverEmployeeId",
                schema: "ptw",
                table: "PermitApprovals",
                columns: new[] { "PermitId", "ApproverEmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermitCertificationRequirements_PermitId",
                schema: "ptw",
                table: "PermitCertificationRequirements",
                column: "PermitId");

            migrationBuilder.CreateIndex(
                name: "IX_PermitDocuments_PermitId",
                schema: "ptw",
                table: "PermitDocuments",
                column: "PermitId");

            migrationBuilder.CreateIndex(
                name: "IX_PermitDocuments_UploadedById",
                schema: "ptw",
                table: "PermitDocuments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_PermitEquipment_PermitId",
                schema: "ptw",
                table: "PermitEquipment",
                column: "PermitId");

            migrationBuilder.CreateIndex(
                name: "IX_PermitEvents_PermitId_OccurredOn",
                schema: "ptw",
                table: "PermitEvents",
                columns: new[] { "PermitId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Permits_CreatedById",
                schema: "ptw",
                table: "Permits",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Permits_FacilityId",
                schema: "ptw",
                table: "Permits",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Permits_LocationId",
                schema: "ptw",
                table: "Permits",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Permits_PermitNumber",
                schema: "ptw",
                table: "Permits",
                column: "PermitNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permits_PermitTypeId",
                schema: "ptw",
                table: "Permits",
                column: "PermitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Permits_ReceiverId",
                schema: "ptw",
                table: "Permits",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_Permits_Status",
                schema: "ptw",
                table: "Permits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Permits_TaskGroupId",
                schema: "ptw",
                table: "Permits",
                column: "TaskGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PermitTypeCertifications_CertificationTypeId",
                schema: "ptw",
                table: "PermitTypeCertifications",
                column: "CertificationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PermitTypeCertifications_PermitTypeId_CertificationTypeId",
                schema: "ptw",
                table: "PermitTypeCertifications",
                columns: new[] { "PermitTypeId", "CertificationTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermitTypes_Code",
                schema: "ptw",
                table: "PermitTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermitWorkers_EmployeeId",
                schema: "ptw",
                table: "PermitWorkers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PermitWorkers_PermitId_EmployeeId",
                schema: "ptw",
                table: "PermitWorkers",
                columns: new[] { "PermitId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskGroups_Code",
                schema: "ptw",
                table: "TaskGroups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityApprovers",
                schema: "org");

            migrationBuilder.DropTable(
                name: "PermitApprovals",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "PermitCertificationRequirements",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "PermitDocuments",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "PermitEquipment",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "PermitEvents",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "PermitTypeCertifications",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "PermitWorkers",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "Permits",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "PermitTypes",
                schema: "ptw");

            migrationBuilder.DropTable(
                name: "TaskGroups",
                schema: "ptw");
        }
    }
}
