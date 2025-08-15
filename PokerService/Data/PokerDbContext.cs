using Microsoft.EntityFrameworkCore;
using PokerService.Models;
using System.Collections.Generic;

namespace PokerService.Data
{
    public class PokerDbContext : DbContext
    {
        public DbSet<Room> Rooms { get; set; }
        public PokerDbContext(DbContextOptions<PokerDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Room>().HasKey(r => r.Id);
            modelBuilder.Entity<Card>().HasKey(c => new { c.Suit, c.Rank });
            modelBuilder.Entity<GameState>().HasKey(g => g.GameStateId);
            modelBuilder.Entity<Player>().HasKey(p => p.PlayerId);

        }
    }
}
