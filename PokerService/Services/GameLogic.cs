using PokerService.Models;

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
            _cardDealer.ResetDeck();
            _cardDealer.Shuffle();

            // پخش کارت‌های خصوصی
            foreach (var player in room.Players)
            {
                player.Hand.Clear();
                player.Hand.AddRange(_cardDealer.DealHoleCards());
            }

            // تنظیم Small Blind و Big Blind
            var smallBlindPlayer = room.Players[0];
            var bigBlindPlayer = room.Players[1];
            smallBlindPlayer.PlaceBet(10); // Small Blind
            bigBlindPlayer.PlaceBet(20); // Big Blind
            room.UpdatePot(30);
            room.GameState.UpdateCurrentBet(20);
            room.GameState.SetCurrentTurn(room.Players[2].UserId); // نوبت بازیکن بعدی
            room.GameState = new GameState { CurrentTurnUserId = room.Players[2].UserId };
        }

        public void AdvancePhase(Room room)
        {
            var phase = GetCurrentPhase(room);
            switch (phase)
            {
                case GamePhase.PreFlop:
                    room.GameState.CommunityCards.AddRange(_cardDealer.DealCommunityCards(3)); // Flop
                    break;
                case GamePhase.Flop:
                    room.GameState.CommunityCards.AddRange(_cardDealer.DealCommunityCards(1)); // Turn
                    break;
                case GamePhase.Turn:
                    room.GameState.CommunityCards.AddRange(_cardDealer.DealCommunityCards(1)); // River
                    break;
                case GamePhase.River:
                    // محاسبه برنده در Showdown
                    var winner = CalculateWinner(room);
                    room.Pot = 0; // Pot به برنده داده می‌شه
                    break;
            }
            room.GameState.SetCurrentTurn(room.Players.First().UserId); // نوبت به نفر اول برمی‌گرده
        }

        private GamePhase GetCurrentPhase(Room room)
        {
            return room.GameState.CommunityCards.Count switch
            {
                0 => GamePhase.PreFlop,
                3 => GamePhase.Flop,
                4 => GamePhase.Turn,
                5 => GamePhase.River,
                _ => GamePhase.Showdown
            };
        }

        private Player CalculateWinner(Room room)
        {
            // ساده‌سازی: فرض می‌کنیم اولین بازیکن برنده است
            // بعداً می‌تونیم منطق واقعی محاسبه دست‌های پوکر رو اضافه کنیم
            return room.Players.First();
        }
    }

    public enum GamePhase { PreFlop, Flop, Turn, River, Showdown }
}
