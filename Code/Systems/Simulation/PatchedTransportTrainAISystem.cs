using BoardingController.Components;
using BoardingController.Utils;
using Game;
using Game.Achievements;
using Game.Buildings;
using Game.City;
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
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace BoardingController.Systems.Simulation
{
    public partial class PatchedTransportTrainAISystem : GameSystemBase
    {
        [BurstCompile]
        private struct TransportTrainTickJob : IJobChunk
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

            public ComponentTypeHandle<Game.Vehicles.CargoTransport> m_CargoTransportType;

            public ComponentTypeHandle<Game.Vehicles.PublicTransport> m_PublicTransportType;

            public ComponentTypeHandle<Target> m_TargetType;

            public ComponentTypeHandle<PathOwner> m_PathOwnerType;

            public ComponentTypeHandle<Odometer> m_OdometerType;

            public BufferTypeHandle<LayoutElement> m_LayoutElementType;

            public BufferTypeHandle<TrainNavigationLane> m_NavigationLaneType;

            public BufferTypeHandle<ServiceDispatch> m_ServiceDispatchType;

            [ReadOnly]
            public EntityStorageInfoLookup m_EntityLookup;

            [ReadOnly]
            public ComponentLookup<Transform> m_TransformData;

            [ReadOnly]
            public ComponentLookup<Game.Objects.SpawnLocation> m_SpawnLocationData;

            [ReadOnly]
            public ComponentLookup<Owner> m_OwnerData;

            [ReadOnly]
            public ComponentLookup<PathInformation> m_PathInformationData;

            [ReadOnly]
            public ComponentLookup<TransportVehicleRequest> m_TransportVehicleRequestData;

            [ReadOnly]
            public ComponentLookup<ParkedTrain> m_ParkedTrainData;

            [ReadOnly]
            public ComponentLookup<Controller> m_ControllerData;

            [ReadOnly]
            public ComponentLookup<Curve> m_CurveData;

            [ReadOnly]
            public ComponentLookup<Lane> m_LaneData;

            [ReadOnly]
            public ComponentLookup<EdgeLane> m_EdgeLaneData;

            [ReadOnly]
            public ComponentLookup<Game.Net.Edge> m_EdgeData;

            [ReadOnly]
            public ComponentLookup<TrainData> m_PrefabTrainData;

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
            public ComponentLookup<Game.Routes.Color> m_RouteColorData;

            [ReadOnly]
            public ComponentLookup<Game.Companies.StorageCompany> m_StorageCompanyData;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.TransportStation> m_TransportStationData;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.TransportDepot> m_TransportDepotData;

            [ReadOnly]
            public ComponentLookup<CurrentVehicle> m_CurrentVehicleData;

            [ReadOnly]
            public BufferLookup<Passenger> m_Passengers;

            [ReadOnly]
            public BufferLookup<Resources> m_EconomyResources;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> m_RouteWaypoints;

            [ReadOnly]
            public BufferLookup<ConnectedEdge> m_ConnectedEdges;

            [ReadOnly]
            public BufferLookup<Game.Net.SubLane> m_SubLanes;

            [NativeDisableContainerSafetyRestriction]
            [ReadOnly]
            public BufferLookup<LayoutElement> m_LayoutElements;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Train> m_TrainData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<TrainCurrentLane> m_CurrentLaneData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<TrainNavigation> m_NavigationData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Blocker> m_BlockerData;

            [NativeDisableParallelForRestriction]
            public BufferLookup<PathElement> m_PathElements;

            [NativeDisableParallelForRestriction]
            public BufferLookup<LoadingResources> m_LoadingResources;

            [ReadOnly]
            public uint m_SimulationFrameIndex;

            [ReadOnly]
            public RandomSeed m_RandomSeed;

            [ReadOnly]
            public EntityArchetype m_TransportVehicleRequestArchetype;

            [ReadOnly]
            public EntityArchetype m_HandleRequestArchetype;

            [ReadOnly]
            public ComponentTypeSet m_MovingToParkedTrainRemoveTypes;

            [ReadOnly]
            public ComponentTypeSet m_MovingToParkedTrainAddTypes;

            [ReadOnly]
            public TransportTrainCarriageSelectData m_TransportTrainCarriageSelectData;

            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

            public TransportBoardingHelpers.BoardingData.Concurrent m_BoardingData;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
                NativeArray<Owner> nativeArray2 = chunk.GetNativeArray(ref m_OwnerType);
                NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray(ref m_PrefabRefType);
                NativeArray<CurrentRoute> nativeArray4 = chunk.GetNativeArray(ref m_CurrentRouteType);
                NativeArray<Game.Vehicles.CargoTransport> nativeArray5 = chunk.GetNativeArray(ref m_CargoTransportType);
                NativeArray<Game.Vehicles.PublicTransport> nativeArray6 = chunk.GetNativeArray(ref m_PublicTransportType);
                NativeArray<Target> nativeArray7 = chunk.GetNativeArray(ref m_TargetType);
                NativeArray<PathOwner> nativeArray8 = chunk.GetNativeArray(ref m_PathOwnerType);
                NativeArray<Odometer> nativeArray9 = chunk.GetNativeArray(ref m_OdometerType);
                BufferAccessor<LayoutElement> bufferAccessor = chunk.GetBufferAccessor(ref m_LayoutElementType);
                BufferAccessor<TrainNavigationLane> bufferAccessor2 = chunk.GetBufferAccessor(ref m_NavigationLaneType);
                BufferAccessor<ServiceDispatch> bufferAccessor3 = chunk.GetBufferAccessor(ref m_ServiceDispatchType);
                Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
                bool isUnspawned = chunk.Has(ref m_UnspawnedType);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    Entity vehicleEntity = nativeArray[i];
                    Owner owner = nativeArray2[i];
                    PrefabRef prefabRef = nativeArray3[i];
                    PathOwner pathOwner = nativeArray8[i];
                    Target target = nativeArray7[i];
                    Odometer odometer = nativeArray9[i];
                    DynamicBuffer<LayoutElement> layout = bufferAccessor[i];
                    DynamicBuffer<TrainNavigationLane> navigationLanes = bufferAccessor2[i];
                    DynamicBuffer<ServiceDispatch> serviceDispatches = bufferAccessor3[i];
                    CurrentRoute currentRoute = default;
                    if (nativeArray4.Length != 0)
                    {
                        currentRoute = nativeArray4[i];
                    }
                    Game.Vehicles.CargoTransport cargoTransport = default;
                    if (nativeArray5.Length != 0)
                    {
                        cargoTransport = nativeArray5[i];
                    }
                    Game.Vehicles.PublicTransport publicTransport = default;
                    if (nativeArray6.Length != 0)
                    {
                        publicTransport = nativeArray6[i];
                    }
                    Tick(
                        unfilteredChunkIndex,
                        ref random,
                        vehicleEntity,
                        owner,
                        prefabRef,
                        currentRoute,
                        layout,
                        navigationLanes,
                        serviceDispatches,
                        isUnspawned,
                        ref cargoTransport,
                        ref publicTransport,
                        ref pathOwner,
                        ref target,
                        ref odometer
                    );
                    nativeArray8[i] = pathOwner;
                    nativeArray7[i] = target;
                    nativeArray9[i] = odometer;
                    if (nativeArray5.Length != 0)
                    {
                        nativeArray5[i] = cargoTransport;
                    }
                    if (nativeArray6.Length != 0)
                    {
                        nativeArray6[i] = publicTransport;
                    }
                }
            }

            private void Tick(
                int jobIndex,
                ref Random random,
                Entity vehicleEntity,
                Owner owner,
                PrefabRef prefabRef,
                CurrentRoute currentRoute,
                DynamicBuffer<LayoutElement> layout,
                DynamicBuffer<TrainNavigationLane> navigationLanes,
                DynamicBuffer<ServiceDispatch> serviceDispatches,
                bool isUnspawned,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref PathOwner pathOwner,
                ref Target target,
                ref Odometer odometer
            )
            {
                if (VehicleUtils.ResetUpdatedPath(ref pathOwner))
                {
                    if (
                        ((cargoTransport.m_State & CargoTransportFlags.DummyTraffic) != 0 || (publicTransport.m_State & PublicTransportFlags.DummyTraffic) != 0)
                        && m_LoadingResources.TryGetBuffer(vehicleEntity, out var bufferData)
                    )
                    {
                        if (bufferData.Length != 0)
                        {
                            QuantityUpdated(jobIndex, vehicleEntity, layout);
                        }
                        if (CheckLoadingResources(jobIndex, ref random, vehicleEntity, dummyTraffic: true, layout, bufferData))
                        {
                            pathOwner.m_State |= PathFlags.Updated;
                            return;
                        }
                    }
                    cargoTransport.m_State &= ~CargoTransportFlags.Arriving;
                    publicTransport.m_State &= ~PublicTransportFlags.Arriving;
                    DynamicBuffer<PathElement> path = m_PathElements[vehicleEntity];
                    float num = VehicleUtils.CalculateLength(vehicleEntity, layout, ref m_PrefabRefData, ref m_PrefabTrainData);
                    PathElement prevElement = default;
                    if ((pathOwner.m_State & PathFlags.Append) != 0)
                    {
                        if (navigationLanes.Length != 0)
                        {
                            TrainNavigationLane trainNavigationLane = navigationLanes[navigationLanes.Length - 1];
                            prevElement = new PathElement(trainNavigationLane.m_Lane, trainNavigationLane.m_CurvePosition);
                        }
                    }
                    else if (VehicleUtils.IsReversedPath(path, pathOwner, vehicleEntity, layout, m_CurveData, m_CurrentLaneData, m_TrainData, m_TransformData))
                    {
                        VehicleUtils.ReverseTrain(vehicleEntity, layout, ref m_TrainData, ref m_CurrentLaneData, ref m_NavigationData);
                    }
                    PathUtils.ExtendReverseLocations(
                        prevElement,
                        path,
                        pathOwner,
                        num,
                        m_CurveData,
                        m_LaneData,
                        m_EdgeLaneData,
                        m_OwnerData,
                        m_EdgeData,
                        m_ConnectedEdges,
                        m_SubLanes
                    );
                    if (
                        !m_WaypointData.HasComponent(target.m_Target)
                        || (m_ConnectedData.HasComponent(target.m_Target) && m_BoardingVehicleData.HasComponent(m_ConnectedData[target.m_Target].m_Connected))
                    )
                    {
                        float distance = num * 0.5f;
                        PathUtils.ExtendPath(
                            path,
                            pathOwner,
                            ref distance,
                            ref m_CurveData,
                            ref m_LaneData,
                            ref m_EdgeLaneData,
                            ref m_OwnerData,
                            ref m_EdgeData,
                            ref m_ConnectedEdges,
                            ref m_SubLanes
                        );
                    }
                    UpdatePantograph(layout);
                }
                Entity entity = vehicleEntity;
                if (layout.Length != 0)
                {
                    entity = layout[0].m_Vehicle;
                }
                Train train = m_TrainData[entity];
                TrainCurrentLane currentLane = m_CurrentLaneData[entity];
                VehicleUtils.CheckUnspawned(jobIndex, vehicleEntity, currentLane, isUnspawned, m_CommandBuffer);
                bool num2 = (cargoTransport.m_State & CargoTransportFlags.EnRoute) == 0 && (publicTransport.m_State & PublicTransportFlags.EnRoute) == 0;
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
                if (num2)
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
                bool flag = false;
                if (VehicleUtils.IsStuck(pathOwner))
                {
                    Blocker blocker = m_BlockerData[vehicleEntity];
                    bool flag2 = m_ParkedTrainData.HasComponent(blocker.m_Blocker);
                    if (flag2)
                    {
                        Entity entity2 = blocker.m_Blocker;
                        if (m_ControllerData.TryGetComponent(entity2, out var componentData))
                        {
                            entity2 = componentData.m_Controller;
                        }
                        m_LayoutElements.TryGetBuffer(entity2, out var bufferData2);
                        VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, entity2, bufferData2);
                    }
                    if (flag2 || blocker.m_Blocker == Entity.Null)
                    {
                        pathOwner.m_State &= ~PathFlags.Stuck;
                        m_BlockerData[vehicleEntity] = default;
                    }
                }
                if (!m_EntityLookup.Exists(target.m_Target) || VehicleUtils.PathfindFailed(pathOwner))
                {
                    if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                    {
                        flag = true;
                        StopBoarding(
                            jobIndex,
                            ref random,
                            vehicleEntity,
                            currentRoute,
                            layout,
                            ref cargoTransport,
                            ref publicTransport,
                            ref target,
                            ref odometer,
                            isCargoVehicle,
                            forcedStop: true
                        );
                    }
                    if (
                        VehicleUtils.IsStuck(pathOwner)
                        || (cargoTransport.m_State & (CargoTransportFlags.Returning | CargoTransportFlags.DummyTraffic)) != 0
                        || (publicTransport.m_State & (PublicTransportFlags.Returning | PublicTransportFlags.DummyTraffic)) != 0
                    )
                    {
                        VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, vehicleEntity, layout);
                        m_TrainData[entity] = train;
                        m_CurrentLaneData[entity] = currentLane;
                        return;
                    }
                    ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches, ref cargoTransport, ref publicTransport, ref train, ref pathOwner, ref target);
                }
                else if (VehicleUtils.PathEndReached(currentLane))
                {
                    if (
                        (cargoTransport.m_State & (CargoTransportFlags.Returning | CargoTransportFlags.DummyTraffic)) != 0
                        || (publicTransport.m_State & (PublicTransportFlags.Returning | PublicTransportFlags.DummyTraffic)) != 0
                    )
                    {
                        if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                        {
                            if (
                                StopBoarding(
                                    jobIndex,
                                    ref random,
                                    vehicleEntity,
                                    currentRoute,
                                    layout,
                                    ref cargoTransport,
                                    ref publicTransport,
                                    ref target,
                                    ref odometer,
                                    isCargoVehicle,
                                    forcedStop: false
                                )
                            )
                            {
                                flag = true;
                                if (
                                    !SelectNextDispatch(
                                        jobIndex,
                                        vehicleEntity,
                                        currentRoute,
                                        layout,
                                        navigationLanes,
                                        serviceDispatches,
                                        ref cargoTransport,
                                        ref publicTransport,
                                        ref train,
                                        ref currentLane,
                                        ref pathOwner,
                                        ref target
                                    )
                                )
                                {
                                    if (!TryParkTrain(jobIndex, vehicleEntity, owner, layout, navigationLanes, ref train, ref cargoTransport, ref publicTransport, ref currentLane))
                                    {
                                        VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, vehicleEntity, layout);
                                    }
                                    m_TrainData[entity] = train;
                                    m_CurrentLaneData[entity] = currentLane;
                                    return;
                                }
                            }
                        }
                        else if (
                            (
                                CountPassengers(vehicleEntity, layout) <= 0
                                || !StartBoarding(jobIndex, vehicleEntity, currentRoute, prefabRef, ref cargoTransport, ref publicTransport, ref target, isCargoVehicle)
                            )
                            && !SelectNextDispatch(
                                jobIndex,
                                vehicleEntity,
                                currentRoute,
                                layout,
                                navigationLanes,
                                serviceDispatches,
                                ref cargoTransport,
                                ref publicTransport,
                                ref train,
                                ref currentLane,
                                ref pathOwner,
                                ref target
                            )
                        )
                        {
                            if (!TryParkTrain(jobIndex, vehicleEntity, owner, layout, navigationLanes, ref train, ref cargoTransport, ref publicTransport, ref currentLane))
                            {
                                VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, vehicleEntity, layout);
                            }
                            m_TrainData[entity] = train;
                            m_CurrentLaneData[entity] = currentLane;
                            return;
                        }
                    }
                    else if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                    {
                        if (
                            StopBoarding(
                                jobIndex,
                                ref random,
                                vehicleEntity,
                                currentRoute,
                                layout,
                                ref cargoTransport,
                                ref publicTransport,
                                ref target,
                                ref odometer,
                                isCargoVehicle,
                                forcedStop: false
                            )
                        )
                        {
                            flag = true;
                            if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == 0 && (publicTransport.m_State & PublicTransportFlags.EnRoute) == 0)
                            {
                                ReturnToDepot(
                                    jobIndex,
                                    vehicleEntity,
                                    currentRoute,
                                    owner,
                                    serviceDispatches,
                                    ref cargoTransport,
                                    ref publicTransport,
                                    ref train,
                                    ref pathOwner,
                                    ref target
                                );
                            }
                            else
                            {
                                SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                            }
                        }
                    }
                    else if (!m_RouteWaypoints.HasBuffer(currentRoute.m_Route) || !m_WaypointData.HasComponent(target.m_Target))
                    {
                        ReturnToDepot(
                            jobIndex,
                            vehicleEntity,
                            currentRoute,
                            owner,
                            serviceDispatches,
                            ref cargoTransport,
                            ref publicTransport,
                            ref train,
                            ref pathOwner,
                            ref target
                        );
                    }
                    else if (!StartBoarding(jobIndex, vehicleEntity, currentRoute, prefabRef, ref cargoTransport, ref publicTransport, ref target, isCargoVehicle))
                    {
                        if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == 0 && (publicTransport.m_State & PublicTransportFlags.EnRoute) == 0)
                        {
                            ReturnToDepot(
                                jobIndex,
                                vehicleEntity,
                                currentRoute,
                                owner,
                                serviceDispatches,
                                ref cargoTransport,
                                ref publicTransport,
                                ref train,
                                ref pathOwner,
                                ref target
                            );
                        }
                        else
                        {
                            SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                        }
                    }
                }
                else if (VehicleUtils.ReturnEndReached(currentLane))
                {
                    m_TrainData[entity] = train;
                    m_CurrentLaneData[entity] = currentLane;
                    VehicleUtils.ReverseTrain(vehicleEntity, layout, ref m_TrainData, ref m_CurrentLaneData, ref m_NavigationData);
                    UpdatePantograph(layout);
                    entity = vehicleEntity;
                    if (layout.Length != 0)
                    {
                        entity = layout[0].m_Vehicle;
                    }
                    train = m_TrainData[entity];
                    currentLane = m_CurrentLaneData[entity];
                }
                else if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                {
                    flag = true;
                    StopBoarding(
                        jobIndex,
                        ref random,
                        vehicleEntity,
                        currentRoute,
                        layout,
                        ref cargoTransport,
                        ref publicTransport,
                        ref target,
                        ref odometer,
                        isCargoVehicle,
                        forcedStop: true
                    );
                }
                train.m_Flags &= ~(Game.Vehicles.TrainFlags.BoardingLeft | Game.Vehicles.TrainFlags.BoardingRight);
                publicTransport.m_State &= ~(PublicTransportFlags.StopLeft | PublicTransportFlags.StopRight);
                Entity skipWaypoint = Entity.Null;
                if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != 0 || (publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                {
                    if (!flag)
                    {
                        Train controllerTrain = m_TrainData[vehicleEntity];
                        UpdateStop(entity, controllerTrain, isBoarding: true, ref train, ref publicTransport, ref target);
                    }
                }
                else if ((cargoTransport.m_State & CargoTransportFlags.Returning) != 0 || (publicTransport.m_State & PublicTransportFlags.Returning) != 0)
                {
                    if (CountPassengers(vehicleEntity, layout) == 0)
                    {
                        SelectNextDispatch(
                            jobIndex,
                            vehicleEntity,
                            currentRoute,
                            layout,
                            navigationLanes,
                            serviceDispatches,
                            ref cargoTransport,
                            ref publicTransport,
                            ref train,
                            ref currentLane,
                            ref pathOwner,
                            ref target
                        );
                    }
                }
                else if ((cargoTransport.m_State & CargoTransportFlags.Arriving) != 0 || (publicTransport.m_State & PublicTransportFlags.Arriving) != 0)
                {
                    Train controllerTrain2 = m_TrainData[vehicleEntity];
                    UpdateStop(entity, controllerTrain2, isBoarding: false, ref train, ref publicTransport, ref target);
                }
                else
                {
                    CheckNavigationLanes(currentRoute, navigationLanes, ref cargoTransport, ref publicTransport, ref currentLane, ref pathOwner, ref target, out skipWaypoint);
                }
                if (((cargoTransport.m_State & CargoTransportFlags.Boarding) == 0 && (publicTransport.m_State & PublicTransportFlags.Boarding) == 0) | flag)
                {
                    if (VehicleUtils.RequireNewPath(pathOwner))
                    {
                        FindNewPath(vehicleEntity, prefabRef, skipWaypoint, ref currentLane, ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                    }
                    else if ((pathOwner.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Stuck)) == 0)
                    {
                        CheckParkingSpace(navigationLanes, ref train, ref pathOwner);
                    }
                }
                m_TrainData[entity] = train;
                m_CurrentLaneData[entity] = currentLane;
            }

            private void CheckParkingSpace(DynamicBuffer<TrainNavigationLane> navigationLanes, ref Train train, ref PathOwner pathOwner)
            {
                if (navigationLanes.Length == 0)
                {
                    return;
                }
                TrainNavigationLane trainNavigationLane = navigationLanes[navigationLanes.Length - 1];
                if ((trainNavigationLane.m_Flags & TrainLaneFlags.ParkingSpace) == 0 || !m_SpawnLocationData.TryGetComponent(trainNavigationLane.m_Lane, out var componentData))
                {
                    return;
                }
                if ((componentData.m_Flags & SpawnLocationFlags.ParkedVehicle) != 0)
                {
                    if ((train.m_Flags & Game.Vehicles.TrainFlags.IgnoreParkedVehicle) == 0)
                    {
                        train.m_Flags |= Game.Vehicles.TrainFlags.IgnoreParkedVehicle;
                        pathOwner.m_State |= PathFlags.Obsolete;
                    }
                }
                else
                {
                    train.m_Flags &= ~Game.Vehicles.TrainFlags.IgnoreParkedVehicle;
                }
            }

            private bool TryParkTrain(
                int jobIndex,
                Entity entity,
                Owner owner,
                DynamicBuffer<LayoutElement> layout,
                DynamicBuffer<TrainNavigationLane> navigationLanes,
                ref Train train,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref TrainCurrentLane currentLane
            )
            {
                if (navigationLanes.Length == 0)
                {
                    return false;
                }
                TrainNavigationLane trainNavigationLane = navigationLanes[navigationLanes.Length - 1];
                if ((trainNavigationLane.m_Flags & TrainLaneFlags.ParkingSpace) == 0)
                {
                    return false;
                }
                train.m_Flags &= ~(
                    Game.Vehicles.TrainFlags.BoardingLeft
                    | Game.Vehicles.TrainFlags.BoardingRight
                    | Game.Vehicles.TrainFlags.Pantograph
                    | Game.Vehicles.TrainFlags.IgnoreParkedVehicle
                );
                cargoTransport.m_State &= CargoTransportFlags.RequiresMaintenance;
                publicTransport.m_State &= PublicTransportFlags.RequiresMaintenance;
                if (m_TransportDepotData.TryGetComponent(owner.m_Owner, out var componentData) && (componentData.m_Flags & TransportDepotFlags.HasAvailableVehicles) == 0)
                {
                    cargoTransport.m_State |= CargoTransportFlags.Disabled;
                    publicTransport.m_State |= PublicTransportFlags.Disabled;
                }
                for (int i = 0; i < layout.Length; i++)
                {
                    Entity vehicle = layout[i].m_Vehicle;
                    m_CommandBuffer.RemoveComponent(jobIndex, vehicle, in m_MovingToParkedTrainRemoveTypes);
                    m_CommandBuffer.AddComponent(jobIndex, vehicle, in m_MovingToParkedTrainAddTypes);
                    if (i == 0)
                    {
                        m_CommandBuffer.SetComponent(jobIndex, vehicle, new ParkedTrain(trainNavigationLane.m_Lane, currentLane));
                        continue;
                    }
                    TrainCurrentLane currentLane2 = m_CurrentLaneData[vehicle];
                    m_CommandBuffer.SetComponent(jobIndex, vehicle, new ParkedTrain(trainNavigationLane.m_Lane, currentLane2));
                }
                if (m_SpawnLocationData.HasComponent(trainNavigationLane.m_Lane))
                {
                    m_CommandBuffer.AddComponent<PathfindUpdated>(jobIndex, trainNavigationLane.m_Lane);
                }
                else
                {
                    m_CommandBuffer.AddComponent(jobIndex, entity, new FixParkingLocation(Entity.Null, entity));
                }
                return true;
            }

            private void UpdatePantograph(DynamicBuffer<LayoutElement> layout)
            {
                bool flag = false;
                for (int i = 0; i < layout.Length; i++)
                {
                    Entity vehicle = layout[i].m_Vehicle;
                    Train value = m_TrainData[vehicle];
                    PrefabRef prefabRef = m_PrefabRefData[vehicle];
                    TrainData trainData = m_PrefabTrainData[prefabRef.m_Prefab];
                    if (flag || (trainData.m_TrainFlags & Game.Prefabs.TrainFlags.Pantograph) == 0)
                    {
                        value.m_Flags &= ~Game.Vehicles.TrainFlags.Pantograph;
                    }
                    else
                    {
                        value.m_Flags |= Game.Vehicles.TrainFlags.Pantograph;
                        flag = (trainData.m_TrainFlags & Game.Prefabs.TrainFlags.MultiUnit) != 0;
                    }
                    m_TrainData[vehicle] = value;
                }
            }

            private void UpdateStop(
                Entity vehicleEntity,
                Train controllerTrain,
                bool isBoarding,
                ref Train train,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Target target
            )
            {
                Transform transform = m_TransformData[vehicleEntity];
                if (!m_ConnectedData.TryGetComponent(target.m_Target, out var componentData) || !m_TransformData.TryGetComponent(componentData.m_Connected, out var componentData2))
                {
                    return;
                }
                bool flag = math.dot(math.mul(transform.m_Rotation, math.right()), componentData2.m_Position - transform.m_Position) < 0f;
                if (isBoarding)
                {
                    if (flag)
                    {
                        train.m_Flags |= Game.Vehicles.TrainFlags.BoardingLeft;
                    }
                    else
                    {
                        train.m_Flags |= Game.Vehicles.TrainFlags.BoardingRight;
                    }
                }
                if (flag ^ (((controllerTrain.m_Flags ^ train.m_Flags) & Game.Vehicles.TrainFlags.Reversed) != 0))
                {
                    publicTransport.m_State |= PublicTransportFlags.StopLeft;
                }
                else
                {
                    publicTransport.m_State |= PublicTransportFlags.StopRight;
                }
            }

            private void FindNewPath(
                Entity vehicleEntity,
                PrefabRef prefabRef,
                Entity skipWaypoint,
                ref TrainCurrentLane currentLane,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                TrainData trainData = m_PrefabTrainData[prefabRef.m_Prefab];
                PathfindParameters parameters = new PathfindParameters
                {
                    m_MaxSpeed = trainData.m_MaxSpeed,
                    m_WalkSpeed = 5.555556f,
                    m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                    m_Methods = PathMethod.Track,
                    m_IgnoredRules = (
                        RuleFlags.ForbidCombustionEngines | RuleFlags.ForbidHeavyTraffic | RuleFlags.ForbidPrivateTraffic | RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles
                    ),
                };
                SetupQueueTarget origin = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Track,
                    m_TrackTypes = trainData.m_TrackType,
                };
                SetupQueueTarget destination = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Track,
                    m_TrackTypes = trainData.m_TrackType,
                    m_Entity = target.m_Target,
                };
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
                    (cargoTransport.m_State & (CargoTransportFlags.EnRoute | CargoTransportFlags.RouteSource)) == (CargoTransportFlags.EnRoute | CargoTransportFlags.RouteSource)
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
                if ((cargoTransport.m_State & CargoTransportFlags.Returning) != 0 || (publicTransport.m_State & PublicTransportFlags.Returning) != 0)
                {
                    destination.m_RandomCost = 30f;
                }
                VehicleUtils.SetupPathfind(
                    item: new SetupQueueItem(vehicleEntity, parameters, origin, destination),
                    currentLane: ref currentLane,
                    pathOwner: ref pathOwner,
                    queue: m_PathfindQueue
                );
            }

            private void CheckNavigationLanes(
                CurrentRoute currentRoute,
                DynamicBuffer<TrainNavigationLane> navigationLanes,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref TrainCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                out Entity skipWaypoint
            )
            {
                skipWaypoint = Entity.Null;
                if (navigationLanes.Length == 0 || navigationLanes.Length >= 10)
                {
                    return;
                }
                TrainNavigationLane value = navigationLanes[navigationLanes.Length - 1];
                if ((value.m_Flags & TrainLaneFlags.EndOfPath) == 0)
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
                        value.m_Flags &= ~TrainLaneFlags.EndOfPath;
                        navigationLanes[navigationLanes.Length - 1] = value;
                        cargoTransport.m_State |= CargoTransportFlags.RouteSource;
                        publicTransport.m_State |= PublicTransportFlags.RouteSource;
                    }
                }
                else
                {
                    cargoTransport.m_State |= CargoTransportFlags.Arriving;
                    publicTransport.m_State |= PublicTransportFlags.Arriving;
                }
            }

            private void SetNextWaypointTarget(CurrentRoute currentRoute, ref PathOwner pathOwnerData, ref Target targetData)
            {
                DynamicBuffer<RouteWaypoint> dynamicBuffer = m_RouteWaypoints[currentRoute.m_Route];
                VehicleUtils.SetTarget(
                    ref pathOwnerData,
                    ref targetData,
                    dynamicBuffer[WaypointUtils.SelectNextWaypoint(ref m_CustomWaypointLookup, dynamicBuffer, m_WaypointData[targetData.m_Target].m_Index)].m_Waypoint
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
                    if ((m_SimulationFrameIndex & (num - 1)) == 3)
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
                DynamicBuffer<LayoutElement> layout,
                DynamicBuffer<TrainNavigationLane> navigationLanes,
                DynamicBuffer<ServiceDispatch> serviceDispatches,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Train train,
                ref TrainCurrentLane currentLane,
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
                    if (m_TransportVehicleRequestData.HasComponent(request))
                    {
                        entity = m_TransportVehicleRequestData[request].m_Route;
                        if (m_PathInformationData.HasComponent(request))
                        {
                            entity2 = m_PathInformationData[request].m_Destination;
                        }
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
                                    UpdateBatches(jobIndex, vehicleEntity, layout);
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
                    train.m_Flags &= ~Game.Vehicles.TrainFlags.IgnoreParkedVehicle;
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
                                float num2 = VehicleUtils.CalculateLength(vehicleEntity, layout, ref m_PrefabRefData, ref m_PrefabTrainData);
                                PathElement prevElement = default;
                                if (navigationLanes.Length != 0)
                                {
                                    TrainNavigationLane trainNavigationLane = navigationLanes[navigationLanes.Length - 1];
                                    prevElement = new PathElement(trainNavigationLane.m_Lane, trainNavigationLane.m_CurvePosition);
                                }
                                PathUtils.ExtendReverseLocations(
                                    prevElement,
                                    dynamicBuffer,
                                    pathOwner,
                                    num2,
                                    m_CurveData,
                                    m_LaneData,
                                    m_EdgeLaneData,
                                    m_OwnerData,
                                    m_EdgeData,
                                    m_ConnectedEdges,
                                    m_SubLanes
                                );
                                if (
                                    !m_WaypointData.HasComponent(target.m_Target)
                                    || (m_ConnectedData.HasComponent(target.m_Target) && m_BoardingVehicleData.HasComponent(m_ConnectedData[target.m_Target].m_Connected))
                                )
                                {
                                    float distance = num2 * 0.5f;
                                    PathUtils.ExtendPath(
                                        dynamicBuffer,
                                        pathOwner,
                                        ref distance,
                                        ref m_CurveData,
                                        ref m_LaneData,
                                        ref m_EdgeLaneData,
                                        ref m_OwnerData,
                                        ref m_EdgeData,
                                        ref m_ConnectedEdges,
                                        ref m_SubLanes
                                    );
                                }
                                return true;
                            }
                        }
                    }
                    VehicleUtils.SetTarget(ref pathOwner, ref target, entity2);
                    return true;
                }
                return false;
            }

            private void UpdateBatches(int jobIndex, Entity vehicleEntity, DynamicBuffer<LayoutElement> layout)
            {
                if (layout.Length != 0)
                {
                    m_CommandBuffer.AddComponent<BatchesUpdated>(jobIndex, layout.Reinterpret<Entity>().AsNativeArray());
                }
                else
                {
                    m_CommandBuffer.AddComponent<BatchesUpdated>(jobIndex, vehicleEntity);
                }
            }

            private void ReturnToDepot(
                int jobIndex,
                Entity vehicleEntity,
                CurrentRoute currentRoute,
                Owner ownerData,
                DynamicBuffer<ServiceDispatch> serviceDispatches,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Train train,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                serviceDispatches.Clear();
                cargoTransport.m_RequestCount = 0;
                cargoTransport.m_State &= ~(CargoTransportFlags.EnRoute | CargoTransportFlags.Refueling | CargoTransportFlags.AbandonRoute);
                cargoTransport.m_State |= CargoTransportFlags.Returning;
                publicTransport.m_RequestCount = 0;
                publicTransport.m_State &= ~(PublicTransportFlags.EnRoute | PublicTransportFlags.Refueling | PublicTransportFlags.AbandonRoute);
                publicTransport.m_State |= PublicTransportFlags.Returning;
                train.m_Flags &= ~Game.Vehicles.TrainFlags.IgnoreParkedVehicle;
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
                            TrainData trainData = m_PrefabTrainData[prefabRef.m_Prefab];
                            flag = (m_TransportStationData[transportStationFromStop].m_TrainRefuelTypes & trainData.m_EnergyType) != 0;
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

            private bool TryChangeCarriagePrefab(int jobIndex, ref Random random, Entity vehicleEntity, bool dummyTraffic, DynamicBuffer<LoadingResources> loadingResources)
            {
                if (m_EconomyResources.HasBuffer(vehicleEntity))
                {
                    DynamicBuffer<Resources> dynamicBuffer = m_EconomyResources[vehicleEntity];
                    PrefabRef prefabRef = m_PrefabRefData[vehicleEntity];
                    if (dynamicBuffer.Length == 0 && m_CargoTransportVehicleData.HasComponent(prefabRef.m_Prefab))
                    {
                        while (loadingResources.Length > 0)
                        {
                            LoadingResources value = loadingResources[0];
                            Entity entity = m_TransportTrainCarriageSelectData.SelectCarriagePrefab(ref random, value.m_Resource, value.m_Amount);
                            if (entity != Entity.Null)
                            {
                                CargoTransportVehicleData cargoTransportVehicleData = m_CargoTransportVehicleData[entity];
                                int amount = math.min(value.m_Amount, cargoTransportVehicleData.m_CargoCapacity);
                                value.m_Amount -= cargoTransportVehicleData.m_CargoCapacity;
                                if (value.m_Amount <= 0)
                                {
                                    loadingResources.RemoveAt(0);
                                }
                                else
                                {
                                    loadingResources[0] = value;
                                }
                                if (dummyTraffic)
                                {
                                    m_CommandBuffer.SetBuffer<Resources>(jobIndex, vehicleEntity).Add(new Resources { m_Resource = value.m_Resource, m_Amount = amount });
                                }
                                m_CommandBuffer.SetComponent(jobIndex, vehicleEntity, new PrefabRef(entity));
                                m_CommandBuffer.AddComponent<Updated>(jobIndex, vehicleEntity, default);
                                return true;
                            }
                            loadingResources.RemoveAt(0);
                        }
                    }
                }
                return false;
            }

            private bool CheckLoadingResources(
                int jobIndex,
                ref Random random,
                Entity vehicleEntity,
                bool dummyTraffic,
                DynamicBuffer<LayoutElement> layout,
                DynamicBuffer<LoadingResources> loadingResources
            )
            {
                bool flag = false;
                if (loadingResources.Length != 0)
                {
                    if (layout.Length != 0)
                    {
                        for (int i = 0; i < layout.Length; i++)
                        {
                            if (loadingResources.Length == 0)
                            {
                                break;
                            }
                            flag |= TryChangeCarriagePrefab(jobIndex, ref random, layout[i].m_Vehicle, dummyTraffic, loadingResources);
                        }
                    }
                    else
                    {
                        flag |= TryChangeCarriagePrefab(jobIndex, ref random, vehicleEntity, dummyTraffic, loadingResources);
                    }
                    loadingResources.Clear();
                }
                return flag;
            }

            private bool StopBoarding(
                int jobIndex,
                ref Random random,
                Entity vehicleEntity,
                CurrentRoute currentRoute,
                DynamicBuffer<LayoutElement> layout,
                ref Game.Vehicles.CargoTransport cargoTransport,
                ref Game.Vehicles.PublicTransport publicTransport,
                ref Target target,
                ref Odometer odometer,
                bool isCargoVehicle,
                bool forcedStop
            )
            {
                bool flag = false;
                if (m_LoadingResources.HasBuffer(vehicleEntity))
                {
                    DynamicBuffer<LoadingResources> loadingResources = m_LoadingResources[vehicleEntity];
                    if (forcedStop)
                    {
                        loadingResources.Clear();
                    }
                    else
                    {
                        bool dummyTraffic = (cargoTransport.m_State & CargoTransportFlags.DummyTraffic) != 0 || (publicTransport.m_State & PublicTransportFlags.DummyTraffic) != 0;
                        flag |= CheckLoadingResources(jobIndex, ref random, vehicleEntity, dummyTraffic, layout, loadingResources);
                    }
                }
                if (flag)
                {
                    return false;
                }
                bool flag2 = false;
                if (
                    m_ConnectedData.TryGetComponent(target.m_Target, out var componentData)
                    && m_BoardingVehicleData.TryGetComponent(componentData.m_Connected, out var componentData2)
                )
                {
                    flag2 = componentData2.m_Vehicle == vehicleEntity;
                }
                if (!forcedStop)
                {
                    uint num = math.max(cargoTransport.m_DepartureFrame, publicTransport.m_DepartureFrame);
                    bool flag3 = num != 0 && m_SimulationFrameIndex >= num + 1800;
                    publicTransport.m_MaxBoardingDistance = math.select(
                        publicTransport.m_MinWaitingDistance + 1f,
                        float.MaxValue,
                        (publicTransport.m_MinWaitingDistance == float.MaxValue || publicTransport.m_MinWaitingDistance == 0f) | flag3
                    );
                    publicTransport.m_MinWaitingDistance = float.MaxValue;
                    if (
                        flag2
                        && (
                            m_SimulationFrameIndex < cargoTransport.m_DepartureFrame
                            || m_SimulationFrameIndex < publicTransport.m_DepartureFrame
                            || publicTransport.m_MaxBoardingDistance != float.MaxValue
                        )
                    )
                    {
                        return false;
                    }
                    if (!flag3)
                    {
                        if (layout.Length != 0)
                        {
                            for (int i = 0; i < layout.Length; i++)
                            {
                                if (!ArePassengersReady(layout[i].m_Vehicle))
                                {
                                    return false;
                                }
                            }
                        }
                        else if (!ArePassengersReady(vehicleEntity))
                        {
                            return false;
                        }
                    }
                }
                if ((cargoTransport.m_State & CargoTransportFlags.Refueling) != 0 || (publicTransport.m_State & PublicTransportFlags.Refueling) != 0)
                {
                    odometer.m_Distance = 0f;
                }
                if (isCargoVehicle)
                {
                    QuantityUpdated(jobIndex, vehicleEntity, layout);
                }
                if (flag2)
                {
                    Entity currentStation = Entity.Null;
                    Entity nextStation = Entity.Null;
                    if (isCargoVehicle && !forcedStop)
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

            private void QuantityUpdated(int jobIndex, Entity vehicleEntity, DynamicBuffer<LayoutElement> layout)
            {
                if (layout.Length != 0)
                {
                    for (int i = 0; i < layout.Length; i++)
                    {
                        m_CommandBuffer.AddComponent<Updated>(jobIndex, layout[i].m_Vehicle, default);
                    }
                }
                else
                {
                    m_CommandBuffer.AddComponent<Updated>(jobIndex, vehicleEntity, default);
                }
            }

            private int CountPassengers(Entity vehicleEntity, DynamicBuffer<LayoutElement> layout)
            {
                int num = 0;
                if (layout.Length != 0)
                {
                    for (int i = 0; i < layout.Length; i++)
                    {
                        Entity vehicle = layout[i].m_Vehicle;
                        if (m_Passengers.HasBuffer(vehicle))
                        {
                            num += m_Passengers[vehicle].Length;
                        }
                    }
                }
                else if (m_Passengers.HasBuffer(vehicleEntity))
                {
                    num += m_Passengers[vehicleEntity].Length;
                }
                return num;
            }

            private bool ArePassengersReady(Entity vehicleEntity)
            {
                if (!m_Passengers.HasBuffer(vehicleEntity))
                {
                    return true;
                }
                DynamicBuffer<Passenger> dynamicBuffer = m_Passengers[vehicleEntity];
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    Entity passenger = dynamicBuffer[i].m_Passenger;
                    if (m_CurrentVehicleData.HasComponent(passenger) && (m_CurrentVehicleData[passenger].m_Flags & CreatureVehicleFlags.Ready) == 0)
                    {
                        return false;
                    }
                }
                return true;
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

        private TransportUsageTrackSystem m_TransportUsageTrackSystem;

        private AchievementTriggerSystem m_AchievementTriggerSystem;

        private CityConfigurationSystem m_CityConfigurationSystem;

        private EntityQuery m_VehicleQuery;

        private EntityQuery m_CarriagePrefabQuery;

        private EntityArchetype m_TransportVehicleRequestArchetype;

        private EntityArchetype m_HandleRequestArchetype;

        private ComponentTypeSet m_MovingToParkedTrainRemoveTypes;

        private ComponentTypeSet m_MovingToParkedTrainAddTypes;

        private TransportTrainCarriageSelectData m_TransportTrainCarriageSelectData;

        private TransportBoardingHelpers.BoardingLookupData m_BoardingLookupData;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 16;
        }

        public override int GetUpdateOffset(SystemUpdatePhase phase)
        {
            return 3;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            m_TransportUsageTrackSystem = World.GetOrCreateSystemManaged<TransportUsageTrackSystem>();
            m_AchievementTriggerSystem = World.GetOrCreateSystemManaged<AchievementTriggerSystem>();
            m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            m_TransportTrainCarriageSelectData = new TransportTrainCarriageSelectData(this);
            m_BoardingLookupData = new TransportBoardingHelpers.BoardingLookupData(this);
            m_VehicleQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[5]
                    {
                        ComponentType.ReadWrite<TrainCurrentLane>(),
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadWrite<PathOwner>(),
                        ComponentType.ReadWrite<Target>(),
                    },
                    Any = new ComponentType[2] { ComponentType.ReadWrite<Game.Vehicles.CargoTransport>(), ComponentType.ReadWrite<Game.Vehicles.PublicTransport>() },
                    None = new ComponentType[4]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<TripSource>(),
                        ComponentType.ReadOnly<OutOfControl>(),
                    },
                }
            );
            m_TransportVehicleRequestArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<ServiceRequest>(),
                ComponentType.ReadWrite<TransportVehicleRequest>(),
                ComponentType.ReadWrite<RequestGroup>()
            );
            m_HandleRequestArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<HandleRequest>(), ComponentType.ReadWrite<Event>());
            m_MovingToParkedTrainRemoveTypes = new ComponentTypeSet(
                new ComponentType[13]
                {
                    ComponentType.ReadWrite<Moving>(),
                    ComponentType.ReadWrite<TransformFrame>(),
                    ComponentType.ReadWrite<InterpolatedTransform>(),
                    ComponentType.ReadWrite<TrainNavigation>(),
                    ComponentType.ReadWrite<TrainNavigationLane>(),
                    ComponentType.ReadWrite<TrainCurrentLane>(),
                    ComponentType.ReadWrite<TrainBogieFrame>(),
                    ComponentType.ReadWrite<PathOwner>(),
                    ComponentType.ReadWrite<Target>(),
                    ComponentType.ReadWrite<Blocker>(),
                    ComponentType.ReadWrite<PathElement>(),
                    ComponentType.ReadWrite<PathInformation>(),
                    ComponentType.ReadWrite<ServiceDispatch>(),
                }
            );
            m_MovingToParkedTrainAddTypes = new ComponentTypeSet(ComponentType.ReadWrite<ParkedTrain>(), ComponentType.ReadWrite<Stopped>(), ComponentType.ReadWrite<Updated>());
            m_CarriagePrefabQuery = GetEntityQuery(TransportTrainCarriageSelectData.GetEntityQueryDesc());
            RequireForUpdate(m_VehicleQuery);
        }

        protected override void OnUpdate()
        {
            TransportBoardingHelpers.BoardingData boardingData = new TransportBoardingHelpers.BoardingData(Allocator.TempJob);
            m_TransportTrainCarriageSelectData.PreUpdate(this, m_CityConfigurationSystem, m_CarriagePrefabQuery, Allocator.TempJob, out var jobHandle);
            m_BoardingLookupData.Update(this);
            JobHandle jobHandle2 = JobChunkExtensions.ScheduleParallel(
                new TransportTrainTickJob
                {
                    m_EntityType = SystemAPI.GetEntityTypeHandle(),
                    m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(true),
                    m_UnspawnedType = SystemAPI.GetComponentTypeHandle<Unspawned>(true),
                    m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                    m_CurrentRouteType = SystemAPI.GetComponentTypeHandle<CurrentRoute>(true),
                    m_CargoTransportType = SystemAPI.GetComponentTypeHandle<Game.Vehicles.CargoTransport>(false),
                    m_PublicTransportType = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PublicTransport>(false),
                    m_TargetType = SystemAPI.GetComponentTypeHandle<Target>(false),
                    m_PathOwnerType = SystemAPI.GetComponentTypeHandle<PathOwner>(false),
                    m_OdometerType = SystemAPI.GetComponentTypeHandle<Odometer>(false),
                    m_LayoutElementType = SystemAPI.GetBufferTypeHandle<LayoutElement>(false),
                    m_NavigationLaneType = SystemAPI.GetBufferTypeHandle<TrainNavigationLane>(false),
                    m_ServiceDispatchType = SystemAPI.GetBufferTypeHandle<ServiceDispatch>(false),
                    m_EntityLookup = SystemAPI.GetEntityStorageInfoLookup(),
                    m_TransformData = SystemAPI.GetComponentLookup<Transform>(true),
                    m_SpawnLocationData = SystemAPI.GetComponentLookup<Game.Objects.SpawnLocation>(true),
                    m_OwnerData = SystemAPI.GetComponentLookup<Owner>(true),
                    m_PathInformationData = SystemAPI.GetComponentLookup<PathInformation>(true),
                    m_TransportVehicleRequestData = SystemAPI.GetComponentLookup<TransportVehicleRequest>(true),
                    m_ParkedTrainData = SystemAPI.GetComponentLookup<ParkedTrain>(true),
                    m_ControllerData = SystemAPI.GetComponentLookup<Controller>(true),
                    m_CurveData = SystemAPI.GetComponentLookup<Curve>(true),
                    m_LaneData = SystemAPI.GetComponentLookup<Lane>(true),
                    m_EdgeLaneData = SystemAPI.GetComponentLookup<EdgeLane>(true),
                    m_EdgeData = SystemAPI.GetComponentLookup<Game.Net.Edge>(true),
                    m_PrefabTrainData = SystemAPI.GetComponentLookup<TrainData>(true),
                    m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                    m_PublicTransportVehicleData = SystemAPI.GetComponentLookup<PublicTransportVehicleData>(true),
                    m_CargoTransportVehicleData = SystemAPI.GetComponentLookup<CargoTransportVehicleData>(true),
                    m_WaypointData = SystemAPI.GetComponentLookup<Waypoint>(true),
                    m_ConnectedData = SystemAPI.GetComponentLookup<Connected>(true),
                    m_CustomWaypointLookup = SystemAPI.GetComponentLookup<CustomWaypoint>(false),
                    m_BoardingVehicleData = SystemAPI.GetComponentLookup<BoardingVehicle>(true),
                    m_RouteColorData = SystemAPI.GetComponentLookup<Game.Routes.Color>(true),
                    m_StorageCompanyData = SystemAPI.GetComponentLookup<Game.Companies.StorageCompany>(true),
                    m_TransportStationData = SystemAPI.GetComponentLookup<Game.Buildings.TransportStation>(true),
                    m_TransportDepotData = SystemAPI.GetComponentLookup<Game.Buildings.TransportDepot>(true),
                    m_CurrentVehicleData = SystemAPI.GetComponentLookup<CurrentVehicle>(true),
                    m_Passengers = SystemAPI.GetBufferLookup<Passenger>(true),
                    m_EconomyResources = SystemAPI.GetBufferLookup<Resources>(true),
                    m_RouteWaypoints = SystemAPI.GetBufferLookup<RouteWaypoint>(true),
                    m_ConnectedEdges = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                    m_SubLanes = SystemAPI.GetBufferLookup<Game.Net.SubLane>(true),
                    m_TrainData = SystemAPI.GetComponentLookup<Train>(false),
                    m_CurrentLaneData = SystemAPI.GetComponentLookup<TrainCurrentLane>(false),
                    m_NavigationData = SystemAPI.GetComponentLookup<TrainNavigation>(false),
                    m_BlockerData = SystemAPI.GetComponentLookup<Blocker>(false),
                    m_PathElements = SystemAPI.GetBufferLookup<PathElement>(false),
                    m_LoadingResources = SystemAPI.GetBufferLookup<LoadingResources>(false),
                    m_LayoutElements = SystemAPI.GetBufferLookup<LayoutElement>(false),
                    m_SimulationFrameIndex = m_SimulationSystem.frameIndex,
                    m_RandomSeed = RandomSeed.Next(),
                    m_TransportVehicleRequestArchetype = m_TransportVehicleRequestArchetype,
                    m_HandleRequestArchetype = m_HandleRequestArchetype,
                    m_MovingToParkedTrainRemoveTypes = m_MovingToParkedTrainRemoveTypes,
                    m_MovingToParkedTrainAddTypes = m_MovingToParkedTrainAddTypes,
                    m_TransportTrainCarriageSelectData = m_TransportTrainCarriageSelectData,
                    m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                    m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
                    m_BoardingData = boardingData.ToConcurrent(),
                },
                m_VehicleQuery,
                JobHandle.CombineDependencies(Dependency, jobHandle)
            );
            JobHandle jobHandle3 = boardingData.ScheduleBoarding(
                this,
                m_CityStatisticsSystem,
                m_TransportUsageTrackSystem,
                m_AchievementTriggerSystem,
                m_BoardingLookupData,
                m_SimulationSystem.frameIndex,
                jobHandle2
            );
            m_TransportTrainCarriageSelectData.PostUpdate(jobHandle2);
            boardingData.Dispose(jobHandle3);
            m_PathfindSetupSystem.AddQueueWriter(jobHandle2);
            m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
            Dependency = jobHandle3;
        }
    }
}
