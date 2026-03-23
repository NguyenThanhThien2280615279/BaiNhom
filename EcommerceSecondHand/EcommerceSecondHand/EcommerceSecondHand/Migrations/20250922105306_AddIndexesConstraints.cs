using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceSecondHand.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adjust column types to support indexing
            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
            // Unique constraints
            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true,
                filter: "[OrderNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TransactionReference",
                table: "PaymentTransactions",
                column: "TransactionReference",
                unique: true,
                filter: "[TransactionReference] IS NOT NULL");

            // Ensure replacing existing non-unique index with unique one
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserStatistics_UserId' AND object_id = OBJECT_ID('[UserStatistics]')) DROP INDEX [IX_UserStatistics_UserId] ON [UserStatistics]");
            migrationBuilder.CreateIndex(
                name: "IX_UserStatistics_UserId",
                table: "UserStatistics",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_User_Product",
                table: "CartItems",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_User_Product",
                table: "Reviews",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            // Performance composite indexes
            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_DateCreated",
                table: "Products",
                columns: new[] { "CategoryId", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Seller_DateCreated",
                table: "Products",
                columns: new[] { "SellerId", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_User_OrderDate",
                table: "Orders",
                columns: new[] { "UserId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Seller_OrderDate",
                table: "Orders",
                columns: new[] { "SellerId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_User_CreatedAt",
                table: "PaymentTransactions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_User_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Sender_DateSent",
                table: "Messages",
                columns: new[] { "SenderId", "DateSent" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Receiver_DateSent",
                table: "Messages",
                columns: new[] { "ReceiverId", "DateSent" });

            migrationBuilder.CreateIndex(
                name: "IX_RechargeTransactions_User_CreatedAt",
                table: "RechargeTransactions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Availability_DateCreated",
                table: "Products",
                columns: new[] { "IsActive", "IsAvailable", "DateCreated" });

            // Check constraints
            migrationBuilder.Sql("ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_Price_NonNegative] CHECK ([Price] >= 0)");
            migrationBuilder.Sql("ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_Quantity_NonNegative] CHECK ([Quantity] >= 0)");
            migrationBuilder.Sql("ALTER TABLE [Reviews] ADD CONSTRAINT [CK_Reviews_Rating_Range] CHECK ([Rating] BETWEEN 1 AND 5)");
            migrationBuilder.Sql("ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_TotalAmount_NonNegative] CHECK ([TotalAmount] >= 0)");
            migrationBuilder.Sql("ALTER TABLE [PaymentTransactions] ADD CONSTRAINT [CK_PaymentTransactions_Amount_NonNegative] CHECK ([Amount] >= 0)");
            migrationBuilder.Sql("ALTER TABLE [AspNetUsers] ADD CONSTRAINT [CK_AspNetUsers_WalletBalance_NonNegative] CHECK ([WalletBalance] >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert column alteration
            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
            // Drop check constraints
            migrationBuilder.Sql("IF OBJECT_ID('CK_AspNetUsers_WalletBalance_NonNegative', 'C') IS NOT NULL ALTER TABLE [AspNetUsers] DROP CONSTRAINT [CK_AspNetUsers_WalletBalance_NonNegative]");
            migrationBuilder.Sql("IF OBJECT_ID('CK_PaymentTransactions_Amount_NonNegative', 'C') IS NOT NULL ALTER TABLE [PaymentTransactions] DROP CONSTRAINT [CK_PaymentTransactions_Amount_NonNegative]");
            migrationBuilder.Sql("IF OBJECT_ID('CK_Orders_TotalAmount_NonNegative', 'C') IS NOT NULL ALTER TABLE [Orders] DROP CONSTRAINT [CK_Orders_TotalAmount_NonNegative]");
            migrationBuilder.Sql("IF OBJECT_ID('CK_Reviews_Rating_Range', 'C') IS NOT NULL ALTER TABLE [Reviews] DROP CONSTRAINT [CK_Reviews_Rating_Range]");
            migrationBuilder.Sql("IF OBJECT_ID('CK_Products_Quantity_NonNegative', 'C') IS NOT NULL ALTER TABLE [Products] DROP CONSTRAINT [CK_Products_Quantity_NonNegative]");
            migrationBuilder.Sql("IF OBJECT_ID('CK_Products_Price_NonNegative', 'C') IS NOT NULL ALTER TABLE [Products] DROP CONSTRAINT [CK_Products_Price_NonNegative]");

            // Drop indexes
            migrationBuilder.DropIndex(name: "IX_Products_Availability_DateCreated", table: "Products");
            migrationBuilder.DropIndex(name: "IX_RechargeTransactions_User_CreatedAt", table: "RechargeTransactions");
            migrationBuilder.DropIndex(name: "IX_Messages_Receiver_DateSent", table: "Messages");
            migrationBuilder.DropIndex(name: "IX_Messages_Sender_DateSent", table: "Messages");
            migrationBuilder.DropIndex(name: "IX_Notifications_User_IsRead_CreatedAt", table: "Notifications");
            migrationBuilder.DropIndex(name: "IX_PaymentTransactions_User_CreatedAt", table: "PaymentTransactions");
            migrationBuilder.DropIndex(name: "IX_Orders_Seller_OrderDate", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_User_OrderDate", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Products_Seller_DateCreated", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Products_Category_DateCreated", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Reviews_User_Product", table: "Reviews");
            migrationBuilder.DropIndex(name: "IX_CartItems_User_Product", table: "CartItems");
            migrationBuilder.DropIndex(name: "IX_UserStatistics_UserId", table: "UserStatistics");
            migrationBuilder.DropIndex(name: "IX_PaymentTransactions_TransactionReference", table: "PaymentTransactions");
            migrationBuilder.DropIndex(name: "IX_Orders_OrderNumber", table: "Orders");
        }
    }
}
