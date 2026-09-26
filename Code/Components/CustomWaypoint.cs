using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Colossal.Serialization.Entities;
using Unity.Entities;

namespace BoardingController.Components
{
    public struct CustomWaypoint : IComponentData, ISerializable
    {
        [Flags]
        public enum Options : uint
        {
            /// <summary>
            /// 连续 Linked 的 waypoint 会视为一个 waypoint group, 行人和车辆会选只会选其中一个
            /// </summary>
            Linked = 1 << 0,
        }

        public Options m_Options;

        // waypoint group leader field

        /// <summary>
        /// 上一次选了哪个 waypoint 作为载具的 target
        /// </summary>
        public byte m_LastSelectIndex;

        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader
        {
            reader.Read(out ushort schemaVersion);

            reader.Read(out uint options);
            m_Options = (Options)options;
            reader.Read(out m_LastSelectIndex);
        }

        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter
        {
            ushort schemaVersion = 1;
            writer.Write(schemaVersion);

            writer.Write((uint)m_Options);
            writer.Write(m_LastSelectIndex);
        }
    }
}
