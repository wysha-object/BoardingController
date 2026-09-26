using BoardingController.Components;
using BoardingController.Utils;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;

namespace GameBoardingController.Systems.Pathfind
{
    public partial class PatchedRoutesModifiedSystem : GameSystemBase
    {
        [BurstCompile]
        private struct AddPathEdgeJob : IJob
        {
            [ReadOnly]
            public NativeList<ArchetypeChunk> m_Chunks;

            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public ComponentTypeHandle<Owner> m_OwnerType;

            [ReadOnly]
            public ComponentTypeHandle<Waypoint> m_WaypointType;

            [ReadOnly]
            public ComponentTypeHandle<Position> m_PositionType;

            [ReadOnly]
            public ComponentTypeHandle<Transform> m_TransformType;

            [ReadOnly]
            public ComponentTypeHandle<AccessLane> m_AccessLaneType;

            [ReadOnly]
            public ComponentTypeHandle<RouteLane> m_RouteLaneType;

            [ReadOnly]
            public ComponentTypeHandle<Game.Routes.Segment> m_SegmentType;

            [ReadOnly]
            public ComponentTypeHandle<TaxiStand> m_TaxiStandType;

            [ReadOnly]
            public ComponentTypeHandle<Game.Routes.TakeoffLocation> m_TakeoffLocationType;

            [ReadOnly]
            public ComponentTypeHandle<Connected> m_ConnectedType;

            [ReadOnly]
            public ComponentTypeHandle<RouteInfo> m_RouteInfoType;

            [ReadOnly]
            public ComponentTypeHandle<Game.Objects.SpawnLocation> m_SpawnLocationType;

            [ReadOnly]
            public ComponentTypeHandle<WaitingPassengers> m_WaitingPassengersType;

            [ReadOnly]
            public ComponentLookup<Position> m_PositionData;

            [ReadOnly]
            public ComponentLookup<Game.Routes.TransportStop> m_TransportStopData;

            [ReadOnly]
            public ComponentLookup<TransportLine> m_TransportLineData;

            [ReadOnly]
            public ComponentLookup<Lane> m_LaneData;

            [ReadOnly]
            public ComponentLookup<Curve> m_CurveData;

            [ReadOnly]
            public ComponentLookup<Game.Net.CarLane> m_CarLaneData;

            [ReadOnly]
            public ComponentLookup<MasterLane> m_MasterLaneData;

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;

            [ReadOnly]
            public ComponentLookup<NetLaneData> m_NetLaneData;

            [ReadOnly]
            public ComponentLookup<TransportLineData> m_PrefabTransportLineData;

            [ReadOnly]
            public ComponentLookup<RouteConnectionData> m_PrefabRouteConnectionData;

            [ReadOnly]
            public ComponentLookup<SpawnLocationData> m_PrefabSpawnLocationData;

            [ReadOnly]
            public ComponentLookup<PathfindTransportData> m_TransportPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindPedestrianData> m_PedestrianPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindCarData> m_CarPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindTrackData> m_TrackPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindConnectionData> m_ConnectionPathfindData;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> m_Waypoints;

            [WriteOnly]
            public NativeArray<CreateActionData> m_Actions;

            [ReadOnly]
            public ComponentLookup<Connected> m_ConnectedLookup;

            [ReadOnly]
            public ComponentLookup<CustomWaypoint> m_CustomWaypointLookup;

            [ReadOnly]
            public ComponentLookup<RouteInfo> m_RouteInfoLookup;

            [ReadOnly]
            public BufferLookup<RouteSegment> m_RouteSegmentLookup;

            public void Execute()
            {
                int num = 0;
                for (int i = 0; i < m_Chunks.Length; i++)
                {
                    ArchetypeChunk archetypeChunk = m_Chunks[i];
                    NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(m_EntityType);
                    NativeArray<Owner> nativeArray2 = archetypeChunk.GetNativeArray(ref m_OwnerType);
                    NativeArray<AccessLane> nativeArray3 = archetypeChunk.GetNativeArray(ref m_AccessLaneType);
                    NativeArray<RouteLane> nativeArray4 = archetypeChunk.GetNativeArray(ref m_RouteLaneType);
                    NativeArray<Game.Objects.SpawnLocation> nativeArray5 = archetypeChunk.GetNativeArray(ref m_SpawnLocationType);
                    if (nativeArray3.Length != 0 || nativeArray4.Length != 0 || nativeArray5.Length != 0)
                    {
                        NativeArray<Waypoint> nativeArray6 = archetypeChunk.GetNativeArray(ref m_WaypointType);
                        NativeArray<Position> nativeArray7 = archetypeChunk.GetNativeArray(ref m_PositionType);
                        NativeArray<Connected> nativeArray8 = archetypeChunk.GetNativeArray(ref m_ConnectedType);
                        NativeArray<Transform> nativeArray9 = archetypeChunk.GetNativeArray(ref m_TransformType);
                        NativeArray<Game.Routes.TakeoffLocation> nativeArray10 = archetypeChunk.GetNativeArray(ref m_TakeoffLocationType);
                        NativeArray<TaxiStand> nativeArray11 = archetypeChunk.GetNativeArray(ref m_TaxiStandType);
                        NativeArray<WaitingPassengers> nativeArray12 = archetypeChunk.GetNativeArray(ref m_WaitingPassengersType);
                        for (int j = 0; j < nativeArray.Length; j++)
                        {
                            Entity entity = nativeArray[j];
                            AccessLane accessLane = default(AccessLane);
                            if (nativeArray3.Length != 0)
                            {
                                accessLane = nativeArray3[j];
                            }
                            Game.Objects.SpawnLocation spawnLocation = default(Game.Objects.SpawnLocation);
                            if (nativeArray5.Length != 0)
                            {
                                spawnLocation = nativeArray5[j];
                            }
                            CreateActionData value = new CreateActionData { m_Owner = entity };
                            Entity lane = Entity.Null;
                            if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane1))
                            {
                                lane = spawnLocation.m_ConnectedLane1;
                                value.m_StartNode = new PathNode(m_LaneData[spawnLocation.m_ConnectedLane1].m_MiddleNode, spawnLocation.m_CurvePosition1);
                            }
                            else if (m_LaneData.HasComponent(accessLane.m_Lane))
                            {
                                lane = accessLane.m_Lane;
                                value.m_StartNode = new PathNode(m_LaneData[accessLane.m_Lane].m_MiddleNode, accessLane.m_CurvePos);
                            }
                            else if (m_TransportStopData.HasComponent(accessLane.m_Lane))
                            {
                                lane = accessLane.m_Lane;
                                value.m_StartNode = new PathNode(accessLane.m_Lane, 2);
                            }
                            else
                            {
                                value.m_StartNode = new PathNode(entity, 2);
                            }
                            value.m_MiddleNode = new PathNode(entity, 1);
                            if (nativeArray7.Length != 0)
                            {
                                value.m_Location = PathUtils.GetLocationSpecification(nativeArray7[j].m_Position);
                            }
                            else
                            {
                                value.m_Location = PathUtils.GetLocationSpecification(nativeArray9[j].m_Position);
                            }
                            if (nativeArray6.Length != 0)
                            {
                                value.m_EndNode = new PathNode(entity, 0);
                                Owner owner = default(Owner);
                                if (nativeArray2.Length != 0)
                                {
                                    owner = nativeArray2[j];
                                }
                                Game.Routes.TransportStop transportStop = default(Game.Routes.TransportStop);
                                bool isWaypoint = true;
                                if (nativeArray8.Length != 0)
                                {
                                    Connected connected = nativeArray8[j];
                                    if (m_TransportStopData.HasComponent(connected.m_Connected))
                                    {
                                        transportStop = m_TransportStopData[connected.m_Connected];
                                        isWaypoint = false;
                                    }
                                }
                                WaitingPassengers waitingPassengers = default(WaitingPassengers);
                                if (nativeArray12.Length != 0)
                                {
                                    waitingPassengers = nativeArray12[j];
                                }
                                TransportLineData transportLineData = GetTransportLineData(owner.m_Owner, out var transportLine);
                                PathfindTransportData transportLinePathfindData = GetTransportLinePathfindData(transportLineData);
                                value.m_Specification = PathUtils.GetTransportStopSpecification(
                                    transportStop,
                                    transportLine,
                                    waitingPassengers,
                                    transportLineData,
                                    transportLinePathfindData,
                                    isWaypoint
                                );
                            }
                            else
                            {
                                RouteLane routeLane = default(RouteLane);
                                if (nativeArray4.Length != 0)
                                {
                                    routeLane = nativeArray4[j];
                                }
                                if (m_LaneData.HasComponent(routeLane.m_EndLane))
                                {
                                    value.m_EndNode = new PathNode(m_LaneData[routeLane.m_EndLane].m_MiddleNode, routeLane.m_EndCurvePos);
                                }
                                else
                                {
                                    value.m_EndNode = new PathNode(entity, 0);
                                }
                                value.m_SecondaryEndNode = value.m_EndNode;
                                if (nativeArray5.Length != 0)
                                {
                                    SpawnLocationData spawnLocationData = GetSpawnLocationData(entity);
                                    if (spawnLocationData.m_ConnectionType != RouteConnectionType.None)
                                    {
                                        if (spawnLocationData.m_ConnectionType == RouteConnectionType.Parking && spawnLocationData.m_RoadTypes == RoadTypes.Bicycle)
                                        {
                                            spawnLocationData.m_ConnectionType = RouteConnectionType.Pedestrian;
                                        }
                                        value.m_Specification = GetSpawnLocationPathSpecification(
                                            value.m_Location.m_Line.a,
                                            spawnLocationData.m_ConnectionType,
                                            spawnLocationData.m_RoadTypes,
                                            spawnLocation.m_ConnectedLane1,
                                            spawnLocation.m_CurvePosition1,
                                            0,
                                            spawnLocation.m_AccessRestriction,
                                            spawnLocationData.m_RequireAuthorization,
                                            (spawnLocation.m_Flags & SpawnLocationFlags.AllowEnter) != 0,
                                            (spawnLocation.m_Flags & SpawnLocationFlags.AllowExit) != 0,
                                            secondaryStart: false,
                                            secondaryEnd: false
                                        );
                                        if (nativeArray11.Length != 0)
                                        {
                                            value.m_EndNode = new PathNode(entity, 0);
                                        }
                                        else
                                        {
                                            RouteConnectionData routeConnectionData = GetRouteConnectionData(entity);
                                            if (
                                                (
                                                    spawnLocationData.m_ConnectionType == RouteConnectionType.Road
                                                    || spawnLocationData.m_ConnectionType == RouteConnectionType.Cargo
                                                    || spawnLocationData.m_ConnectionType == RouteConnectionType.Parking
                                                    || spawnLocationData.m_ConnectionType == RouteConnectionType.Offroad
                                                )
                                                && spawnLocationData.m_ConnectionType != routeConnectionData.m_AccessConnectionType
                                            )
                                            {
                                                int laneCrossCount = 1;
                                                if (m_MasterLaneData.HasComponent(spawnLocation.m_ConnectedLane1))
                                                {
                                                    MasterLane masterLane = m_MasterLaneData[spawnLocation.m_ConnectedLane1];
                                                    laneCrossCount = masterLane.m_MaxIndex - masterLane.m_MinIndex + 1;
                                                }
                                                bool flag = false;
                                                if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane2))
                                                {
                                                    value.m_SecondaryStartNode = new PathNode(
                                                        m_LaneData[spawnLocation.m_ConnectedLane2].m_MiddleNode,
                                                        spawnLocation.m_CurvePosition2
                                                    );
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                else
                                                {
                                                    flag = true;
                                                    value.m_SecondaryStartNode = new PathNode(entity, 3);
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                value.m_SecondarySpecification = GetSpawnLocationPathSpecification(
                                                    value.m_Location.m_Line.a,
                                                    spawnLocationData.m_ConnectionType,
                                                    spawnLocationData.m_RoadTypes,
                                                    spawnLocation.m_ConnectedLane2,
                                                    spawnLocation.m_CurvePosition2,
                                                    laneCrossCount,
                                                    spawnLocation.m_AccessRestriction,
                                                    spawnLocationData.m_RequireAuthorization,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowEnter) != 0,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowExit) != 0,
                                                    secondaryStart: false,
                                                    secondaryEnd: false
                                                );
                                                if (flag)
                                                {
                                                    value.m_SecondarySpecification.m_Flags &= ~(EdgeFlags.Forward | EdgeFlags.Backward);
                                                }
                                            }
                                            else if (
                                                spawnLocationData.m_ConnectionType == RouteConnectionType.Pedestrian
                                                && routeConnectionData.m_AccessConnectionType == RouteConnectionType.None
                                                && spawnLocationData.m_ActivityMask.m_Mask == 0
                                            )
                                            {
                                                if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane2))
                                                {
                                                    spawnLocation.m_ConnectedLane1 = spawnLocation.m_ConnectedLane2;
                                                    spawnLocation.m_CurvePosition1 = spawnLocation.m_CurvePosition2;
                                                }
                                                if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane1))
                                                {
                                                    value.m_SecondaryStartNode = new PathNode(
                                                        m_LaneData[spawnLocation.m_ConnectedLane1].m_MiddleNode,
                                                        spawnLocation.m_CurvePosition1
                                                    );
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                else
                                                {
                                                    value.m_SecondaryStartNode = new PathNode(entity, 3);
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                value.m_SecondarySpecification = GetSpawnLocationPathSpecification(
                                                    value.m_Location.m_Line.a,
                                                    RouteConnectionType.Road,
                                                    RoadTypes.Bicycle,
                                                    spawnLocation.m_ConnectedLane1,
                                                    spawnLocation.m_CurvePosition1,
                                                    0,
                                                    spawnLocation.m_AccessRestriction,
                                                    spawnLocationData.m_RequireAuthorization,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowEnter) != 0,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowExit) != 0,
                                                    secondaryStart: true,
                                                    secondaryEnd: true
                                                );
                                                if (spawnLocationData.m_RoadTypes == RoadTypes.Bicycle)
                                                {
                                                    value.m_SecondarySpecification.m_Costs = value.m_Specification.m_Costs;
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (nativeArray10.Length != 0)
                                {
                                    Game.Routes.TakeoffLocation takeoffLocation = nativeArray10[j];
                                    RouteConnectionData routeConnectionData2 = GetRouteConnectionData(entity);
                                    value.m_Specification = GetSpawnLocationPathSpecification(
                                        value.m_Location.m_Line.a,
                                        routeConnectionData2.m_RouteConnectionType,
                                        routeConnectionData2.m_RouteRoadType,
                                        routeLane.m_EndLane,
                                        routeLane.m_EndCurvePos,
                                        0,
                                        takeoffLocation.m_AccessRestriction,
                                        requireAuthorization: false,
                                        (takeoffLocation.m_Flags & TakeoffLocationFlags.AllowEnter) != 0,
                                        (takeoffLocation.m_Flags & TakeoffLocationFlags.AllowExit) != 0,
                                        secondaryStart: false,
                                        secondaryEnd: false
                                    );
                                    if (
                                        routeConnectionData2.m_RouteConnectionType == RouteConnectionType.Air
                                        && routeConnectionData2.m_RouteRoadType == RoadTypes.Airplane
                                        && m_CarLaneData.TryGetComponent(accessLane.m_Lane, out var componentData)
                                        && (componentData.m_Flags & CarLaneFlags.Twoway) == 0
                                    )
                                    {
                                        value.m_Specification.m_Flags &= (EdgeFlags)((accessLane.m_CurvePos >= 0.5f) ? 65533 : 65534);
                                    }
                                    if (
                                        routeConnectionData2.m_RouteConnectionType == RouteConnectionType.Pedestrian
                                        && routeConnectionData2.m_AccessConnectionType == RouteConnectionType.Pedestrian
                                    )
                                    {
                                        value.m_SecondaryStartNode = value.m_StartNode;
                                        bool secondaryEnd = false;
                                        if (routeLane.m_StartLane != routeLane.m_EndLane && m_LaneData.HasComponent(routeLane.m_StartLane))
                                        {
                                            value.m_SecondaryEndNode = new PathNode(m_LaneData[routeLane.m_StartLane].m_MiddleNode, routeLane.m_StartCurvePos);
                                        }
                                        else
                                        {
                                            value.m_SecondaryEndNode = value.m_EndNode;
                                            secondaryEnd = true;
                                        }
                                        value.m_SecondarySpecification = GetSpawnLocationPathSpecification(
                                            value.m_Location.m_Line.a,
                                            RouteConnectionType.Road,
                                            RoadTypes.Bicycle,
                                            routeLane.m_StartLane,
                                            routeLane.m_StartCurvePos,
                                            0,
                                            takeoffLocation.m_AccessRestriction,
                                            requireAuthorization: false,
                                            (takeoffLocation.m_Flags & (TakeoffLocationFlags.AllowEnter | TakeoffLocationFlags.AllowExit)) == TakeoffLocationFlags.AllowEnter,
                                            allowExit: false,
                                            secondaryStart: true,
                                            secondaryEnd
                                        );
                                    }
                                }
                                if (nativeArray11.Length != 0)
                                {
                                    TaxiStand taxiStand = nativeArray11[j];
                                    Game.Routes.TransportStop transportStop2 = default(Game.Routes.TransportStop);
                                    if (m_TransportStopData.HasComponent(entity))
                                    {
                                        transportStop2 = m_TransportStopData[entity];
                                    }
                                    WaitingPassengers waitingPassengers2 = default(WaitingPassengers);
                                    if (nativeArray12.Length != 0)
                                    {
                                        waitingPassengers2 = nativeArray12[j];
                                    }
                                    PathfindTransportData netLaneTransportPathfindData = GetNetLaneTransportPathfindData(lane);
                                    value.m_SecondaryStartNode = value.m_StartNode;
                                    value.m_SecondarySpecification = PathUtils.GetTaxiStopSpecification(
                                        transportStop2,
                                        taxiStand,
                                        waitingPassengers2,
                                        netLaneTransportPathfindData
                                    );
                                }
                            }
                            m_Actions[num++] = value;
                        }
                    }
                    NativeArray<Game.Routes.Segment> nativeArray13 = archetypeChunk.GetNativeArray(ref m_SegmentType);
                    if (nativeArray13.Length == 0)
                    {
                        continue;
                    }
                    NativeArray<RouteInfo> nativeArray14 = archetypeChunk.GetNativeArray(ref m_RouteInfoType);
                    for (int k = 0; k < nativeArray13.Length; k++)
                    {
                        Entity owner2 = nativeArray[k];
                        Owner owner3 = nativeArray2[k];
                        Game.Routes.Segment segment = nativeArray13[k];
                        DynamicBuffer<RouteWaypoint> routeWaypointBuffer = m_Waypoints[owner3.m_Owner];
                        DynamicBuffer<RouteSegment> routeSegmentBuffer = m_RouteSegmentLookup[owner3.m_Owner];
                        int leaderIndex = WaypointUtils.GetLeaderIndex(m_ConnectedLookup, m_CustomWaypointLookup, routeWaypointBuffer, segment.m_Index, out int linkedCount);
                        int groupLastIndex = math.select((leaderIndex + linkedCount - 1) % routeWaypointBuffer.Length, segment.m_Index, linkedCount >= routeWaypointBuffer.Length);
                        if (!m_RouteInfoLookup.TryGetComponent(routeSegmentBuffer[groupLastIndex].m_Segment, out var routeInfo))
                        {
                            routeInfo = default(RouteInfo);
                        }
                        int nextLeaderIndex;
                        int nextLinkedCount;
                        if (linkedCount >= routeWaypointBuffer.Length)
                        {
                            nextLeaderIndex = (segment.m_Index + 1) % routeWaypointBuffer.Length;
                            nextLinkedCount = 1;
                        }
                        else
                        {
                            nextLeaderIndex = WaypointUtils.GetLeaderIndex(
                                m_ConnectedLookup,
                                m_CustomWaypointLookup,
                                routeWaypointBuffer,
                                (leaderIndex + linkedCount) % routeWaypointBuffer.Length,
                                out nextLinkedCount
                            );
                        }
                        Entity waypoint = routeWaypointBuffer[segment.m_Index].m_Waypoint;
                        Position position = m_PositionData[waypoint];
                        TransportLineData transportLineData2 = GetTransportLineData(owner3.m_Owner, out var _);
                        PathfindTransportData transportLinePathfindData2 = GetTransportLinePathfindData(transportLineData2);
                        for (int j = 0; j < nextLinkedCount; j++)
                        {
                            int nextIndex = (nextLeaderIndex + j) % routeWaypointBuffer.Length;

                            Entity waypoint2 = routeWaypointBuffer[nextIndex].m_Waypoint;
                            Position position2 = m_PositionData[waypoint2];
                            CreateActionData value2 = new CreateActionData
                            {
                                m_Owner = owner2,
                                m_StartNode = new PathNode(waypoint, 0),
                                m_MiddleNode = new PathNode(owner2, 0),
                                m_EndNode = new PathNode(waypoint2, 0),
                                m_Specification = PathUtils.GetTransportLineSpecification(transportLineData2, transportLinePathfindData2, routeInfo),
                                m_Location = PathUtils.GetLocationSpecification(position.m_Position, position2.m_Position),
                            };
                            m_Actions[num++] = value2;
                        }
                    }
                }
            }

            private SpawnLocationData GetSpawnLocationData(Entity entity)
            {
                PrefabRef prefabRef = m_PrefabRefData[entity];
                if (m_PrefabSpawnLocationData.HasComponent(prefabRef.m_Prefab))
                {
                    return m_PrefabSpawnLocationData[prefabRef.m_Prefab];
                }
                return default(SpawnLocationData);
            }

            private RouteConnectionData GetRouteConnectionData(Entity entity)
            {
                PrefabRef prefabRef = m_PrefabRefData[entity];
                if (m_PrefabRouteConnectionData.HasComponent(prefabRef.m_Prefab))
                {
                    return m_PrefabRouteConnectionData[prefabRef.m_Prefab];
                }
                return default(RouteConnectionData);
            }

            private TransportLineData GetTransportLineData(Entity owner, out TransportLine transportLine)
            {
                if (m_TransportLineData.HasComponent(owner))
                {
                    transportLine = m_TransportLineData[owner];
                    PrefabRef prefabRef = m_PrefabRefData[owner];
                    return m_PrefabTransportLineData[prefabRef.m_Prefab];
                }
                transportLine = default(TransportLine);
                return default(TransportLineData);
            }

            private PathfindTransportData GetTransportLinePathfindData(TransportLineData transportLineData)
            {
                if (m_TransportPathfindData.HasComponent(transportLineData.m_PathfindPrefab))
                {
                    return m_TransportPathfindData[transportLineData.m_PathfindPrefab];
                }
                return default(PathfindTransportData);
            }

            private PathfindTransportData GetNetLaneTransportPathfindData(Entity lane)
            {
                if (m_PrefabRefData.HasComponent(lane))
                {
                    PrefabRef prefabRef = m_PrefabRefData[lane];
                    if (m_NetLaneData.HasComponent(prefabRef.m_Prefab))
                    {
                        NetLaneData netLaneData = m_NetLaneData[prefabRef.m_Prefab];
                        if (m_TransportPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            return m_TransportPathfindData[netLaneData.m_PathfindPrefab];
                        }
                    }
                }
                return default(PathfindTransportData);
            }

            private PathSpecification GetSpawnLocationPathSpecification(
                float3 position,
                RouteConnectionType connectionType,
                RoadTypes roadType,
                Entity lane,
                float curvePos,
                int laneCrossCount,
                Entity accessRestriction,
                bool requireAuthorization,
                bool allowEnter,
                bool allowExit,
                bool secondaryStart,
                bool secondaryEnd
            )
            {
                NetLaneData netLaneData = default(NetLaneData);
                if (m_PrefabRefData.HasComponent(lane))
                {
                    PrefabRef prefabRef = m_PrefabRefData[lane];
                    if (m_NetLaneData.HasComponent(prefabRef.m_Prefab))
                    {
                        netLaneData = m_NetLaneData[prefabRef.m_Prefab];
                    }
                }
                switch (connectionType)
                {
                    case RouteConnectionType.Pedestrian:
                    {
                        float distance2 = 0f;
                        if (m_CurveData.HasComponent(lane))
                        {
                            distance2 = math.distance(position, MathUtils.Position(m_CurveData[lane].m_Bezier, curvePos));
                        }
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            PathfindConnectionData connectionPathfindData3 = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                            return PathUtils.GetSpawnLocationSpecification(
                                connectionType,
                                connectionPathfindData3,
                                roadType,
                                distance2,
                                accessRestriction,
                                requireAuthorization,
                                allowEnter,
                                allowExit,
                                secondaryStart,
                                secondaryEnd
                            );
                        }
                        PathfindPedestrianData pedestrianPathfindData = default(PathfindPedestrianData);
                        if (m_PedestrianPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            pedestrianPathfindData = m_PedestrianPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(pedestrianPathfindData, distance2, accessRestriction, requireAuthorization, allowEnter, allowExit);
                    }
                    case RouteConnectionType.Road:
                    case RouteConnectionType.Cargo:
                    case RouteConnectionType.Parking:
                    case RouteConnectionType.Offroad:
                    {
                        float distance = 0f;
                        if (m_CurveData.HasComponent(lane))
                        {
                            distance = math.distance(position, MathUtils.Position(m_CurveData[lane].m_Bezier, curvePos));
                        }
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            PathfindConnectionData connectionPathfindData2 = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                            return PathUtils.GetSpawnLocationSpecification(
                                connectionType,
                                connectionPathfindData2,
                                roadType,
                                distance,
                                accessRestriction,
                                requireAuthorization,
                                allowEnter,
                                allowExit,
                                secondaryStart,
                                secondaryEnd
                            );
                        }
                        Game.Net.CarLane carLane = default(Game.Net.CarLane);
                        if (m_CarLaneData.HasComponent(lane))
                        {
                            carLane = m_CarLaneData[lane];
                        }
                        else
                        {
                            carLane.m_SpeedLimit = 277.77777f;
                        }
                        PathfindCarData carPathfindData = default(PathfindCarData);
                        if (m_CarPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            carPathfindData = m_CarPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(
                            connectionType,
                            roadType,
                            carPathfindData,
                            carLane,
                            distance,
                            laneCrossCount,
                            accessRestriction,
                            requireAuthorization,
                            allowEnter,
                            allowExit,
                            secondaryStart,
                            secondaryEnd
                        );
                    }
                    case RouteConnectionType.Track:
                    {
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            PathfindConnectionData connectionPathfindData4 = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                            return PathUtils.GetSpawnLocationSpecification(
                                connectionType,
                                connectionPathfindData4,
                                roadType,
                                0f,
                                accessRestriction,
                                requireAuthorization,
                                allowEnter,
                                allowExit,
                                secondaryStart,
                                secondaryEnd
                            );
                        }
                        PathfindTrackData trackPathfindData = default(PathfindTrackData);
                        if (m_TrackPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            trackPathfindData = m_TrackPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(trackPathfindData, accessRestriction);
                    }
                    case RouteConnectionType.Air:
                    {
                        PathfindConnectionData connectionPathfindData = default(PathfindConnectionData);
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            connectionPathfindData = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(
                            connectionType,
                            connectionPathfindData,
                            roadType,
                            0f,
                            accessRestriction,
                            requireAuthorization,
                            allowEnter,
                            allowExit,
                            secondaryStart,
                            secondaryEnd
                        );
                    }
                    default:
                        return default(PathSpecification);
                }
            }
        }

        [BurstCompile]
        private struct UpdatePathEdgeJob : IJob
        {
            [ReadOnly]
            public NativeList<ArchetypeChunk> m_Chunks;

            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public ComponentTypeHandle<Owner> m_OwnerType;

            [ReadOnly]
            public ComponentTypeHandle<Waypoint> m_WaypointType;

            [ReadOnly]
            public ComponentTypeHandle<Position> m_PositionType;

            [ReadOnly]
            public ComponentTypeHandle<Transform> m_TransformType;

            [ReadOnly]
            public ComponentTypeHandle<AccessLane> m_AccessLaneType;

            [ReadOnly]
            public ComponentTypeHandle<RouteLane> m_RouteLaneType;

            [ReadOnly]
            public ComponentTypeHandle<Game.Routes.Segment> m_SegmentType;

            [ReadOnly]
            public ComponentTypeHandle<TaxiStand> m_TaxiStandType;

            [ReadOnly]
            public ComponentTypeHandle<Game.Routes.TakeoffLocation> m_TakeoffLocationType;

            [ReadOnly]
            public ComponentTypeHandle<Connected> m_ConnectedType;

            [ReadOnly]
            public ComponentTypeHandle<RouteInfo> m_RouteInfoType;

            [ReadOnly]
            public ComponentTypeHandle<Game.Objects.SpawnLocation> m_SpawnLocationType;

            [ReadOnly]
            public ComponentTypeHandle<WaitingPassengers> m_WaitingPassengersType;

            [ReadOnly]
            public ComponentLookup<Position> m_PositionData;

            [ReadOnly]
            public ComponentLookup<Game.Routes.TransportStop> m_TransportStopData;

            [ReadOnly]
            public ComponentLookup<TransportLine> m_TransportLineData;

            [ReadOnly]
            public ComponentLookup<Lane> m_LaneData;

            [ReadOnly]
            public ComponentLookup<Curve> m_CurveData;

            [ReadOnly]
            public ComponentLookup<Game.Net.CarLane> m_CarLaneData;

            [ReadOnly]
            public ComponentLookup<MasterLane> m_MasterLaneData;

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;

            [ReadOnly]
            public ComponentLookup<NetLaneData> m_NetLaneData;

            [ReadOnly]
            public ComponentLookup<TransportLineData> m_PrefabTransportLineData;

            [ReadOnly]
            public ComponentLookup<RouteConnectionData> m_PrefabRouteConnectionData;

            [ReadOnly]
            public ComponentLookup<SpawnLocationData> m_PrefabSpawnLocationData;

            [ReadOnly]
            public ComponentLookup<PathfindTransportData> m_TransportPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindPedestrianData> m_PedestrianPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindCarData> m_CarPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindTrackData> m_TrackPathfindData;

            [ReadOnly]
            public ComponentLookup<PathfindConnectionData> m_ConnectionPathfindData;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> m_Waypoints;

            [WriteOnly]
            public NativeArray<UpdateActionData> m_Actions;

            [ReadOnly]
            public ComponentLookup<Connected> m_ConnectedLookup;

            [ReadOnly]
            public ComponentLookup<CustomWaypoint> m_CustomWaypointLookup;

            [ReadOnly]
            public ComponentLookup<RouteInfo> m_RouteInfoLookup;

            [ReadOnly]
            public BufferLookup<RouteSegment> m_RouteSegmentLookup;

            public void Execute()
            {
                int num = 0;
                for (int i = 0; i < m_Chunks.Length; i++)
                {
                    ArchetypeChunk archetypeChunk = m_Chunks[i];
                    NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(m_EntityType);
                    NativeArray<Owner> nativeArray2 = archetypeChunk.GetNativeArray(ref m_OwnerType);
                    NativeArray<AccessLane> nativeArray3 = archetypeChunk.GetNativeArray(ref m_AccessLaneType);
                    NativeArray<RouteLane> nativeArray4 = archetypeChunk.GetNativeArray(ref m_RouteLaneType);
                    NativeArray<Game.Objects.SpawnLocation> nativeArray5 = archetypeChunk.GetNativeArray(ref m_SpawnLocationType);
                    if (nativeArray3.Length != 0 || nativeArray4.Length != 0 || nativeArray5.Length != 0)
                    {
                        NativeArray<Waypoint> nativeArray6 = archetypeChunk.GetNativeArray(ref m_WaypointType);
                        NativeArray<Position> nativeArray7 = archetypeChunk.GetNativeArray(ref m_PositionType);
                        NativeArray<Connected> nativeArray8 = archetypeChunk.GetNativeArray(ref m_ConnectedType);
                        NativeArray<Transform> nativeArray9 = archetypeChunk.GetNativeArray(ref m_TransformType);
                        NativeArray<Game.Routes.TakeoffLocation> nativeArray10 = archetypeChunk.GetNativeArray(ref m_TakeoffLocationType);
                        NativeArray<TaxiStand> nativeArray11 = archetypeChunk.GetNativeArray(ref m_TaxiStandType);
                        NativeArray<WaitingPassengers> nativeArray12 = archetypeChunk.GetNativeArray(ref m_WaitingPassengersType);
                        for (int j = 0; j < nativeArray.Length; j++)
                        {
                            Entity entity = nativeArray[j];
                            AccessLane accessLane = default(AccessLane);
                            if (nativeArray3.Length != 0)
                            {
                                accessLane = nativeArray3[j];
                            }
                            Game.Objects.SpawnLocation spawnLocation = default(Game.Objects.SpawnLocation);
                            if (nativeArray5.Length != 0)
                            {
                                spawnLocation = nativeArray5[j];
                            }
                            UpdateActionData value = new UpdateActionData { m_Owner = entity };
                            Entity lane = Entity.Null;
                            if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane1))
                            {
                                lane = spawnLocation.m_ConnectedLane1;
                                value.m_StartNode = new PathNode(m_LaneData[spawnLocation.m_ConnectedLane1].m_MiddleNode, spawnLocation.m_CurvePosition1);
                            }
                            else if (m_LaneData.HasComponent(accessLane.m_Lane))
                            {
                                lane = accessLane.m_Lane;
                                value.m_StartNode = new PathNode(m_LaneData[accessLane.m_Lane].m_MiddleNode, accessLane.m_CurvePos);
                            }
                            else if (m_TransportStopData.HasComponent(accessLane.m_Lane))
                            {
                                lane = accessLane.m_Lane;
                                value.m_StartNode = new PathNode(accessLane.m_Lane, 2);
                            }
                            else
                            {
                                value.m_StartNode = new PathNode(entity, 2);
                            }
                            value.m_MiddleNode = new PathNode(entity, 1);
                            if (nativeArray7.Length != 0)
                            {
                                value.m_Location = PathUtils.GetLocationSpecification(nativeArray7[j].m_Position);
                            }
                            else
                            {
                                value.m_Location = PathUtils.GetLocationSpecification(nativeArray9[j].m_Position);
                            }
                            if (nativeArray6.Length != 0)
                            {
                                value.m_EndNode = new PathNode(entity, 0);
                                Owner owner = default(Owner);
                                if (nativeArray2.Length != 0)
                                {
                                    owner = nativeArray2[j];
                                }
                                Game.Routes.TransportStop transportStop = default(Game.Routes.TransportStop);
                                bool isWaypoint = true;
                                if (nativeArray8.Length != 0)
                                {
                                    Connected connected = nativeArray8[j];
                                    if (m_TransportStopData.HasComponent(connected.m_Connected))
                                    {
                                        transportStop = m_TransportStopData[connected.m_Connected];
                                        isWaypoint = false;
                                    }
                                }
                                WaitingPassengers waitingPassengers = default(WaitingPassengers);
                                if (nativeArray12.Length != 0)
                                {
                                    waitingPassengers = nativeArray12[j];
                                }
                                TransportLineData transportLineData = GetTransportLineData(owner.m_Owner, out var transportLine);
                                PathfindTransportData transportLinePathfindData = GetTransportLinePathfindData(transportLineData);
                                value.m_Specification = PathUtils.GetTransportStopSpecification(
                                    transportStop,
                                    transportLine,
                                    waitingPassengers,
                                    transportLineData,
                                    transportLinePathfindData,
                                    isWaypoint
                                );
                            }
                            else
                            {
                                RouteLane routeLane = default(RouteLane);
                                if (nativeArray4.Length != 0)
                                {
                                    routeLane = nativeArray4[j];
                                }
                                if (m_LaneData.HasComponent(routeLane.m_EndLane))
                                {
                                    value.m_EndNode = new PathNode(m_LaneData[routeLane.m_EndLane].m_MiddleNode, routeLane.m_EndCurvePos);
                                }
                                else
                                {
                                    value.m_EndNode = new PathNode(entity, 0);
                                }
                                value.m_SecondaryEndNode = value.m_EndNode;
                                if (nativeArray5.Length != 0)
                                {
                                    SpawnLocationData spawnLocationData = GetSpawnLocationData(entity);
                                    if (spawnLocationData.m_ConnectionType != RouteConnectionType.None)
                                    {
                                        if (spawnLocationData.m_ConnectionType == RouteConnectionType.Parking && spawnLocationData.m_RoadTypes == RoadTypes.Bicycle)
                                        {
                                            spawnLocationData.m_ConnectionType = RouteConnectionType.Pedestrian;
                                        }
                                        value.m_Specification = GetSpawnLocationPathSpecification(
                                            value.m_Location.m_Line.a,
                                            spawnLocationData.m_ConnectionType,
                                            spawnLocationData.m_RoadTypes,
                                            spawnLocation.m_ConnectedLane1,
                                            spawnLocation.m_CurvePosition1,
                                            0,
                                            spawnLocation.m_AccessRestriction,
                                            spawnLocationData.m_RequireAuthorization,
                                            (spawnLocation.m_Flags & SpawnLocationFlags.AllowEnter) != 0,
                                            (spawnLocation.m_Flags & SpawnLocationFlags.AllowExit) != 0,
                                            secondaryStart: false,
                                            secondaryEnd: false
                                        );
                                        if (nativeArray11.Length != 0)
                                        {
                                            value.m_EndNode = new PathNode(entity, 0);
                                        }
                                        else
                                        {
                                            RouteConnectionData routeConnectionData = GetRouteConnectionData(entity);
                                            if (
                                                (
                                                    spawnLocationData.m_ConnectionType == RouteConnectionType.Road
                                                    || spawnLocationData.m_ConnectionType == RouteConnectionType.Cargo
                                                    || spawnLocationData.m_ConnectionType == RouteConnectionType.Parking
                                                    || spawnLocationData.m_ConnectionType == RouteConnectionType.Offroad
                                                )
                                                && spawnLocationData.m_ConnectionType != routeConnectionData.m_AccessConnectionType
                                            )
                                            {
                                                int laneCrossCount = 1;
                                                if (m_MasterLaneData.HasComponent(spawnLocation.m_ConnectedLane1))
                                                {
                                                    MasterLane masterLane = m_MasterLaneData[spawnLocation.m_ConnectedLane1];
                                                    laneCrossCount = masterLane.m_MaxIndex - masterLane.m_MinIndex + 1;
                                                }
                                                bool flag = false;
                                                if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane2))
                                                {
                                                    value.m_SecondaryStartNode = new PathNode(
                                                        m_LaneData[spawnLocation.m_ConnectedLane2].m_MiddleNode,
                                                        spawnLocation.m_CurvePosition2
                                                    );
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                else
                                                {
                                                    flag = true;
                                                    value.m_SecondaryStartNode = new PathNode(entity, 3);
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                value.m_SecondarySpecification = GetSpawnLocationPathSpecification(
                                                    value.m_Location.m_Line.a,
                                                    spawnLocationData.m_ConnectionType,
                                                    spawnLocationData.m_RoadTypes,
                                                    spawnLocation.m_ConnectedLane2,
                                                    spawnLocation.m_CurvePosition2,
                                                    laneCrossCount,
                                                    spawnLocation.m_AccessRestriction,
                                                    spawnLocationData.m_RequireAuthorization,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowEnter) != 0,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowExit) != 0,
                                                    secondaryStart: false,
                                                    secondaryEnd: false
                                                );
                                                if (flag)
                                                {
                                                    value.m_SecondarySpecification.m_Flags &= ~(EdgeFlags.Forward | EdgeFlags.Backward);
                                                }
                                            }
                                            else if (
                                                spawnLocationData.m_ConnectionType == RouteConnectionType.Pedestrian
                                                && routeConnectionData.m_AccessConnectionType == RouteConnectionType.None
                                                && spawnLocationData.m_ActivityMask.m_Mask == 0
                                            )
                                            {
                                                if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane2))
                                                {
                                                    spawnLocation.m_ConnectedLane1 = spawnLocation.m_ConnectedLane2;
                                                    spawnLocation.m_CurvePosition1 = spawnLocation.m_CurvePosition2;
                                                }
                                                if (m_LaneData.HasComponent(spawnLocation.m_ConnectedLane1))
                                                {
                                                    value.m_SecondaryStartNode = new PathNode(
                                                        m_LaneData[spawnLocation.m_ConnectedLane1].m_MiddleNode,
                                                        spawnLocation.m_CurvePosition1
                                                    );
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                else
                                                {
                                                    value.m_SecondaryStartNode = new PathNode(entity, 3);
                                                    value.m_SecondaryEndNode = value.m_EndNode;
                                                }
                                                value.m_SecondarySpecification = GetSpawnLocationPathSpecification(
                                                    value.m_Location.m_Line.a,
                                                    RouteConnectionType.Road,
                                                    RoadTypes.Bicycle,
                                                    spawnLocation.m_ConnectedLane1,
                                                    spawnLocation.m_CurvePosition1,
                                                    0,
                                                    spawnLocation.m_AccessRestriction,
                                                    spawnLocationData.m_RequireAuthorization,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowEnter) != 0,
                                                    (spawnLocation.m_Flags & SpawnLocationFlags.AllowExit) != 0,
                                                    secondaryStart: true,
                                                    secondaryEnd: true
                                                );
                                                if (spawnLocationData.m_RoadTypes == RoadTypes.Bicycle)
                                                {
                                                    value.m_SecondarySpecification.m_Costs = value.m_Specification.m_Costs;
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (nativeArray10.Length != 0)
                                {
                                    Game.Routes.TakeoffLocation takeoffLocation = nativeArray10[j];
                                    RouteConnectionData routeConnectionData2 = GetRouteConnectionData(entity);
                                    value.m_Specification = GetSpawnLocationPathSpecification(
                                        value.m_Location.m_Line.a,
                                        routeConnectionData2.m_RouteConnectionType,
                                        routeConnectionData2.m_RouteRoadType,
                                        routeLane.m_EndLane,
                                        routeLane.m_EndCurvePos,
                                        0,
                                        takeoffLocation.m_AccessRestriction,
                                        requireAuthorization: false,
                                        (takeoffLocation.m_Flags & TakeoffLocationFlags.AllowEnter) != 0,
                                        (takeoffLocation.m_Flags & TakeoffLocationFlags.AllowExit) != 0,
                                        secondaryStart: false,
                                        secondaryEnd: false
                                    );
                                    if (
                                        routeConnectionData2.m_RouteConnectionType == RouteConnectionType.Air
                                        && routeConnectionData2.m_RouteRoadType == RoadTypes.Airplane
                                        && m_CarLaneData.TryGetComponent(accessLane.m_Lane, out var componentData)
                                        && (componentData.m_Flags & CarLaneFlags.Twoway) == 0
                                    )
                                    {
                                        value.m_Specification.m_Flags &= (EdgeFlags)((accessLane.m_CurvePos >= 0.5f) ? 65533 : 65534);
                                    }
                                    if (
                                        routeConnectionData2.m_RouteConnectionType == RouteConnectionType.Pedestrian
                                        && routeConnectionData2.m_AccessConnectionType == RouteConnectionType.Pedestrian
                                    )
                                    {
                                        value.m_SecondaryStartNode = value.m_StartNode;
                                        bool secondaryEnd = false;
                                        if (routeLane.m_StartLane != routeLane.m_EndLane && m_LaneData.HasComponent(routeLane.m_StartLane))
                                        {
                                            value.m_SecondaryEndNode = new PathNode(m_LaneData[routeLane.m_StartLane].m_MiddleNode, routeLane.m_StartCurvePos);
                                        }
                                        else
                                        {
                                            value.m_SecondaryEndNode = value.m_EndNode;
                                            secondaryEnd = true;
                                        }
                                        value.m_SecondarySpecification = GetSpawnLocationPathSpecification(
                                            value.m_Location.m_Line.a,
                                            RouteConnectionType.Road,
                                            RoadTypes.Bicycle,
                                            routeLane.m_StartLane,
                                            routeLane.m_StartCurvePos,
                                            0,
                                            takeoffLocation.m_AccessRestriction,
                                            requireAuthorization: false,
                                            (takeoffLocation.m_Flags & (TakeoffLocationFlags.AllowEnter | TakeoffLocationFlags.AllowExit)) == TakeoffLocationFlags.AllowEnter,
                                            allowExit: false,
                                            secondaryStart: true,
                                            secondaryEnd
                                        );
                                    }
                                }
                                if (nativeArray11.Length != 0)
                                {
                                    TaxiStand taxiStand = nativeArray11[j];
                                    Game.Routes.TransportStop transportStop2 = default(Game.Routes.TransportStop);
                                    if (m_TransportStopData.HasComponent(entity))
                                    {
                                        transportStop2 = m_TransportStopData[entity];
                                    }
                                    WaitingPassengers waitingPassengers2 = default(WaitingPassengers);
                                    if (nativeArray12.Length != 0)
                                    {
                                        waitingPassengers2 = nativeArray12[j];
                                    }
                                    PathfindTransportData netLaneTransportPathfindData = GetNetLaneTransportPathfindData(lane);
                                    value.m_SecondaryStartNode = value.m_StartNode;
                                    value.m_SecondarySpecification = PathUtils.GetTaxiStopSpecification(
                                        transportStop2,
                                        taxiStand,
                                        waitingPassengers2,
                                        netLaneTransportPathfindData
                                    );
                                }
                            }
                            m_Actions[num++] = value;
                        }
                    }
                    NativeArray<Game.Routes.Segment> nativeArray13 = archetypeChunk.GetNativeArray(ref m_SegmentType);
                    if (nativeArray13.Length == 0)
                    {
                        continue;
                    }
                    NativeArray<RouteInfo> nativeArray14 = archetypeChunk.GetNativeArray(ref m_RouteInfoType);
                    for (int k = 0; k < nativeArray13.Length; k++)
                    {
                        Entity owner2 = nativeArray[k];
                        Owner owner3 = nativeArray2[k];
                        Game.Routes.Segment segment = nativeArray13[k];
                        DynamicBuffer<RouteWaypoint> routeWaypointBuffer = m_Waypoints[owner3.m_Owner];
                        DynamicBuffer<RouteSegment> routeSegmentBuffer = m_RouteSegmentLookup[owner3.m_Owner];
                        int leaderIndex = WaypointUtils.GetLeaderIndex(m_ConnectedLookup, m_CustomWaypointLookup, routeWaypointBuffer, segment.m_Index, out int linkedCount);
                        int groupLastIndex = math.select((leaderIndex + linkedCount - 1) % routeWaypointBuffer.Length, segment.m_Index, linkedCount >= routeWaypointBuffer.Length);
                        if (!m_RouteInfoLookup.TryGetComponent(routeSegmentBuffer[groupLastIndex].m_Segment, out var routeInfo))
                        {
                            routeInfo = default(RouteInfo);
                        }
                        int nextLeaderIndex;
                        int nextLinkedCount;
                        if (linkedCount >= routeWaypointBuffer.Length)
                        {
                            nextLeaderIndex = (segment.m_Index + 1) % routeWaypointBuffer.Length;
                            nextLinkedCount = 1;
                        }
                        else
                        {
                            nextLeaderIndex = WaypointUtils.GetLeaderIndex(
                                m_ConnectedLookup,
                                m_CustomWaypointLookup,
                                routeWaypointBuffer,
                                (leaderIndex + linkedCount) % routeWaypointBuffer.Length,
                                out nextLinkedCount
                            );
                        }
                        Entity waypoint = routeWaypointBuffer[segment.m_Index].m_Waypoint;
                        Position position = m_PositionData[waypoint];
                        TransportLineData transportLineData2 = GetTransportLineData(owner3.m_Owner, out var _);
                        PathfindTransportData transportLinePathfindData2 = GetTransportLinePathfindData(transportLineData2);
                        for (int j = 0; j < nextLinkedCount; j++)
                        {
                            int nextIndex = (nextLeaderIndex + j) % routeWaypointBuffer.Length;

                            Entity waypoint2 = routeWaypointBuffer[nextIndex].m_Waypoint;
                            Position position2 = m_PositionData[waypoint2];
                            UpdateActionData value2 = new UpdateActionData
                            {
                                m_Owner = owner2,
                                m_StartNode = new PathNode(waypoint, 0),
                                m_MiddleNode = new PathNode(owner2, 0),
                                m_EndNode = new PathNode(waypoint2, 0),
                                m_Specification = PathUtils.GetTransportLineSpecification(transportLineData2, transportLinePathfindData2, routeInfo),
                                m_Location = PathUtils.GetLocationSpecification(position.m_Position, position2.m_Position),
                            };
                            m_Actions[num++] = value2;
                        }
                    }
                }
            }

            private SpawnLocationData GetSpawnLocationData(Entity entity)
            {
                PrefabRef prefabRef = m_PrefabRefData[entity];
                if (m_PrefabSpawnLocationData.HasComponent(prefabRef.m_Prefab))
                {
                    return m_PrefabSpawnLocationData[prefabRef.m_Prefab];
                }
                return default(SpawnLocationData);
            }

            private RouteConnectionData GetRouteConnectionData(Entity entity)
            {
                PrefabRef prefabRef = m_PrefabRefData[entity];
                if (m_PrefabRouteConnectionData.HasComponent(prefabRef.m_Prefab))
                {
                    return m_PrefabRouteConnectionData[prefabRef.m_Prefab];
                }
                return default(RouteConnectionData);
            }

            private TransportLineData GetTransportLineData(Entity owner, out TransportLine transportLine)
            {
                if (m_TransportLineData.HasComponent(owner))
                {
                    transportLine = m_TransportLineData[owner];
                    PrefabRef prefabRef = m_PrefabRefData[owner];
                    return m_PrefabTransportLineData[prefabRef.m_Prefab];
                }
                transportLine = default(TransportLine);
                return default(TransportLineData);
            }

            private PathfindTransportData GetTransportLinePathfindData(TransportLineData transportLineData)
            {
                if (m_TransportPathfindData.HasComponent(transportLineData.m_PathfindPrefab))
                {
                    return m_TransportPathfindData[transportLineData.m_PathfindPrefab];
                }
                return default(PathfindTransportData);
            }

            private PathfindTransportData GetNetLaneTransportPathfindData(Entity lane)
            {
                if (m_PrefabRefData.HasComponent(lane))
                {
                    PrefabRef prefabRef = m_PrefabRefData[lane];
                    if (m_NetLaneData.HasComponent(prefabRef.m_Prefab))
                    {
                        NetLaneData netLaneData = m_NetLaneData[prefabRef.m_Prefab];
                        if (m_TransportPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            return m_TransportPathfindData[netLaneData.m_PathfindPrefab];
                        }
                    }
                }
                return default(PathfindTransportData);
            }

            private PathfindConnectionData GetNetLaneConnectionPathfindData(Entity lane)
            {
                if (m_PrefabRefData.HasComponent(lane))
                {
                    PrefabRef prefabRef = m_PrefabRefData[lane];
                    if (m_NetLaneData.HasComponent(prefabRef.m_Prefab))
                    {
                        NetLaneData netLaneData = m_NetLaneData[prefabRef.m_Prefab];
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            return m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                        }
                    }
                }
                return default(PathfindConnectionData);
            }

            private PathSpecification GetSpawnLocationPathSpecification(
                float3 position,
                RouteConnectionType connectionType,
                RoadTypes roadType,
                Entity lane,
                float curvePos,
                int laneCrossCount,
                Entity accessRestriction,
                bool requireAuthorization,
                bool allowEnter,
                bool allowExit,
                bool secondaryStart,
                bool secondaryEnd
            )
            {
                NetLaneData netLaneData = default(NetLaneData);
                if (m_PrefabRefData.HasComponent(lane))
                {
                    PrefabRef prefabRef = m_PrefabRefData[lane];
                    if (m_NetLaneData.HasComponent(prefabRef.m_Prefab))
                    {
                        netLaneData = m_NetLaneData[prefabRef.m_Prefab];
                    }
                }
                switch (connectionType)
                {
                    case RouteConnectionType.Pedestrian:
                    {
                        float distance2 = 0f;
                        if (m_CurveData.HasComponent(lane))
                        {
                            distance2 = math.distance(position, MathUtils.Position(m_CurveData[lane].m_Bezier, curvePos));
                        }
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            PathfindConnectionData connectionPathfindData3 = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                            return PathUtils.GetSpawnLocationSpecification(
                                connectionType,
                                connectionPathfindData3,
                                roadType,
                                distance2,
                                accessRestriction,
                                requireAuthorization,
                                allowEnter,
                                allowExit,
                                secondaryStart,
                                secondaryEnd
                            );
                        }
                        PathfindPedestrianData pedestrianPathfindData = default(PathfindPedestrianData);
                        if (m_PedestrianPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            pedestrianPathfindData = m_PedestrianPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(pedestrianPathfindData, distance2, accessRestriction, requireAuthorization, allowEnter, allowExit);
                    }
                    case RouteConnectionType.Road:
                    case RouteConnectionType.Cargo:
                    case RouteConnectionType.Parking:
                    case RouteConnectionType.Offroad:
                    {
                        float distance = 0f;
                        if (m_CurveData.HasComponent(lane))
                        {
                            distance = math.distance(position, MathUtils.Position(m_CurveData[lane].m_Bezier, curvePos));
                        }
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            PathfindConnectionData connectionPathfindData2 = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                            return PathUtils.GetSpawnLocationSpecification(
                                connectionType,
                                connectionPathfindData2,
                                roadType,
                                distance,
                                accessRestriction,
                                requireAuthorization,
                                allowEnter,
                                allowExit,
                                secondaryStart,
                                secondaryEnd
                            );
                        }
                        Game.Net.CarLane carLane = default(Game.Net.CarLane);
                        if (m_CarLaneData.HasComponent(lane))
                        {
                            carLane = m_CarLaneData[lane];
                        }
                        else
                        {
                            carLane.m_SpeedLimit = 277.77777f;
                        }
                        PathfindCarData carPathfindData = default(PathfindCarData);
                        if (m_CarPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            carPathfindData = m_CarPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(
                            connectionType,
                            roadType,
                            carPathfindData,
                            carLane,
                            distance,
                            laneCrossCount,
                            accessRestriction,
                            requireAuthorization,
                            allowEnter,
                            allowExit,
                            secondaryStart,
                            secondaryEnd
                        );
                    }
                    case RouteConnectionType.Track:
                    {
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            PathfindConnectionData connectionPathfindData4 = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                            return PathUtils.GetSpawnLocationSpecification(
                                connectionType,
                                connectionPathfindData4,
                                roadType,
                                0f,
                                accessRestriction,
                                requireAuthorization,
                                allowEnter,
                                allowExit,
                                secondaryStart,
                                secondaryEnd
                            );
                        }
                        PathfindTrackData trackPathfindData = default(PathfindTrackData);
                        if (m_TrackPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            trackPathfindData = m_TrackPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(trackPathfindData, accessRestriction);
                    }
                    case RouteConnectionType.Air:
                    {
                        PathfindConnectionData connectionPathfindData = default(PathfindConnectionData);
                        if (m_ConnectionPathfindData.HasComponent(netLaneData.m_PathfindPrefab))
                        {
                            connectionPathfindData = m_ConnectionPathfindData[netLaneData.m_PathfindPrefab];
                        }
                        return PathUtils.GetSpawnLocationSpecification(
                            connectionType,
                            connectionPathfindData,
                            roadType,
                            0f,
                            accessRestriction,
                            requireAuthorization,
                            allowEnter,
                            allowExit,
                            secondaryStart,
                            secondaryEnd
                        );
                    }
                    default:
                        return default(PathSpecification);
                }
            }
        }

        [BurstCompile]
        private struct RemovePathEdgeJob : IJob
        {
            [ReadOnly]
            public NativeList<ArchetypeChunk> m_Chunks;

            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [WriteOnly]
            public NativeArray<DeleteActionData> m_Actions;

            public void Execute()
            {
                int num = 0;
                for (int i = 0; i < m_Chunks.Length; i++)
                {
                    NativeArray<Entity> nativeArray = m_Chunks[i].GetNativeArray(m_EntityType);
                    for (int j = 0; j < nativeArray.Length; j++)
                    {
                        DeleteActionData value = new DeleteActionData { m_Owner = nativeArray[j] };
                        m_Actions[num++] = value;
                    }
                }
            }
        }

        private PathfindQueueSystem m_PathfindQueueSystem;

        private EntityQuery m_CreatedSubElementQuery;

        private EntityQuery m_UpdatedSubElementQuery;

        private EntityQuery m_DeletedSubElementQuery;

        private EntityQuery m_AllSubElementQuery;

        private bool m_Loaded;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PathfindQueueSystem = base.World.GetOrCreateSystemManaged<PathfindQueueSystem>();
            m_CreatedSubElementQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[1] { ComponentType.ReadOnly<Created>() },
                    Any = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<AccessLane>(),
                        ComponentType.ReadOnly<RouteLane>(),
                        ComponentType.ReadOnly<Game.Routes.Segment>(),
                        ComponentType.ReadOnly<Game.Objects.SpawnLocation>(),
                    },
                    None = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<Game.Routes.MailBox>(),
                        ComponentType.ReadOnly<LivePath>(),
                        ComponentType.ReadOnly<VerifiedPath>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                }
            );
            m_UpdatedSubElementQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[1] { ComponentType.ReadOnly<Updated>() },
                    Any = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<AccessLane>(),
                        ComponentType.ReadOnly<RouteLane>(),
                        ComponentType.ReadOnly<Game.Routes.Segment>(),
                        ComponentType.ReadOnly<Game.Objects.SpawnLocation>(),
                    },
                    None = new ComponentType[6]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Game.Routes.MailBox>(),
                        ComponentType.ReadOnly<LivePath>(),
                        ComponentType.ReadOnly<VerifiedPath>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                },
                new EntityQueryDesc
                {
                    All = new ComponentType[1] { ComponentType.ReadOnly<PathfindUpdated>() },
                    Any = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<AccessLane>(),
                        ComponentType.ReadOnly<RouteLane>(),
                        ComponentType.ReadOnly<Game.Routes.Segment>(),
                        ComponentType.ReadOnly<Game.Objects.SpawnLocation>(),
                    },
                    None = new ComponentType[6]
                    {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Game.Routes.MailBox>(),
                        ComponentType.ReadOnly<LivePath>(),
                        ComponentType.ReadOnly<VerifiedPath>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                }
            );
            m_DeletedSubElementQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[1] { ComponentType.ReadOnly<Deleted>() },
                    Any = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<AccessLane>(),
                        ComponentType.ReadOnly<RouteLane>(),
                        ComponentType.ReadOnly<Game.Routes.Segment>(),
                        ComponentType.ReadOnly<Game.Objects.SpawnLocation>(),
                    },
                    None = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<Game.Routes.MailBox>(),
                        ComponentType.ReadOnly<LivePath>(),
                        ComponentType.ReadOnly<VerifiedPath>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                }
            );
            m_AllSubElementQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    Any = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<AccessLane>(),
                        ComponentType.ReadOnly<RouteLane>(),
                        ComponentType.ReadOnly<Game.Routes.Segment>(),
                        ComponentType.ReadOnly<Game.Objects.SpawnLocation>(),
                    },
                    None = new ComponentType[5]
                    {
                        ComponentType.ReadOnly<Game.Routes.MailBox>(),
                        ComponentType.ReadOnly<LivePath>(),
                        ComponentType.ReadOnly<VerifiedPath>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Deleted>(),
                    },
                }
            );
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            m_Loaded = true;
        }

        private bool GetLoaded()
        {
            if (m_Loaded)
            {
                m_Loaded = false;
                return true;
            }
            return false;
        }

        protected override void OnUpdate()
        {
            EntityQuery entityQuery;
            int num;
            if (GetLoaded())
            {
                entityQuery = m_AllSubElementQuery;
                num = 0;
            }
            else
            {
                entityQuery = m_CreatedSubElementQuery;
                num = m_UpdatedSubElementQuery.CalculateEntityCount();
            }
            int num2 = entityQuery.CalculateEntityCount();
            int num3 = m_DeletedSubElementQuery.CalculateEntityCount();
            if (num2 != 0 || num != 0 || num3 != 0)
            {
                JobHandle jobHandle = base.Dependency;
                if (num2 != 0)
                {
                    CreateAction action = new CreateAction(num2, Allocator.Persistent);
                    NativeList<ArchetypeChunk> chunks = entityQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var outJobHandle);
                    JobHandle jobHandle2 = IJobExtensions.Schedule(
                        new AddPathEdgeJob
                        {
                            m_Chunks = chunks,
                            m_EntityType = SystemAPI.GetEntityTypeHandle(),
                            m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(true),
                            m_WaypointType = SystemAPI.GetComponentTypeHandle<Waypoint>(true),
                            m_PositionType = SystemAPI.GetComponentTypeHandle<Position>(true),
                            m_TransformType = SystemAPI.GetComponentTypeHandle<Transform>(true),
                            m_AccessLaneType = SystemAPI.GetComponentTypeHandle<AccessLane>(true),
                            m_RouteLaneType = SystemAPI.GetComponentTypeHandle<RouteLane>(true),
                            m_SegmentType = SystemAPI.GetComponentTypeHandle<Game.Routes.Segment>(true),
                            m_TaxiStandType = SystemAPI.GetComponentTypeHandle<TaxiStand>(true),
                            m_TakeoffLocationType = SystemAPI.GetComponentTypeHandle<Game.Routes.TakeoffLocation>(true),
                            m_ConnectedType = SystemAPI.GetComponentTypeHandle<Connected>(true),
                            m_RouteInfoType = SystemAPI.GetComponentTypeHandle<RouteInfo>(true),
                            m_SpawnLocationType = SystemAPI.GetComponentTypeHandle<Game.Objects.SpawnLocation>(true),
                            m_WaitingPassengersType = SystemAPI.GetComponentTypeHandle<WaitingPassengers>(true),
                            m_PositionData = SystemAPI.GetComponentLookup<Position>(true),
                            m_TransportStopData = SystemAPI.GetComponentLookup<Game.Routes.TransportStop>(true),
                            m_TransportLineData = SystemAPI.GetComponentLookup<TransportLine>(true),
                            m_LaneData = SystemAPI.GetComponentLookup<Lane>(true),
                            m_CurveData = SystemAPI.GetComponentLookup<Curve>(true),
                            m_CarLaneData = SystemAPI.GetComponentLookup<Game.Net.CarLane>(true),
                            m_MasterLaneData = SystemAPI.GetComponentLookup<MasterLane>(true),
                            m_Waypoints = SystemAPI.GetBufferLookup<RouteWaypoint>(true),
                            m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                            m_NetLaneData = SystemAPI.GetComponentLookup<NetLaneData>(true),
                            m_PrefabTransportLineData = SystemAPI.GetComponentLookup<TransportLineData>(true),
                            m_PrefabRouteConnectionData = SystemAPI.GetComponentLookup<RouteConnectionData>(true),
                            m_PrefabSpawnLocationData = SystemAPI.GetComponentLookup<SpawnLocationData>(true),
                            m_TransportPathfindData = SystemAPI.GetComponentLookup<PathfindTransportData>(true),
                            m_PedestrianPathfindData = SystemAPI.GetComponentLookup<PathfindPedestrianData>(true),
                            m_CarPathfindData = SystemAPI.GetComponentLookup<PathfindCarData>(true),
                            m_TrackPathfindData = SystemAPI.GetComponentLookup<PathfindTrackData>(true),
                            m_ConnectionPathfindData = SystemAPI.GetComponentLookup<PathfindConnectionData>(true),
                            m_Actions = action.m_CreateData,
                            m_ConnectedLookup = SystemAPI.GetComponentLookup<Connected>(true),
                            m_CustomWaypointLookup = SystemAPI.GetComponentLookup<CustomWaypoint>(true),
                            m_RouteInfoLookup = SystemAPI.GetComponentLookup<RouteInfo>(true),
                            m_RouteSegmentLookup = SystemAPI.GetBufferLookup<RouteSegment>(true),
                        },
                        JobHandle.CombineDependencies(base.Dependency, outJobHandle)
                    );
                    jobHandle = JobHandle.CombineDependencies(jobHandle, jobHandle2);
                    chunks.Dispose(jobHandle2);
                    m_PathfindQueueSystem.Enqueue(action, jobHandle2);
                }
                if (num != 0)
                {
                    UpdateAction action2 = new UpdateAction(num, Allocator.Persistent);
                    NativeList<ArchetypeChunk> chunks2 = m_UpdatedSubElementQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var outJobHandle2);
                    JobHandle jobHandle3 = IJobExtensions.Schedule(
                        new UpdatePathEdgeJob
                        {
                            m_Chunks = chunks2,
                            m_EntityType = SystemAPI.GetEntityTypeHandle(),
                            m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(true),
                            m_WaypointType = SystemAPI.GetComponentTypeHandle<Waypoint>(true),
                            m_PositionType = SystemAPI.GetComponentTypeHandle<Position>(true),
                            m_TransformType = SystemAPI.GetComponentTypeHandle<Transform>(true),
                            m_AccessLaneType = SystemAPI.GetComponentTypeHandle<AccessLane>(true),
                            m_RouteLaneType = SystemAPI.GetComponentTypeHandle<RouteLane>(true),
                            m_SegmentType = SystemAPI.GetComponentTypeHandle<Game.Routes.Segment>(true),
                            m_TaxiStandType = SystemAPI.GetComponentTypeHandle<TaxiStand>(true),
                            m_TakeoffLocationType = SystemAPI.GetComponentTypeHandle<Game.Routes.TakeoffLocation>(true),
                            m_ConnectedType = SystemAPI.GetComponentTypeHandle<Connected>(true),
                            m_RouteInfoType = SystemAPI.GetComponentTypeHandle<RouteInfo>(true),
                            m_SpawnLocationType = SystemAPI.GetComponentTypeHandle<Game.Objects.SpawnLocation>(true),
                            m_WaitingPassengersType = SystemAPI.GetComponentTypeHandle<Game.Routes.WaitingPassengers>(true),
                            m_PositionData = SystemAPI.GetComponentLookup<Game.Routes.Position>(true),
                            m_TransportStopData = SystemAPI.GetComponentLookup<Game.Routes.TransportStop>(true),
                            m_TransportLineData = SystemAPI.GetComponentLookup<Game.Routes.TransportLine>(true),
                            m_LaneData = SystemAPI.GetComponentLookup<Game.Net.Lane>(true),
                            m_CurveData = SystemAPI.GetComponentLookup<Game.Net.Curve>(true),
                            m_CarLaneData = SystemAPI.GetComponentLookup<Game.Net.CarLane>(true),
                            m_MasterLaneData = SystemAPI.GetComponentLookup<Game.Net.MasterLane>(true),
                            m_Waypoints = SystemAPI.GetBufferLookup<Game.Routes.RouteWaypoint>(true),
                            m_PrefabRefData = SystemAPI.GetComponentLookup<Game.Prefabs.PrefabRef>(true),
                            m_NetLaneData = SystemAPI.GetComponentLookup<Game.Prefabs.NetLaneData>(true),
                            m_PrefabTransportLineData = SystemAPI.GetComponentLookup<Game.Prefabs.TransportLineData>(true),
                            m_PrefabRouteConnectionData = SystemAPI.GetComponentLookup<Game.Prefabs.RouteConnectionData>(true),
                            m_PrefabSpawnLocationData = SystemAPI.GetComponentLookup<Game.Prefabs.SpawnLocationData>(true),
                            m_TransportPathfindData = SystemAPI.GetComponentLookup<Game.Prefabs.PathfindTransportData>(true),
                            m_PedestrianPathfindData = SystemAPI.GetComponentLookup<Game.Prefabs.PathfindPedestrianData>(true),
                            m_CarPathfindData = SystemAPI.GetComponentLookup<Game.Prefabs.PathfindCarData>(true),
                            m_TrackPathfindData = SystemAPI.GetComponentLookup<Game.Prefabs.PathfindTrackData>(true),
                            m_ConnectionPathfindData = SystemAPI.GetComponentLookup<Game.Prefabs.PathfindConnectionData>(true),
                            m_Actions = action2.m_UpdateData,
                            m_ConnectedLookup = SystemAPI.GetComponentLookup<Connected>(true),
                            m_CustomWaypointLookup = SystemAPI.GetComponentLookup<CustomWaypoint>(true),
                            m_RouteInfoLookup = SystemAPI.GetComponentLookup<RouteInfo>(true),
                            m_RouteSegmentLookup = SystemAPI.GetBufferLookup<RouteSegment>(true),
                        },
                        JobHandle.CombineDependencies(base.Dependency, outJobHandle2)
                    );
                    jobHandle = JobHandle.CombineDependencies(jobHandle, jobHandle3);
                    chunks2.Dispose(jobHandle3);
                    m_PathfindQueueSystem.Enqueue(action2, jobHandle3);
                }
                if (num3 != 0)
                {
                    DeleteAction action3 = new DeleteAction(num3, Allocator.Persistent);
                    NativeList<ArchetypeChunk> chunks3 = m_DeletedSubElementQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var outJobHandle3);
                    JobHandle jobHandle4 = IJobExtensions.Schedule(
                        new RemovePathEdgeJob
                        {
                            m_Chunks = chunks3,
                            m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref __TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef),
                            m_Actions = action3.m_DeleteData,
                        },
                        JobHandle.CombineDependencies(base.Dependency, outJobHandle3)
                    );
                    jobHandle = JobHandle.CombineDependencies(jobHandle, jobHandle4);
                    chunks3.Dispose(jobHandle4);
                    m_PathfindQueueSystem.Enqueue(action3, jobHandle4);
                }
                base.Dependency = jobHandle;
            }
        }

        public PatchedRoutesModifiedSystem() { }
    }
}
