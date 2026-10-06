using BoardingController.Components;
using BoardingController.Utils;
using Game;
using Game.Achievements;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Creatures;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Rendering;
using Game.Routes;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace BoardingController.Systems.Simulation
{
    public partial class PatchedTransportAircraftAISystem : GameSystemBase
    {
        [BurstCompile]
        private struct TransportAircraftTickJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public ComponentTypeHandle<Owner> m_OwnerType;

            [ReadOnly]
            public ComponentTypeHandle<Unspawned> m_UnspawnedType;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

            [ReadOnly]
            public ComponentTypeHandle<CurrentRoute> m_CurrentRouteType;

            [ReadOnly]
            public ComponentTypeHandle<Stopped> m_StoppedType;

            [ReadOnly]
            public BufferTypeHandle<Passenger> m_PassengerType;

            public ComponentTypeHandle<Game.Vehicles.CargoTransport> m_CargoTransportType;

            public ComponentTypeHandle<Game.Vehicles.PublicTransport> m_PublicTransportType;

            public ComponentTypeHandle<Aircraft> m_AircraftType;

            public ComponentTypeHandle<AircraftCurrentLane> m_CurrentLaneType;

            public ComponentTypeHandle<Target> m_TargetType;

            public ComponentTypeHandle<PathOwner> m_PathOwnerType;

            public ComponentTypeHandle<Odometer> m_OdometerType;

            public BufferTypeHandle<AircraftNavigationLane> m_AircraftNavigationLaneType;

            public BufferTypeHandle<ServiceDispatch> m_ServiceDispatchType;

            [ReadOnly]
            public ComponentLookup<Owner> m_OwnerData;

            [ReadOnly]
            public ComponentLookup<PathInformation> m_PathInformationData;

            [ReadOnly]
            public ComponentLookup<TransportVehicleRequest> m_TransportVehicleRequestData;

            [ReadOnly]
            public ComponentLookup<AircraftData> m_PrefabAircraftData;

            [ReadOnly]
            public ComponentLookup<HelicopterData> m_PrefabHelicopterData;

            [ReadOnly]
            public ComponentLookup<AirplaneData> m_PrefabAirplaneData;

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;

            [ReadOnly]
            public ComponentLookup<PublicTransportVehicleData> m_PublicTransportVehicleData;

            [ReadOnly]
            public ComponentLookup<CargoTransportVehicleData> m_CargoTransportVehicleData;

            [ReadOnly]
            public ComponentLookup<Waypoint> m_WaypointData;

            [ReadOnly]
            public ComponentLookup<Connected> m_ConnectedData;

            public ComponentLookup<CustomWaypoint> m_CustomWaypointLookup;

            [ReadOnly]
            public ComponentLookup<BoardingVehicle> m_BoardingVehicleData;

            [ReadOnly]
            public ComponentLookup<RouteLane> m_RouteLaneData;

            [ReadOnly]
            public ComponentLookup<Game.Routes.Color> m_RouteColorData;

            [ReadOnly]
            public ComponentLookup<Game.Companies.StorageCompany> m_StorageCompanyData;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.TransportStation> m_TransportStationData;

            [ReadOnly]
            public ComponentLookup<Lane> m_LaneData;

            [ReadOnly]
            public ComponentLookup<CurrentVehicle> m_CurrentVehicleData;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> m_RouteWaypoints;

            [ReadOnly]
            public BufferLookup<Game.Net.SubLane> m_SubLanes;

            [NativeDisableParallelForRestriction]
            public BufferLookup<PathElement> m_PathElements;

            [NativeDisableParallelForRestriction]
            public BufferLookup<LoadingResources> m_LoadingResources;

            [ReadOnly]
            public uint m_SimulationFrameIndex;

            [ReadOnly]
            public EntityArchetype m_TransportVehicleRequestArchetype;

            [ReadOnly]
            public EntityArchetype m_HandleRequestArchetype;

            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

            public TransportBoardingHelpers.BoardingData.Concurrent m_BoardingData;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
                NativeArray<Owner> nativeArray2 = chunk.GetNativeArray(ref m_OwnerType);
                NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray(ref m_PrefabRefType);
                NativeArray<CurrentRoute> nativeArray4 = chunk.GetNativeArray(ref m_CurrentRouteType);
                NativeArray<AircraftCurrentLane> nativeArray5 = chunk.GetNativeArray(ref m_CurrentLaneType);
                NativeArray<Game.Vehicles.CargoTransport> nativeArray6 = chunk.GetNativeArray(ref m_CargoTransportType);
                NativeArray<Game.Vehicles.PublicTransport> nativeArray7 = chunk.GetNativeArray(ref m_PublicTransportType);
                NativeArray<Aircraft> nativeArray8 = chunk.GetNativeArray(ref m_AircraftType);
                NativeArray<Target> nativeArray9 = chunk.GetNativeArray(ref m_TargetType);
                NativeArray<PathOwner> nativeArray10 = chunk.GetNativeArray(ref m_PathOwnerType);
                NativeArray<Odometer> nativeArray11 = chunk.GetNativeArray(ref m_OdometerType);
                BufferAccessor<AircraftNavigationLane> bufferAccessor = chunk.GetBufferAccessor(ref m_AircraftNavigationLaneType);
                BufferAccessor<Passenger> bufferAccessor2 = chunk.GetBufferAccessor(ref m_PassengerType);
                BufferAccessor<ServiceDispatch> bufferAccessor3 = chunk.GetBufferAccessor(ref m_ServiceDispatchType);
                bool isStopped = chunk.Has(ref m_StoppedType);
                bool isUnspawned = chunk.Has(ref m_UnspawnedType);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    Entity entity = nativeArray[i];
                    Owner owner = nativeArray2[i];
                    PrefabRef prefabRef = nativeArray3[i];
                    Aircraft aircraft = nativeArray8[i];
                    AircraftCurrentLane currentLane = nativeArray5[i];
                    PathOwner pathOwner = nativeArray10[i];
                    Target target = nativeArray9[i];
                    Odometer odometer = nativeArray11[i];
                    DynamicBuffer<AircraftNavigationLane> navigationLanes = bufferAccessor[i];
                    DynamicBuffer<ServiceDispatch> serviceDispatches = bufferAccessor3[i];
                    CurrentRoute currentRoute = default;
                    if (nativeArray4.Length != 0)
                    {
                        currentRoute = nativeArray4[i];
                    }
                    Game.Vehicles.CargoTransport cargoTransport = default;
                    if (nativeArray6.Length != 0)
                    {
                        cargoTransport = nativeArray6[i];
                    }
                    Game.Vehicles.PublicTransport publicTransport = default;
                    DynamicBuffer<Passenger> passengers = default;
                    if (nativeArray7.Length != 0)
                    {
                        publicTransport = nativeArray7[i];
                        passengers = bufferAccessor2[i];
                    }
                    VehicleUtils.CheckUnspawned(unfilteredChunkIndex, entity, currentLane, isUnspawned, m_CommandBuffer);
                    Tick(
                        unfilteredChunkIndex,
                        entity,
                        owner,
                        prefabRef,
                        currentRoute,
                        navigationLanes,
                        passengers,
                        serviceDispatches,
                        isStopped,
                        ref cargoTransport,
                        ref publicTransport,
                        ref aircraft,
                        ref currentLane,
                        ref pathOwner,
                        ref target,
                        ref odometer
                    );
                    nativeArray8[i] = aircraft;
                    nativeArray5[i] = currentLane;
                    nativeArray10[i] = pathOwner;
                    nativeArray9[i] = target;
                    nativeArray11[i] = odometer;
                    if (nativeArray6.Length != 0)
                    {
                        nativeArray6[i] = cargoTransport;
                    }
                    if (nativeArray7.Length != 0)
                    {
                        nativeArray7[i] = publicTransport;
                    }
                }
            }

            private void Tick(
                int jobIndex,
                Entity vehicleEntity,
                Owner owner,
                PrefabRef prefabRef,
                CurrentRoute currentRoute,
                DynamicBuffer<AircraftNavigationLane> navigationLanes,
                DynamicBuffer<Passenger> passengers,
                DynamicBuffer<ServiceDispatch> serviceDispatches,
                bool isStopped,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Aircraft aircraft,
                ref AircraftCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                ref Odometer odometer
            )
            {
                if (VehicleUtils.ResetUpdatedPath(ref pathOwner))
                {
                    ResetPath(vehicleEntity, ref cargoTransport, ref publicTransport, ref aircraft, ref currentLane, ref pathOwner);
                    if (
                        ((publicTransport.m_State & PublicTransportFlags.DummyTraffic) != 0 || (cargoTransport.m_State & CargoTransportFlags.DummyTraffic) != 0)
                        && m_LoadingResources.TryGetBuffer(vehicleEntity, out var bufferData)
                    )
                    {
                        CheckDummyResources(jobIndex, vehicleEntity, prefabRef, bufferData);
                    }
                }
                bool num = (cargoTransport.m_State & CargoTransportFlags.EnRoute) == 0 && (publicTransport.m_State & PublicTransportFlags.EnRoute) == 0;
                if (m_PublicTransportVehicleData.HasComponent(prefabRef.m_Prefab))
                {
                    PublicTransportVehicleData publicTransportVehicleData = m_PublicTransportVehicleData[prefabRef.m_Prefab];
                    if (
                        odometer.m_Distance >= publicTransportVehicleData.m_MaintenanceRange
                        && publicTransportVehicleData.m_MaintenanceRange > 0.1f
                        && (publicTransport.m_State & PublicTransportFlags.Refueling) == 0
                    )
                    {
                        publicTransport.m_State |= PublicTransportFlags.RequiresMaintenance;
                    }
                }
                bool isCargoVehicle = false;
                if (m_CargoTransportVehicleData.HasComponent(prefabRef.m_Prefab))
                {
                    CargoTransportVehicleData cargoTransportVehicleData = m_CargoTransportVehicleData[prefabRef.m_Prefab];
                    if (
                        odometer.m_Distance >= cargoTransportVehicleData.m_MaintenanceRange
                        && cargoTransportVehicleData.m_MaintenanceRange > 0.1f
                        && (cargoTransport.m_State & CargoTransportFlags.Refueling) == 0
                    )
                    {
                        cargoTransport.m_State |= CargoTransportFlags.RequiresMaintenance;
                    }
                    isCargoVehicle = true;
                }
                if (num)
                {
                    CheckServiceDispatches(vehicleEntity, serviceDispatches, ref cargoTransport, ref publicTransport);
                    if (
                        serviceDispatches.Length == 0
                        && (cargoTransport.m_State & (CargoTransportFlags.RequiresMaintenance | CargoTransportFlags.DummyTraffic | CargoTransportFlags.Disabled)) == 0
                        && (publicTransport.m_State & (PublicTransportFlags.RequiresMaintenance | PublicTransportFlags.DummyTraffic | PublicTransportFlags.Disabled)) == 0
                    )
                    {
                        RequestTargetIfNeeded(jobIndex, vehicleEntity, ref publicTransport, ref cargoTransport);
                    }
                }
                else
                {
                    serviceDispatches.Clear();
                    cargoTransport.m_RequestCount = 0;
                    publicTransport.m_RequestCount = 0;
                }
                if (!m_PrefabRefData.HasComponent(target.m_Target) || VehicleUtils.PathfindFailed(pathOwner))
                {
                    if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                    {
                        StopBoarding(vehicleEntity, currentRoute, passengers, ref cargoTransport, ref publicTransport, ref target, ref odometer, forcedStop: true);
                    }
                    if (
                        VehicleUtils.IsStuck(pathOwner)
                        || (cargoTransport.m_State & (CargoTransportFlags.Returning | CargoTransportFlags.DummyTraffic)) != 0
                        || (publicTransport.m_State & (PublicTransportFlags.Returning | PublicTransportFlags.Launched | PublicTransportFlags.DummyTraffic)) != 0
                    )
                    {
                        m_CommandBuffer.AddComponent<Deleted>(jobIndex, vehicleEntity, default);
                        return;
                    }
                    ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches, ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                }
                else if (VehicleUtils.PathEndReached(currentLane))
                {
                    if (
                        (cargoTransport.m_State & (CargoTransportFlags.Returning | CargoTransportFlags.DummyTraffic)) != 0
                        || (publicTransport.m_State & (PublicTransportFlags.Returning | PublicTransportFlags.Launched | PublicTransportFlags.DummyTraffic)) != 0
                    )
                    {
                        if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                        {
                            if (
                                StopBoarding(vehicleEntity, currentRoute, passengers, ref cargoTransport, ref publicTransport, ref target, ref odometer, forcedStop: false)
                                && !SelectNextDispatch(
                                    jobIndex,
                                    vehicleEntity,
                                    currentRoute,
                                    navigationLanes,
                                    serviceDispatches,
                                    ref cargoTransport,
                                    ref publicTransport,
                                    ref aircraft,
                                    ref currentLane,
                                    ref pathOwner,
                                    ref target
                                )
                            )
                            {
                                m_CommandBuffer.AddComponent<Deleted>(jobIndex, vehicleEntity, default);
                                return;
                            }
                        }
                        else if (
                            (
                                !passengers.IsCreated
                                || passengers.Length <= 0
                                || !StartBoarding(jobIndex, vehicleEntity, currentRoute, prefabRef, ref cargoTransport, ref publicTransport, ref target, isCargoVehicle)
                            )
                            && !SelectNextDispatch(
                                jobIndex,
                                vehicleEntity,
                                currentRoute,
                                navigationLanes,
                                serviceDispatches,
                                ref cargoTransport,
                                ref publicTransport,
                                ref aircraft,
                                ref currentLane,
                                ref pathOwner,
                                ref target
                            )
                        )
                        {
                            m_CommandBuffer.AddComponent<Deleted>(jobIndex, vehicleEntity, default);
                            return;
                        }
                    }
                    else if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                    {
                        if (StopBoarding(vehicleEntity, currentRoute, passengers, ref cargoTransport, ref publicTransport, ref target, ref odometer, forcedStop: false))
                        {
                            if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == 0 && (publicTransport.m_State & PublicTransportFlags.EnRoute) == 0)
                            {
                                ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches, ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                            }
                            else
                            {
                                SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                            }
                        }
                    }
                    else if (!m_RouteWaypoints.HasBuffer(currentRoute.m_Route) || !m_WaypointData.HasComponent(target.m_Target))
                    {
                        ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches, ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                    }
                    else if (!StartBoarding(jobIndex, vehicleEntity, currentRoute, prefabRef, ref cargoTransport, ref publicTransport, ref target, isCargoVehicle))
                    {
                        if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == 0 && (publicTransport.m_State & PublicTransportFlags.EnRoute) == 0)
                        {
                            ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches, ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                        }
                        else
                        {
                            SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                        }
                    }
                }
                else
                {
                    if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                    {
                        StopBoarding(vehicleEntity, currentRoute, passengers, ref cargoTransport, ref publicTransport, ref target, ref odometer, forcedStop: true);
                    }
                    if (isStopped)
                    {
                        StartVehicle(jobIndex, vehicleEntity, ref currentLane);
                    }
                }
                Entity skipWaypoint = Entity.Null;
                if ((cargoTransport.m_State & CargoTransportFlags.Boarding) == 0 && (publicTransport.m_State & PublicTransportFlags.Boarding) == 0)
                {
                    if ((cargoTransport.m_State & CargoTransportFlags.Returning) != 0 || (publicTransport.m_State & PublicTransportFlags.Returning) != 0)
                    {
                        if (!passengers.IsCreated || passengers.Length == 0)
                        {
                            SelectNextDispatch(
                                jobIndex,
                                vehicleEntity,
                                currentRoute,
                                navigationLanes,
                                serviceDispatches,
                                ref cargoTransport,
                                ref publicTransport,
                                ref aircraft,
                                ref currentLane,
                                ref pathOwner,
                                ref target
                            );
                        }
                    }
                    else if (
                        (cargoTransport.m_State & CargoTransportFlags.Arriving) == 0
                        && (publicTransport.m_State & (PublicTransportFlags.Arriving | PublicTransportFlags.Launched)) == 0
                    )
                    {
                        CheckNavigationLanes(currentRoute, navigationLanes, ref cargoTransport, ref publicTransport, ref currentLane, ref pathOwner, ref target, out skipWaypoint);
                    }
                }
                FindPathIfNeeded(vehicleEntity, prefabRef, skipWaypoint, ref currentLane, ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
            }

            private void FindPathIfNeeded(
                Entity vehicleEntity,
                PrefabRef prefabRef,
                Entity skipWaypoint,
                ref AircraftCurrentLane currentLane,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                if (VehicleUtils.RequireNewPath(pathOwner))
                {
                    AircraftData aircraftData = m_PrefabAircraftData[prefabRef.m_Prefab];
                    PathfindParameters parameters = new PathfindParameters
                    {
                        m_WalkSpeed = 5.555556f,
                        m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                        m_Methods = VehicleUtils.GetPathMethods(aircraftData),
                        m_IgnoredRules = (
                            RuleFlags.ForbidCombustionEngines
                            | RuleFlags.ForbidTransitTraffic
                            | RuleFlags.ForbidPrivateTraffic
                            | RuleFlags.ForbidSlowTraffic
                            | RuleFlags.AvoidBicycles
                        ),
                    };
                    SetupQueueTarget origin = new SetupQueueTarget { m_Type = SetupTargetType.CurrentLocation, m_Methods = VehicleUtils.GetPathMethods(aircraftData) };
                    SetupQueueTarget destination = new SetupQueueTarget
                    {
                        m_Type = SetupTargetType.CurrentLocation,
                        m_Methods = VehicleUtils.GetPathMethods(aircraftData),
                        m_Entity = target.m_Target,
                    };
                    if ((publicTransport.m_State & PublicTransportFlags.Launched) == 0)
                    {
                        destination.m_Methods &= ~PathMethod.Flying;
                    }
                    if (m_PrefabHelicopterData.HasComponent(prefabRef.m_Prefab))
                    {
                        parameters.m_MaxSpeed = m_PrefabHelicopterData[prefabRef.m_Prefab].m_FlyingMaxSpeed;
                        origin.m_RoadTypes = RoadTypes.Helicopter;
                        origin.m_FlyingTypes = RoadTypes.Helicopter;
                        destination.m_RoadTypes = RoadTypes.Helicopter;
                        destination.m_FlyingTypes = RoadTypes.Helicopter;
                    }
                    else if (m_PrefabAirplaneData.HasComponent(prefabRef.m_Prefab))
                    {
                        parameters.m_MaxSpeed = m_PrefabAirplaneData[prefabRef.m_Prefab].m_FlyingSpeed.y;
                        origin.m_RoadTypes = RoadTypes.Airplane;
                        origin.m_FlyingTypes = RoadTypes.Airplane;
                        destination.m_RoadTypes = RoadTypes.Airplane;
                        destination.m_FlyingTypes = RoadTypes.Airplane;
                    }
                    if ((currentLane.m_LaneFlags & AircraftLaneFlags.Flying) == 0)
                    {
                        origin.m_Methods &= ~PathMethod.Flying;
                    }
                    if (skipWaypoint != Entity.Null)
                    {
                        origin.m_Entity = skipWaypoint;
                        pathOwner.m_State |= PathFlags.Append;
                    }
                    else
                    {
                        pathOwner.m_State &= ~PathFlags.Append;
                    }
                    if (
                        (cargoTransport.m_State & (CargoTransportFlags.EnRoute | CargoTransportFlags.RouteSource))
                            == (CargoTransportFlags.EnRoute | CargoTransportFlags.RouteSource)
                        || (publicTransport.m_State & (PublicTransportFlags.EnRoute | PublicTransportFlags.RouteSource))
                            == (PublicTransportFlags.EnRoute | PublicTransportFlags.RouteSource)
                    )
                    {
                        parameters.m_PathfindFlags = PathfindFlags.Stable | PathfindFlags.IgnoreFlow;
                    }
                    else if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == 0 && (publicTransport.m_State & PublicTransportFlags.EnRoute) == 0)
                    {
                        cargoTransport.m_State &= ~CargoTransportFlags.RouteSource;
                        publicTransport.m_State &= ~PublicTransportFlags.RouteSource;
                    }
                    VehicleUtils.SetupPathfind(
                        item: new SetupQueueItem(vehicleEntity, parameters, origin, destination),
                        currentLane: ref currentLane,
                        pathOwner: ref pathOwner,
                        queue: m_PathfindQueue
                    );
                }
            }

            private void StartVehicle(int jobIndex, Entity entity, ref AircraftCurrentLane currentLane)
            {
                m_CommandBuffer.RemoveComponent<Stopped>(jobIndex, entity);
                m_CommandBuffer.AddComponent<Moving>(jobIndex, entity, default);
                m_CommandBuffer.AddBuffer<TransformFrame>(jobIndex, entity);
                m_CommandBuffer.AddComponent<InterpolatedTransform>(jobIndex, entity, default);
                m_CommandBuffer.AddComponent<Updated>(jobIndex, entity, default);
                if ((currentLane.m_LaneFlags & AircraftLaneFlags.Airway) != 0)
                {
                    currentLane.m_LaneFlags |= AircraftLaneFlags.TakingOff;
                }
            }

            private void CheckNavigationLanes(
                CurrentRoute currentRoute,
                DynamicBuffer<AircraftNavigationLane> navigationLanes,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref AircraftCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                out Entity skipWaypoint
            )
            {
                skipWaypoint = Entity.Null;
                if (navigationLanes.Length == 0 || navigationLanes.Length >= 8)
                {
                    return;
                }
                AircraftNavigationLane value = navigationLanes[navigationLanes.Length - 1];
                if ((value.m_Flags & AircraftLaneFlags.EndOfPath) == 0)
                {
                    return;
                }
                if (
                    m_WaypointData.HasComponent(target.m_Target)
                    && m_RouteWaypoints.HasBuffer(currentRoute.m_Route)
                    && (!m_ConnectedData.HasComponent(target.m_Target) || !m_BoardingVehicleData.HasComponent(m_ConnectedData[target.m_Target].m_Connected))
                )
                {
                    if ((pathOwner.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Obsolete)) == 0)
                    {
                        skipWaypoint = target.m_Target;
                        SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                        value.m_Flags &= ~AircraftLaneFlags.EndOfPath;
                        navigationLanes[navigationLanes.Length - 1] = value;
                        cargoTransport.m_State |= CargoTransportFlags.RouteSource;
                        publicTransport.m_State |= PublicTransportFlags.RouteSource;
                    }
                    return;
                }
                cargoTransport.m_State |= CargoTransportFlags.Arriving;
                publicTransport.m_State |= PublicTransportFlags.Arriving;
                if (!m_RouteLaneData.HasComponent(target.m_Target))
                {
                    return;
                }
                RouteLane routeLane = m_RouteLaneData[target.m_Target];
                if (routeLane.m_StartLane != routeLane.m_EndLane)
                {
                    value.m_CurvePosition.y = 1f;
                    AircraftNavigationLane elem = new AircraftNavigationLane { m_Lane = value.m_Lane };
                    if (FindNextLane(ref elem.m_Lane))
                    {
                        value.m_Flags &= ~AircraftLaneFlags.EndOfPath;
                        navigationLanes[navigationLanes.Length - 1] = value;
                        elem.m_Flags |= AircraftLaneFlags.EndOfPath;
                        elem.m_CurvePosition = new float2(0f, routeLane.m_EndCurvePos);
                        navigationLanes.Add(elem);
                    }
                    else
                    {
                        navigationLanes[navigationLanes.Length - 1] = value;
                    }
                }
                else
                {
                    value.m_CurvePosition.y = routeLane.m_EndCurvePos;
                    navigationLanes[navigationLanes.Length - 1] = value;
                }
            }

            private bool FindNextLane(ref Entity lane)
            {
                if (!m_OwnerData.HasComponent(lane) || !m_LaneData.HasComponent(lane))
                {
                    return false;
                }
                Owner owner = m_OwnerData[lane];
                Lane lane2 = m_LaneData[lane];
                if (!m_SubLanes.HasBuffer(owner.m_Owner))
                {
                    return false;
                }
                DynamicBuffer<Game.Net.SubLane> dynamicBuffer = m_SubLanes[owner.m_Owner];
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    Entity subLane = dynamicBuffer[i].m_SubLane;
                    Lane lane3 = m_LaneData[subLane];
                    if (lane2.m_EndNode.Equals(lane3.m_StartNode))
                    {
                        lane = subLane;
                        return true;
                    }
                }
                return false;
            }

            private void ResetPath(
                Entity vehicleEntity,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Aircraft aircraft,
                ref AircraftCurrentLane currentLane,
                ref PathOwner pathOwner
            )
            {
                cargoTransport.m_State &= ~CargoTransportFlags.Arriving;
                publicTransport.m_State &= ~PublicTransportFlags.Arriving;
                if ((pathOwner.m_State & PathFlags.Append) == 0)
                {
                    DynamicBuffer<PathElement> path = m_PathElements[vehicleEntity];
                    PathUtils.ResetPath(ref currentLane, path);
                }
                if ((publicTransport.m_State & PublicTransportFlags.Launched) != 0)
                {
                    aircraft.m_Flags &= ~AircraftFlags.StayOnTaxiway;
                    aircraft.m_Flags |= AircraftFlags.StayMidAir;
                }
                else if (
                    (cargoTransport.m_State & (CargoTransportFlags.Returning | CargoTransportFlags.DummyTraffic)) != 0
                    || (publicTransport.m_State & (PublicTransportFlags.Returning | PublicTransportFlags.DummyTraffic)) != 0
                )
                {
                    aircraft.m_Flags &= ~(AircraftFlags.StayOnTaxiway | AircraftFlags.StayMidAir);
                }
                else
                {
                    aircraft.m_Flags &= ~AircraftFlags.StayMidAir;
                    aircraft.m_Flags |= AircraftFlags.StayOnTaxiway;
                }
            }

            private void CheckDummyResources(int jobIndex, Entity vehicleEntity, PrefabRef prefabRef, DynamicBuffer<LoadingResources> loadingResources)
            {
                if (loadingResources.Length == 0)
                {
                    return;
                }
                if (m_CargoTransportVehicleData.HasComponent(prefabRef.m_Prefab))
                {
                    CargoTransportVehicleData cargoTransportVehicleData = m_CargoTransportVehicleData[prefabRef.m_Prefab];
                    DynamicBuffer<Resources> dynamicBuffer = m_CommandBuffer.SetBuffer<Resources>(jobIndex, vehicleEntity);
                    for (int i = 0; i < loadingResources.Length; i++)
                    {
                        if (dynamicBuffer.Length >= cargoTransportVehicleData.m_MaxResourceCount)
                        {
                            break;
                        }
                        LoadingResources loadingResources2 = loadingResources[i];
                        int num = math.min(loadingResources2.m_Amount, cargoTransportVehicleData.m_CargoCapacity);
                        loadingResources2.m_Amount -= num;
                        cargoTransportVehicleData.m_CargoCapacity -= num;
                        if (num > 0)
                        {
                            dynamicBuffer.Add(new Resources { m_Resource = loadingResources2.m_Resource, m_Amount = num });
                        }
                    }
                }
                loadingResources.Clear();
            }

            private void SetNextWaypointTarget(CurrentRoute currentRoute, ref PathOwner pathOwner, ref Target target)
            {
                DynamicBuffer<RouteWaypoint> dynamicBuffer = m_RouteWaypoints[currentRoute.m_Route];
                VehicleUtils.SetTarget(
                    ref pathOwner,
                    ref target,
                    dynamicBuffer[WaypointUtils.SelectNextWaypoint(ref m_CustomWaypointLookup, dynamicBuffer, m_WaypointData[target.m_Target].m_Index)].m_Waypoint
                );
            }

            private void CheckServiceDispatches(
                Entity vehicleEntity,
                DynamicBuffer<ServiceDispatch> serviceDispatches,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport
            )
            {
                if (serviceDispatches.Length > 1)
                {
                    serviceDispatches.RemoveRange(1, serviceDispatches.Length - 1);
                }
                cargoTransport.m_RequestCount = math.min(1, cargoTransport.m_RequestCount);
                publicTransport.m_RequestCount = math.min(1, publicTransport.m_RequestCount);
                int num = cargoTransport.m_RequestCount + publicTransport.m_RequestCount;
                if (serviceDispatches.Length <= num)
                {
                    return;
                }
                float num2 = -1f;
                Entity entity = Entity.Null;
                for (int i = num; i < serviceDispatches.Length; i++)
                {
                    Entity request = serviceDispatches[i].m_Request;
                    if (m_TransportVehicleRequestData.HasComponent(request))
                    {
                        TransportVehicleRequest transportVehicleRequest = m_TransportVehicleRequestData[request];
                        if (m_PrefabRefData.HasComponent(transportVehicleRequest.m_Route) && transportVehicleRequest.m_Priority > num2)
                        {
                            num2 = transportVehicleRequest.m_Priority;
                            entity = request;
                        }
                    }
                }
                if (entity != Entity.Null)
                {
                    serviceDispatches[num++] = new ServiceDispatch(entity);
                    publicTransport.m_RequestCount++;
                    cargoTransport.m_RequestCount++;
                }
                if (serviceDispatches.Length > num)
                {
                    serviceDispatches.RemoveRange(num, serviceDispatches.Length - num);
                }
            }

            private void RequestTargetIfNeeded(int jobIndex, Entity entity, ref Game.Vehicles.PublicTransport publicTransport, ref Game.Vehicles.CargoTransport cargoTransport)
            {
                if (!m_TransportVehicleRequestData.HasComponent(publicTransport.m_TargetRequest) && !m_TransportVehicleRequestData.HasComponent(cargoTransport.m_TargetRequest))
                {
                    uint num = math.max(256u, 16u);
                    if ((m_SimulationFrameIndex & (num - 1)) == 10)
                    {
                        Entity e = m_CommandBuffer.CreateEntity(jobIndex, m_TransportVehicleRequestArchetype);
                        m_CommandBuffer.SetComponent(jobIndex, e, new ServiceRequest(reversed: true));
                        m_CommandBuffer.SetComponent(jobIndex, e, new TransportVehicleRequest(entity, 1f));
                        m_CommandBuffer.SetComponent(jobIndex, e, new RequestGroup(8u));
                    }
                }
            }

            private bool SelectNextDispatch(
                int jobIndex,
                Entity vehicleEntity,
                CurrentRoute currentRoute,
                DynamicBuffer<AircraftNavigationLane> navigationLanes,
                DynamicBuffer<ServiceDispatch> serviceDispatches,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Aircraft aircraft,
                ref AircraftCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                if (
                    (cargoTransport.m_State & CargoTransportFlags.Returning) == 0
                    && (publicTransport.m_State & PublicTransportFlags.Returning) == 0
                    && cargoTransport.m_RequestCount + publicTransport.m_RequestCount > 0
                    && serviceDispatches.Length > 0
                )
                {
                    serviceDispatches.RemoveAt(0);
                    cargoTransport.m_RequestCount = math.max(0, cargoTransport.m_RequestCount - 1);
                    publicTransport.m_RequestCount = math.max(0, publicTransport.m_RequestCount - 1);
                }
                if (
                    (cargoTransport.m_State & (CargoTransportFlags.RequiresMaintenance | CargoTransportFlags.Disabled)) != 0
                    || (publicTransport.m_State & (PublicTransportFlags.RequiresMaintenance | PublicTransportFlags.Disabled)) != 0
                )
                {
                    cargoTransport.m_RequestCount = 0;
                    publicTransport.m_RequestCount = 0;
                    serviceDispatches.Clear();
                    return false;
                }
                while (cargoTransport.m_RequestCount + publicTransport.m_RequestCount > 0 && serviceDispatches.Length > 0)
                {
                    Entity request = serviceDispatches[0].m_Request;
                    Entity entity = Entity.Null;
                    Entity entity2 = Entity.Null;
                    AircraftFlags aircraftFlags = aircraft.m_Flags;
                    if (m_TransportVehicleRequestData.HasComponent(request))
                    {
                        entity = m_TransportVehicleRequestData[request].m_Route;
                        if (m_PathInformationData.HasComponent(request))
                        {
                            entity2 = m_PathInformationData[request].m_Destination;
                        }
                        aircraftFlags &= ~AircraftFlags.StayMidAir;
                        aircraftFlags |= AircraftFlags.StayOnTaxiway;
                    }
                    if (!m_PrefabRefData.HasComponent(entity2))
                    {
                        serviceDispatches.RemoveAt(0);
                        cargoTransport.m_RequestCount = math.max(0, cargoTransport.m_RequestCount - 1);
                        publicTransport.m_RequestCount = math.max(0, publicTransport.m_RequestCount - 1);
                        continue;
                    }
                    if (m_TransportVehicleRequestData.HasComponent(request))
                    {
                        serviceDispatches.Clear();
                        cargoTransport.m_RequestCount = 0;
                        publicTransport.m_RequestCount = 0;
                        if (m_PrefabRefData.HasComponent(entity))
                        {
                            if (currentRoute.m_Route != entity)
                            {
                                m_CommandBuffer.AddComponent(jobIndex, vehicleEntity, new CurrentRoute(entity));
                                m_CommandBuffer.AppendToBuffer(jobIndex, entity, new RouteVehicle(vehicleEntity));
                                if (m_RouteColorData.TryGetComponent(entity, out var componentData))
                                {
                                    m_CommandBuffer.AddComponent(jobIndex, vehicleEntity, componentData);
                                    m_CommandBuffer.AddComponent<BatchesUpdated>(jobIndex, vehicleEntity);
                                }
                            }
                            cargoTransport.m_State |= CargoTransportFlags.EnRoute;
                            publicTransport.m_State |= PublicTransportFlags.EnRoute;
                        }
                        else
                        {
                            m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
                        }
                        Entity e = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);
                        m_CommandBuffer.SetComponent(jobIndex, e, new HandleRequest(request, vehicleEntity, completed: true));
                    }
                    else
                    {
                        m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
                        Entity e2 = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);
                        m_CommandBuffer.SetComponent(jobIndex, e2, new HandleRequest(request, vehicleEntity, completed: false, pathConsumed: true));
                    }
                    cargoTransport.m_State &= ~CargoTransportFlags.Returning;
                    publicTransport.m_State &= ~PublicTransportFlags.Returning;
                    aircraft.m_Flags = aircraftFlags;
                    if (m_TransportVehicleRequestData.HasComponent(publicTransport.m_TargetRequest))
                    {
                        Entity e3 = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);
                        m_CommandBuffer.SetComponent(jobIndex, e3, new HandleRequest(publicTransport.m_TargetRequest, Entity.Null, completed: true));
                    }
                    if (m_TransportVehicleRequestData.HasComponent(cargoTransport.m_TargetRequest))
                    {
                        Entity e4 = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);
                        m_CommandBuffer.SetComponent(jobIndex, e4, new HandleRequest(cargoTransport.m_TargetRequest, Entity.Null, completed: true));
                    }
                    if (m_PathElements.HasBuffer(request))
                    {
                        DynamicBuffer<PathElement> appendPath = m_PathElements[request];
                        if (appendPath.Length != 0)
                        {
                            DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[vehicleEntity];
                            PathUtils.TrimPath(dynamicBuffer, ref pathOwner);
                            float num =
                                math.max(cargoTransport.m_PathElementTime, publicTransport.m_PathElementTime) * (float)dynamicBuffer.Length
                                + m_PathInformationData[request].m_Duration;
                            if (PathUtils.TryAppendPath(ref currentLane, navigationLanes, dynamicBuffer, appendPath))
                            {
                                cargoTransport.m_PathElementTime = num / (float)math.max(1, dynamicBuffer.Length);
                                publicTransport.m_PathElementTime = cargoTransport.m_PathElementTime;
                                target.m_Target = entity2;
                                VehicleUtils.ClearEndOfPath(ref currentLane, navigationLanes);
                                cargoTransport.m_State &= ~CargoTransportFlags.Arriving;
                                publicTransport.m_State &= ~PublicTransportFlags.Arriving;
                                return true;
                            }
                        }
                    }
                    VehicleUtils.SetTarget(ref pathOwner, ref target, entity2);
                    return true;
                }
                return false;
            }

            private void ReturnToDepot(
                int jobIndex,
                Entity vehicleEntity,
                CurrentRoute currentRoute,
                Owner ownerData,
                DynamicBuffer<ServiceDispatch> serviceDispatches,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                serviceDispatches.Clear();
                cargoTransport.m_RequestCount = 0;
                cargoTransport.m_State &= ~(CargoTransportFlags.EnRoute | CargoTransportFlags.Refueling | CargoTransportFlags.AbandonRoute);
                cargoTransport.m_State |= CargoTransportFlags.Returning;
                publicTransport.m_RequestCount = 0;
                publicTransport.m_State &= ~(PublicTransportFlags.EnRoute | PublicTransportFlags.Launched | PublicTransportFlags.Refueling | PublicTransportFlags.AbandonRoute);
                publicTransport.m_State |= PublicTransportFlags.Returning;
                m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
                VehicleUtils.SetTarget(ref pathOwner, ref target, ownerData.m_Owner);
            }

            private bool StartBoarding(
                int jobIndex,
                Entity vehicleEntity,
                CurrentRoute currentRoute,
                PrefabRef prefabRef,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Target target,
                bool isCargoVehicle
            )
            {
                if (m_ConnectedData.HasComponent(target.m_Target))
                {
                    Connected connected = m_ConnectedData[target.m_Target];
                    if (m_BoardingVehicleData.HasComponent(connected.m_Connected))
                    {
                        Entity transportStationFromStop = GetTransportStationFromStop(connected.m_Connected);
                        Entity nextStation = Entity.Null;
                        bool flag = false;
                        if (m_TransportStationData.HasComponent(transportStationFromStop))
                        {
                            _ = m_PrefabAircraftData[prefabRef.m_Prefab];
                            flag = (m_TransportStationData[transportStationFromStop].m_AircraftRefuelTypes & EnergyTypes.Fuel) != 0;
                        }
                        if (
                            (
                                !flag
                                && (
                                    (cargoTransport.m_State & CargoTransportFlags.RequiresMaintenance) != 0
                                    || (publicTransport.m_State & PublicTransportFlags.RequiresMaintenance) != 0
                                )
                            )
                            || (cargoTransport.m_State & CargoTransportFlags.AbandonRoute) != 0
                            || (publicTransport.m_State & PublicTransportFlags.AbandonRoute) != 0
                        )
                        {
                            cargoTransport.m_State &= ~(CargoTransportFlags.EnRoute | CargoTransportFlags.AbandonRoute);
                            publicTransport.m_State &= ~(PublicTransportFlags.EnRoute | PublicTransportFlags.AbandonRoute);
                            if (currentRoute.m_Route != Entity.Null)
                            {
                                m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
                            }
                        }
                        else
                        {
                            cargoTransport.m_State &= ~CargoTransportFlags.RequiresMaintenance;
                            publicTransport.m_State &= ~PublicTransportFlags.RequiresMaintenance;
                            cargoTransport.m_State |= CargoTransportFlags.EnRoute;
                            publicTransport.m_State |= PublicTransportFlags.EnRoute;
                            if (isCargoVehicle)
                            {
                                nextStation = GetNextStorageCompany(currentRoute.m_Route, target.m_Target);
                            }
                        }
                        cargoTransport.m_State |= CargoTransportFlags.RouteSource;
                        publicTransport.m_State |= PublicTransportFlags.RouteSource;
                        transportStationFromStop = Entity.Null;
                        if (isCargoVehicle)
                        {
                            transportStationFromStop = GetStorageCompanyFromStop(connected.m_Connected);
                        }
                        m_BoardingData.BeginBoarding(vehicleEntity, currentRoute.m_Route, connected.m_Connected, target.m_Target, transportStationFromStop, nextStation, flag);
                        return true;
                    }
                }
                if (m_WaypointData.HasComponent(target.m_Target))
                {
                    cargoTransport.m_State |= CargoTransportFlags.RouteSource;
                    publicTransport.m_State |= PublicTransportFlags.RouteSource;
                    return false;
                }
                cargoTransport.m_State &= ~(CargoTransportFlags.EnRoute | CargoTransportFlags.AbandonRoute);
                publicTransport.m_State &= ~(PublicTransportFlags.EnRoute | PublicTransportFlags.AbandonRoute);
                if (currentRoute.m_Route != Entity.Null)
                {
                    m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
                }
                return false;
            }

            private bool StopBoarding(
                Entity vehicleEntity,
                CurrentRoute currentRoute,
                DynamicBuffer<Passenger> passengers,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Target target,
                ref Odometer odometer,
                bool forcedStop
            )
            {
                bool flag = false;
                if (
                    m_ConnectedData.TryGetComponent(target.m_Target, out var componentData)
                    && m_BoardingVehicleData.TryGetComponent(componentData.m_Connected, out var componentData2)
                )
                {
                    flag = componentData2.m_Vehicle == vehicleEntity;
                }
                if (!forcedStop)
                {
                    uint num = math.max(cargoTransport.m_DepartureFrame, publicTransport.m_DepartureFrame);
                    bool flag2 = num != 0 && m_SimulationFrameIndex >= num + 1800;
                    publicTransport.m_MaxBoardingDistance = math.select(
                        publicTransport.m_MinWaitingDistance + 1f,
                        float.MaxValue,
                        (publicTransport.m_MinWaitingDistance == float.MaxValue || publicTransport.m_MinWaitingDistance == 0f) | flag2
                    );
                    publicTransport.m_MinWaitingDistance = float.MaxValue;
                    if (
                        flag
                        && (
                            m_SimulationFrameIndex < cargoTransport.m_DepartureFrame
                            || m_SimulationFrameIndex < publicTransport.m_DepartureFrame
                            || publicTransport.m_MaxBoardingDistance != float.MaxValue
                        )
                    )
                    {
                        return false;
                    }
                    if (!flag2 && passengers.IsCreated)
                    {
                        for (int i = 0; i < passengers.Length; i++)
                        {
                            Entity passenger = passengers[i].m_Passenger;
                            if (m_CurrentVehicleData.HasComponent(passenger) && (m_CurrentVehicleData[passenger].m_Flags & CreatureVehicleFlags.Ready) == 0)
                            {
                                return false;
                            }
                        }
                    }
                }
                if ((cargoTransport.m_State & CargoTransportFlags.Refueling) != 0 || (publicTransport.m_State & PublicTransportFlags.Refueling) != 0)
                {
                    odometer.m_Distance = 0f;
                }
                if (flag)
                {
                    Entity currentStation = Entity.Null;
                    Entity nextStation = Entity.Null;
                    if (!forcedStop && (cargoTransport.m_State & CargoTransportFlags.Boarding) != 0)
                    {
                        currentStation = GetStorageCompanyFromStop(componentData.m_Connected);
                        if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) != 0)
                        {
                            nextStation = GetNextStorageCompany(currentRoute.m_Route, target.m_Target);
                        }
                    }
                    m_BoardingData.EndBoarding(vehicleEntity, currentRoute.m_Route, componentData.m_Connected, target.m_Target, currentStation, nextStation);
                    return true;
                }
                cargoTransport.m_State &= ~(CargoTransportFlags.Boarding | CargoTransportFlags.Refueling);
                publicTransport.m_State &= ~(PublicTransportFlags.Boarding | PublicTransportFlags.Refueling);
                return true;
            }

            private bool IsValidBoarding(Entity vehicleEntity, ref Target target, out Connected connected)
            {
                if (m_ConnectedData.TryGetComponent(target.m_Target, out connected) && m_BoardingVehicleData.TryGetComponent(connected.m_Connected, out var componentData))
                {
                    return componentData.m_Vehicle == vehicleEntity;
                }
                return false;
            }

            private Entity GetTransportStationFromStop(Entity stop)
            {
                while (true)
                {
                    if (m_TransportStationData.HasComponent(stop))
                    {
                        if (m_OwnerData.HasComponent(stop))
                        {
                            Entity owner = m_OwnerData[stop].m_Owner;
                            if (m_TransportStationData.HasComponent(owner))
                            {
                                return owner;
                            }
                        }
                        return stop;
                    }
                    if (!m_OwnerData.HasComponent(stop))
                    {
                        break;
                    }
                    stop = m_OwnerData[stop].m_Owner;
                }
                return Entity.Null;
            }

            private Entity GetStorageCompanyFromStop(Entity stop)
            {
                while (true)
                {
                    if (m_StorageCompanyData.HasComponent(stop))
                    {
                        return stop;
                    }
                    if (!m_OwnerData.HasComponent(stop))
                    {
                        break;
                    }
                    stop = m_OwnerData[stop].m_Owner;
                }
                return Entity.Null;
            }

            private Entity GetNextStorageCompany(Entity route, Entity currentWaypoint)
            {
                DynamicBuffer<RouteWaypoint> dynamicBuffer = m_RouteWaypoints[route];
                int num = m_WaypointData[currentWaypoint].m_Index + 1;
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    num = math.select(num, 0, num >= dynamicBuffer.Length);
                    Entity waypoint = dynamicBuffer[num].m_Waypoint;
                    if (m_ConnectedData.HasComponent(waypoint))
                    {
                        Entity connected = m_ConnectedData[waypoint].m_Connected;
                        Entity storageCompanyFromStop = GetStorageCompanyFromStop(connected);
                        if (storageCompanyFromStop != Entity.Null)
                        {
                            return storageCompanyFromStop;
                        }
                    }
                    num++;
                }
                return Entity.Null;
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        private EndFrameBarrier m_EndFrameBarrier;

        private SimulationSystem m_SimulationSystem;

        private PathfindSetupSystem m_PathfindSetupSystem;

        private CityStatisticsSystem m_CityStatisticsSystem;

        private AchievementTriggerSystem m_AchievementTriggerSystem;

        private TransportUsageTrackSystem m_TransportUsageTrackSystem;

        private EntityQuery m_VehicleQuery;

        private EntityArchetype m_TransportVehicleRequestArchetype;

        private EntityArchetype m_HandleRequestArchetype;

        private TransportBoardingHelpers.BoardingLookupData m_BoardingLookupData;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 16;
        }

        public override int GetUpdateOffset(SystemUpdatePhase phase)
        {
            return 10;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            m_AchievementTriggerSystem = World.GetOrCreateSystemManaged<AchievementTriggerSystem>();
            m_TransportUsageTrackSystem = World.GetOrCreateSystemManaged<TransportUsageTrackSystem>();
            m_BoardingLookupData = new TransportBoardingHelpers.BoardingLookupData(this);
            m_VehicleQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[5]
                    {
                        ComponentType.ReadWrite<AircraftCurrentLane>(),
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadWrite<PathOwner>(),
                        ComponentType.ReadWrite<Target>(),
                    },
                    Any = new ComponentType[2] { ComponentType.ReadWrite<Game.Vehicles.CargoTransport>(), ComponentType.ReadWrite<Game.Vehicles.PublicTransport>() },
                    None = new ComponentType[5]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<TripSource>(),
                        ComponentType.ReadOnly<OutOfControl>(),
                        ComponentType.ReadOnly<Produced>(),
                    },
                }
            );
            m_TransportVehicleRequestArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<ServiceRequest>(),
                ComponentType.ReadWrite<TransportVehicleRequest>(),
                ComponentType.ReadWrite<RequestGroup>()
            );
            m_HandleRequestArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<HandleRequest>(), ComponentType.ReadWrite<Event>());
            RequireForUpdate(m_VehicleQuery);
        }

        protected override void OnUpdate()
        {
            TransportBoardingHelpers.BoardingData boardingData = new TransportBoardingHelpers.BoardingData(Allocator.TempJob);
            m_BoardingLookupData.Update(this);
            JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(
                new TransportAircraftTickJob
                {
                    m_EntityType = SystemAPI.GetEntityTypeHandle(),
                    m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(true),
                    m_UnspawnedType = SystemAPI.GetComponentTypeHandle<Unspawned>(true),
                    m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                    m_CurrentRouteType = SystemAPI.GetComponentTypeHandle<CurrentRoute>(true),
                    m_StoppedType = SystemAPI.GetComponentTypeHandle<Stopped>(true),
                    m_PassengerType = SystemAPI.GetBufferTypeHandle<Passenger>(true),
                    m_CargoTransportType = SystemAPI.GetComponentTypeHandle<Game.Vehicles.CargoTransport>(false),
                    m_PublicTransportType = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PublicTransport>(false),
                    m_AircraftType = SystemAPI.GetComponentTypeHandle<Aircraft>(false),
                    m_CurrentLaneType = SystemAPI.GetComponentTypeHandle<AircraftCurrentLane>(false),
                    m_TargetType = SystemAPI.GetComponentTypeHandle<Target>(false),
                    m_PathOwnerType = SystemAPI.GetComponentTypeHandle<PathOwner>(false),
                    m_OdometerType = SystemAPI.GetComponentTypeHandle<Odometer>(false),
                    m_AircraftNavigationLaneType = SystemAPI.GetBufferTypeHandle<AircraftNavigationLane>(false),
                    m_ServiceDispatchType = SystemAPI.GetBufferTypeHandle<ServiceDispatch>(false),
                    m_OwnerData = SystemAPI.GetComponentLookup<Owner>(true),
                    m_PathInformationData = SystemAPI.GetComponentLookup<PathInformation>(true),
                    m_TransportVehicleRequestData = SystemAPI.GetComponentLookup<TransportVehicleRequest>(true),
                    m_PrefabAircraftData = SystemAPI.GetComponentLookup<AircraftData>(true),
                    m_PrefabHelicopterData = SystemAPI.GetComponentLookup<HelicopterData>(true),
                    m_PrefabAirplaneData = SystemAPI.GetComponentLookup<AirplaneData>(true),
                    m_PublicTransportVehicleData = SystemAPI.GetComponentLookup<PublicTransportVehicleData>(true),
                    m_CargoTransportVehicleData = SystemAPI.GetComponentLookup<CargoTransportVehicleData>(true),
                    m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                    m_WaypointData = SystemAPI.GetComponentLookup<Waypoint>(true),
                    m_ConnectedData = SystemAPI.GetComponentLookup<Connected>(true),
                    m_CustomWaypointLookup = SystemAPI.GetComponentLookup<CustomWaypoint>(false),
                    m_BoardingVehicleData = SystemAPI.GetComponentLookup<BoardingVehicle>(true),
                    m_RouteLaneData = SystemAPI.GetComponentLookup<RouteLane>(true),
                    m_RouteColorData = SystemAPI.GetComponentLookup<Game.Routes.Color>(true),
                    m_StorageCompanyData = SystemAPI.GetComponentLookup<Game.Companies.StorageCompany>(true),
                    m_TransportStationData = SystemAPI.GetComponentLookup<Game.Buildings.TransportStation>(true),
                    m_LaneData = SystemAPI.GetComponentLookup<Lane>(true),
                    m_CurrentVehicleData = SystemAPI.GetComponentLookup<CurrentVehicle>(true),
                    m_RouteWaypoints = SystemAPI.GetBufferLookup<RouteWaypoint>(true),
                    m_SubLanes = SystemAPI.GetBufferLookup<Game.Net.SubLane>(true),
                    m_PathElements = SystemAPI.GetBufferLookup<PathElement>(false),
                    m_LoadingResources = SystemAPI.GetBufferLookup<LoadingResources>(false),
                    m_SimulationFrameIndex = m_SimulationSystem.frameIndex,
                    m_TransportVehicleRequestArchetype = m_TransportVehicleRequestArchetype,
                    m_HandleRequestArchetype = m_HandleRequestArchetype,
                    m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                    m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
                    m_BoardingData = boardingData.ToConcurrent(),
                },
                m_VehicleQuery,
                Dependency
            );
            JobHandle jobHandle2 = boardingData.ScheduleBoarding(
                this,
                m_CityStatisticsSystem,
                m_TransportUsageTrackSystem,
                m_AchievementTriggerSystem,
                m_BoardingLookupData,
                m_SimulationSystem.frameIndex,
                jobHandle
            );
            boardingData.Dispose(jobHandle2);
            m_PathfindSetupSystem.AddQueueWriter(jobHandle);
            m_EndFrameBarrier.AddJobHandleForProducer(jobHandle);
            Dependency = jobHandle2;
        }
    }
}
