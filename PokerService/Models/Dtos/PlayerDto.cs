namespace PokerService.Models.Dtos
{

    public class PlayerDto
    {
        public string Username { get; set; }
        public int ChipCount { get; set; }
        public List<CardDto> Hand { get; set; }
        public int CurrentBet { get; set; }
        public bool IsFolded { get; set; }
        public bool IsAllIn { get; set; }
        public bool IsDealer { get; set; }
        public bool IsSmallBlind { get; set; }
        public bool IsBigBlind { get; set; }
    }
}
