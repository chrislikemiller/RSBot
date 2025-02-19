﻿using RSBot.Core.Components;
using RSBot.Core.Network;
using RSBot.Core.Objects.Spawn;

namespace RSBot.Core.Objects
{
    public class Action
    {
        /// <summary>
        /// Gets or sets the skill identifier.
        /// </summary>
        /// <value>
        /// The skill identifier.
        /// </value>
        public uint SkillId { get; set; }

        /// <summary>
        /// Gets or sets the executor identifier.
        /// </summary>
        /// <value>
        /// The executor identifier.
        /// </value>
        public uint ExecutorId { get; set; }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public uint Id { get; set; }

        /// <summary>
        /// Gets or sets the target identifier.
        /// </summary>
        /// <value>
        /// The target identifier.
        /// </value>
        public uint TargetId { get; set; }

        /// <summary>
        /// Gets or sets the flag.
        /// </summary>
        /// <value>
        /// The flag.
        /// </value>
        public ActionStateFlag Flag { get; set; }

        /// <summary>
        /// Gets a value indicating whether [player is executor].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [player is executor]; otherwise, <c>false</c>.
        /// </value>
        public bool PlayerIsExecutor => Game.Player.UniqueId == ExecutorId;

        /// <summary>
        /// Gets a value indicating whether [player is target].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [player is target]; otherwise, <c>false</c>.
        /// </value>
        public bool PlayerIsTarget => Game.Player.UniqueId == TargetId;

        /*
        /// <summary>
        /// The action damage
        /// </summary>
        public Dictionary<uint,int> Damages { get; set; }
        */

        /// <summary>
        /// Deserialize the packet. 0xB070
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <returns>Deserialized <see cref="Action"/></returns>
        public static Action DeserializeBegin(Packet packet)
        {
            if (Game.ClientType > GameClientType.Thailand)
                packet.ReadUShort();

            var action = new Action
            {
                SkillId = packet.ReadUInt(),
                ExecutorId = packet.ReadUInt(),
                Id = packet.ReadUInt(),
            };

            if (Game.ClientType >= GameClientType.Global)
                packet.ReadUInt(); // ?

            action.TargetId = packet.ReadUInt();
            action.Flag = (ActionStateFlag)packet.ReadByte();

            if (Game.ClientType >= GameClientType.Global)
                packet.ReadByte();

            action.SerializeDetail(packet);

            return action;
        }

        /// <summary>
        /// Deserialize the packet. 0xB071
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <returns>Deserialized <see cref="Action"/></returns>
        public static Action DeserializeEnd(Packet packet)
        {
            packet.ReadUInt(); //ActionId
            packet.ReadUInt(); //originalTargetId

            var action = new Action();
            action.Flag = (ActionStateFlag)packet.ReadByte();
            action.SerializeDetail(packet);

            return action;
        }

        public void SerializeDetail(Packet packet)
        {
            if (Flag.HasFlag(ActionStateFlag.Attack))
            {
                var hitCount = packet.ReadByte();
                var affectedObjectCount = packet.ReadByte();

                for (int i = 0; i < affectedObjectCount; i++)
                {
                    var uniqueId = packet.ReadUInt();
                    if (!SpawnManager.TryGetEntity<SpawnedBionic>(uniqueId, out var entity))
                        continue;

                    for (int j = 0; j < hitCount; j++)
                    {
                        var state = (ActionHitStateFlag)packet.ReadByte();
                        if (state == ActionHitStateFlag.Abort)
                            break;

                        if (entity != null)
                            entity.State.HitState = state;

                        if (state != ActionHitStateFlag.Block)
                        {
                            var critStatus = packet.ReadByte();
                            var damage = packet.ReadInt();

                            packet.ReadUShort();
                            packet.ReadByte();

                            /*Damages = Damages ?? new Dictionary<uint, int>();
                            Damages.Add(uniqueId, damage);*/
                        }

                        // dont worry it will return true for knockdown states
                        if (state.HasFlag(ActionHitStateFlag.KnockBack))
                        {
                            var position = Position.FromPacketInt(packet);
                            if (entity == null)
                                continue;

                            entity.SetSource(position);
                        }
                    }
                }
            }

            if (Flag.HasFlag(ActionStateFlag.Teleport))
            {
                var position = Position.FromPacketInt(packet);
                if (PlayerIsExecutor)
                {
                    Game.Player.SetSource(position);
                }
                else
                {
                    var executor = GetExecutor<SpawnedBionic>();
                    if (executor == null)
                        return;

                    executor.SetSource(position);
                }
            }
        }

        /// <summary>
        /// Gets the executor.
        /// </summary>
        public T GetExecutor<T>() where T : SpawnedBionic
        {
            if (ExecutorId == Game.Player.UniqueId)
                return default(T);

            return SpawnManager.TryGetEntity<T>(ExecutorId);
        }

        /// <summary>
        /// Gets the target.
        /// </summary>
        /// <returns></returns>
        public SpawnedEntity GetTarget()
        {
            if (TargetId == Game.Player.UniqueId)
                return null;

            return SpawnManager.TryGetEntity<SpawnedEntity>(TargetId);
        }
    }
}