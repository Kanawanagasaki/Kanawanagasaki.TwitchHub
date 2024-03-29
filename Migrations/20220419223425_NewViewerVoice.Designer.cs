﻿// <auto-generated />
using System;
using Kanawanagasaki.TwitchHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Kanawanagasaki.TwitchHub.Migrations
{
    [DbContext(typeof(SQLiteContext))]
    [Migration("20220419223425_NewViewerVoice")]
    partial class NewViewerVoice
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.3");

            modelBuilder.Entity("Kanawanagasaki.TwitchHub.Models.JsAfkCodeModel", b =>
                {
                    b.Property<Guid>("Uuid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Channel")
                        .HasColumnType("TEXT");

                    b.Property<string>("InitCode")
                        .HasColumnType("TEXT");

                    b.Property<string>("SymbolTickCode")
                        .HasColumnType("TEXT");

                    b.Property<string>("TickCode")
                        .HasColumnType("TEXT");

                    b.HasKey("Uuid");

                    b.ToTable("js_afk_code_model");
                });

            modelBuilder.Entity("Kanawanagasaki.TwitchHub.Models.TextCommandModel", b =>
                {
                    b.Property<Guid>("Uuid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("Text")
                        .HasColumnType("TEXT");

                    b.HasKey("Uuid");

                    b.ToTable("text_command");
                });

            modelBuilder.Entity("Kanawanagasaki.TwitchHub.Models.TwitchAuthModel", b =>
                {
                    b.Property<Guid>("Uuid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("AccessToken")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsValid")
                        .HasColumnType("INTEGER");

                    b.Property<string>("RefreshToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Username")
                        .HasColumnType("TEXT");

                    b.HasKey("Uuid");

                    b.ToTable("twitch_auth");
                });

            modelBuilder.Entity("Kanawanagasaki.TwitchHub.Models.ViewerVoice", b =>
                {
                    b.Property<Guid>("Uuid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<int>("Pitch")
                        .HasColumnType("INTEGER");

                    b.Property<double>("Rate")
                        .HasColumnType("REAL");

                    b.Property<string>("VoiceName")
                        .HasColumnType("TEXT");

                    b.HasKey("Uuid");

                    b.ToTable("viewer_voice");
                });
#pragma warning restore 612, 618
        }
    }
}
