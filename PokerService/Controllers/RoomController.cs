// PokerService/Controllers/RoomController.cs
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokerService.Models;
using PokerService.Models.Dtos;
using PokerService.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PokerService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoomController : ControllerBase
    {
        private readonly GameService _gameService;
        private readonly IMapper _mapper;
        private readonly ILogger<RoomController> _logger;

        public RoomController(GameService gameService, IMapper mapper, ILogger<RoomController> logger)
        {
            _gameService = gameService;
            _mapper = mapper;
            _logger = logger;
        }

        /// <summary>
        /// Get all active rooms
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoomDto>>> GetActiveRooms()
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

                return Ok(roomDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active rooms");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get specific room details
        /// </summary>
        [HttpGet("{roomId}")]
        public async Task<ActionResult<RoomDto>> GetRoom(string roomId)
        {
            try
            {
                var room = await _gameService.GetRoomAsync(roomId);

                if (room == null)
                    return NotFound(new { error = "Room not found" });

                var username = GetUsername();
                var roomDto = _mapper.Map<RoomDto>(room);

                // Hide other players' cards if user is in the room
                var isInRoom = room.Players.Any(p => p.Username == username);
                if (isInRoom)
                {
                    foreach (var player in roomDto.Players)
                    {
                        if (player.Username != username)
                        {
                            player.Hand = null;
                        }
                    }
                }
                else
                {
                    // Hide all cards for spectators
                    foreach (var player in roomDto.Players)
                    {
                        player.Hand = null;
                    }
                }

                return Ok(roomDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting room {RoomId}", roomId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Create a new room
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<RoomDto>> CreateRoom()
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.CreateRoomAsync(username);
                var roomDto = _mapper.Map<RoomDto>(room);

                return CreatedAtAction(nameof(GetRoom), new { roomId = room.Id }, roomDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Join a room
        /// </summary>
        [HttpPost("{roomId}/join")]
        public async Task<ActionResult<RoomDto>> JoinRoom(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.JoinRoomAsync(roomId, username);
                var roomDto = _mapper.Map<RoomDto>(room);

                // Hide other players' cards
                foreach (var player in roomDto.Players)
                {
                    if (player.Username != username)
                    {
                        player.Hand = null;
                    }
                }

                return Ok(roomDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room {RoomId}", roomId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Start a new hand
        /// </summary>
        [HttpPost("{roomId}/start")]
        public async Task<ActionResult<RoomDto>> StartHand(string roomId)
        {
            try
            {
                var username = GetUsername();
                var room = await _gameService.GetRoomAsync(roomId);

                if (room == null)
                    return NotFound(new { error = "Room not found" });

                if (room.CreatedByUsername != username)
                    return Forbid("Only room creator can start the game");

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

                return Ok(roomDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting hand in room {RoomId}", roomId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get user's current rooms
        /// </summary>
        [HttpGet("my-rooms")]
        public async Task<ActionResult<IEnumerable<RoomDto>>> GetMyRooms()
        {
            try
            {
                var username = GetUsername();
                var allRooms = await _gameService.GetActiveRoomsAsync();
                var myRooms = allRooms.Where(r => r.Players.Any(p => p.Username == username));
                var roomDtos = _mapper.Map<List<RoomDto>>(myRooms);

                // Show user's own cards, hide others
                foreach (var room in roomDtos)
                {
                    foreach (var player in room.Players)
                    {
                        if (player.Username != username)
                        {
                            player.Hand = null;
                        }
                    }
                }

                return Ok(roomDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user rooms");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private string GetUsername()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
            {
                username = User.FindFirst("username")?.Value;
            }

            if (string.IsNullOrEmpty(username))
            {
                username = User.FindFirst(ClaimTypes.Name)?.Value;
            }

            return username ?? "Unknown";
        }
    }
}
