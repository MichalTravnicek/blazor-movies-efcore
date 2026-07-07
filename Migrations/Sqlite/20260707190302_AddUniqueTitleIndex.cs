using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebAppMovies.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUniqueTitleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Movie_Title",
                table: "Movie",
                column: "Title",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Movie_Title",
                table: "Movie");
        }
    }
}
