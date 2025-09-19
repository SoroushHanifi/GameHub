// PokerService/Services/GameService.cs
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PokerService.Data;
using PokerService.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PokerService.Services
{
    public class GameService
    {
        private readonly PokerDbContext _dbContext;
        private readonly IConnectionMultiplexer _redis;
        private readonly IMapper _mapper;
        private readonly GameLogic _gameLogic;
        private readonly JsonSerializerOptions _jsonOptions;

        public GameService(PokerDbContext dbContext, IConnectionMultiplexer redis, IMapper mapper, GameLogic gameLogic)
        {
            _dbContext = dbContext;
            _redis = redis;
            _mapper = mapper;
            _gameLogic = gameLogic;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<Room> GetRoomAsync(string roomId)
        {
            try
            {
                var db = _redis.GetDatabase();
                var roomJson = await db.StringGetAsync($"room:{roomId}");

                if (!roomJson.HasValue)
                {
                    // Try to load from database
                    if (Guid.TryParse(roomId, out var guid))
                    {
                        var dbRoom = await _dbContext.Rooms
                            .FirstOrDefaultAsync(r => r.Id == guid);

                        if (dbRoom != null)
                        {
                            // Initialize collections if null
                            dbRoom.Players = dbRoom.Players ?? new List<Player>();
                            dbRoom.GameState = dbRoom.GameState ?? new GameState();

                            // Cache it in Redis
                            await UpdateRoomAsync(dbRoom);
                            return dbRoom;
                        }
                    }
                    return null;
                }

                var room = JsonSerializer.Deserialize<Room>(roomJson, _jsonOptions);

                // Ensure collections are initialized
                room.Players = room.Players ?? new List<Player>();
                room.GameState = room.GameState ?? new GameState();

                return room;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting room: {ex.Message}");
                return null;
            }
        }

        public async Task UpdateRoomAsync(Room room)
        {
            try
            {
                var db = _redis.GetDatabase();
                var roomJson = JsonSerializer.Serialize(room, _jsonOptions);
                await db.StringSetAsync($"room:{room.Id}", roomJson, TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating room in Redis: {ex.Message}");
            }
        }

        public async Task SaveRoomToDbAsync(Room room)
        {
            try
            {
                var existingRoom = await _dbContext.Rooms
                    .FirstOrDefaultAsync(r => r.Id == room.Id);

                if (existingRoom == null)
                {
                    _dbContext.Rooms.Add(room);
                }
                else
                {
                    _dbContext.Entry(existingRoom).CurrentValues.SetValues(room);
                }

                await _dbContext.SaveChangesAsync();
                await UpdateRoomAsync(room);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving room to database: {ex.Message}");
                throw;
            }
        }

        public async Task<Room> CreateRoomAsync(string username, string connectionId = null)
        {
            var room = new Room
            {
                Name = $"Room by {username}",
                CreatedByUsername = username
            };

            var player = new Player(username, 1000)
            {
                ConnectionId = connectionId
            };

            room.AddPlayer(player);
            await SaveRoomToDbAsync(room);

            return room;
        }

        public async Task<Room> StartHandRoomAsync(string roomId)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            if (!room.IsReadyToStart())
                throw new InvalidOperationException("Not enough players to start.");

            _gameLogic.StartNewHand(room);
            await UpdateRoomAsync(room);

            return room;
        }

        public async Task<Room> JoinRoomAsync(string roomId, string username, string connectionId = null)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            if (room.Players.Any(p => p.Username == username))
                throw new InvalidOperationException("Player already in room.");

            var player = new Player(username, 1000)
            {
                ConnectionId = connectionId
            };

            if (!room.AddPlayer(player))
                throw new InvalidOperationException("Room is full.");

            await UpdateRoomAsync(room);
            return room;
        }

        public async Task<Room> PlaceBetAsync(string roomId, string username, int amount)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            if (room.GameState.CurrentTurnUsername != username)
                throw new InvalidOperationException("Not your turn.");

            var player = room.Players.FirstOrDefault(p => p.Username == username);
            if (player == null)
                throw new InvalidOperationException("Player not found.");

            if (player.IsFolded || player.IsAllIn)
                throw new InvalidOperationException("Player cannot bet.");

            // Validate bet amount
            if (amount < room.GameState.CurrentBet - player.CurrentBet && amount != player.ChipCount)
                throw new InvalidOperationException($"Bet must be at least {room.GameState.CurrentBet}");

            if (!player.PlaceBet(amount))
                throw new InvalidOperationException("Invalid bet amount.");

            room.UpdatePot(amount);
            room.GameState.UpdateCurrentBet(player.CurrentBet);
            room.GameState.AddAction($"{username} bet {amount}");

            // Check if betting round is complete
            if (room.IsBettingRoundComplete())
            {
                _gameLogic.AdvancePhase(room);
            }
            else
            {
                // Move to next player
                var nextPlayer = room.GetNextActivePlayer(username);
                if (nextPlayer != null)
                {
                    room.GameState.SetCurrentTurn(nextPlayer.Username);
                }
            }

            await UpdateRoomAsync(room);
            return room;
        }

        public async Task<Room> FoldAsync(string roomId, string username)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            if (room.GameState.CurrentTurnUsername != username)
                throw new InvalidOperationException("Not your turn.");

            var player = room.Players.FirstOrDefault(p => p.Username == username);
            if (player == null)
                throw new InvalidOperationException("Player not found.");

            player.Fold();
            room.GameState.AddAction($"{username} folded");

            // Check if only one player left
            var activePlayers = room.GetActivePlayers().ToList();
            if (activePlayers.Count == 1)
            {
                // Winner takes pot
                var winner = activePlayers.First();
                winner.Win(room.Pot);
                room.ResetPot();
                room.EndHand();
                room.GameState.AddAction($"{winner.Username} wins {room.Pot}");
            }
            else
            {
                // Move to next player
                var nextPlayer = room.GetNextActivePlayer(username);
                if (nextPlayer != null)
                {
                    room.GameState.SetCurrentTurn(nextPlayer.Username);
                }
            }

            await UpdateRoomAsync(room);
            return room;
        }

        public async Task<Room> CheckAsync(string roomId, string username)
        {
            return await PlaceBetAsync(roomId, username, 0);
        }

        public async Task<Room> CallAsync(string roomId, string username)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            var player = room.Players.FirstOrDefault(p => p.Username == username);
            if (player == null)
                throw new InvalidOperationException("Player not found.");

            var callAmount = room.GameState.CurrentBet - player.CurrentBet;
            return await PlaceBetAsync(roomId, username, callAmount);
        }

        public async Task<Room> RaiseAsync(string roomId, string username, int raiseAmount)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            if (raiseAmount < room.GameState.MinRaise)
                throw new InvalidOperationException($"Minimum raise is {room.GameState.MinRaise}");

            return await PlaceBetAsync(roomId, username, raiseAmount);
        }

        public async Task<Room> AllInAsync(string roomId, string username)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            var player = room.Players.FirstOrDefault(p => p.Username == username);
            if (player == null)
                throw new InvalidOperationException("Player not found.");

            return await PlaceBetAsync(roomId, username, player.ChipCount);
        }

        public async Task AdvancePhaseAsync(string roomId)
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                throw new InvalidOperationException("Room not found.");

            _gameLogic.AdvancePhase(room);
            await UpdateRoomAsync(room);
        }

        public async Task<List<Room>> GetActiveRoomsAsync()
        {
            try
            {
                var rooms = await _dbContext.Rooms
                    .Where(r => r.Status != RoomStatus.Finished)
                    .OrderByDescending(r => r.LastActivityAt)
                    .Take(20)
                    .ToListAsync();

                return rooms;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting active rooms: {ex.Message}");
                return new List<Room>();
            }
        }

        public async Task CleanupInactiveRoomsAsync()
        {
            var inactiveTime = DateTime.UtcNow.AddHours(-2);
            var inactiveRooms = await _dbContext.Rooms
                .Where(r => r.LastActivityAt < inactiveTime && r.Status != RoomStatus.Playing)
                .ToListAsync();

            foreach (var room in inactiveRooms)
            {
                room.Status = RoomStatus.Finished;
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync($"room:{room.Id}");
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}

