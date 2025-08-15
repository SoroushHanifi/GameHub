using System.Collections.Generic;

namespace PokerService.Models
{
    public class Player
    {
        public Guid PlayerId { get; set; }
        public string Username { get; private set; }
        public int Chips { get; private set; }
        public List<Card> Hand { get; private set; } = new();

        public Player() { } // EF نیاز دارد

        public Player(string username, int initialChips)
        {
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
