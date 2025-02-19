﻿using RSBot.Core.Network;

namespace RSBot.Core.Objects.Spawn
{
    public class SpawnedNpc : SpawnedBionic
    {
        /// <summary>
        /// Gets or sets the npc talk.
        /// </summary>
        public NpcTalk Talk { get; set; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="objId">The ref obj id</param>
        public SpawnedNpc(uint objId) 
            : base(objId) { }

        /// <summary>
        /// Froms the packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <param name="bionic">The bionic.</param>
        /// <returns></returns>
        internal virtual void Deserialize(Packet packet)
        {
            Talk = new NpcTalk();
            Talk.Deserialize(packet);
        }
    }
}