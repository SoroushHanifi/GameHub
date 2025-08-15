using System.Collections.Generic;

namespace PokerService.Models
{
    public class GameState
    {
        public Guid GameStateId { get; set; }
        public List<Card> CommunityCards { get; set; } = new(); 
        public string CurrentTurnUsername { get; set; }
        public int CurrentBet { get; set; }

        public void AddCommunityCard(Card card)
        {
            CommunityCards.Add(card);
        }

        public void SetCurrentTurn(string username)
        {
            CurrentTurnUsername = username;
        }

        public void UpdateCurrentBet(int amount)
        {
            CurrentBet = amount;
        }
    }
}
