using System.Collections.Generic;

namespace PokerService.Models
{
    public class GameState
    {
        public List<Card> CommunityCards { get; set; } = new(); 
        public string CurrentTurnUserId { get; set; }
        public int CurrentBet { get; set; }

        public void AddCommunityCard(Card card)
        {
            CommunityCards.Add(card);
        }

        public void SetCurrentTurn(string userId)
        {
            CurrentTurnUserId = userId;
        }

        public void UpdateCurrentBet(int amount)
        {
            CurrentBet = amount;
        }
    }
}
