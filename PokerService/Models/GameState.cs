using System.Collections.Generic;

namespace PokerService.Models
{

    public class GameState
    {
        public string Id { get; set; }
        public List<Card> CommunityCards { get; set; }
        public string CurrentTurnUsername { get; set; }
        public int CurrentBet { get; set; }
        public int MinRaise { get; set; }
        public GamePhase Phase { get; set; }
        public int DealerIndex { get; set; }
        public int SmallBlind { get; set; }
        public int BigBlind { get; set; }
        public bool IsHandActive { get; set; }
        public DateTime LastActionTime { get; set; }
        public int HandNumber { get; set; }
        public List<string> ActionHistory { get; set; }

        public GameState()
        {
            Id = Guid.NewGuid().ToString();
            CommunityCards = new List<Card>();
            CurrentBet = 0;
            MinRaise = 0;
            Phase = GamePhase.PreFlop;
            DealerIndex = 0;
            SmallBlind = 10;
            BigBlind = 20;
            IsHandActive = false;
            LastActionTime = DateTime.UtcNow;
            HandNumber = 0;
            ActionHistory = new List<string>();
        }

        public void SetCurrentTurn(string username)
        {
            CurrentTurnUsername = username;
            LastActionTime = DateTime.UtcNow;
        }

        public void UpdateCurrentBet(int amount)
        {
            if (amount > CurrentBet)
            {
                MinRaise = amount + (amount - CurrentBet);
                CurrentBet = amount;
            }
        }

        public void NextPhase()
        {
            switch (Phase)
            {
                case GamePhase.PreFlop:
                    Phase = GamePhase.Flop;
                    break;
                case GamePhase.Flop:
                    Phase = GamePhase.Turn;
                    break;
                case GamePhase.Turn:
                    Phase = GamePhase.River;
                    break;
                case GamePhase.River:
                    Phase = GamePhase.Showdown;
                    break;
                case GamePhase.Showdown:
                    Phase = GamePhase.PreFlop;
                    break;
            }
            CurrentBet = 0;
            MinRaise = BigBlind;
        }

        public void AddAction(string action)
        {
            ActionHistory.Add($"{DateTime.UtcNow:HH:mm:ss} - {action}");
        }

        public void ResetForNewHand()
        {
            CommunityCards.Clear();
            CurrentBet = 0;
            MinRaise = BigBlind;
            Phase = GamePhase.PreFlop;
            IsHandActive = true;
            HandNumber++;
            ActionHistory.Clear();
            LastActionTime = DateTime.UtcNow;
        }
    }

    public enum GamePhase
    {
        PreFlop,
        Flop,
        Turn,
        River,
        Showdown
    }

}
