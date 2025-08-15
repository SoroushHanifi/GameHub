using AutoMapper;
using PokerService.Data;
using PokerService.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace PokerService.Services
{
    public class GameService
    {
        private readonly PokerDbContext _dbContext; 
        private readonly IConnectionMultiplexer _redis;
        private readonly IMapper _mapper;
        private readonly GameLogic _gameLogic;

        public GameService(PokerDbContext dbContext, IConnectionMultiplexer redis, IMapper mapper, GameLogic gameLogic)
        {
            _dbContext = dbContext;
            _redis = redis;
            _mapper = mapper;
            _gameLogic = gameLogic;
        }

        public async Task<Room> GetRoomAsync(string roomId)
        {
            var db = _redis.GetDatabase();
            var roomJson = await db.StringGetAsync($"Room:{roomId}");
            return roomJson.HasValue ? JsonSerializer.Deserialize<Room>(roomJson) : null;
        }

        public async Task UpdateRoomAsync(Room room)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"Room:{room.Id}", JsonSerializer.Serialize(room));
        }

        public async Task SaveRoomToDbAsync(Room room)
        {
            try
            {
                _dbContext.Rooms.Add(room);
                await _dbContext.SaveChangesAsync();

                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync($"Room:{room.Id}");
            }
            catch (Exception ex)
            {
                
                throw new Exception(ex.Message);
            }
        }

        public async Task<Room> CreateRoomAsync(string username)
        {
            var room = new Room();
            var player = new Player(username, 1000);
            room.AddPlayer(player);
            
            await SaveRoomToDbAsync(room);
            return room;
        }

        public async Task<Room> StartHandRoomAsync(string roomId)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null) throw new InvalidOperationException("Room not found.");

            if (room.Players != null && room.Players.Count > 1) 
                _gameLogic.StartNewHand(room);

            await UpdateRoomAsync(room);
            return room;
        }

        public async Task JoinRoomAsync(string roomId, string username)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null) throw new InvalidOperationException("Room not found.");

            var player = new Player(username, 1000);
            room.AddPlayer(player);

            await UpdateRoomAsync(room);
        }

        public async Task PlaceBetAsync(string roomId, string username, int amount)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null) throw new InvalidOperationException("Room not found.");

            var player = room.Players.FirstOrDefault(p => p.Username == username);
            if (player == null) throw new InvalidOperationException("Player not found.");

            player.PlaceBet(amount);
            room.UpdatePot(amount);
            room.GameState.UpdateCurrentBet(amount);

            // تغییر نوبت به بازیکن بعدی
            var currentPlayerIndex = room.Players.FindIndex(p => p.Username == room.GameState.CurrentTurnUsername);
            var nextPlayerIndex = (currentPlayerIndex + 1) % room.Players.Count;
            room.GameState.SetCurrentTurn(room.Players[nextPlayerIndex].Username);

            if (room.GameState.CommunityCards.Count == 5)
            {
                _gameLogic.AdvancePhase(room);
                await SaveRoomToDbAsync(room);
            }
            else
            {
                await UpdateRoomAsync(room);
            }
        }

        public async Task FoldAsync(string roomId, string username)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null) throw new InvalidOperationException("Room not found.");

            var player = room.Players.FirstOrDefault(p => p.Username == username);
            if (player == null) throw new InvalidOperationException("Player not found.");

            room.Players.Remove(player); // بازیکن Fold می‌کنه و از بازی خارج می‌شه

            var currentPlayerIndex = room.Players.FindIndex(p => p.Username == room.GameState.CurrentTurnUsername);
            var nextPlayerIndex = (currentPlayerIndex + 1) % room.Players.Count;
            room.GameState.SetCurrentTurn(room.Players[nextPlayerIndex].Username);

            await UpdateRoomAsync(room);
        }

        public async Task AdvancePhaseAsync(string roomId)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null) throw new InvalidOperationException("Room not found.");

            _gameLogic.AdvancePhase(room);
            await UpdateRoomAsync(room);

            if (room.GameState.CommunityCards.Count == 5)
            {
                await SaveRoomToDbAsync(room);
            }
        }
    }
}
