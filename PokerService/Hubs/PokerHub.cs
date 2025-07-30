using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using PokerService.Models.Dtos;
using PokerService.Services;

namespace PokerService.Hubs
{
    public class PokerHub : Hub
    {
        private readonly GameService _gameService;
        private readonly IMapper _mapper;

        public PokerHub(GameService gameService, IMapper mapper)
        {
            _gameService = gameService;
            _mapper = mapper;
        }

        public async Task JoinRoom(string roomId)
        {
            var userId = Context.UserIdentifier; // از JWT
            var username = Context.User.Identity.Name; // از JWT یا AuthService
            await _gameService.JoinRoomAsync(roomId, userId, username);

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            var room = await _gameService.GetRoomAsync(roomId);
            var roomDto = _mapper.Map<RoomDto>(room);
            await Clients.Group(roomId).SendAsync("PlayerJoined", roomDto);
        }

        public async Task PlaceBet(string roomId, int amount)
        {
            var userId = Context.UserIdentifier;
            await _gameService.PlaceBetAsync(roomId, userId, amount);

            var room = await _gameService.GetRoomAsync(roomId);
            var roomDto = _mapper.Map<RoomDto>(room);
            await Clients.Group(roomId).SendAsync("BetPlaced", userId, amount, roomDto);
        }

        public async Task Fold(string roomId)
        {
            var userId = Context.UserIdentifier;
            await _gameService.FoldAsync(roomId, userId);

            var room = await _gameService.GetRoomAsync(roomId);
            var roomDto = _mapper.Map<RoomDto>(room);
            await Clients.Group(roomId).SendAsync("PlayerFolded", userId, roomDto);
        }

        public async Task AdvancePhase(string roomId)
        {
            await _gameService.AdvancePhaseAsync(roomId);

            var room = await _gameService.GetRoomAsync(roomId);
            var roomDto = _mapper.Map<RoomDto>(room);
            await Clients.Group(roomId).SendAsync("PhaseAdvanced", roomDto);
        }
    }
}
