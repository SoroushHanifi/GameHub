using PokerService.Models;
using System.Collections.Generic;

namespace PokerService.Services
{
    public class CardDealer
    {
        private readonly List<Card> _deck = new();
        private readonly Random _random = new();

        public CardDealer()
        {
            // ایجاد دسته کارت (52 تایی)
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    _deck.Add(new Card(suit, rank));
                }
            }
        }

        public void Shuffle()
        {
            // الگوریتم Fisher-Yates برای شافل کردن
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = _random.Next(0, i + 1);
                var temp = _deck[i];
                _deck[i] = _deck[j];
                _deck[j] = temp;
            }
        }

        public List<Card> DealHoleCards(int numberOfCards = 2)
        {
            if (_deck.Count < numberOfCards) throw new InvalidOperationException("Not enough cards in deck.");
            var cards = _deck.Take(numberOfCards).ToList();
            _deck.RemoveRange(0, numberOfCards);
            return cards;
        }

        public List<Card> DealCommunityCards(int numberOfCards)
        {
            if (_deck.Count < numberOfCards) throw new InvalidOperationException("Not enough cards in deck.");
            var cards = _deck.Take(numberOfCards).ToList();
            _deck.RemoveRange(0, numberOfCards);
            return cards;
        }

        public void ResetDeck()
        {
            _deck.Clear();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    _deck.Add(new Card(suit, rank));
                }
            }
        }
    }
}
