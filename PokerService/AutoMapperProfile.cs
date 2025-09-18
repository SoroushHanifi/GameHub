using AutoMapper;
using PokerService.Models;
using PokerService.Models.Dtos;

namespace PokerService
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Room, RoomDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<Player, PlayerDto>();

            CreateMap<Card, CardDto>()
                .ForMember(dest => dest.Suit, opt => opt.MapFrom(src => src.Suit.ToString()))
                .ForMember(dest => dest.Rank, opt => opt.MapFrom(src => src.Rank.ToString()));

            CreateMap<GameState, GameStateDto>()
                .ForMember(dest => dest.Phase, opt => opt.MapFrom(src => src.Phase.ToString()));
        }
    }
}
