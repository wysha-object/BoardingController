using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoardingController.Components;
using Game.Common;
using Game.Routes;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace BoardingController.Utils
{
    public static class PatchedRouteUtils
    {
        public static bool ShouldExitVehicle(
            Entity nextLane,
            Entity targetWaypoint,
            Entity currentVehicle,
            ref ComponentLookup<Owner> ownerData,
            ref ComponentLookup<Connected> connectedData,
            ref ComponentLookup<BoardingVehicle> boardingVehicleData,
            ref ComponentLookup<CurrentRoute> currentRouteData,
            ref ComponentLookup<AccessLane> accessLaneData,
            ref ComponentLookup<Game.Vehicles.PublicTransport> publicTransportData,
            ref BufferLookup<ConnectedRoute> connectedRoutes,
            bool testing,
            out bool obsolete,
            ref ComponentLookup<Waypoint> waypointLookup,
            ref ComponentLookup<CustomWaypoint> customWaypointLookup,
            ref BufferLookup<RouteWaypoint> routeWaypointLookup
        )
        {
            if (
                currentRouteData.TryGetComponent(currentVehicle, out var componentData2)
                && waypointLookup.TryGetComponent(targetWaypoint, out var waypoint)
                && ownerData.TryGetComponent(targetWaypoint, out var owner)
                && routeWaypointLookup.TryGetBuffer(owner.m_Owner, out DynamicBuffer<RouteWaypoint> routeWaypointBuffer)
            )
            {
                int leaderIndex = WaypointUtils.GetLeaderIndex(ref customWaypointLookup, routeWaypointBuffer, waypoint.m_Index, out int linkedCount);
                for (int i = 0; i < linkedCount; i++)
                {
                    int index = (leaderIndex + i) % routeWaypointBuffer.Length;
                    var routeWaypoint = routeWaypointBuffer[index];
                    if (connectedData.TryGetComponent(routeWaypoint.m_Waypoint, out var componentData))
                    {
                        Entity connected = componentData.m_Connected;
                        if (boardingVehicleData.TryGetComponent(connected, out var componentData3) && connectedRoutes.TryGetBuffer(connected, out var bufferData))
                        {
                            if ((testing ? componentData3.m_Testing : componentData3.m_Vehicle) == currentVehicle)
                            {
                                obsolete = false;
                                if (nextLane != Entity.Null && accessLaneData.TryGetComponent(targetWaypoint, out var componentData4))
                                {
                                    Entity entity = Entity.Null;
                                    Entity entity2 = Entity.Null;
                                    if (ownerData.TryGetComponent(nextLane, out var componentData5))
                                    {
                                        entity = componentData5.m_Owner;
                                    }
                                    if (ownerData.TryGetComponent(componentData4.m_Lane, out var componentData6))
                                    {
                                        entity2 = componentData6.m_Owner;
                                    }
                                    if (entity != entity2)
                                    {
                                        obsolete = true;
                                    }
                                }
                                return true;
                            }
                        }
                    }
                }
                if (publicTransportData.TryGetComponent(currentVehicle, out var componentData7) && (componentData7.m_State & PublicTransportFlags.EnRoute) == 0)
                {
                    obsolete = true;
                    return true;
                }
                for (int i = 0; i < linkedCount; i++)
                {
                    int index = (leaderIndex + i) % routeWaypointBuffer.Length;
                    var routeWaypoint = routeWaypointBuffer[index];
                    if (connectedData.TryGetComponent(routeWaypoint.m_Waypoint, out var componentData))
                    {
                        Entity connected = componentData.m_Connected;
                        if (boardingVehicleData.HasComponent(connected) && connectedRoutes.TryGetBuffer(connected, out var bufferData))
                        {
                            for (int j = 0; j < bufferData.Length; j++)
                            {
                                if (ownerData[bufferData[j].m_Waypoint].m_Owner == componentData2.m_Route)
                                {
                                    obsolete = false;
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            obsolete = true;
            return true;
        }
    }
}
