using System.Collections.Generic;

namespace PokerService.Models.Dtos
{
    public class RoomDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<PlayerDto> Players { get; set; }
        public int Pot { get; set; }
        public GameStateDto GameState { get; set; }
        public int MaxPlayers { get; set; }
        public int MinPlayers { get; set; }
        public string Status { get; set; }
        public string CreatedByUsername { get; set; }
    }

}
