﻿using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Objects;
using System;
using System.IO;
using System.Threading;

namespace RSBot.Bot.Default.Bundle.Loop
{
    internal class LoopBundle : IBundle
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public LoopConfig Config { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="LoopBundle"/> is running.
        /// </summary>
        /// <value>
        ///   <c>true</c> if running; otherwise, <c>false</c>.
        /// </value>
        public bool Running { get; private set; }

        /// <summary>
        /// Gets a value indicating whether [townscript running].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [townscript running]; otherwise, <c>false</c>.
        /// </value>
        public bool TownscriptRunning { get; private set; }

        /// <summary>
        /// Invokes this instance.
        /// </summary>
        public void Invoke()
        {
            if (!Running) return;

            if (Config.UseVehicle && !Game.Player.HasActiveVehicle && !Game.Player.IsInDungeon)
            {
                Game.Player.SummonVehicle();

                //Wait for the vehicle to spawn
                Thread.Sleep(1000);
            }

            //We don't need to use buffs in town...
            if (Config.CastBuffs && !TownscriptRunning)
                Bundles.Buff.Invoke();
        }

        /// <summary>
        /// Refreshes this instance.
        /// </summary>
        public void Refresh()
        {
            Config = new LoopConfig
            {
                WalkScript = PlayerConfig.Get<string>("RSBot.Walkback.Script"),
                UseSpeedDrug = PlayerConfig.Get<bool>("RSBot.Walkback.UseSpeedDrug"),
                UseVehicle = PlayerConfig.Get<bool>("RSBot.Walkback.UseMount", true),
                CastBuffs = PlayerConfig.Get<bool>("RSBot.Walkback.CastBuffs", true)
            };
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            Running = true;

            CheckForTownScript();

            Running = false;
        }

        /// <summary>
        /// Checks for town script.
        /// </summary>
        public void CheckForTownScript()
        {
            if (ScriptManager.Running) return;

            var filename = Environment.CurrentDirectory + "\\Scripts\\Towns\\" +
                           Game.Player.Movement.Source.RegionID +
                           ".rbs";

            //The player is in town, therefore, we need to run the townscript first.
            if (!File.Exists(filename))
            {
                CheckForWalkbackScript();
                return;
            }

            Log.Notify($"Loading townscript [{filename}]...");

            TownscriptRunning = true;
            ScriptManager.Load(filename);
            ScriptManager.RunScript();
            TownscriptRunning = false;

            Invoke();

            CheckForWalkbackScript();
        }

        /// <summary>
        /// Checks for walkback script.
        /// </summary>
        public void CheckForWalkbackScript()
        {
            if (Config.WalkScript == null || 
                ScriptManager.Running || 
                !File.Exists(Config.WalkScript) || 
                !Kernel.Bot.Running) 
                return;

            Invoke();
            Log.Notify($"Loading walkscript [{Config.WalkScript}]...");

            ScriptManager.Load(Config.WalkScript);
            ScriptManager.RunScript();
        }
    }
}