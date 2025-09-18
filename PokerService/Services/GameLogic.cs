// PokerService/Services/GameLogic.cs
using PokerService.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerService.Services
{
    public class GameLogic
    {
        private readonly CardDealer _cardDealer;

        public GameLogic(CardDealer cardDealer)
        {
            _cardDealer = cardDealer;
        }

        public void StartNewHand(Room room)
        {
            if (room.Players.Count < 2)
                throw new InvalidOperationException("Need at least 2 players to start.");

            room.StartHand();

            _cardDealer.ResetDeck();
            _cardDealer.Shuffle();

            // Deal hole cards to each player
            foreach (var player in room.Players)
            {
                player.Hand.Clear();
                player.Hand.AddRange(_cardDealer.DealHoleCards());
            }

            // Set dealer, small blind, and big blind positions
            SetBlinds(room);

            // Post blinds
            var smallBlindPlayer = room.Players[GetSmallBlindIndex(room)];
            var bigBlindPlayer = room.Players[GetBigBlindIndex(room)];

            smallBlindPlayer.PlaceBet(room.GameState.SmallBlind);
            bigBlindPlayer.PlaceBet(room.GameState.BigBlind);

            room.UpdatePot(room.GameState.SmallBlind + room.GameState.BigBlind);
            room.GameState.UpdateCurrentBet(room.GameState.BigBlind);

            // Set first player to act (after big blind)
            var firstToActIndex = (GetBigBlindIndex(room) + 1) % room.Players.Count;
            room.GameState.SetCurrentTurn(room.Players[firstToActIndex].Username);

            room.GameState.AddAction($"New hand started - Hand #{room.GameState.HandNumber}");
            room.GameState.AddAction($"{smallBlindPlayer.Username} posts small blind {room.GameState.SmallBlind}");
            room.GameState.AddAction($"{bigBlindPlayer.Username} posts big blind {room.GameState.BigBlind}");
        }

        private void SetBlinds(Room room)
        {
            // Reset all blind positions
            foreach (var player in room.Players)
            {
                player.IsDealer = false;
                player.IsSmallBlind = false;
                player.IsBigBlind = false;
            }

            var dealerIndex = room.GameState.DealerIndex % room.Players.Count;
            room.Players[dealerIndex].IsDealer = true;

            if (room.Players.Count == 2)
            {
                // Heads-up: dealer is small blind
                room.Players[dealerIndex].IsSmallBlind = true;
                room.Players[(dealerIndex + 1) % 2].IsBigBlind = true;
            }
            else
            {
                // Normal: small blind is after dealer
                var sbIndex = (dealerIndex + 1) % room.Players.Count;
                var bbIndex = (dealerIndex + 2) % room.Players.Count;
                room.Players[sbIndex].IsSmallBlind = true;
                room.Players[bbIndex].IsBigBlind = true;
            }
        }

        private int GetSmallBlindIndex(Room room)
        {
            for (int i = 0; i < room.Players.Count; i++)
            {
                if (room.Players[i].IsSmallBlind)
                    return i;
            }
            return 0;
        }

        private int GetBigBlindIndex(Room room)
        {
            for (int i = 0; i < room.Players.Count; i++)
            {
                if (room.Players[i].IsBigBlind)
                    return i;
            }
            return 1;
        }

        public void AdvancePhase(Room room)
        {
            var phase = room.GameState.Phase;

            switch (phase)
            {
                case GamePhase.PreFlop:
                    // Deal flop (3 cards)
                    room.GameState.CommunityCards.AddRange(_cardDealer.DealCommunityCards(3));
                    room.GameState.AddAction("Flop dealt");
                    break;

                case GamePhase.Flop:
                    // Deal turn (1 card)
                    room.GameState.CommunityCards.AddRange(_cardDealer.DealCommunityCards(1));
                    room.GameState.AddAction("Turn dealt");
                    break;

                case GamePhase.Turn:
                    // Deal river (1 card)
                    room.GameState.CommunityCards.AddRange(_cardDealer.DealCommunityCards(1));
                    room.GameState.AddAction("River dealt");
                    break;

                case GamePhase.River:
                    // Showdown - determine winner
                    DetermineWinner(room);
                    room.EndHand();
                    return;
            }

            // Move to next phase
            room.GameState.NextPhase();
            room.StartNewBettingRound();

            // Set first to act (small blind or first active player)
            var firstToAct = GetFirstToActPostFlop(room);
            if (firstToAct != null)
            {
                room.GameState.SetCurrentTurn(firstToAct.Username);
            }
        }

        private Player GetFirstToActPostFlop(Room room)
        {
            // Start from small blind position
            var sbIndex = GetSmallBlindIndex(room);

            for (int i = 0; i < room.Players.Count; i++)
            {
                var index = (sbIndex + i) % room.Players.Count;
                var player = room.Players[index];

                if (!player.IsFolded && !player.IsAllIn)
                    return player;
            }

            return null;
        }

        private void DetermineWinner(Room room)
        {
            var activePlayers = room.GetActivePlayers().ToList();

            if (activePlayers.Count == 1)
            {
                // Only one player left, they win
                var winner = activePlayers.First();
                winner.Win(room.Pot);
                room.GameState.AddAction($"{winner.Username} wins {room.Pot} chips");
                room.ResetPot();
                return;
            }

            // Evaluate hands
            var playerHands = new List<(Player player, HandRank rank)>();

            foreach (var player in activePlayers)
            {
                var hand = EvaluateHand(player.Hand.Concat(room.GameState.CommunityCards).ToList());
                playerHands.Add((player, hand));
            }

            // Sort by hand rank (best first)
            playerHands = playerHands.OrderByDescending(ph => ph.rank.Rank)
                                     .ThenByDescending(ph => ph.rank.HighCard)
                                     .ThenByDescending(ph => ph.rank.Kicker)
                                     .ToList();

            // Check for ties
            var bestHand = playerHands.First().rank;
            var winners = playerHands.Where(ph =>
                ph.rank.Rank == bestHand.Rank &&
                ph.rank.HighCard == bestHand.HighCard &&
                ph.rank.Kicker == bestHand.Kicker).Select(ph => ph.player).ToList();

            if (winners.Count == 1)
            {
                var winner = winners.First();
                winner.Win(room.Pot);
                room.GameState.AddAction($"{winner.Username} wins {room.Pot} chips with {bestHand.Name}");
            }
            else
            {
                // Split pot
                var splitAmount = room.Pot / winners.Count;
                foreach (var winner in winners)
                {
                    winner.Win(splitAmount);
                }
                var winnerNames = string.Join(", ", winners.Select(w => w.Username));
                room.GameState.AddAction($"Split pot: {winnerNames} each win {splitAmount} chips");
            }

            room.ResetPot();
        }

        private HandRank EvaluateHand(List<Card> cards)
        {
            if (cards.Count < 5)
                return new HandRank { Rank = 0, Name = "High Card" };

            var allCombinations = GetCombinations(cards, 5);
            var bestHand = allCombinations.Select(EvaluateFiveCards)
                                          .OrderByDescending(h => h.Rank)
                                          .ThenByDescending(h => h.HighCard)
                                          .ThenByDescending(h => h.Kicker)
                                          .First();
            return bestHand;
        }

        private IEnumerable<List<Card>> GetCombinations(List<Card> cards, int count)
        {
            if (count == 0)
            {
                yield return new List<Card>();
                yield break;
            }

            for (int i = 0; i <= cards.Count - count; i++)
            {
                foreach (var combo in GetCombinations(cards.Skip(i + 1).ToList(), count - 1))
                {
                    combo.Insert(0, cards[i]);
                    yield return combo;
                }
            }
        }

        private HandRank EvaluateFiveCards(List<Card> hand)
        {
            var sorted = hand.OrderByDescending(c => c.Rank).ToList();
            var groups = hand.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            var isFlush = hand.GroupBy(c => c.Suit).Any(g => g.Count() == 5);
            var isStraight = IsStraight(sorted);

            // Royal Flush / Straight Flush
            if (isFlush && isStraight)
            {
                if (sorted[0].Rank == Rank.Ace && sorted[1].Rank == Rank.King)
                    return new HandRank { Rank = 9, Name = "Royal Flush", HighCard = (int)Rank.Ace };
                return new HandRank { Rank = 8, Name = "Straight Flush", HighCard = (int)sorted[0].Rank };
            }

            // Four of a Kind
            if (groups[0].Count() == 4)
            {
                return new HandRank
                {
                    Rank = 7,
                    Name = "Four of a Kind",
                    HighCard = (int)groups[0].Key,
                    Kicker = (int)groups[1].Key
                };
            }

            // Full House
            if (groups[0].Count() == 3 && groups[1].Count() == 2)
            {
                return new HandRank
                {
                    Rank = 6,
                    Name = "Full House",
                    HighCard = (int)groups[0].Key,
                    Kicker = (int)groups[1].Key
                };
            }

            // Flush
            if (isFlush)
            {
                return new HandRank
                {
                    Rank = 5,
                    Name = "Flush",
                    HighCard = (int)sorted[0].Rank,
                    Kicker = (int)sorted[1].Rank
                };
            }

            // Straight
            if (isStraight)
            {
                return new HandRank { Rank = 4, Name = "Straight", HighCard = (int)sorted[0].Rank };
            }

            // Three of a Kind
            if (groups[0].Count() == 3)
            {
                return new HandRank
                {
                    Rank = 3,
                    Name = "Three of a Kind",
                    HighCard = (int)groups[0].Key,
                    Kicker = (int)groups[1].Key
                };
            }

            // Two Pair
            if (groups[0].Count() == 2 && groups[1].Count() == 2)
            {
                return new HandRank
                {
                    Rank = 2,
                    Name = "Two Pair",
                    HighCard = (int)groups[0].Key,
                    Kicker = (int)groups[1].Key
                };
            }

            // One Pair
            if (groups[0].Count() == 2)
            {
                return new HandRank
                {
                    Rank = 1,
                    Name = "Pair",
                    HighCard = (int)groups[0].Key,
                    Kicker = (int)groups[1].Key
                };
            }

            // High Card
            return new HandRank
            {
                Rank = 0,
                Name = "High Card",
                HighCard = (int)sorted[0].Rank,
                Kicker = (int)sorted[1].Rank
            };
        }

        private bool IsStraight(List<Card> sorted)
        {
            // Check for normal straight
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if ((int)sorted[i].Rank - (int)sorted[i + 1].Rank != 1)
                {
                    // Check for Ace-low straight (A-2-3-4-5)
                    if (i == 0 && sorted[0].Rank == Rank.Ace && sorted[1].Rank == Rank.Five)
                    {
                        continue;
                    }
                    return false;
                }
            }
            return true;
        }
    }

    public class HandRank
    {
        public int Rank { get; set; }
        public string Name { get; set; }
        public int HighCard { get; set; }
        public int Kicker { get; set; }
    }
}