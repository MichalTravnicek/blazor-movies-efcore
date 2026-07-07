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
        CreateMap<Movie, MovieDto>();

        // CreateMovieDto → Movie (input, no Id — ignored because DB generates it)
        CreateMap<CreateMovieDto, Movie>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());

        // UpdateMovieDto → Movie (input, no Id — ignored to preserve existing entity Id)
        CreateMap<UpdateMovieDto, Movie>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
    }
}
