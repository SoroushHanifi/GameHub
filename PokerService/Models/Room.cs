using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Generic;
using System.Numerics;

namespace PokerService.Models
{
    public class Room
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<Player> Players { get; set; }
        public int Pot { get; set; }
        public GameState GameState { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public int MaxPlayers { get; set; }
        public int MinPlayers { get; set; }
        public bool IsPrivate { get; set; }
        public string CreatedByUsername { get; set; }
        public RoomStatus Status { get; set; }

        public Room()
        {
            Id = Guid.NewGuid();
            Players = new List<Player>();
            Pot = 0;
            GameState = new GameState();
            CreatedAt = DateTime.UtcNow;
            LastActivityAt = DateTime.UtcNow;
            MaxPlayers = 9;
            MinPlayers = 2;
            IsPrivate = false;
            Status = RoomStatus.Waiting;
        }

        public bool AddPlayer(Player player)
        {
            if (Players.Count >= MaxPlayers)
                return false;

            if (Players.Any(p => p.Username == player.Username))
                return false;

            Players.Add(player);
            LastActivityAt = DateTime.UtcNow;
            return true;
        }

        public bool RemovePlayer(string username)
        {
            var player = Players.FirstOrDefault(p => p.Username == username);
            if (player != null)
            {
                Players.Remove(player);
                LastActivityAt = DateTime.UtcNow;

                // If game is active and only one player left, end the hand
                if (GameState.IsHandActive && GetActivePlayers().Count() == 1)
                {
                    EndHand();
                }

                return true;
            }
            return false;
        }

        public void UpdatePot(int amount)
        {
            Pot += amount;
            LastActivityAt = DateTime.UtcNow;
        }

        public void ResetPot()
        {
            Pot = 0;
        }

        public IEnumerable<Player> GetActivePlayers()
        {
            return Players.Where(p => !p.IsFolded && p.ChipCount > 0);
        }

        public Player GetNextActivePlayer(string currentUsername)
        {
            var currentIndex = Players.FindIndex(p => p.Username == currentUsername);
            if (currentIndex == -1) return null;

            for (int i = 1; i <= Players.Count; i++)
            {
                var nextIndex = (currentIndex + i) % Players.Count;
                var nextPlayer = Players[nextIndex];

                if (!nextPlayer.IsFolded && !nextPlayer.IsAllIn)
                    return nextPlayer;
            }

            return null;
        }

        public bool IsReadyToStart()
        {
            return Players.Count >= MinPlayers && Status == RoomStatus.Waiting;
        }

        public bool IsBettingRoundComplete()
        {
            var activePlayers = GetActivePlayers().Where(p => !p.IsAllIn).ToList();

            if (activePlayers.Count <= 1)
                return true;

            var maxBet = activePlayers.Max(p => p.CurrentBet);
            return activePlayers.All(p => p.CurrentBet == maxBet);
        }

        public void StartHand()
        {
            Status = RoomStatus.Playing;
            GameState.ResetForNewHand();

            foreach (var player in Players)
            {
                player.ResetForNewHand();
            }

            ResetPot();
            LastActivityAt = DateTime.UtcNow;
        }

        public void EndHand()
        {
            Status = RoomStatus.Waiting;
            GameState.IsHandActive = false;

            // Move dealer button
            GameState.DealerIndex = (GameState.DealerIndex + 1) % Players.Count;

            // Remove broke players
            Players.RemoveAll(p => p.ChipCount <= 0);

            LastActivityAt = DateTime.UtcNow;
        }

        public void StartNewBettingRound()
        {
            foreach (var player in Players)
            {
                player.ResetForNewBettingRound();
            }
            GameState.CurrentBet = 0;
            GameState.MinRaise = GameState.BigBlind;
        }
    }

    public enum RoomStatus
    {
        Waiting,
        Playing,
        Finished
    }

}
