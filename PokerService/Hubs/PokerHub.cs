// PokerService/Hubs/PokerHub.cs
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using PokerService.Models;
using PokerService.Models.Dtos;
using PokerService.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace PokerService.Hubs
{
    [Authorize]
    public class PokerHub : Hub
    {
        private readonly GameService _gameService;
        private readonly IMapper _mapper;
        private static readonly ConcurrentDictionary<string, string> _userConnections = new();
        private static readonly ConcurrentDictionary<string, string> _connectionToRoom = new();

        public PokerHub(GameService gameService, IMapper mapper)
        {
            _gameService = gameService;
            _mapper = mapper;
        }

        public override async Task OnConnectedAsync()
        {
            var username = GetUsername();
            if (!string.IsNullOrEmpty(username))
            {
                _userConnections[username] = Context.ConnectionId;
                await Clients.Caller.SendAsync("Connected", new { username, connectionId = Context.ConnectionId });
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var username = GetUsername();
            if (!string.IsNullOrEmpty(username))
            {
                _userConnections.TryRemove(username, out _);

                // Handle leaving room
                if (_connectionToRoom.TryRemove(Context.ConnectionId, out var roomId))
                {
                    await LeaveRoom(roomId);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<string> CreateRoom()
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username))
                    throw new InvalidOperationException("User not authenticated");

                var room = await _gameService.CreateRoomAsync(username, Context.ConnectionId);

                // Add to SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, room.Id.ToString());
                _connectionToRoom[Context.ConnectionId] = room.Id.ToString();

                var roomDto = _mapper.Map<RoomDto>(room);
                await Clients.Caller.SendAsync("RoomCreated", roomDto);

                return room.Id.ToString();
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task JoinRoom(string roomId)
        {
            try
            {
                var username = GetUsername();
                if (string.IsNullOrEmpty(username))
                    throw new InvalidOperationException("User not authenticated");

                var room = await _gameService.JoinRoomAsync(roomId, username, Context.ConnectionId);

                // Add to SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                _connectionToRoom[Context.ConnectionId] = roomId;

                var roomDto = _mapper.Map<RoomDto>(room);

                // Notify all players in room
                await Clients.Group(roomId).SendAsync("PlayerJoined", roomDto);

                // Send room state to the joining player
                await Clients.Caller.SendAsync("JoinedRoom", roomDto);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task StartHandRoom(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.GetRoomAsync(roomId);

                if (room == null)
                    throw new InvalidOperationException("Room not found");

                // Only room creator can start the game
                if (room.CreatedByUsername != username)
                    throw new InvalidOperationException("Only room creator can start the game");

                room = await _gameService.StartHandRoomAsync(roomId);

                var roomDto = _mapper.Map<RoomDto>(room);

                // Hide other players' cards
                foreach (var player in roomDto.Players)
                {
                    if (player.Username != username)
                    {
                        player.Hand = null;
                    }
                }

                // Send personalized room state to each player
                foreach (var player in room.Players)
                {
                    if (_userConnections.TryGetValue(player.Username, out var connectionId))
                    {
                        var personalizedDto = CreatePersonalizedRoomDto(room, player.Username);
                        await Clients.Client(connectionId).SendAsync("HandStarted", personalizedDto);
                    }
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task PlaceBet(string roomId, int amount)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.PlaceBetAsync(roomId, username, amount);

                await SendRoomUpdateToAll(room, roomId, "BetPlaced", new { username, amount });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task Check(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.CheckAsync(roomId, username);

                await SendRoomUpdateToAll(room, roomId, "PlayerChecked", new { username });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task Call(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.CallAsync(roomId, username);

                await SendRoomUpdateToAll(room, roomId, "PlayerCalled", new { username });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task Raise(string roomId, int amount)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.RaiseAsync(roomId, username, amount);

                await SendRoomUpdateToAll(room, roomId, "PlayerRaised", new { username, amount });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task Fold(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.FoldAsync(roomId, username);

                await SendRoomUpdateToAll(room, roomId, "PlayerFolded", new { username });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task AllIn(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.AllInAsync(roomId, username);

                await SendRoomUpdateToAll(room, roomId, "PlayerAllIn", new { username });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task GetRoomState(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.GetRoomAsync(roomId);

                if (room == null)
                {
                    await Clients.Caller.SendAsync("Error", new { message = "Room not found" });
                    return;
                }

                var roomDto = CreatePersonalizedRoomDto(room, username);
                await Clients.Caller.SendAsync("RoomState", roomDto);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task GetActiveRooms()
        {
            try
            {
                var rooms = await _gameService.GetActiveRoomsAsync();
                var roomDtos = _mapper.Map<List<RoomDto>>(rooms);

                // Remove sensitive data
                foreach (var room in roomDtos)
                {
                    foreach (var player in room.Players)
                    {
                        player.Hand = null;
                    }
                }

                await Clients.Caller.SendAsync("ActiveRooms", roomDtos);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        public async Task SendMessage(string roomId, string message)
        {
            try
            {
                var username = GetUsername();

                // Simple chat functionality
                await Clients.Group(roomId).SendAsync("MessageReceived", new
                {
                    username,
                    message,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
                throw;
            }
        }

        private async Task LeaveRoom(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.GetRoomAsync(roomId);

                if (room != null)
                {
                    room.RemovePlayer(username);
                    await _gameService.UpdateRoomAsync(room);

                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

                    var roomDto = _mapper.Map<RoomDto>(room);
                    await Clients.Group(roomId).SendAsync("PlayerLeft", new { username, room = roomDto });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error leaving room: {ex.Message}");
            }
        }

        private async Task SendRoomUpdateToAll(Room room, string roomId, string eventName, object additionalData = null)
        {
            // Send personalized updates to each player
            foreach (var player in room.Players)
            {
                if (_userConnections.TryGetValue(player.Username, out var connectionId))
                {
                    var personalizedDto = CreatePersonalizedRoomDto(room, player.Username);

                    if (additionalData != null)
                    {
                        await Clients.Client(connectionId).SendAsync(eventName, additionalData, personalizedDto);
                    }
                    else
                    {
                        await Clients.Client(connectionId).SendAsync(eventName, personalizedDto);
                    }
                }
            }

            // Also update spectators if any
            var roomDto = _mapper.Map<RoomDto>(room);
            foreach (var playerDto in roomDto.Players)
            {
                playerDto.Hand = null; // Hide cards for spectators
            }

            await Clients.GroupExcept(roomId, room.Players
                .Select(p => _userConnections.GetValueOrDefault(p.Username))
                .Where(c => c != null))
                .SendAsync(eventName + "Spectator", roomDto);
        }

        private RoomDto CreatePersonalizedRoomDto(Room room, string username)
        {
            var roomDto = _mapper.Map<RoomDto>(room);

            // Hide other players' cards
            foreach (var playerDto in roomDto.Players)
            {
                if (playerDto.Username != username)
                {
                    playerDto.Hand = null;
                }
            }

            return roomDto;
        }

        private string GetUsername()
        {
            // Try to get username from JWT claims
            var username = Context.User?.Identity?.Name;

            if (string.IsNullOrEmpty(username))
            {
                // Fallback to User claim
                username = Context.User?.FindFirst("username")?.Value;
            }

            if (string.IsNullOrEmpty(username))
            {
                // Fallback to ConnectionId for testing
                username = $"Player_{Context.ConnectionId.Substring(0, 8)}";
            }

            return username;
        }
    }
}