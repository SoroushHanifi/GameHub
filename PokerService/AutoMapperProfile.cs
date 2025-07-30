using AutoMapper;
using PokerService.Models;
using PokerService.Models.Dtos;

namespace PokerService
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile() 
        { 
            CreateMap<Room, RoomDto>();
            CreateMap<Player, PlayerDto>();
            CreateMap<GameState, GameStateDto>(); 
        }
    }
}
