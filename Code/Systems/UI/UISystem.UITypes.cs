using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace BoardingController.Systems.UI
{
    public partial class UISystem
    {
        public struct Waypoint
        {
            public Entity entity;
            public bool isLinked;
        }
    }
}
