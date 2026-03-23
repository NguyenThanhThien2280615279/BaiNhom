using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceSecondHand.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockchainReputationFieldsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockchainAddress",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OnChainAverageRating",
                table: "AspNetUsers",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<byte>(
                name: "OnChainBadgeLevel",
                table: "AspNetUsers",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "OnChainSuccessfulSales",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockchainAddress",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OnChainAverageRating",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OnChainBadgeLevel",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OnChainSuccessfulSales",
                table: "AspNetUsers");
        }
    }
}
