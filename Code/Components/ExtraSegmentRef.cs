using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Colossal.Serialization.Entities;
using Unity.Entities;

namespace BoardingController.Components
{
    public struct ExtraSegmentRef : IBufferElementData, ISerializable
    {
        public Entity m_CustomSegment;

        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader
        {
            reader.Read(out ushort schemaVersion);

            reader.Read(out m_CustomSegment);
        }

        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter
        {
            ushort schemaVersion = 1;
            writer.Write(schemaVersion);

            writer.Write(m_CustomSegment);
        }
    }
}
