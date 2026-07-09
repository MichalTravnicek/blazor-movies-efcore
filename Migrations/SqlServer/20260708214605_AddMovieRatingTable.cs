using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlazorWebAppMovies.Migrations.SqlServer
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieRating", x => x.Id);
                });

            // 2. Add MovieRatingId as nullable so we can populate it without FK issues
            migrationBuilder.AddColumn<int>(
                name: "MovieRatingId",
                table: "Movie",
                type: "int",
                nullable: true);

            // 4. Migrate existing rows: map the old Rating string to the new FK
            migrationBuilder.Sql(@"
                UPDATE Movie
                SET MovieRatingId = (SELECT Id FROM MovieRating WHERE Code = Movie.Rating)
                WHERE Rating IS NOT NULL AND Rating != ''");

            // 5. Make MovieRatingId non-nullable now that all rows have valid values
            migrationBuilder.Sql(@"
                ALTER TABLE Movie ALTER COLUMN MovieRatingId int NOT NULL");

            // 6. Drop the old Rating column
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Movie");

            // 7. Create index and FK constraint
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
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
