using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Generic;
using System.Numerics;

namespace PokerService.Models
{
    public class Room : BaseModel
    {
        public List<Player> Players { get; set; }
        public GameState GameState { get; set; }
        public int Pot { get; set; }
        public Room()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            GameState = new GameState();
        }

        public void AddPlayer(Player player)
        {
            if (Players.Count >= 9) throw new InvalidOperationException("Room is full.");
            Players.Add(player);
        }

        public void UpdateGameState(GameState state)
        {
            GameState = state;
        }

        public void UpdatePot(int amount)
        {
            Pot += amount;
        }
    }
}
