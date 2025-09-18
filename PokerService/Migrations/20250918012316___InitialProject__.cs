using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerService.Migrations
{
    /// <inheritdoc />
    public partial class __InitialProject__ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDelete",
                table: "Rooms",
                newName: "IsPrivate");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUsername",
                table: "Rooms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "Rooms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPlayers",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinPlayers",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Rooms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUsername",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "MaxPlayers",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "MinPlayers",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Rooms");

            migrationBuilder.RenameColumn(
                name: "IsPrivate",
                table: "Rooms",
                newName: "IsDelete");
        }
    }
}
