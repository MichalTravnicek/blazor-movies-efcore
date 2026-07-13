using AutoMapper;
using BlazorWebAppMovies.Models.Dtos;

namespace BlazorWebAppMovies.Models.Mapping;

/// <summary>
/// AutoMapper profile for Movie entity ↔ DTO mappings.
/// Properties with matching names are mapped automatically.
/// </summary>
public class MovieProfile : Profile
{
    public MovieProfile()
    {
        // Movie → MovieDto (output, includes Id)
        // Map Rating from the navigation property's Code
        CreateMap<Movie, MovieDto>()
            .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.MovieRating != null ? src.MovieRating.Code : null));

        // CreateMovieDto → Movie (input, no Id — ignored because DB generates it)
        // PosterUrl is handled by PosterService on the backend, not from DTO
        CreateMap<CreateMovieDto, Movie>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.MovieRatingId, opt => opt.Ignore())
            .ForMember(dest => dest.MovieRating, opt => opt.Ignore())
            .ForMember(dest => dest.PosterUrl, opt => opt.Ignore());

        // UpdateMovieDto → Movie (input, no Id — ignored to preserve existing entity Id)
        // PosterUrl is handled by PosterService on the backend, not from DTO
        CreateMap<UpdateMovieDto, Movie>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.MovieRatingId, opt => opt.Ignore())
            .ForMember(dest => dest.MovieRating, opt => opt.Ignore())
            .ForMember(dest => dest.PosterUrl, opt => opt.Ignore());
    }
}
