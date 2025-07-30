using System.Collections.Generic;

namespace PokerService.Models
{
    public class Player
    {
        public string UserId { get; private set; }
        public string Username { get; private set; }
        public int Chips { get; private set; }
        public List<Card> Hand { get; private set; } = new();

        public Player(string userId, string username, int initialChips)
        {
            UserId = userId;
            Username = username;
            Chips = initialChips;
        }

        public void AddCard(Card card)
        {
            Hand.Add(card);
        }

        public void PlaceBet(int amount)
        {
            if (amount > Chips) throw new InvalidOperationException("Not enough chips.");
            Chips -= amount;
        }
    }
}
