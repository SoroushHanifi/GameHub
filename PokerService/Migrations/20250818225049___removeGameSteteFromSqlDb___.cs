using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerService.Migrations
{
    /// <inheritdoc />
    public partial class __removeGameSteteFromSqlDb___ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Card_GameState_GameStateId",
                table: "Card");

            migrationBuilder.DropForeignKey(
                name: "FK_Player_Rooms_RoomId",
                table: "Player");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_GameState_GameStateId",
                table: "Rooms");

            migrationBuilder.DropTable(
                name: "GameState");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_GameStateId",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Card_GameStateId",
                table: "Card");

            migrationBuilder.DropColumn(
                name: "GameStateId",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "GameStateId",
                table: "Card");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "Player",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Player_Rooms_RoomId",
                table: "Player",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Player_Rooms_RoomId",
                table: "Player");

            migrationBuilder.AddColumn<Guid>(
                name: "GameStateId",
                table: "Rooms",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "Player",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "GameStateId",
                table: "Card",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameState",
                columns: table => new
                {
                    GameStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentBet = table.Column<int>(type: "int", nullable: false),
                    CurrentTurnUsername = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameState", x => x.GameStateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_GameStateId",
                table: "Rooms",
                column: "GameStateId");

            migrationBuilder.CreateIndex(
                name: "IX_Card_GameStateId",
                table: "Card",
                column: "GameStateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Card_GameState_GameStateId",
                table: "Card",
                column: "GameStateId",
                principalTable: "GameState",
                principalColumn: "GameStateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Player_Rooms_RoomId",
                table: "Player",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_GameState_GameStateId",
                table: "Rooms",
                column: "GameStateId",
                principalTable: "GameState",
                principalColumn: "GameStateId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
