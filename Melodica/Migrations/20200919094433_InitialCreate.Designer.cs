﻿// <auto-generated />
using Melodica.Services.Settings;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Melodica.Migrations
{
    [DbContext(typeof(GuildSettingsContext))]
    [Migration("20200919094433_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.8");

            modelBuilder.Entity("Melodica.Core.Settings.GuildSettings", b =>
                {
                    b.Property<ulong>("GuildID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Prefix")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("GuildID");

                    b.ToTable("GuildSettings");
                });
#pragma warning restore 612, 618
        }
    }
}
