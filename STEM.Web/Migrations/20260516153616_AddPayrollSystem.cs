using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STEM.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Banners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Banners__3214EC070C7B5A18", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetAgeMin = table.Column<byte>(type: "tinyint", nullable: false),
                    TargetAgeMax = table.Column<byte>(type: "tinyint", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSessions = table.Column<int>(type: "int", nullable: false),
                    MinStudents = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DifficultyLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Courses__3214EC0715D4E233", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Equipmen__3214EC0708814373", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayRateConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherTier = table.Column<int>(type: "int", nullable: false),
                    CourseDifficulty = table.Column<int>(type: "int", nullable: false),
                    RatePerSession = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayRateConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Roles__3214EC07EDAD7F9F", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false, defaultValue: 30)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterestedId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Leads__3214EC07A2FCE2D2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Course",
                        column: x => x.InterestedId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Equipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Equipmen__3214EC07428901EF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipments_Category",
                        column: x => x.CategoryId,
                        principalTable: "EquipmentCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__3214EC071248A9EA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    ClassCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Classes__3214EC07C8A04ADF", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classes_Courses",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Classes_Teacher",
                        column: x => x.TeacherId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    ReportedBy = table.Column<int>(type: "int", nullable: false),
                    Issue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Maintena__3214EC07F0FC039A", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintLogs_Equipment",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintLogs_Reporter",
                        column: x => x.ReportedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    TotalValidSessions = table.Column<int>(type: "int", nullable: false),
                    SessionEarnings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Bonuses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRecords_Users_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Posts__3214EC074776CE99", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Posts_Author",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CurrentSchool = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GuardianName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GuardianPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MedicalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StudentP__1788CC4CFA93FF51", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StudentProfiles_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeacherProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SalaryTier = table.Column<int>(type: "int", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CustomSessionRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_TeacherProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    EnrollDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Enrollme__3214EC07EA03FFEB", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollments_Classes",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Enrollments_Student",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ClassId = table.Column<int>(type: "int", nullable: true),
                    InvoiceNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Invoices__3214EC07A9A8E95F", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Class",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Student",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    SessionNo = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TeachingMaterialUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassMediaUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssistantNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayrollStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Sessions__3214EC074B24E950", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Classes",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sessions_Rooms",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TransDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payments__3214EC073957C8E5", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Invoice",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    IsExcused = table.Column<bool>(type: "bit", nullable: false),
                    ProductMediaUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TeacherRawNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiEvaluation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VideoTranscript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiProcessStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Attendan__3214EC07CBE09DDC", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Sessions",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Attendances_Student",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EquipmentBorrows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    BorrowerId = table.Column<int>(type: "int", nullable: false),
                    BorrowTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    ReturnTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Equipmen__3214EC07F1639B96", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EqBorrows_Borrower",
                        column: x => x.BorrowerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EqBorrows_Equipment",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EqBorrows_Session",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSkillScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceId = table.Column<int>(type: "int", nullable: false),
                    SkillName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSkillScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSkillScores_Attendances",
                        column: x => x.AttendanceId,
                        principalTable: "Attendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_SessionId",
                table: "Attendances",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_StudentId",
                table: "Attendances",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSkillScores_AttendanceId",
                table: "AttendanceSkillScores",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_CourseId",
                table: "Classes",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_TeacherId",
                table: "Classes",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "UQ__Classes__2ECD4A55BB375314",
                table: "Classes",
                column: "ClassCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Courses__A25C5AA769A22348",
                table: "Courses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ClassId",
                table: "Enrollments",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "UQ_Enrollment_Student_Class",
                table: "Enrollments",
                columns: new[] { "StudentId", "ClassId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBorrows_BorrowerId",
                table: "EquipmentBorrows",
                column: "BorrowerId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBorrows_EquipmentId",
                table: "EquipmentBorrows",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentBorrows_SessionId",
                table: "EquipmentBorrows",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_CategoryId",
                table: "Equipments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "UQ__Equipmen__048A0008296303C3",
                table: "Equipments",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClassId",
                table: "Invoices",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_StudentId",
                table: "Invoices",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ__Invoices__D796B227747B5D19",
                table: "Invoices",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_InterestedId",
                table: "Leads",
                column: "InterestedId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogs_EquipmentId",
                table: "MaintenanceLogs",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogs_ReportedBy",
                table: "MaintenanceLogs",
                column: "ReportedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRecords_TeacherId",
                table: "PayrollRecords",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId",
                table: "Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "UQ__Posts__BC7B5FB60B72526D",
                table: "Posts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Roles__737584F621924AD9",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClassId_Date",
                table: "Sessions",
                columns: new[] { "ClassId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Date",
                table: "Sessions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RoomId",
                table: "Sessions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__536C85E4F7CBAA04",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceSkillScores");

            migrationBuilder.DropTable(
                name: "Banners");

            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "EquipmentBorrows");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "MaintenanceLogs");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PayRateConfigs");

            migrationBuilder.DropTable(
                name: "PayrollRecords");

            migrationBuilder.DropTable(
                name: "Posts");

            migrationBuilder.DropTable(
                name: "StudentProfiles");

            migrationBuilder.DropTable(
                name: "TeacherProfiles");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "Equipments");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "EquipmentCategories");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Courses");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
