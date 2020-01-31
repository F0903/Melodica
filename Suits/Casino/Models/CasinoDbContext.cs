using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Suits.Casino.Models
{
    public class CasinoDbContext : DbContext
    {
        public CasinoDbContext()
        {
            Database.Migrate();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite("Data Source=CasinoDB.db");
    }
}
