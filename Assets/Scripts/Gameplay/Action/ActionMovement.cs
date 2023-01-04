using System;
using Unity.Netcode;
using UnityEngine;

//namespace Unity.Multiplayer.Samples.BossRoom
namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Comprehensive class that contains information needed to play back character's movement on the server. This is what gets sent client->server when
    /// the movement gets played, and also what gets sent server->client to broadcast the movement event. Note that the OUTCOMES of the movement
    /// don't ride along with this object when it is broadcast to clients; that information is sync'd separately, usually by NetworkVariables.
    /// </summary>
    public struct ActionMovement : INetworkSerializable
    {
        public Vector3 Position;            // position of character.
        public Quaternion Rotation;         // rotation of character's facing.
        public float RotationX;
        public float UpwardVelocity;        // upward velocity of character.

        public static Vector3 PositionNull
        {
            get { return new Vector3(-10000f, 0f, 0f);  }
        }

        public static Quaternion RotationNull
        {
            get { return new Quaternion(0f, 0f, 0f, 0f);  }
        }

        [Flags]
        private enum PackFlags
        {
            None = 0,
            HasPosition = 1,
            HasRotation = 1 << 1,
            HasRotationX = 1 << 2,
            HasUpwardVelocity = 1 << 3
        }

        public static bool IsNull(Vector3 value)
        {
            return (value.x != -10000f || value.y != 0f || value.z != 0f) ? false : true;
        }

        public static bool IsNull(Quaternion value)
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
            if (!IsNull(Rotation))
            {
                flags |= PackFlags.HasRotation;
            }
            if (RotationX != 0f)
            {
                flags |= PackFlags.HasRotationX;
            }
            if (UpwardVelocity != 0f)
            {
                flags |= PackFlags.HasUpwardVelocity;
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
            if ((flags & PackFlags.HasRotation) != 0)
            {
                serializer.SerializeValue(ref Rotation);
            }
            if ((flags & PackFlags.HasRotationX) != 0)
            {
                serializer.SerializeValue(ref RotationX);
            }
            if ((flags & PackFlags.HasUpwardVelocity) != 0)
            {
                serializer.SerializeValue(ref UpwardVelocity);
            }
        }
    }
}
