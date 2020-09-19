using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

using Microsoft.EntityFrameworkCore;

#pragma warning disable CS8618

namespace Melodica.Services.Settings
{
    class GuildSettingsContext : DbContext
    {
        public DbSet<GuildSettings> GuildSettings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=./guildsettings.db");
        }
    }

    public class GuildSettings
    {
        [Key]
        public ulong GuildID { get; set; }
        public string Prefix { get; set; }
    }
}

#pragma warning restore CS8618