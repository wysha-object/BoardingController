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
using Unity.Mathematics;

namespace BoardingController.Utils
{
    public static class PatchedRouteUtils
    {
        private static bool CheckVehicleRoute(
            Entity transportStop1,
            Entity transportStop2,
            Entity vehicle,
            Entity route,
            ref ComponentLookup<Target> targetData,
            ref ComponentLookup<Connected> connectedData,
            ref BufferLookup<RouteWaypoint> routeWaypointData
        )
        {
            if (targetData.TryGetComponent(vehicle, out var componentData) && routeWaypointData.TryGetBuffer(route, out var bufferData))
            {
                int num = -1;
                int num2 = -1;
                for (int i = 0; i < bufferData.Length; i++)
                {
                    Connected componentData2;
                    if (bufferData[i].m_Waypoint == componentData.m_Target)
                    {
                        num = 0;
                    }
                    else if (connectedData.TryGetComponent(bufferData[i].m_Waypoint, out componentData2))
                    {
                        if (componentData2.m_Connected == transportStop2)
                        {
                            if (num == 0)
                            {
                                return true;
                            }
                            num = 2;
                        }
                        else if (componentData2.m_Connected == transportStop1)
                        {
                            num = 1;
                        }
                    }
                    num2 = math.select(num2, num, num2 == -1);
                }
                if (num == 0 && num2 == 2)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool GetBoardingVehicle(
            Entity currentLane,
            Entity currentWaypoint,
            Entity targetWaypoint,
            uint minDeparture,
            ref ComponentLookup<Owner> ownerData,
            ref ComponentLookup<Target> targetData,
            ref ComponentLookup<Connected> connectedData,
            ref ComponentLookup<BoardingVehicle> boardingVehicleData,
            ref ComponentLookup<CurrentRoute> currentRouteData,
            ref ComponentLookup<AccessLane> accessLaneData,
            ref ComponentLookup<Game.Vehicles.PublicTransport> publicTransportData,
            ref ComponentLookup<Game.Vehicles.Taxi> taxiData,
            ref BufferLookup<ConnectedRoute> connectedRoutes,
            ref BufferLookup<RouteWaypoint> routeWaypoints,
            ref BufferLookup<ExtraBoardingVehicle> extraBoardingVehicleLookup,
            out Entity vehicle,
            out bool testing,
            out bool obsolete
        )
        {
            if (currentLane != currentWaypoint && accessLaneData.TryGetComponent(currentWaypoint, out var componentData))
            {
                Entity entity = Entity.Null;
                Entity entity2 = Entity.Null;
                if (ownerData.TryGetComponent(currentLane, out var componentData2))
                {
                    entity = componentData2.m_Owner;
                }
                if (ownerData.TryGetComponent(componentData.m_Lane, out var componentData3))
                {
                    entity2 = componentData3.m_Owner;
                }
                if (entity != entity2 && (!connectedData.TryGetComponent(currentWaypoint, out var componentData4) || componentData4.m_Connected != currentLane))
                {
                    vehicle = Entity.Null;
                    testing = false;
                    obsolete = true;
                    return false;
                }
            }
            if (boardingVehicleData.TryGetComponent(currentWaypoint, out var componentData5))
            {
                if (
                    componentData5.m_Vehicle != Entity.Null
                    && taxiData.TryGetComponent(componentData5.m_Vehicle, out var componentData6)
                    && (componentData6.m_State & TaxiFlags.Boarding) != 0
                )
                {
                    vehicle = componentData5.m_Vehicle;
                    testing = false;
                    obsolete = false;
                    return true;
                }
                vehicle = Entity.Null;
                testing = false;
                obsolete = false;
                return false;
            }
            if (connectedData.TryGetComponent(currentWaypoint, out var componentData7) && connectedData.TryGetComponent(targetWaypoint, out var componentData8))
            {
                Entity connected = componentData7.m_Connected;
                Entity connected2 = componentData8.m_Connected;
                if (boardingVehicleData.TryGetComponent(connected, out var boardingVehicle) && connectedRoutes.TryGetBuffer(connected2, out var bufferData))
                {
                    using (NativeList<Entity> candidateVehicleList = new NativeList<Entity>(1, Allocator.Temp))
                    {
                        candidateVehicleList.AddNoResize(boardingVehicle.m_Vehicle);
                        if (extraBoardingVehicleLookup.TryGetBuffer(connected, out var extraBoardingVehicleBuffer))
                        {
                            foreach (var item in extraBoardingVehicleBuffer)
                            {
                                candidateVehicleList.Add(item.m_Vehicle);
                            }
                        }
                        foreach (var item in candidateVehicleList)
                        {
                            if (
                                currentRouteData.TryGetComponent(item, out var currentRoute)
                                && (
                                    !publicTransportData.TryGetComponent(item, out var componentData10)
                                    || (
                                        (componentData10.m_State & (PublicTransportFlags.EnRoute | PublicTransportFlags.Boarding))
                                            == (PublicTransportFlags.EnRoute | PublicTransportFlags.Boarding)
                                        && (componentData10.m_DepartureFrame >= minDeparture || componentData10.m_MaxBoardingDistance != float.MaxValue)
                                    )
                                )
                            )
                            {
                                for (int i = 0; i < bufferData.Length; i++)
                                {
                                    if (ownerData[bufferData[i].m_Waypoint].m_Owner == currentRoute.m_Route)
                                    {
                                        if (!CheckVehicleRoute(connected, connected2, item, currentRoute.m_Route, ref targetData, ref connectedData, ref routeWaypoints))
                                        {
                                            break;
                                        }
                                        vehicle = item;
                                        testing = false;
                                        obsolete = false;
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    if (
                        currentRouteData.TryGetComponent(boardingVehicle.m_Testing, out var componentData9)
                        && (
                            !publicTransportData.TryGetComponent(boardingVehicle.m_Testing, out var componentData11)
                            || (componentData11.m_State & (PublicTransportFlags.EnRoute | PublicTransportFlags.Testing | PublicTransportFlags.RequireStop))
                                == (PublicTransportFlags.EnRoute | PublicTransportFlags.Testing)
                        )
                    )
                    {
                        for (int j = 0; j < bufferData.Length; j++)
                        {
                            if (ownerData[bufferData[j].m_Waypoint].m_Owner == componentData9.m_Route)
                            {
                                if (
                                    !CheckVehicleRoute(
                                        connected,
                                        connected2,
                                        boardingVehicle.m_Testing,
                                        componentData9.m_Route,
                                        ref targetData,
                                        ref connectedData,
                                        ref routeWaypoints
                                    )
                                )
                                {
                                    break;
                                }
                                vehicle = boardingVehicle.m_Testing;
                                testing = true;
                                obsolete = false;
                                return false;
                            }
                        }
                    }
                    vehicle = Entity.Null;
                    testing = false;
                    obsolete = false;
                    return false;
                }
            }
            vehicle = Entity.Null;
            testing = false;
            obsolete = true;
            return false;
        }

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
            ref BufferLookup<RouteWaypoint> routeWaypointLookup,
            ref BufferLookup<ExtraBoardingVehicle> extraBoardingVehicleLookup
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
                            bool isBoardingVehicle = (testing ? componentData3.m_Testing : componentData3.m_Vehicle) == currentVehicle;
                            if (!testing && !isBoardingVehicle && extraBoardingVehicleLookup.TryGetBuffer(connected, out var extraBoardingVehicles))
                            {
                                foreach (var extraBoardingVehicle in extraBoardingVehicles)
                                {
                                    if (extraBoardingVehicle.m_Vehicle == currentVehicle)
                                    {
                                        isBoardingVehicle = true;
                                        break;
                                    }
                                }
                            }
                            if (isBoardingVehicle)
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
