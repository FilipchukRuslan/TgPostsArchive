using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TelegramPostsArchive.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramChannels",
                columns: table => new
                {
                    ChannelTag = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    LastProcessedMessageId = table.Column<int>(type: "integer", nullable: true),
                    IsHistoricalCollectionCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    NewestCollectedMessageId = table.Column<int>(type: "integer", nullable: true),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChannels", x => x.ChannelTag);
                });

            migrationBuilder.CreateTable(
                name: "TelegramPosts",
                columns: table => new
                {
                    ChannelTag = table.Column<string>(type: "character varying(32)", nullable: false),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    Views = table.Column<int>(type: "integer", nullable: true),
                    MediaUrls = table.Column<string[]>(type: "text[]", nullable: false),
                    ForwardFrom = table.Column<string>(type: "text", nullable: true),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramPosts", x => new { x.ChannelTag, x.MessageId });
                    table.ForeignKey(
                        name: "FK_TelegramPosts_TelegramChannels_ChannelTag",
                        column: x => x.ChannelTag,
                        principalTable: "TelegramChannels",
                        principalColumn: "ChannelTag",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "TelegramChannels",
                columns: new[] { "ChannelTag", "IsHistoricalCollectionCompleted", "LastProcessedMessageId", "LastSuccessfulSyncAt", "NewestCollectedMessageId", "Title" },
                values: new object[,]
                {
                    { "dsns_telegram", false, null, null, null, null },
                    { "kpszsu", false, null, null, null, null },
                    { "ukrainenow", false, null, null, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramPosts");

            migrationBuilder.DropTable(
                name: "TelegramChannels");
        }
    }
}
