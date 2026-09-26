using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoardingController.Components;
using Game.Routes;
using Unity.Entities;

namespace BoardingController.Utils
{
    public static class WaypointUtils
    {
        public static int SelectNextWaypoint(
            ComponentLookup<Connected> connectedLookup,
            ComponentLookup<CustomWaypoint> customWaypointLookup,
            DynamicBuffer<RouteWaypoint> routeWaypointBuffer,
            int currentIndex
        )
        {
            int leaderIndex = GetLeaderIndex(connectedLookup, customWaypointLookup, routeWaypointBuffer, currentIndex, out int linkedCount);
            if (linkedCount == routeWaypointBuffer.Length)
            {
                return (currentIndex + 1) % routeWaypointBuffer.Length;
            }

            int nextLeaderIndex = GetLeaderIndex(
                connectedLookup,
                customWaypointLookup,
                routeWaypointBuffer,
                (leaderIndex + linkedCount) % routeWaypointBuffer.Length,
                out int nextLinkedCount
            );
            Entity nextLeaderWaypointEntity = routeWaypointBuffer[nextLeaderIndex].m_Waypoint;
            int nextIndex;
            if (customWaypointLookup.TryGetComponent(nextLeaderWaypointEntity, out CustomWaypoint nextLeaderCustomWaypoint))
            {
                nextIndex = (nextLeaderCustomWaypoint.m_LastSelectIndex + 1) % routeWaypointBuffer.Length;
                nextIndex =
                    (nextLeaderIndex + ((nextIndex - nextLeaderIndex + routeWaypointBuffer.Length) % routeWaypointBuffer.Length) % nextLinkedCount) % routeWaypointBuffer.Length;
                nextLeaderCustomWaypoint.m_LastSelectIndex = (byte)nextIndex;
                customWaypointLookup[nextLeaderWaypointEntity] = nextLeaderCustomWaypoint;
            }
            else
            {
                nextIndex = nextLeaderIndex;
            }
            return nextIndex;
        }

        public static int GetLeaderIndex(
            ComponentLookup<Connected> connectedLookup,
            ComponentLookup<CustomWaypoint> customWaypointLookup,
            DynamicBuffer<RouteWaypoint> routeWaypointBuffer,
            int index,
            out int linkedCount
        )
        {
            var leaderIndex = index;
            linkedCount = 1;

            int i = index;
            while (true)
            {
                i = (routeWaypointBuffer.Length + i - 1) % routeWaypointBuffer.Length;
                if (i == index)
                {
                    break;
                }
                var routeWaypoint = routeWaypointBuffer[i].m_Waypoint;
                if (connectedLookup.HasComponent(routeWaypoint))
                {
                    if (customWaypointLookup.TryGetComponent(routeWaypoint, out var customWaypoint) && (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0)
                    {
                        leaderIndex = i;
                        linkedCount++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            int j = index;
            while (true)
            {
                var routeWaypoint = routeWaypointBuffer[j].m_Waypoint;
                if (connectedLookup.HasComponent(routeWaypoint))
                {
                    if (customWaypointLookup.TryGetComponent(routeWaypoint, out var customWaypoint) && (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0)
                    {
                        linkedCount++;
                    }
                    else
                    {
                        break;
                    }
                }
                j = (j + 1) % routeWaypointBuffer.Length;
                if (j == (routeWaypointBuffer.Length + index - 1) % routeWaypointBuffer.Length)
                {
                    break;
                }
            }

            if (linkedCount >= routeWaypointBuffer.Length)
            {
                linkedCount = routeWaypointBuffer.Length;
                return 0;
            }

            return leaderIndex;
        }
    }
}
