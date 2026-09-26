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
            int leaderIndex = GetLeaderIndex(connectedLookup, customWaypointLookup, routeWaypointBuffer, currentIndex, out bool allLinked, out int linkedCount);
            if (allLinked)
            {
                return (currentIndex + 1) % routeWaypointBuffer.Length;
            }

            int nextLeaderIndex = GetLeaderIndex(
                connectedLookup,
                customWaypointLookup,
                routeWaypointBuffer,
                (leaderIndex + linkedCount) % routeWaypointBuffer.Length,
                out _,
                out int nextLinkedCount
            );
            Entity nextLeaderWaypointEntity = routeWaypointBuffer[nextLeaderIndex].m_Waypoint;
            int nextIndex;
            if (customWaypointLookup.TryGetComponent(nextLeaderWaypointEntity, out CustomWaypoint nextLeaderCustomWaypoint))
            {
                nextIndex = (nextLeaderCustomWaypoint.m_LastSelectIndex + 1) % routeWaypointBuffer.Length;
                nextIndex = nextLeaderIndex + (nextIndex - nextLeaderIndex) % nextLinkedCount;
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
            out bool allLinked,
            out int linkedCount
        )
        {
            var leaderIndex = index;
            allLinked = false;
            linkedCount = 1;
            for (int i = index - 1; i >= 0; i--)
            {
                var routeWaypoint = routeWaypointBuffer[i];
                if (connectedLookup.HasComponent(routeWaypoint.m_Waypoint))
                {
                    if (customWaypointLookup.TryGetComponent(routeWaypoint.m_Waypoint, out var customWaypoint) && (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0)
                    {
                        leaderIndex--;
                        linkedCount++;
                        continue;
                    }
                    break;
                }
            }
            for (int i = index; i <= routeWaypointBuffer.Length - 1; i++)
            {
                var routeWaypoint = routeWaypointBuffer[i];
                if (connectedLookup.HasComponent(routeWaypoint.m_Waypoint))
                {
                    if (customWaypointLookup.TryGetComponent(routeWaypoint.m_Waypoint, out var customWaypoint) && (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0)
                    {
                        linkedCount++;
                        continue;
                    }
                    break;
                }
            }

            if (leaderIndex == 0)
            {
                for (int i = routeWaypointBuffer.Length - 1; i >= (leaderIndex + linkedCount) % routeWaypointBuffer.Length; i--)
                {
                    var routeWaypoint = routeWaypointBuffer[i];
                    if (connectedLookup.HasComponent(routeWaypoint.m_Waypoint))
                    {
                        if (
                            customWaypointLookup.TryGetComponent(routeWaypoint.m_Waypoint, out var customWaypoint)
                            && (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0
                        )
                        {
                            leaderIndex = (leaderIndex + routeWaypointBuffer.Length - 1) % routeWaypointBuffer.Length;
                            linkedCount++;
                            continue;
                        }
                        return leaderIndex;
                    }
                }
            }
            if ((leaderIndex + linkedCount - 1) % routeWaypointBuffer.Length == 0)
            {
                for (int i = 0; i <= leaderIndex - 1 - 1; i++)
                {
                    var routeWaypoint = routeWaypointBuffer[i];
                    if (connectedLookup.HasComponent(routeWaypoint.m_Waypoint))
                    {
                        if (
                            customWaypointLookup.TryGetComponent(routeWaypoint.m_Waypoint, out var customWaypoint)
                            && (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0
                        )
                        {
                            linkedCount++;
                            continue;
                        }
                        break;
                    }
                }
            }

            if (linkedCount >= routeWaypointBuffer.Length)
            {
                allLinked = true;
                return 0;
            }

            return leaderIndex;
        }
    }
}
