using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FcmToken = table.Column<string>(type: "text", nullable: true),
                    VehicleId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Commodities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommodityName = table.Column<string>(type: "text", nullable: false),
                    HsCode = table.Column<string>(type: "text", nullable: false),
                    IsHazardous = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commodities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchCode = table.Column<string>(type: "text", nullable: false),
                    BranchName = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyBranches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContainerNumber = table.Column<string>(type: "text", nullable: false),
                    ContainerType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContainerSizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SizeName = table.Column<string>(type: "text", nullable: false),
                    ShortCode = table.Column<string>(type: "text", nullable: false),
                    TeuFactor = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    PayloadKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerSizes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CountryName = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurrencyName = table.Column<string>(type: "text", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    ExchangeRateToInr = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentTypeName = table.Column<string>(type: "text", nullable: false),
                    HasExpiry = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverDocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    From = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasAttachment = table.Column<bool>(type: "boolean", nullable: false),
                    JobCreated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FcmRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FcmRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<int>(type: "integer", nullable: true),
                    JobCode = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegionName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePagePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    PagePath = table.Column<string>(type: "text", nullable: false),
                    Permission = table.Column<int>(type: "integer", nullable: false),
                    IsGranted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePagePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sacs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SacCode = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    GstRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sacs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityCode = table.Column<string>(type: "text", nullable: false),
                    ActivityName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepartmentName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDepartments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transporters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    ContactPerson = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    WhatsAppNumber = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transporters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentTypeName = table.Column<string>(type: "text", nullable: false),
                    HasExpiry = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleDocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlateNumber = table.Column<string>(type: "text", nullable: false),
                    VehicleType = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vessels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VesselName = table.Column<string>(type: "text", nullable: false),
                    ImoNumber = table.Column<string>(type: "text", nullable: false),
                    Flag = table.Column<string>(type: "text", nullable: false),
                    CallSign = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vessels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebPushSubs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    P256dh = table.Column<string>(type: "text", nullable: false),
                    Auth = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebPushSubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContainerId = table.Column<int>(type: "integer", nullable: true),
                    JobId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PortName = table.Column<string>(type: "text", nullable: false),
                    PortCode = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CountryId = table.Column<int>(type: "integer", nullable: true),
                    City = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ports_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "States",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateName = table.Column<string>(type: "text", nullable: false),
                    StateCode = table.Column<string>(type: "text", nullable: false),
                    RegionId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_States", x => x.Id);
                    table.ForeignKey(
                        name: "FK_States_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChargeCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChargeName = table.Column<string>(type: "text", nullable: false),
                    ShortCode = table.Column<string>(type: "text", nullable: false),
                    SacId = table.Column<int>(type: "integer", nullable: true),
                    DefaultAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargeCodes_Sacs_SacId",
                        column: x => x.SacId,
                        principalTable: "Sacs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StaffDesignations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DesignationName = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDesignations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffDesignations_StaffDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "StaffDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AwbShipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HawbNo = table.Column<string>(type: "text", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StationCode = table.Column<string>(type: "text", nullable: false),
                    ShipperAccount = table.Column<string>(type: "text", nullable: false),
                    ShipperName = table.Column<string>(type: "text", nullable: false),
                    ShipperAddress = table.Column<string>(type: "text", nullable: false),
                    ShipperPhone = table.Column<string>(type: "text", nullable: false),
                    ShipperContact = table.Column<string>(type: "text", nullable: false),
                    ConsigneeAccount = table.Column<string>(type: "text", nullable: false),
                    ConsigneeName = table.Column<string>(type: "text", nullable: false),
                    ConsigneeAddress = table.Column<string>(type: "text", nullable: false),
                    ConsigneePhone = table.Column<string>(type: "text", nullable: false),
                    ConsigneeContact = table.Column<string>(type: "text", nullable: false),
                    OriginStation = table.Column<string>(type: "text", nullable: false),
                    DestinationStation = table.Column<string>(type: "text", nullable: false),
                    ReferenceNumbers = table.Column<string>(type: "text", nullable: false),
                    HandlingInfo = table.Column<string>(type: "text", nullable: false),
                    Pieces = table.Column<int>(type: "integer", nullable: false),
                    GrossWeightKg = table.Column<double>(type: "double precision", nullable: false),
                    ChargeableWeight = table.Column<double>(type: "double precision", nullable: false),
                    RateClass = table.Column<string>(type: "text", nullable: false),
                    GoodsDescription = table.Column<string>(type: "text", nullable: false),
                    HsCode = table.Column<string>(type: "text", nullable: false),
                    Dimensions = table.Column<string>(type: "text", nullable: false),
                    VolumeCbm = table.Column<double>(type: "double precision", nullable: false),
                    Slac = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    DeclaredValueCarriage = table.Column<string>(type: "text", nullable: false),
                    DeclaredValueCustoms = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceEmail = table.Column<string>(type: "text", nullable: false),
                    SourceFile = table.Column<string>(type: "text", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TransporterId = table.Column<int>(type: "integer", nullable: true),
                    PickupLocation = table.Column<string>(type: "text", nullable: true),
                    DropLocation = table.Column<string>(type: "text", nullable: true),
                    TransporterAssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VehicleNumber = table.Column<string>(type: "text", nullable: true),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    DriverMobile = table.Column<string>(type: "text", nullable: true),
                    VehicleDetailsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtPortAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveryPhotoPath = table.Column<string>(type: "text", nullable: true),
                    GodownReceiptPath = table.Column<string>(type: "text", nullable: true),
                    CustomsDocsReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomsDocPath = table.Column<string>(type: "text", nullable: true),
                    InvoiceSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    InvoiceFilePath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwbShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwbShipments_Transporters_TransporterId",
                        column: x => x.TransporterId,
                        principalTable: "Transporters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExportJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobReference = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CargoDescription = table.Column<string>(type: "text", nullable: false),
                    HsCode = table.Column<string>(type: "text", nullable: false),
                    Pieces = table.Column<int>(type: "integer", nullable: false),
                    GrossWeightKg = table.Column<double>(type: "double precision", nullable: false),
                    IsEmergency = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PickupInitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransporterId = table.Column<int>(type: "integer", nullable: true),
                    TransporterBookedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VehicleNumber = table.Column<string>(type: "text", nullable: true),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    DriverMobile = table.Column<string>(type: "text", nullable: true),
                    VehicleAssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RiNumber = table.Column<string>(type: "text", nullable: true),
                    CargoCollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChecklistRef = table.Column<string>(type: "text", nullable: true),
                    ChecklistSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IcegateRef = table.Column<string>(type: "text", nullable: true),
                    ShippingBillNumber = table.Column<string>(type: "text", nullable: true),
                    ShippingBillAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OperationStaffName = table.Column<string>(type: "text", nullable: true),
                    HandedOverAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PortArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PortClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExportReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BookingReference = table.Column<string>(type: "text", nullable: true),
                    BookingReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SurveyTeamName = table.Column<string>(type: "text", nullable: true),
                    SurveyForwardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SurveyCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoadingAuthReference = table.Column<string>(type: "text", nullable: true),
                    LoadingAuthAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContainerNumber = table.Column<string>(type: "text", nullable: true),
                    SealNumber = table.Column<string>(type: "text", nullable: true),
                    CargoLoadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShippingInstruction = table.Column<string>(type: "text", nullable: true),
                    DeliveryOrderRef = table.Column<string>(type: "text", nullable: true),
                    ThirdPartyName = table.Column<string>(type: "text", nullable: true),
                    SiDoReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InTransitToIcttAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Sez4Reference = table.Column<string>(type: "text", nullable: true),
                    VesselName = table.Column<string>(type: "text", nullable: true),
                    VoyageNumber = table.Column<string>(type: "text", nullable: true),
                    TerminalGateInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportJobs_Transporters_TransporterId",
                        column: x => x.TransporterId,
                        principalTable: "Transporters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobCode = table.Column<string>(type: "text", nullable: false),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    PickupAddress = table.Column<string>(type: "text", nullable: false),
                    PickupCity = table.Column<string>(type: "text", nullable: false),
                    VolumeCbm = table.Column<double>(type: "double precision", nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    Destination = table.Column<string>(type: "text", nullable: false),
                    DhlReference = table.Column<string>(type: "text", nullable: false),
                    PickupDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedExecutiveId = table.Column<string>(type: "text", nullable: true),
                    AssignedVehicleId = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PickedUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Vehicles_AssignedVehicleId",
                        column: x => x.AssignedVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VehicleDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    VehicleDocumentTypeId = table.Column<int>(type: "integer", nullable: false),
                    DocumentNumber = table.Column<string>(type: "text", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FilePath = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleDocuments_VehicleDocumentTypes_VehicleDocumentTypeId",
                        column: x => x.VehicleDocumentTypeId,
                        principalTable: "VehicleDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleDocuments_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleDrivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DriverName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    LicenseNo = table.Column<string>(type: "text", nullable: false),
                    LicenseExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedVehicleId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleDrivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleDrivers_Vehicles_AssignedVehicleId",
                        column: x => x.AssignedVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SezLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LocationName = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    StateId = table.Column<int>(type: "integer", nullable: true),
                    CountryId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SezLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SezLocations_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SezLocations_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DesignationId = table.Column<int>(type: "integer", nullable: true),
                    DateOfJoining = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Staff_StaffDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "StaffDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Staff_StaffDesignations_DesignationId",
                        column: x => x.DesignationId,
                        principalTable: "StaffDesignations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AwbShipmentId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentEvents_AwbShipments_AwbShipmentId",
                        column: x => x.AwbShipmentId,
                        principalTable: "AwbShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExportJobEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExportJobId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportJobEvents_ExportJobs_ExportJobId",
                        column: x => x.ExportJobId,
                        principalTable: "ExportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GpsLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Speed = table.Column<double>(type: "double precision", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PickupJobId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GpsLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GpsLocations_Jobs_PickupJobId",
                        column: x => x.PickupJobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JobOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ShipmentMode = table.Column<int>(type: "integer", nullable: false),
                    ShipmentType = table.Column<int>(type: "integer", nullable: false),
                    CargoType = table.Column<int>(type: "integer", nullable: false),
                    JobOrderNo = table.Column<string>(type: "text", nullable: false),
                    JobOrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinYear = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    BillingClientId = table.Column<int>(type: "integer", nullable: false),
                    ShipperId = table.Column<int>(type: "integer", nullable: false),
                    ConsigneeId = table.Column<int>(type: "integer", nullable: false),
                    SaleStaffId = table.Column<int>(type: "integer", nullable: true),
                    LoadPortId = table.Column<int>(type: "integer", nullable: true),
                    DischargePortId = table.Column<int>(type: "integer", nullable: true),
                    CommodityId = table.Column<int>(type: "integer", nullable: true),
                    CargoDescription = table.Column<string>(type: "text", nullable: false),
                    ContainerSizeId = table.Column<int>(type: "integer", nullable: true),
                    LclUnits = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    LclUnitType = table.Column<string>(type: "text", nullable: true),
                    GrossWeightKg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    VolumeCbm = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    CurrencyId = table.Column<int>(type: "integer", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsNominated = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmergency = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedBy = table.Column<string>(type: "text", nullable: true),
                    VerifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    RejectedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOrders_Clients_BillingClientId",
                        column: x => x.BillingClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Clients_ConsigneeId",
                        column: x => x.ConsigneeId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Clients_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Commodities_CommodityId",
                        column: x => x.CommodityId,
                        principalTable: "Commodities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_CompanyBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "CompanyBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_ContainerSizes_ContainerSizeId",
                        column: x => x.ContainerSizeId,
                        principalTable: "ContainerSizes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_Ports_DischargePortId",
                        column: x => x.DischargePortId,
                        principalTable: "Ports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Ports_LoadPortId",
                        column: x => x.LoadPortId,
                        principalTable: "Ports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Staff_SaleStaffId",
                        column: x => x.SaleStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JobOrderEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOrderId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Actor = table.Column<string>(type: "text", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOrderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOrderEvents_JobOrders_JobOrderId",
                        column: x => x.JobOrderId,
                        principalTable: "JobOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwbShipments_TransporterId",
                table: "AwbShipments",
                column: "TransporterId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeCodes_SacId",
                table: "ChargeCodes",
                column: "SacId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ContainerId",
                table: "Collections",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyBranches_BranchCode",
                table: "CompanyBranches",
                column: "BranchCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_CountryCode",
                table: "Countries",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_CurrencyCode",
                table: "Currencies",
                column: "CurrencyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobEvents_ExportJobId",
                table: "ExportJobEvents",
                column: "ExportJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_TransporterId",
                table: "ExportJobs",
                column: "TransporterId");

            migrationBuilder.CreateIndex(
                name: "IX_GpsLocations_PickupJobId",
                table: "GpsLocations",
                column: "PickupJobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrderEvents_JobOrderId_At",
                table: "JobOrderEvents",
                columns: new[] { "JobOrderId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_BillingClientId",
                table: "JobOrders",
                column: "BillingClientId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_BranchId",
                table: "JobOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_CommodityId",
                table: "JobOrders",
                column: "CommodityId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_ConsigneeId",
                table: "JobOrders",
                column: "ConsigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_ContainerSizeId",
                table: "JobOrders",
                column: "ContainerSizeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_CurrencyId",
                table: "JobOrders",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_DischargePortId",
                table: "JobOrders",
                column: "DischargePortId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_JobOrderNo",
                table: "JobOrders",
                column: "JobOrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_LoadPortId",
                table: "JobOrders",
                column: "LoadPortId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_Mode_FinYear",
                table: "JobOrders",
                columns: new[] { "Mode", "FinYear" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_SaleStaffId",
                table: "JobOrders",
                column: "SaleStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_ShipperId",
                table: "JobOrders",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_Status",
                table: "JobOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AssignedVehicleId",
                table: "Jobs",
                column: "AssignedVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Ports_CountryId",
                table: "Ports",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Ports_PortCode",
                table: "Ports",
                column: "PortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePagePermissions_RoleId_PagePath",
                table: "RolePagePermissions",
                columns: new[] { "RoleId", "PagePath" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePagePermissions_RoleId_PagePath_Permission",
                table: "RolePagePermissions",
                columns: new[] { "RoleId", "PagePath", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sacs_SacCode",
                table: "Sacs",
                column: "SacCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SezLocations_CountryId",
                table: "SezLocations",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_SezLocations_StateId",
                table: "SezLocations",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentActivities_ActivityCode",
                table: "ShipmentActivities",
                column: "ActivityCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentEvents_AwbShipmentId",
                table: "ShipmentEvents",
                column: "AwbShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_DepartmentId",
                table: "Staff",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_DesignationId",
                table: "Staff",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDesignations_DepartmentId",
                table: "StaffDesignations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_States_RegionId",
                table: "States",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_States_StateCode",
                table: "States",
                column: "StateCode");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleDocuments_VehicleDocumentTypeId",
                table: "VehicleDocuments",
                column: "VehicleDocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleDocuments_VehicleId",
                table: "VehicleDocuments",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleDrivers_AssignedVehicleId",
                table: "VehicleDrivers",
                column: "AssignedVehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ChargeCodes");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "DriverDocumentTypes");

            migrationBuilder.DropTable(
                name: "EmailLogs");

            migrationBuilder.DropTable(
                name: "ExportJobEvents");

            migrationBuilder.DropTable(
                name: "FcmRegistrations");

            migrationBuilder.DropTable(
                name: "GpsLocations");

            migrationBuilder.DropTable(
                name: "JobOrderEvents");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "RolePagePermissions");

            migrationBuilder.DropTable(
                name: "SezLocations");

            migrationBuilder.DropTable(
                name: "ShipmentActivities");

            migrationBuilder.DropTable(
                name: "ShipmentEvents");

            migrationBuilder.DropTable(
                name: "VehicleDocuments");

            migrationBuilder.DropTable(
                name: "VehicleDrivers");

            migrationBuilder.DropTable(
                name: "Vessels");

            migrationBuilder.DropTable(
                name: "WebPushSubs");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Sacs");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "ExportJobs");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "JobOrders");

            migrationBuilder.DropTable(
                name: "States");

            migrationBuilder.DropTable(
                name: "AwbShipments");

            migrationBuilder.DropTable(
                name: "VehicleDocumentTypes");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Commodities");

            migrationBuilder.DropTable(
                name: "CompanyBranches");

            migrationBuilder.DropTable(
                name: "ContainerSizes");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "Ports");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropTable(
                name: "Transporters");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "StaffDesignations");

            migrationBuilder.DropTable(
                name: "StaffDepartments");
        }
    }
}
