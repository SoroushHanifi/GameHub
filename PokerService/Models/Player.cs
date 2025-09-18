using System.Collections.Generic;

namespace PokerService.Models
{
    public class Player
    {
        public string Username { get; set; }
        public string ConnectionId { get; set; }
        public int ChipCount { get; set; }
        public List<Card> Hand { get; set; }
        public int CurrentBet { get; set; }
        public bool IsFolded { get; set; }
        public bool IsAllIn { get; set; }
        public int TotalBetInRound { get; set; }
        public bool IsDealer { get; set; }
        public bool IsSmallBlind { get; set; }
        public bool IsBigBlind { get; set; }

        public Player()
        {
            Hand = new List<Card>();
        }

        public Player(string username, int initialChips = 1000)
        {
            Username = username;
            ChipCount = initialChips;
            Hand = new List<Card>();
            CurrentBet = 0;
            IsFolded = false;
            IsAllIn = false;
            TotalBetInRound = 0;
        }

        public bool PlaceBet(int amount)
        {
            if (amount > ChipCount)
            {
                // All-in
                CurrentBet = ChipCount;
                ChipCount = 0;
                IsAllIn = true;
                return true;
            }

            if (amount < 0)
                return false;

            ChipCount -= amount;
            CurrentBet += amount;
            TotalBetInRound += amount;

            if (ChipCount == 0)
                IsAllIn = true;

            return true;
        }

        public void Fold()
        {
            IsFolded = true;
            Hand.Clear();
        }

        public void ResetForNewHand()
        {
            Hand.Clear();
            CurrentBet = 0;
            TotalBetInRound = 0;
            IsFolded = false;
            IsAllIn = false;
        }

        public void ResetForNewBettingRound()
        {
            CurrentBet = 0;
        }

        public void Win(int amount)
        {
            ChipCount += amount;
        }
    }
}
