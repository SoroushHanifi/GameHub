using System.Collections.Generic;

namespace PokerService.Models.Dtos
{
    public class RoomDto
    {
        public string Id { get; set; }
        public List<Player> Players { get; set; }
        public GameStateDto GameState { get; set; }
        public int Pot { get; set; }
    }
}
