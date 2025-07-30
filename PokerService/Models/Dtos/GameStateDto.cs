using System.Collections.Generic;

namespace PokerService.Models.Dtos
{
    public class GameStateDto
    {
        public List<Card> CommunityCards { get; set; }
        public string CurrentTurnUserId { get; set; }
        public int CurrentBet { get; set; }
    }
}
