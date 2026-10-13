using Colossal.UI.Binding;

namespace BoardingController.Systems.UI
{
    public static class UITypes
    {
        public struct Entity : IJsonReadable, IJsonWritable
        {
            public int index;
            public int version;

            public void Read(IJsonReader reader)
            {
                reader.ReadMapBegin();
                reader.ReadProperty(nameof(index));
                reader.Read(out index);
                reader.ReadProperty(nameof(version));
                reader.Read(out version);
                reader.ReadMapEnd();
            }

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(typeof(Entity).FullName);
                writer.PropertyName(nameof(index));
                writer.Write(index);
                writer.PropertyName(nameof(version));
                writer.Write(version);
                writer.TypeEnd();
            }
        }

        public struct Waypoint : IJsonReadable, IJsonWritable
        {
            public Entity entity;
            public bool isLinked;

            public void Read(IJsonReader reader)
            {
                reader.ReadMapBegin();
                reader.ReadProperty(nameof(entity));
                entity.Read(reader);
                reader.ReadProperty(nameof(isLinked));
                reader.Read(out isLinked);
                reader.ReadMapEnd();
            }

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(typeof(Waypoint).FullName);
                writer.PropertyName(nameof(entity));
                writer.Write(entity);
                writer.PropertyName(nameof(isLinked));
                writer.Write(isLinked);
                writer.TypeEnd();
            }
        }

        public struct GetWaypointsInput : IJsonReadable
        {
            public Entity line;

            public void Read(IJsonReader reader)
            {
                reader.ReadMapBegin();
                reader.ReadProperty(nameof(line));
                line.Read(reader);
                reader.ReadMapEnd();
            }
        }
    }
}
