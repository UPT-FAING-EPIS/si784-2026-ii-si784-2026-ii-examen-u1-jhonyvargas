using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Subasta.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador de la categoría")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Nombre de la categoría")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                },
                comment: "Categorías de artículos");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador único del usuario"),
                    UserName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Nombre de usuario visible (único)"),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Correo electrónico (único)"),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Hash PBKDF2 de la contraseña"),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Rol: User o Admin"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica si la cuenta está habilitada"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Fecha de registro (UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                },
                comment: "Usuarios registrados de la plataforma");

            migrationBuilder.CreateTable(
                name: "auctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador de la subasta"),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, comment: "Título del artículo"),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false, comment: "Descripción del artículo"),
                    CategoryId = table.Column<int>(type: "integer", nullable: false, comment: "Categoría del artículo"),
                    StartingPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Precio inicial"),
                    MinIncrement = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Incremento mínimo entre pujas"),
                    CurrentPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Precio actual (mejor puja)"),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Fecha/hora de inicio (UTC)"),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Fecha/hora de cierre (UTC)"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Estado: Scheduled, Active, Finished, Cancelled"),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false, comment: "Usuario que publica la subasta"),
                    WinnerId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Usuario adjudicatario"),
                    WinningBidId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Puja ganadora"),
                    BidCount = table.Column<int>(type: "integer", nullable: false, comment: "Cantidad de pujas recibidas"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Fecha de publicación (UTC)"),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Fecha de cierre/adjudicación (UTC)"),
                    Version = table.Column<int>(type: "integer", nullable: false, comment: "Control de concurrencia optimista")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auctions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auctions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_auctions_users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_auctions_users_WinnerId",
                        column: x => x.WinnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Artículos publicados para subasta");

            migrationBuilder.CreateTable(
                name: "auction_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador de la imagen"),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false, comment: "Subasta a la que pertenece"),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nombre original del archivo"),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Tipo MIME de la imagen"),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false, comment: "Contenido binario de la imagen"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, comment: "Orden de visualización"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Fecha de carga (UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auction_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auction_images_auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Imágenes de los artículos subastados");

            migrationBuilder.CreateTable(
                name: "bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador de la puja"),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: false, comment: "Subasta pujada"),
                    BidderId = table.Column<Guid>(type: "uuid", nullable: false, comment: "Usuario que puja"),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Monto ofertado"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Fecha/hora de la puja (UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bids_auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bids_users_BidderId",
                        column: x => x.BidderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Ofertas (pujas) realizadas");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador de la notificación"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false, comment: "Usuario destinatario"),
                    AuctionId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Subasta relacionada"),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Tipo de notificación"),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Mensaje mostrado al usuario"),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica si fue leída"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Fecha de creación (UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Notificaciones enviadas a los usuarios");

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Electrónica" },
                    { 2, "Hogar" },
                    { 3, "Moda" },
                    { 4, "Vehículos" },
                    { 5, "Arte y colecciones" },
                    { 6, "Deportes" },
                    { 7, "Otros" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_auction_images_AuctionId",
                table: "auction_images",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_auctions_CategoryId",
                table: "auctions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_auctions_SellerId",
                table: "auctions",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_auctions_StartAt",
                table: "auctions",
                column: "StartAt");

            migrationBuilder.CreateIndex(
                name: "IX_auctions_Status_EndAt",
                table: "auctions",
                columns: new[] { "Status", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_auctions_WinnerId",
                table: "auctions",
                column: "WinnerId");

            migrationBuilder.CreateIndex(
                name: "IX_bids_AuctionId_Amount",
                table: "bids",
                columns: new[] { "AuctionId", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_bids_BidderId",
                table: "bids",
                column: "BidderId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_Name",
                table: "categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_AuctionId",
                table: "notifications",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_IsRead",
                table: "notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_UserName",
                table: "users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auction_images");

            migrationBuilder.DropTable(
                name: "bids");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "auctions");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
