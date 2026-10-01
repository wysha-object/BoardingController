using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace BoardingController.Systems.UI
{
    public static class UITypes
    {
        public struct Entity
        {
            public int index;
            public int version;
        }

        public struct Waypoint
        {
            public Entity entity;
            public bool isLinked;
        }
    }
}
