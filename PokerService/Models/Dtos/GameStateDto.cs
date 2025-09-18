using System.Collections.Generic;

namespace PokerService.Models.Dtos
{
    public class GameStateDto
    {
        public List<CardDto> CommunityCards { get; set; }
        public string CurrentTurnUsername { get; set; }
        public int CurrentBet { get; set; }
        public int MinRaise { get; set; }
        public string Phase { get; set; }
        public int SmallBlind { get; set; }
        public int BigBlind { get; set; }
        public bool IsHandActive { get; set; }
        public int HandNumber { get; set; }
    }
}
