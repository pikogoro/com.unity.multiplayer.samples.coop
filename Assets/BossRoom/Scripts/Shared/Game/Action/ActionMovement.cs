using System;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Multiplayer.Samples.BossRoom
{
    /// <summary>
    /// Comprehensive class that contains information needed to play back character's movement on the server. This is what gets sent client->server when
    /// the movement gets played, and also what gets sent server->client to broadcast the movement event. Note that the OUTCOMES of the movement
    /// don't ride along with this object when it is broadcast to clients; that information is sync'd separately, usually by NetworkVariables.
    /// </summary>
    public struct ActionMovement : INetworkSerializable
    {
        public Vector3 Position;           //position of character.
        public Quaternion Direction;       //direction of character's facing.

        [Flags]
        private enum PackFlags
        {
            None = 0,
            HasPosition = 1,
            HasDirection = 1 << 1
        }

        public static bool IsZero(Quaternion value)
        {
            return (value.x != 0f || value.y != 0f || value.z != 0f || value.w != 0f) ? false : true;
        }

        private PackFlags GetPackFlags()
        {
            PackFlags flags = PackFlags.None;
            if (Position != Vector3.zero)
            {
                flags |= PackFlags.HasPosition;
            }
            if (!IsZero(Direction))
            {
                flags |= PackFlags.HasDirection;
            }

            return flags;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            PackFlags flags = PackFlags.None;
            if (!serializer.IsReader)
            {
                flags = GetPackFlags();
            }

            serializer.SerializeValue(ref flags);

            if ((flags & PackFlags.HasPosition) != 0)
            {
                serializer.SerializeValue(ref Position);
            }
            if ((flags & PackFlags.HasDirection) != 0)
            {
                serializer.SerializeValue(ref Direction);
            }
        }
    }
}
