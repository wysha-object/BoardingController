using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BoardingController.Components;
using Colossal.PSI.Common;
using Game.Achievements;
using Game.Buildings;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.Vehicles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BoardingController.Utils
{
    public static class PatchedTransportBoardingHelpers
    {
        public struct BoardingLookupData(SystemBase system)
        {
            [ReadOnly]
            public ComponentLookup<TransportLine> m_TransportLineData = system.GetComponentLookup<TransportLine>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<Connected> m_ConnectedData = system.GetComponentLookup<Connected>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<Game.Routes.TransportStop> m_TransportStopData = system.GetComponentLookup<Game.Routes.TransportStop>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData = system.GetComponentLookup<PrefabRef>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<TransportLineData> m_PrefabTransportLineData = system.GetComponentLookup<TransportLineData>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<CargoTransportVehicleData> m_PrefabCargoTransportVehicleData = system.GetComponentLookup<CargoTransportVehicleData>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<Train> m_TrainData = system.GetComponentLookup<Train>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<Aircraft> m_AircraftData = system.GetComponentLookup<Aircraft>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<Watercraft> m_WatercraftData = system.GetComponentLookup<Watercraft>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTruckData = system.GetComponentLookup<Game.Vehicles.DeliveryTruck>(isReadOnly: true);

            [ReadOnly]
            public ComponentLookup<Owner> m_OwnerData = system.GetComponentLookup<Owner>(isReadOnly: true);

            [ReadOnly]
            public BufferLookup<LayoutElement> m_LayoutElements = system.GetBufferLookup<LayoutElement>(isReadOnly: true);

            [ReadOnly]
            public BufferLookup<RouteModifier> m_RouteModifiers = system.GetBufferLookup<RouteModifier>(isReadOnly: true);

            public ComponentLookup<Game.Vehicles.CargoTransport> m_CargoTransportData = system.GetComponentLookup<Game.Vehicles.CargoTransport>();

            public ComponentLookup<Game.Vehicles.PublicTransport> m_PublicTransportData = system.GetComponentLookup<Game.Vehicles.PublicTransport>();

            public ComponentLookup<BoardingVehicle> m_BoardingVehicleData = system.GetComponentLookup<BoardingVehicle>();

            public ComponentLookup<VehicleTiming> m_VehicleTimingData = system.GetComponentLookup<VehicleTiming>();

            public ComponentLookup<Game.Buildings.CargoTransportStation> m_CargoTransportStationData = system.GetComponentLookup<Game.Buildings.CargoTransportStation>();

            public BufferLookup<StorageTransferRequest> m_StorageTransferRequests = system.GetBufferLookup<StorageTransferRequest>();

            public BufferLookup<Resources> m_EconomyResources = system.GetBufferLookup<Resources>();

            public BufferLookup<LoadingResources> m_LoadingResources = system.GetBufferLookup<LoadingResources>();

            public BufferLookup<ExtraBoardingVehicle> m_ExtraBoardingVehicleLookup = system.GetBufferLookup<ExtraBoardingVehicle>();

            public void Update(SystemBase system)
            {
                m_TransportLineData.Update(system);
                m_ConnectedData.Update(system);
                m_TransportStopData.Update(system);
                m_PrefabRefData.Update(system);
                m_PrefabTransportLineData.Update(system);
                m_PrefabCargoTransportVehicleData.Update(system);
                m_LayoutElements.Update(system);
                m_RouteModifiers.Update(system);
                m_CargoTransportData.Update(system);
                m_PublicTransportData.Update(system);
                m_BoardingVehicleData.Update(system);
                m_VehicleTimingData.Update(system);
                m_CargoTransportStationData.Update(system);
                m_StorageTransferRequests.Update(system);
                m_EconomyResources.Update(system);
                m_LoadingResources.Update(system);
                m_ExtraBoardingVehicleLookup.Update(system);
                m_DeliveryTruckData.Update(system);
                m_OwnerData.Update(system);
                m_AircraftData.Update(system);
                m_WatercraftData.Update(system);
                m_TrainData.Update(system);
            }
        }

        public struct BoardingData(Allocator allocator)
        {
            public struct Concurrent(BoardingData data)
            {
                private NativeQueue<BoardingItem>.ParallelWriter m_BoardingQueue = data.m_BoardingQueue.AsParallelWriter();

                public void BeginBoarding(Entity vehicle, Entity route, Entity stop, Entity waypoint, Entity currentStation, Entity nextStation, bool refuel)
                {
                    BoardingItem value = default;
                    value.m_Begin = true;
                    value.m_Refuel = refuel;
                    value.m_Testing = false;
                    value.m_Vehicle = vehicle;
                    value.m_Route = route;
                    value.m_Stop = stop;
                    value.m_Waypoint = waypoint;
                    value.m_CurrentStation = currentStation;
                    value.m_NextStation = nextStation;
                    m_BoardingQueue.Enqueue(value);
                }

                public void EndBoarding(Entity vehicle, Entity route, Entity stop, Entity waypoint, Entity currentStation, Entity nextStation)
                {
                    BoardingItem value = default;
                    value.m_Begin = false;
                    value.m_Refuel = false;
                    value.m_Testing = false;
                    value.m_Vehicle = vehicle;
                    value.m_Route = route;
                    value.m_Stop = stop;
                    value.m_Waypoint = waypoint;
                    value.m_CurrentStation = currentStation;
                    value.m_NextStation = nextStation;
                    m_BoardingQueue.Enqueue(value);
                }

                public void BeginTesting(Entity vehicle, Entity route, Entity stop, Entity waypoint)
                {
                    BoardingItem value = default;
                    value.m_Begin = true;
                    value.m_Refuel = false;
                    value.m_Testing = true;
                    value.m_Vehicle = vehicle;
                    value.m_Route = route;
                    value.m_Stop = stop;
                    value.m_Waypoint = waypoint;
                    value.m_CurrentStation = Entity.Null;
                    value.m_NextStation = Entity.Null;
                    m_BoardingQueue.Enqueue(value);
                }

                public void EndTesting(Entity vehicle, Entity route, Entity stop, Entity waypoint)
                {
                    BoardingItem value = default;
                    value.m_Begin = false;
                    value.m_Refuel = false;
                    value.m_Testing = true;
                    value.m_Vehicle = vehicle;
                    value.m_Route = route;
                    value.m_Stop = stop;
                    value.m_Waypoint = waypoint;
                    value.m_CurrentStation = Entity.Null;
                    value.m_NextStation = Entity.Null;
                    m_BoardingQueue.Enqueue(value);
                }
            }

            private NativeQueue<BoardingItem> m_BoardingQueue = new NativeQueue<BoardingItem>(allocator);

            public void Dispose()
            {
                m_BoardingQueue.Dispose();
            }

            public void Dispose(JobHandle inputDeps)
            {
                m_BoardingQueue.Dispose(inputDeps);
            }

            public JobHandle ScheduleBoarding(
                SystemBase system,
                CityStatisticsSystem statsSystem,
                TransportUsageTrackSystem transportUsageTrackSystem,
                AchievementTriggerSystem achievementTriggerSystem,
                BoardingLookupData lookupData,
                uint simulationFrameIndex,
                JobHandle inputDeps
            )
            {
                JobHandle jobHandle = IJobExtensions.Schedule(
                    new TransportBoardingJob
                    {
                        m_SimulationFrameIndex = simulationFrameIndex,
                        m_ShouldTrackTransportedResource = (
                            PlatformManager.instance.achievementsEnabled
                            && PlatformManager.instance.GetAchievement(Game.Achievements.Achievements.ShipIt, out var achievement)
                            && !achievement.achieved
                        ),
                        m_BoardingLookupData = lookupData,
                        m_OutsideConnections = system.GetComponentLookup<Game.Objects.OutsideConnection>(isReadOnly: true),
                        m_BoardingQueue = m_BoardingQueue,
                        m_StatisticsEventQueue = statsSystem.GetStatisticsEventQueue(out var deps),
                        m_TransportUsageQueue = transportUsageTrackSystem.GetQueue(out var deps2),
                        m_TransportedResourceQueue = achievementTriggerSystem.GetTransportedResourceQueue(),
                    },
                    JobHandle.CombineDependencies(inputDeps, deps, deps2)
                );
                transportUsageTrackSystem.AddQueueWriter(jobHandle);
                statsSystem.AddWriter(jobHandle);
                achievementTriggerSystem.AddWriter(jobHandle);
                return jobHandle;
            }

            public Concurrent ToConcurrent()
            {
                return new Concurrent(this);
            }
        }

        private struct BoardingItem
        {
            public bool m_Begin;

            public bool m_Refuel;

            public bool m_Testing;

            public Entity m_Vehicle;

            public Entity m_Route;

            public Entity m_Stop;

            public Entity m_Waypoint;

            public Entity m_CurrentStation;

            public Entity m_NextStation;
        }

        [BurstCompile]
        private struct TransportBoardingJob : IJob
        {
            [StructLayout(LayoutKind.Sequential, Size = 1)]
            private struct LoadingResourceComparer : IComparer<LoadingResources>
            {
                public int Compare(LoadingResources x, LoadingResources y)
                {
                    return x.m_Amount - y.m_Amount;
                }
            }

            [ReadOnly]
            public uint m_SimulationFrameIndex;

            [ReadOnly]
            public bool m_ShouldTrackTransportedResource;

            [ReadOnly]
            public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

            public BoardingLookupData m_BoardingLookupData;

            public NativeQueue<BoardingItem> m_BoardingQueue;

            public NativeQueue<StatisticsEvent> m_StatisticsEventQueue;

            public NativeQueue<TransportUsageEvent> m_TransportUsageQueue;

            public NativeQueue<TransportedResource> m_TransportedResourceQueue;

            public void Execute()
            {
                BoardingItem item;
                while (m_BoardingQueue.TryDequeue(out item))
                {
                    if (item.m_Testing)
                    {
                        if (item.m_Begin)
                        {
                            BeginTesting(item);
                        }
                        else
                        {
                            EndTesting(item);
                        }
                    }
                    else if (item.m_Begin)
                    {
                        BeginBoarding(item);
                    }
                    else
                    {
                        EndBoarding(item);
                    }
                }
            }

            private void BeginTesting(BoardingItem data)
            {
                BoardingVehicle value = m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop];
                if (
                    !(value.m_Testing != Entity.Null)
                    || !(value.m_Testing != data.m_Vehicle)
                    || (
                        (
                            !m_BoardingLookupData.m_CargoTransportData.TryGetComponent(value.m_Testing, out var componentData)
                            || (componentData.m_State & CargoTransportFlags.Testing) == 0
                        )
                        && (
                            !m_BoardingLookupData.m_PublicTransportData.TryGetComponent(value.m_Testing, out var componentData2)
                            || (componentData2.m_State & PublicTransportFlags.Testing) == 0
                        )
                    )
                )
                {
                    value.m_Testing = data.m_Vehicle;
                    m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop] = value;
                    if (m_BoardingLookupData.m_CargoTransportData.HasComponent(data.m_Vehicle))
                    {
                        Game.Vehicles.CargoTransport value2 = m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle];
                        value2.m_State &= ~CargoTransportFlags.RequireStop;
                        value2.m_State |= CargoTransportFlags.Testing;
                        m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle] = value2;
                    }
                    if (m_BoardingLookupData.m_PublicTransportData.HasComponent(data.m_Vehicle))
                    {
                        Game.Vehicles.PublicTransport value3 = m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle];
                        value3.m_State &= ~PublicTransportFlags.RequireStop;
                        value3.m_State |= PublicTransportFlags.Testing;
                        m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle] = value3;
                    }
                }
            }

            private void EndTesting(BoardingItem data)
            {
                BoardingVehicle value = m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop];
                if (value.m_Testing == data.m_Vehicle)
                {
                    value.m_Testing = Entity.Null;
                    m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop] = value;
                }
                if (m_BoardingLookupData.m_CargoTransportData.HasComponent(data.m_Vehicle))
                {
                    Game.Vehicles.CargoTransport value2 = m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle];
                    value2.m_State &= ~CargoTransportFlags.Testing;
                    m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle] = value2;
                }
                if (m_BoardingLookupData.m_PublicTransportData.HasComponent(data.m_Vehicle))
                {
                    Game.Vehicles.PublicTransport value3 = m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle];
                    value3.m_State &= ~PublicTransportFlags.Testing;
                    m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle] = value3;
                }
            }

            private void BeginBoarding(BoardingItem data)
            {
                DynamicBuffer<ExtraBoardingVehicle> extraBoardingVehicleBuffer = m_BoardingLookupData.m_ExtraBoardingVehicleLookup[data.m_Stop];
                BoardingVehicle value = m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop];
                bool isExtraBoardingVehicle = false;
                if (
                    value.m_Vehicle != Entity.Null
                    && value.m_Vehicle != data.m_Vehicle
                    && (
                        (
                            m_BoardingLookupData.m_CargoTransportData.TryGetComponent(value.m_Vehicle, out var componentData)
                            && (componentData.m_State & CargoTransportFlags.Boarding) != 0
                        )
                        || (
                            m_BoardingLookupData.m_PublicTransportData.TryGetComponent(value.m_Vehicle, out var componentData2)
                            && (componentData2.m_State & PublicTransportFlags.Boarding) != 0
                        )
                    )
                )
                {
                    isExtraBoardingVehicle = true;
                }
                PrefabRef prefabRef = m_BoardingLookupData.m_PrefabRefData[data.m_Route];
                TransportLine transportLine = m_BoardingLookupData.m_TransportLineData[data.m_Route];
                VehicleTiming value2 = m_BoardingLookupData.m_VehicleTimingData[data.m_Waypoint];
                Connected connected = m_BoardingLookupData.m_ConnectedData[data.m_Waypoint];
                DynamicBuffer<RouteModifier> routeModifiers = m_BoardingLookupData.m_RouteModifiers[data.m_Route];
                Game.Vehicles.CargoTransport value3 = default;
                Game.Vehicles.PublicTransport value4 = default;
                uint departureFrame = 0u;
                if (m_BoardingLookupData.m_CargoTransportData.HasComponent(data.m_Vehicle))
                {
                    value3 = m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle];
                    departureFrame = value3.m_DepartureFrame;
                }
                if (m_BoardingLookupData.m_PublicTransportData.HasComponent(data.m_Vehicle))
                {
                    value4 = m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle];
                    departureFrame = value4.m_DepartureFrame;
                }
                TransportLineData prefabLineData = m_BoardingLookupData.m_PrefabTransportLineData[prefabRef.m_Prefab];
                float targetStopTime = (
                    (!m_BoardingLookupData.m_TransportStopData.HasComponent(connected.m_Connected))
                        ? prefabLineData.m_StopDuration
                        : RouteUtils.GetStopDuration(prefabLineData, m_BoardingLookupData.m_TransportStopData[connected.m_Connected])
                );
                if (isExtraBoardingVehicle)
                    {
                        extraBoardingVehicleBuffer.Add(new ExtraBoardingVehicle { m_Vehicle = data.m_Vehicle });
                }
                else
                {
                    value.m_Vehicle = data.m_Vehicle;
                }
                value2.m_AverageTravelTime = RouteUtils.UpdateAverageTravelTime(value2.m_AverageTravelTime, departureFrame, m_SimulationFrameIndex);
                departureFrame = (
                    ((value3.m_State & CargoTransportFlags.EnRoute) == 0 && (value4.m_State & PublicTransportFlags.EnRoute) == 0)
                        ? (m_SimulationFrameIndex + 60)
                        : (
                            value2.m_LastDepartureFrame = RouteUtils.CalculateDepartureFrame(
                                transportLine,
                                prefabLineData,
                                routeModifiers,
                                targetStopTime,
                                value2.m_LastDepartureFrame,
                                m_SimulationFrameIndex
                            )
                        )
                );
                m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop] = value;
                m_BoardingLookupData.m_VehicleTimingData[data.m_Waypoint] = value2;
                if (m_BoardingLookupData.m_CargoTransportData.HasComponent(data.m_Vehicle))
                {
                    value3.m_State |= CargoTransportFlags.Boarding;
                    if (data.m_Refuel)
                    {
                        value3.m_State |= CargoTransportFlags.Refueling;
                    }
                    value3.m_DepartureFrame = departureFrame;
                    m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle] = value3;
                }
                if (m_BoardingLookupData.m_PublicTransportData.HasComponent(data.m_Vehicle))
                {
                    value4.m_State |= PublicTransportFlags.Boarding;
                    if (data.m_Refuel)
                    {
                        value4.m_State |= PublicTransportFlags.Refueling;
                    }
                    value4.m_DepartureFrame = departureFrame;
                    value4.m_MaxBoardingDistance = 0f;
                    value4.m_MinWaitingDistance = float.MaxValue;
                    m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle] = value4;
                }
                DynamicBuffer<LoadingResources> loadingResources = default;
                if (m_BoardingLookupData.m_LoadingResources.HasBuffer(data.m_Vehicle))
                {
                    loadingResources = m_BoardingLookupData.m_LoadingResources[data.m_Vehicle];
                    loadingResources.Clear();
                }
                int workAmount = 0;
                UnloadResources(data.m_Vehicle, data.m_CurrentStation, ref workAmount);
                LoadResources(data.m_Vehicle, data.m_CurrentStation, data.m_NextStation, loadingResources, ref workAmount);
                AddWork(data.m_Stop, workAmount);
            }

            private void AddWork(Entity target, int workAmount)
            {
                if (workAmount <= 0)
                {
                    return;
                }
                Owner componentData;
                while (m_BoardingLookupData.m_OwnerData.TryGetComponent(target, out componentData))
                {
                    target = componentData.m_Owner;
                    if (m_BoardingLookupData.m_CargoTransportStationData.TryGetComponent(target, out var componentData2))
                    {
                        componentData2.m_WorkAmount += workAmount;
                        m_BoardingLookupData.m_CargoTransportStationData[target] = componentData2;
                        break;
                    }
                }
            }

            private void EndBoarding(BoardingItem data)
            {
                if (m_BoardingLookupData.m_ExtraBoardingVehicleLookup.TryGetBuffer(data.m_Stop, out DynamicBuffer<ExtraBoardingVehicle> extraBoardingVehicleBuffer))
                {
                    for (int i = extraBoardingVehicleBuffer.Length - 1; i >= 0; i--)
                    {
                        var item = extraBoardingVehicleBuffer[i];
                        if (item.m_Vehicle == data.m_Vehicle)
                        {
                            extraBoardingVehicleBuffer.RemoveAtSwapBack(i);
                        }
                    }
                }

                BoardingVehicle value = m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop];
                if (value.m_Vehicle == data.m_Vehicle)
                {
                    value.m_Vehicle = Entity.Null;
                    m_BoardingLookupData.m_BoardingVehicleData[data.m_Stop] = value;
                    int workAmount = 0;
                    LoadResources(data.m_Vehicle, data.m_CurrentStation, data.m_NextStation, default, ref workAmount);
                    AddWork(data.m_Stop, workAmount);
                }
                if (m_BoardingLookupData.m_CargoTransportData.HasComponent(data.m_Vehicle))
                {
                    Game.Vehicles.CargoTransport value2 = m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle];
                    value2.m_State &= ~(CargoTransportFlags.Boarding | CargoTransportFlags.Refueling);
                    m_BoardingLookupData.m_CargoTransportData[data.m_Vehicle] = value2;
                }
                if (m_BoardingLookupData.m_PublicTransportData.HasComponent(data.m_Vehicle))
                {
                    Game.Vehicles.PublicTransport value3 = m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle];
                    value3.m_State &= ~(PublicTransportFlags.Boarding | PublicTransportFlags.Refueling);
                    m_BoardingLookupData.m_PublicTransportData[data.m_Vehicle] = value3;
                }
            }

            private void UnloadResources(Entity vehicle, Entity target, ref int workAmount)
            {
                if (!m_BoardingLookupData.m_EconomyResources.HasBuffer(target))
                {
                    return;
                }
                DynamicBuffer<Resources> targetResources = m_BoardingLookupData.m_EconomyResources[target];
                if (m_BoardingLookupData.m_LayoutElements.HasBuffer(vehicle))
                {
                    DynamicBuffer<LayoutElement> dynamicBuffer = m_BoardingLookupData.m_LayoutElements[vehicle];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        Entity vehicle2 = dynamicBuffer[i].m_Vehicle;
                        TransportType vehicleType = GetVehicleType(vehicle2);
                        if (m_BoardingLookupData.m_EconomyResources.HasBuffer(vehicle2))
                        {
                            DynamicBuffer<Resources> sourceResources = m_BoardingLookupData.m_EconomyResources[vehicle2];
                            UnloadResources(sourceResources, targetResources, target, ref workAmount, vehicleType);
                        }
                    }
                }
                else if (m_BoardingLookupData.m_EconomyResources.HasBuffer(vehicle))
                {
                    TransportType vehicleType2 = GetVehicleType(vehicle);
                    DynamicBuffer<Resources> sourceResources2 = m_BoardingLookupData.m_EconomyResources[vehicle];
                    UnloadResources(sourceResources2, targetResources, target, ref workAmount, vehicleType2);
                }
            }

            private void UnloadResources(
                DynamicBuffer<Resources> sourceResources,
                DynamicBuffer<Resources> targetResources,
                Entity target,
                ref int workAmount,
                TransportType transportType
            )
            {
                for (int i = 0; i < sourceResources.Length; i++)
                {
                    Resources resources = sourceResources[i];
                    EconomyUtils.AddResources(resources.m_Resource, resources.m_Amount, targetResources);
                    workAmount += resources.m_Amount;
                    if (m_ShouldTrackTransportedResource && resources.m_Amount != 0)
                    {
                        m_TransportedResourceQueue.Enqueue(new TransportedResource { m_Amount = resources.m_Amount, m_CargoTransport = target });
                    }
                    if (!m_OutsideConnections.HasComponent(target))
                    {
                        m_TransportUsageQueue.Enqueue(
                            new TransportUsageEvent
                            {
                                m_Building = target,
                                m_TransportedCargo = resources.m_Amount,
                                m_TransportType = transportType,
                            }
                        );
                    }
                }
                sourceResources.Clear();
            }

            private void LoadResources(Entity vehicle, Entity source, Entity target, DynamicBuffer<LoadingResources> loadingResources, ref int workAmount)
            {
                if (m_BoardingLookupData.m_EconomyResources.HasBuffer(source) && m_BoardingLookupData.m_StorageTransferRequests.HasBuffer(source))
                {
                    DynamicBuffer<Resources> resources = m_BoardingLookupData.m_EconomyResources[source];
                    DynamicBuffer<StorageTransferRequest> dynamicBuffer = m_BoardingLookupData.m_StorageTransferRequests[source];
                    int num = 0;
                    while (num < dynamicBuffer.Length)
                    {
                        StorageTransferRequest value = dynamicBuffer[num];
                        if ((value.m_Flags & StorageTransferFlags.Incoming) != 0 || (value.m_Flags & StorageTransferFlags.Transport) == 0 || value.m_Target != target)
                        {
                            num++;
                            continue;
                        }
                        int requestNewAmount = value.m_Amount;
                        if (value.m_Amount > 0)
                        {
                            int resources2 = EconomyUtils.GetResources(value.m_Resource, resources);
                            LoadResources(value.m_Resource, ref requestNewAmount, vehicle, resources2, source);
                            if (requestNewAmount < value.m_Amount)
                            {
                                EconomyUtils.AddResources(value.m_Resource, requestNewAmount - value.m_Amount, resources);
                                workAmount += value.m_Amount - requestNewAmount;
                            }
                        }
                        if (requestNewAmount == 0)
                        {
                            dynamicBuffer.RemoveAt(num);
                            continue;
                        }
                        value.m_Amount = requestNewAmount;
                        dynamicBuffer[num++] = value;
                        if (!loadingResources.IsCreated)
                        {
                            continue;
                        }
                        for (int i = 0; i < loadingResources.Length; i++)
                        {
                            LoadingResources value2 = loadingResources[i];
                            if (value2.m_Resource == value.m_Resource)
                            {
                                value2.m_Amount += value.m_Amount;
                                loadingResources[i] = value2;
                                value.m_Amount = 0;
                                break;
                            }
                        }
                        if (value.m_Amount != 0)
                        {
                            loadingResources.Add(new LoadingResources { m_Resource = value.m_Resource, m_Amount = value.m_Amount });
                        }
                    }
                }
                if (loadingResources.IsCreated && loadingResources.Length >= 2)
                {
                    loadingResources.AsNativeArray().Sort<LoadingResources, LoadingResourceComparer>(default);
                }
            }

            private void LoadResources(Resource resource, ref int requestNewAmount, Entity vehicle, int sourceStoredAmount, Entity source)
            {
                if (m_BoardingLookupData.m_LayoutElements.HasBuffer(vehicle))
                {
                    DynamicBuffer<LayoutElement> dynamicBuffer = m_BoardingLookupData.m_LayoutElements[vehicle];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        Entity vehicle2 = dynamicBuffer[i].m_Vehicle;
                        if (m_BoardingLookupData.m_EconomyResources.HasBuffer(vehicle2))
                        {
                            PrefabRef prefabRef = m_BoardingLookupData.m_PrefabRefData[vehicle2];
                            CargoTransportVehicleData vehicleData = m_BoardingLookupData.m_PrefabCargoTransportVehicleData[prefabRef.m_Prefab];
                            DynamicBuffer<Resources> targetResources = m_BoardingLookupData.m_EconomyResources[vehicle2];
                            TransportType vehicleType = GetVehicleType(vehicle2);
                            LoadResources(resource, ref requestNewAmount, vehicleData, targetResources, vehicleType, ref sourceStoredAmount, source);
                            if (requestNewAmount == 0 || sourceStoredAmount <= 0)
                            {
                                break;
                            }
                        }
                    }
                }
                else if (m_BoardingLookupData.m_EconomyResources.HasBuffer(vehicle))
                {
                    PrefabRef prefabRef2 = m_BoardingLookupData.m_PrefabRefData[vehicle];
                    CargoTransportVehicleData vehicleData2 = m_BoardingLookupData.m_PrefabCargoTransportVehicleData[prefabRef2.m_Prefab];
                    DynamicBuffer<Resources> targetResources2 = m_BoardingLookupData.m_EconomyResources[vehicle];
                    TransportType vehicleType2 = GetVehicleType(vehicle);
                    LoadResources(resource, ref requestNewAmount, vehicleData2, targetResources2, vehicleType2, ref sourceStoredAmount, source);
                }
            }

            private void LoadResources(
                Resource resource,
                ref int requestNewAmount,
                CargoTransportVehicleData vehicleData,
                DynamicBuffer<Resources> targetResources,
                TransportType transportType,
                ref int sourceStoredAmount,
                Entity source
            )
            {
                int num = vehicleData.m_CargoCapacity;
                int num2 = -1;
                for (int i = 0; i < targetResources.Length; i++)
                {
                    Resources resources = targetResources[i];
                    num -= resources.m_Amount;
                    num2 = math.select(num2, i, resources.m_Resource == resource);
                }
                int num3 = math.min(num, math.min(requestNewAmount, math.max(sourceStoredAmount, 0)));
                if (num3 == 0)
                {
                    return;
                }
                if (num2 >= 0)
                {
                    Resources value = targetResources[num2];
                    value.m_Amount += num3;
                    targetResources[num2] = value;
                }
                else
                {
                    if (targetResources.Length >= vehicleData.m_MaxResourceCount || (vehicleData.m_Resources & resource) == Resource.NoResource)
                    {
                        return;
                    }
                    targetResources.Add(new Resources { m_Resource = resource, m_Amount = num3 });
                }
                requestNewAmount -= num3;
                sourceStoredAmount -= num3;
                if (m_ShouldTrackTransportedResource && num3 != 0)
                {
                    m_TransportedResourceQueue.Enqueue(new TransportedResource { m_Amount = num3, m_CargoTransport = source });
                }
                if (!m_OutsideConnections.HasComponent(source))
                {
                    m_TransportUsageQueue.Enqueue(
                        new TransportUsageEvent
                        {
                            m_Building = source,
                            m_TransportedCargo = num3,
                            m_TransportType = transportType,
                        }
                    );
                }
                switch (transportType)
                {
                    case TransportType.Train:
                        m_StatisticsEventQueue.Enqueue(new StatisticsEvent { m_Statistic = StatisticType.CargoCountTrain, m_Change = num3 });
                        break;
                    case TransportType.Ship:
                        m_StatisticsEventQueue.Enqueue(new StatisticsEvent { m_Statistic = StatisticType.CargoCountShip, m_Change = num3 });
                        break;
                    case TransportType.Airplane:
                        m_StatisticsEventQueue.Enqueue(new StatisticsEvent { m_Statistic = StatisticType.CargoCountAirplane, m_Change = num3 });
                        break;
                }
            }

            private TransportType GetVehicleType(Entity vehicle)
            {
                if (m_BoardingLookupData.m_AircraftData.HasComponent(vehicle))
                {
                    return TransportType.Airplane;
                }
                if (m_BoardingLookupData.m_TrainData.HasComponent(vehicle))
                {
                    return TransportType.Train;
                }
                if (m_BoardingLookupData.m_WatercraftData.HasComponent(vehicle))
                {
                    return TransportType.Ship;
                }
                if (m_BoardingLookupData.m_DeliveryTruckData.HasComponent(vehicle))
                {
                    return TransportType.Bus;
                }
                return TransportType.None;
            }
        }
    }
}
