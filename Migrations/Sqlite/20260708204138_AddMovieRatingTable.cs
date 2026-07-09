using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlazorWebAppMovies.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddMovieRatingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the MovieRating lookup table first so it exists for FK references
            migrationBuilder.CreateTable(
                name: "MovieRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieRating", x => x.Id);
                });

            // 2. Add MovieRatingId with a valid default (1 = "G") so existing rows
            //    get a valid FK value during the SQLite table rebuild
            migrationBuilder.AddColumn<int>(
                name: "MovieRatingId",
                table: "Movie",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // 4. Migrate existing rows: map the old Rating string to the new FK
            migrationBuilder.Sql(@"
                UPDATE ""Movie""
                SET ""MovieRatingId"" = (
                    SELECT ""Id"" FROM ""MovieRating"" WHERE ""Code"" = ""Movie"".""Rating""
                )
                WHERE ""Rating"" IS NOT NULL AND ""Rating"" != ''");

            // 5. Drop the old Rating column
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Movie");

            // 6. Create index and FK constraint
            migrationBuilder.CreateIndex(
                name: "IX_Movie_MovieRatingId",
                table: "Movie",
                column: "MovieRatingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Movie_MovieRating_MovieRatingId",
                table: "Movie",
                column: "MovieRatingId",
                principalTable: "MovieRating",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movie_MovieRating_MovieRatingId",
                table: "Movie");

            migrationBuilder.DropTable(
                name: "MovieRating");

            migrationBuilder.DropIndex(
                name: "IX_Movie_MovieRatingId",
                table: "Movie");

            migrationBuilder.DropColumn(
                name: "MovieRatingId",
                table: "Movie");

            migrationBuilder.AddColumn<string>(
                name: "Rating",
                table: "Movie",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
