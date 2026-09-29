using System.Runtime.CompilerServices;
using System.Threading;
using BoardingController.Components;
using BoardingController.Utils;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Creatures;
using Game.Debug;
using Game.Economy;
using Game.Events;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Reflection;
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
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace BoardingController.Systems.Simulation
{
    public partial class PatchedResidentAISystem : GameSystemBase
    {
        private enum DeletedResidentType
        {
            StuckLoop,
            NoPathToHome,
            NoPathToHome_AlreadyOutside,
            WaitingHome_AlreadyOutside,
            NoPath_AlreadyMovingAway,
            InvalidVehicleTarget,
            Dead,
            Count,
        }

        [BurstCompile]
        private struct ResidentTickJob : IJobChunk
        {
            private struct TransportEstimateBuffer : RouteUtils.ITransportEstimateBuffer
            {
                public NativeQueue<ResidentAISystem.Boarding>.ParallelWriter m_BoardingQueue;

                public void AddWaitEstimate(Entity waypoint, int seconds)
                {
                    m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.WaitTimeEstimate(waypoint, seconds));
                }
            }

            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public ComponentTypeHandle<CurrentVehicle> m_CurrentVehicleType;

            [ReadOnly]
            public ComponentTypeHandle<GroupMember> m_GroupMemberType;

            [ReadOnly]
            public ComponentTypeHandle<Unspawned> m_UnspawnedType;

            [ReadOnly]
            public ComponentTypeHandle<HumanNavigation> m_HumanNavigationType;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

            [ReadOnly]
            public BufferTypeHandle<GroupCreature> m_GroupCreatureType;

            public ComponentTypeHandle<Game.Creatures.Resident> m_ResidentType;

            public ComponentTypeHandle<Creature> m_CreatureType;

            [NativeDisableContainerSafetyRestriction]
            public ComponentTypeHandle<Human> m_HumanType;

            public ComponentTypeHandle<HumanCurrentLane> m_CurrentLaneType;

            [NativeDisableContainerSafetyRestriction]
            public ComponentTypeHandle<Target> m_TargetType;

            public ComponentTypeHandle<Divert> m_DivertType;

            [ReadOnly]
            public EntityStorageInfoLookup m_EntityLookup;

            [ReadOnly]
            public ComponentLookup<Game.Objects.Transform> m_TransformData;

            [ReadOnly]
            public ComponentLookup<Owner> m_OwnerData;

            [NativeDisableContainerSafetyRestriction]
            [ReadOnly]
            public ComponentLookup<Target> m_TargetData;

            [ReadOnly]
            public ComponentLookup<PseudoRandomSeed> m_PseudoRandomSeedData;

            [ReadOnly]
            public ComponentLookup<CurrentVehicle> m_CurrentVehicleData;

            [ReadOnly]
            public ComponentLookup<Destroyed> m_DestroyedData;

            [ReadOnly]
            public ComponentLookup<Deleted> m_DeletedData;

            [ReadOnly]
            public ComponentLookup<Unspawned> m_UnspawnedData;

            [ReadOnly]
            public ComponentLookup<RideNeeder> m_RideNeederData;

            [ReadOnly]
            public ComponentLookup<Dispatched> m_Dispatched;

            [ReadOnly]
            public ComponentLookup<ServiceRequest> m_ServiceRequestData;

            [ReadOnly]
            public ComponentLookup<Moving> m_MovingData;

            [ReadOnly]
            public ComponentLookup<Game.Objects.SpawnLocation> m_SpawnLocation;

            [ReadOnly]
            public ComponentLookup<Animal> m_AnimalData;

            [ReadOnly]
            public ComponentLookup<OnFire> m_OnFireData;

            [ReadOnly]
            public ComponentLookup<Game.Net.Edge> m_EdgeData;

            [ReadOnly]
            public ComponentLookup<Curve> m_CurveData;

            [ReadOnly]
            public ComponentLookup<Lane> m_LaneData;

            [ReadOnly]
            public ComponentLookup<EdgeLane> m_EdgeLaneData;

            [ReadOnly]
            public ComponentLookup<Game.Net.ParkingLane> m_ParkingLaneData;

            [ReadOnly]
            public ComponentLookup<GarageLane> m_GarageLaneData;

            [ReadOnly]
            public ComponentLookup<Game.Net.PedestrianLane> m_PedestrianLaneData;

            [ReadOnly]
            public ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneData;

            [ReadOnly]
            public ComponentLookup<LaneSignal> m_LaneSignalData;

            [ReadOnly]
            public ComponentLookup<HangaroundLocation> m_HangaroundLocationData;

            [ReadOnly]
            public ComponentLookup<Citizen> m_CitizenData;

            [ReadOnly]
            public ComponentLookup<HouseholdMember> m_HouseholdMembers;

            [ReadOnly]
            public ComponentLookup<Household> m_HouseholdData;

            [ReadOnly]
            public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

            [ReadOnly]
            public ComponentLookup<CurrentTransport> m_CurrentTransportData;

            [ReadOnly]
            public ComponentLookup<Worker> m_WorkerData;

            [ReadOnly]
            public ComponentLookup<CarKeeper> m_CarKeeperData;

            [ReadOnly]
            public ComponentLookup<BicycleOwner> m_BicycleOwnerData;

            [ReadOnly]
            public ComponentLookup<HealthProblem> m_HealthProblemData;

            [ReadOnly]
            public ComponentLookup<TravelPurpose> m_TravelPurposeData;

            [ReadOnly]
            public ComponentLookup<TouristHousehold> m_TouristHouseholds;

            [ReadOnly]
            public ComponentLookup<HomelessHousehold> m_HomelessHouseholdData;

            [ReadOnly]
            public ComponentLookup<HouseholdNeed> m_HouseholdNeedData;

            [ReadOnly]
            public ComponentLookup<AttendingMeeting> m_AttendingMeetingData;

            [ReadOnly]
            public ComponentLookup<CoordinatedMeeting> m_CoordinatedMeetingData;

            [ReadOnly]
            public ComponentLookup<MovingAway> m_MovingAwayData;

            [ReadOnly]
            public ComponentLookup<ServiceAvailable> m_ServiceAvailableData;

            [ReadOnly]
            public ComponentLookup<ParkedCar> m_ParkedCarData;

            [ReadOnly]
            public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;

            [ReadOnly]
            public ComponentLookup<Game.Vehicles.Taxi> m_TaxiData;

            [ReadOnly]
            public ComponentLookup<Game.Vehicles.PublicTransport> m_PublicTransportData;

            [ReadOnly]
            public ComponentLookup<Game.Vehicles.PoliceCar> m_PoliceCarData;

            [ReadOnly]
            public ComponentLookup<Game.Vehicles.Ambulance> m_AmbulanceData;

            [ReadOnly]
            public ComponentLookup<Game.Vehicles.Hearse> m_HearseData;

            [ReadOnly]
            public ComponentLookup<Controller> m_ControllerData;

            [ReadOnly]
            public ComponentLookup<Vehicle> m_VehicleData;

            [ReadOnly]
            public ComponentLookup<Train> m_TrainData;

            [ReadOnly]
            public ComponentLookup<PropertyRenter> m_PropertyRenters;

            [ReadOnly]
            public ComponentLookup<AttractivenessProvider> m_AttractivenessProviderData;

            [ReadOnly]
            public ComponentLookup<Connected> m_RouteConnectedData;

            [ReadOnly]
            public ComponentLookup<BoardingVehicle> m_BoardingVehicleData;

            [ReadOnly]
            public ComponentLookup<CurrentRoute> m_CurrentRouteData;

            [ReadOnly]
            public ComponentLookup<TransportLine> m_TransportLineData;

            [ReadOnly]
            public ComponentLookup<AccessLane> m_AccessLaneLaneData;

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;

            [ReadOnly]
            public ComponentLookup<CreatureData> m_PrefabCreatureData;

            [ReadOnly]
            public ComponentLookup<HumanData> m_PrefabHumanData;

            [ReadOnly]
            public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;

            [ReadOnly]
            public ComponentLookup<CarData> m_PrefabCarData;

            [ReadOnly]
            public ComponentLookup<IndustrialProcessData> m_PrefabIndustrialProcessData;

            [ReadOnly]
            public ComponentLookup<TransportStopData> m_PrefabTransportStopData;

            [ReadOnly]
            public ComponentLookup<SpawnLocationData> m_PrefabSpawnLocationData;

            [ReadOnly]
            public BufferLookup<HouseholdAnimal> m_HouseholdAnimals;

            [ReadOnly]
            public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

            [ReadOnly]
            public BufferLookup<ConnectedRoute> m_ConnectedRoutes;

            [ReadOnly]
            public BufferLookup<LayoutElement> m_VehicleLayouts;

            [ReadOnly]
            public BufferLookup<CarNavigationLane> m_CarNavigationLanes;

            [ReadOnly]
            public BufferLookup<ConnectedEdge> m_ConnectedEdges;

            [ReadOnly]
            public BufferLookup<Game.Net.SubLane> m_SubLanes;

            [ReadOnly]
            public BufferLookup<Game.Areas.Node> m_AreaNodes;

            [ReadOnly]
            public BufferLookup<Triangle> m_AreaTriangles;

            [ReadOnly]
            public BufferLookup<ConnectedBuilding> m_ConnectedBuildings;

            [ReadOnly]
            public BufferLookup<Renter> m_Renters;

            [ReadOnly]
            public BufferLookup<SpawnLocationElement> m_SpawnLocationElements;

            [ReadOnly]
            public BufferLookup<Game.Economy.Resources> m_Resources;

            [ReadOnly]
            public BufferLookup<ServiceDispatch> m_ServiceDispatches;

            [ReadOnly]
            public BufferLookup<ActivityLocationElement> m_PrefabActivityLocationElements;

            [ReadOnly]
            public ComponentLookup<CurrentDistrict> m_CurrentDistrictData;

            [ReadOnly]
            public BufferLookup<DistrictModifier> m_DistrictModifiers;

            [NativeDisableContainerSafetyRestriction]
            public ComponentLookup<Human> m_HumanData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<PathOwner> m_PathOwnerData;

            [NativeDisableParallelForRestriction]
            public BufferLookup<PathElement> m_PathElements;

            [ReadOnly]
            public float m_TimeOfDay;

            [ReadOnly]
            public RandomSeed m_RandomSeed;

            [ReadOnly]
            public uint m_SimulationFrameIndex;

            [ReadOnly]
            public bool m_LefthandTraffic;

            [ReadOnly]
            public bool m_GroupMember;

            [ReadOnly]
            public PersonalCarSelectData m_PersonalCarSelectData;

            [ReadOnly]
            public EntityArchetype m_ResetTripArchetype;

            [ReadOnly]
            public ComponentTypeSet m_ParkedToMovingCarRemoveTypes;

            [ReadOnly]
            public ComponentTypeSet m_ParkedToMovingCarAddTypes;

            [ReadOnly]
            public ComponentTypeSet m_ParkedToMovingTrailerAddTypes;

            [ReadOnly]
            public NativeArray<int> m_DeletedResidents;

            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

            public NativeQueue<ResidentAISystem.Boarding>.ParallelWriter m_BoardingQueue;

            public NativeQueue<ResidentAISystem.ResidentAction>.ParallelWriter m_ActionQueue;

            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            [ReadOnly]
            public ComponentLookup<Waypoint> m_WaypointLookup;

            [ReadOnly]
            public ComponentLookup<CustomWaypoint> m_CustomWaypointLookup;

            [ReadOnly]
            public BufferLookup<RouteWaypoint> m_RouteWaypointLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabRefType);
                NativeArray<Creature> nativeArray3 = chunk.GetNativeArray(ref m_CreatureType);
                NativeArray<Game.Creatures.Resident> nativeArray4 = chunk.GetNativeArray(ref m_ResidentType);
                NativeArray<Target> nativeArray5 = chunk.GetNativeArray(ref m_TargetType);
                NativeArray<CurrentVehicle> nativeArray6 = chunk.GetNativeArray(ref m_CurrentVehicleType);
                NativeArray<HumanCurrentLane> nativeArray7 = chunk.GetNativeArray(ref m_CurrentLaneType);
                NativeArray<HumanNavigation> nativeArray8 = chunk.GetNativeArray(ref m_HumanNavigationType);
                NativeArray<Divert> nativeArray9 = chunk.GetNativeArray(ref m_DivertType);
                Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
                if (m_GroupMember)
                {
                    NativeArray<GroupMember> nativeArray10 = chunk.GetNativeArray(ref m_GroupMemberType);
                    RefRW<Human> refRW;
                    if (nativeArray6.Length != 0)
                    {
                        for (int i = 0; i < nativeArray.Length; i++)
                        {
                            Entity entity = nativeArray[i];
                            PrefabRef prefabRef = nativeArray2[i];
                            Game.Creatures.Resident resident = nativeArray4[i];
                            Creature creature = nativeArray3[i];
                            CurrentVehicle currentVehicle = nativeArray6[i];
                            Target target = nativeArray5[i];
                            GroupMember groupMember = nativeArray10[i];
                            CollectionUtils.TryGet(nativeArray7, i, out var value);
                            CollectionUtils.TryGet(nativeArray8, i, out var value2);
                            CollectionUtils.TryGet(nativeArray9, i, out var value3);
                            PathOwner pathOwner = m_PathOwnerData[entity];
                            refRW = m_HumanData.GetRefRW(entity);
                            ref Human valueRW = ref refRW.ValueRW;
                            TickGroupMemberInVehicle(
                                unfilteredChunkIndex,
                                ref random,
                                entity,
                                prefabRef,
                                value2,
                                groupMember,
                                currentVehicle,
                                nativeArray7.Length != 0,
                                ref resident,
                                ref valueRW,
                                ref value,
                                ref pathOwner,
                                ref target,
                                ref value3
                            );
                            TickQueue(ref random, ref resident, ref creature, ref value);
                            m_PathOwnerData[entity] = pathOwner;
                            nativeArray4[i] = resident;
                            nativeArray3[i] = creature;
                            nativeArray5[i] = target;
                            CollectionUtils.TrySet(nativeArray7, i, value);
                            CollectionUtils.TrySet(nativeArray9, i, value3);
                        }
                        return;
                    }
                    bool isUnspawned = chunk.Has(ref m_UnspawnedType);
                    for (int j = 0; j < nativeArray.Length; j++)
                    {
                        Entity entity2 = nativeArray[j];
                        PrefabRef prefabRef2 = nativeArray2[j];
                        Game.Creatures.Resident resident2 = nativeArray4[j];
                        Creature creature2 = nativeArray3[j];
                        HumanNavigation navigation = nativeArray8[j];
                        Target target2 = nativeArray5[j];
                        GroupMember groupMember2 = nativeArray10[j];
                        CollectionUtils.TryGet(nativeArray7, j, out var value4);
                        CollectionUtils.TryGet(nativeArray9, j, out var value5);
                        PathOwner pathOwner2 = m_PathOwnerData[entity2];
                        refRW = m_HumanData.GetRefRW(entity2);
                        ref Human valueRW2 = ref refRW.ValueRW;
                        CreatureUtils.CheckUnspawned(unfilteredChunkIndex, entity2, value4, valueRW2, isUnspawned, m_CommandBuffer);
                        TickGroupMemberWalking(
                            unfilteredChunkIndex,
                            ref random,
                            entity2,
                            prefabRef2,
                            navigation,
                            groupMember2,
                            ref resident2,
                            ref creature2,
                            ref valueRW2,
                            ref value4,
                            ref pathOwner2,
                            ref target2,
                            ref value5
                        );
                        TickQueue(ref random, ref resident2, ref creature2, ref value4);
                        m_PathOwnerData[entity2] = pathOwner2;
                        nativeArray4[j] = resident2;
                        nativeArray3[j] = creature2;
                        nativeArray5[j] = target2;
                        CollectionUtils.TrySet(nativeArray7, j, value4);
                        CollectionUtils.TrySet(nativeArray9, j, value5);
                    }
                    return;
                }
                NativeArray<Human> nativeArray11 = chunk.GetNativeArray(ref m_HumanType);
                BufferAccessor<GroupCreature> bufferAccessor = chunk.GetBufferAccessor(ref m_GroupCreatureType);
                if (nativeArray6.Length != 0)
                {
                    for (int k = 0; k < nativeArray.Length; k++)
                    {
                        Entity entity3 = nativeArray[k];
                        PrefabRef prefabRef3 = nativeArray2[k];
                        Game.Creatures.Resident resident3 = nativeArray4[k];
                        Creature creature3 = nativeArray3[k];
                        Human human = nativeArray11[k];
                        CurrentVehicle currentVehicle2 = nativeArray6[k];
                        Target target3 = nativeArray5[k];
                        CollectionUtils.TryGet(nativeArray7, k, out var value6);
                        CollectionUtils.TryGet(nativeArray8, k, out var value7);
                        CollectionUtils.TryGet(nativeArray9, k, out var value8);
                        CollectionUtils.TryGet(bufferAccessor, k, out var value9);
                        PathOwner pathOwner3 = m_PathOwnerData[entity3];
                        TickInVehicle(
                            unfilteredChunkIndex,
                            ref random,
                            entity3,
                            prefabRef3,
                            value7,
                            currentVehicle2,
                            nativeArray7.Length != 0,
                            ref resident3,
                            ref creature3,
                            ref human,
                            ref value6,
                            ref pathOwner3,
                            ref target3,
                            ref value8,
                            value9
                        );
                        TickQueue(ref random, ref resident3, ref creature3, ref value6);
                        m_PathOwnerData[entity3] = pathOwner3;
                        nativeArray4[k] = resident3;
                        nativeArray3[k] = creature3;
                        nativeArray11[k] = human;
                        nativeArray5[k] = target3;
                        CollectionUtils.TrySet(nativeArray7, k, value6);
                        CollectionUtils.TrySet(nativeArray9, k, value8);
                    }
                    return;
                }
                bool isUnspawned2 = chunk.Has(ref m_UnspawnedType);
                for (int l = 0; l < nativeArray.Length; l++)
                {
                    Entity entity4 = nativeArray[l];
                    PrefabRef prefabRef4 = nativeArray2[l];
                    Game.Creatures.Resident resident4 = nativeArray4[l];
                    Creature creature4 = nativeArray3[l];
                    Human human2 = nativeArray11[l];
                    HumanNavigation navigation2 = nativeArray8[l];
                    Target target4 = nativeArray5[l];
                    CollectionUtils.TryGet(nativeArray7, l, out var value10);
                    CollectionUtils.TryGet(nativeArray9, l, out var value11);
                    CollectionUtils.TryGet(bufferAccessor, l, out var value12);
                    PathOwner pathOwner4 = m_PathOwnerData[entity4];
                    CreatureUtils.CheckUnspawned(unfilteredChunkIndex, entity4, value10, human2, isUnspawned2, m_CommandBuffer);
                    TickWalking(
                        unfilteredChunkIndex,
                        ref random,
                        entity4,
                        prefabRef4,
                        navigation2,
                        isUnspawned2,
                        ref resident4,
                        ref creature4,
                        ref human2,
                        ref value10,
                        ref pathOwner4,
                        ref target4,
                        ref value11,
                        value12
                    );
                    TickQueue(ref random, ref resident4, ref creature4, ref value10);
                    m_PathOwnerData[entity4] = pathOwner4;
                    nativeArray4[l] = resident4;
                    nativeArray3[l] = creature4;
                    nativeArray11[l] = human2;
                    nativeArray5[l] = target4;
                    CollectionUtils.TrySet(nativeArray7, l, value10);
                    CollectionUtils.TrySet(nativeArray9, l, value11);
                }
            }

            private void TickGroupMemberInVehicle(
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity entity,
                PrefabRef prefabRef,
                HumanNavigation navigation,
                GroupMember groupMember,
                CurrentVehicle currentVehicle,
                bool hasCurrentLane,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                ref Divert divert
            )
            {
                if (!m_EntityLookup.Exists(currentVehicle.m_Vehicle))
                {
                    AddDeletedResident(DeletedResidentType.InvalidVehicleTarget);
                    m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                    return;
                }
                Entity entity2 = currentVehicle.m_Vehicle;
                if (m_ControllerData.TryGetComponent(currentVehicle.m_Vehicle, out var componentData) && componentData.m_Controller != Entity.Null)
                {
                    entity2 = componentData.m_Controller;
                }
                if ((currentVehicle.m_Flags & CreatureVehicleFlags.Ready) == 0)
                {
                    if (hasCurrentLane)
                    {
                        if (CreatureUtils.IsStuck(pathOwner))
                        {
                            AddDeletedResident(DeletedResidentType.StuckLoop);
                            m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                            return;
                        }
                        if (!m_CurrentVehicleData.HasComponent(groupMember.m_Leader))
                        {
                            CancelEnterVehicle(entity, currentVehicle.m_Vehicle, ref resident, ref human, ref currentLane, ref pathOwner);
                            return;
                        }
                        if (m_PublicTransportData.TryGetComponent(entity2, out var componentData2))
                        {
                            if (m_SimulationFrameIndex >= componentData2.m_DepartureFrame)
                            {
                                human.m_Flags |= HumanFlags.Run;
                            }
                            if ((componentData2.m_State & PublicTransportFlags.Boarding) == 0 && currentLane.m_Lane == currentVehicle.m_Vehicle)
                            {
                                currentLane.m_Flags |= CreatureLaneFlags.EndReached;
                            }
                        }
                        if (CreatureUtils.ParkingSpaceReached(currentLane) || CreatureUtils.TransportStopReached(currentLane))
                        {
                            SetEnterVehiclePath(entity, currentVehicle.m_Vehicle, groupMember, ref random, ref currentLane, ref pathOwner);
                        }
                        else if (CreatureUtils.PathEndReached(currentLane) || CreatureUtils.RequireNewPath(pathOwner) || resident.m_Timer >= 250)
                        {
                            if (ShouldFinishEnterVehicle(navigation))
                            {
                                FinishEnterVehicle(entity, currentVehicle.m_Vehicle, entity2, ref resident, ref human, ref currentLane);
                                hasCurrentLane = false;
                            }
                            else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Entering) == 0)
                            {
                                currentVehicle.m_Flags |= CreatureVehicleFlags.Entering;
                                m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
                                m_CommandBuffer.AddComponent<BatchesUpdated>(jobIndex, entity, default);
                            }
                        }
                        else if (CreatureUtils.ActionLocationReached(currentLane) && ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
                        {
                            return;
                        }
                    }
                    if (!hasCurrentLane)
                    {
                        currentVehicle.m_Flags &= ~CreatureVehicleFlags.Entering;
                        currentVehicle.m_Flags |= CreatureVehicleFlags.Ready;
                        m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
                    }
                }
                else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Exiting) != 0)
                {
                    if (ShouldFinishExitVehicle(navigation))
                    {
                        FinishExitVehicle(entity, currentVehicle.m_Vehicle, ref currentLane);
                    }
                }
                else
                {
                    if ((resident.m_Flags & ResidentFlags.Disembarking) == 0 && !m_CurrentVehicleData.HasComponent(groupMember.m_Leader))
                    {
                        GroupLeaderDisembarking(entity, ref resident, ref pathOwner);
                    }
                    if ((resident.m_Flags & ResidentFlags.Disembarking) != 0)
                    {
                        ExitVehicle(entity, jobIndex, ref random, entity2, prefabRef, currentVehicle, groupMember, ref resident, ref human, ref divert, ref pathOwner);
                    }
                }
                UpdateMoodFlags(ref random, navigation, hasCurrentLane, ref resident, ref human, ref divert);
            }

            private void TickInVehicle(
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity entity,
                PrefabRef prefabRef,
                HumanNavigation navigation,
                CurrentVehicle currentVehicle,
                bool hasCurrentLane,
                ref Game.Creatures.Resident resident,
                ref Creature creature,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                ref Divert divert,
                DynamicBuffer<GroupCreature> groupCreatures
            )
            {
                if (!m_PrefabRefData.HasComponent(currentVehicle.m_Vehicle))
                {
                    AddDeletedResident(DeletedResidentType.InvalidVehicleTarget);
                    m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                    return;
                }
                if (
                    CreatureUtils.ResetUpdatedPath(ref pathOwner)
                    && CheckPath(jobIndex, entity, prefabRef, ref random, ref creature, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident)
                )
                {
                    FindNewPath(entity, prefabRef, ref random, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
                    return;
                }
                Entity entity2 = currentVehicle.m_Vehicle;
                if (m_ControllerData.TryGetComponent(currentVehicle.m_Vehicle, out var componentData) && componentData.m_Controller != Entity.Null)
                {
                    entity2 = componentData.m_Controller;
                }
                if ((currentVehicle.m_Flags & CreatureVehicleFlags.Ready) == 0)
                {
                    if (hasCurrentLane)
                    {
                        if (CreatureUtils.IsStuck(pathOwner))
                        {
                            AddDeletedResident(DeletedResidentType.StuckLoop);
                            m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                            return;
                        }
                        if (m_PublicTransportData.TryGetComponent(entity2, out var componentData2))
                        {
                            if (m_SimulationFrameIndex >= componentData2.m_DepartureFrame)
                            {
                                human.m_Flags |= HumanFlags.Run;
                            }
                            if ((componentData2.m_State & PublicTransportFlags.Boarding) == 0)
                            {
                                if (!(currentLane.m_Lane == currentVehicle.m_Vehicle))
                                {
                                    CancelEnterVehicle(entity, currentVehicle.m_Vehicle, ref resident, ref human, ref currentLane, ref pathOwner);
                                    return;
                                }
                                currentLane.m_Flags |= CreatureLaneFlags.EndReached;
                            }
                        }
                        if (CreatureUtils.ParkingSpaceReached(currentLane) || CreatureUtils.TransportStopReached(currentLane))
                        {
                            SetEnterVehiclePath(entity, currentVehicle.m_Vehicle, default, ref random, ref currentLane, ref pathOwner);
                        }
                        else if (CreatureUtils.PathEndReached(currentLane) || CreatureUtils.RequireNewPath(pathOwner) || resident.m_Timer >= 250)
                        {
                            if (ShouldFinishEnterVehicle(navigation))
                            {
                                FinishEnterVehicle(entity, currentVehicle.m_Vehicle, entity2, ref resident, ref human, ref currentLane);
                                hasCurrentLane = false;
                            }
                            else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Entering) == 0)
                            {
                                currentVehicle.m_Flags |= CreatureVehicleFlags.Entering;
                                m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
                                m_CommandBuffer.AddComponent<BatchesUpdated>(jobIndex, entity, default);
                            }
                        }
                        else if (CreatureUtils.ActionLocationReached(currentLane) && ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
                        {
                            return;
                        }
                    }
                    if (!hasCurrentLane && HasEveryoneBoarded(groupCreatures))
                    {
                        currentVehicle.m_Flags &= ~CreatureVehicleFlags.Entering;
                        currentVehicle.m_Flags |= CreatureVehicleFlags.Ready;
                        m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
                    }
                }
                else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Exiting) != 0)
                {
                    if (ShouldFinishExitVehicle(navigation))
                    {
                        FinishExitVehicle(entity, currentVehicle.m_Vehicle, ref currentLane);
                    }
                }
                else
                {
                    if ((resident.m_Flags & ResidentFlags.Disembarking) == 0)
                    {
                        if (m_DestroyedData.HasComponent(entity2))
                        {
                            if (!m_MovingData.HasComponent(entity2))
                            {
                                resident.m_Flags |= ResidentFlags.Disembarking;
                                pathOwner.m_State &= ~PathFlags.Failed;
                                pathOwner.m_State |= PathFlags.Obsolete;
                            }
                        }
                        else if (m_PersonalCarData.HasComponent(entity2))
                        {
                            Game.Vehicles.PersonalCar personalCar = m_PersonalCarData[entity2];
                            if ((personalCar.m_State & PersonalCarFlags.Disembarking) != 0)
                            {
                                CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
                            }
                            else if ((personalCar.m_State & PersonalCarFlags.Transporting) != 0)
                            {
                                CurrentVehicleTransporting(entity, entity2, ref pathOwner);
                            }
                        }
                        else if (m_PublicTransportData.HasComponent(entity2))
                        {
                            Game.Vehicles.PublicTransport publicTransport = m_PublicTransportData[entity2];
                            if ((publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
                            {
                                CurrentVehicleBoarding(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
                            }
                            else if ((publicTransport.m_State & (PublicTransportFlags.Testing | PublicTransportFlags.RequireStop)) == PublicTransportFlags.Testing)
                            {
                                CurrentVehicleTesting(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
                            }
                        }
                        else if (m_TaxiData.HasComponent(entity2))
                        {
                            Game.Vehicles.Taxi taxi = m_TaxiData[entity2];
                            if ((taxi.m_State & TaxiFlags.Disembarking) != 0)
                            {
                                CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
                            }
                            else if ((taxi.m_State & TaxiFlags.Transporting) != 0)
                            {
                                CurrentVehicleTransporting(entity, entity2, ref pathOwner);
                            }
                        }
                        else if (m_PoliceCarData.HasComponent(entity2))
                        {
                            if ((m_PoliceCarData[entity2].m_State & PoliceCarFlags.Disembarking) != 0)
                            {
                                CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
                            }
                            else
                            {
                                CurrentVehicleTransporting(entity, entity2, ref pathOwner);
                            }
                        }
                        else if (m_AmbulanceData.HasComponent(entity2))
                        {
                            if ((m_AmbulanceData[entity2].m_State & AmbulanceFlags.Disembarking) != 0)
                            {
                                CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
                            }
                            else
                            {
                                CurrentVehicleTransporting(entity, entity2, ref pathOwner);
                            }
                        }
                        else if (m_HearseData.HasComponent(entity2))
                        {
                            if ((m_HearseData[entity2].m_State & HearseFlags.Disembarking) != 0)
                            {
                                CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
                            }
                            else
                            {
                                CurrentVehicleTransporting(entity, entity2, ref pathOwner);
                            }
                        }
                    }
                    if ((resident.m_Flags & ResidentFlags.Disembarking) != 0)
                    {
                        ExitVehicle(entity, jobIndex, ref random, entity2, prefabRef, currentVehicle, default, ref resident, ref human, ref divert, ref pathOwner);
                    }
                    else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Leader) == 0)
                    {
                        currentVehicle.m_Flags |= CreatureVehicleFlags.Leader;
                        m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
                    }
                }
                UpdateMoodFlags(ref random, navigation, hasCurrentLane, ref resident, ref human, ref divert);
            }

            private static bool ShouldFinishEnterVehicle(HumanNavigation humanNavigation)
            {
                if (humanNavigation.m_TargetActivity == 10)
                {
                    if (humanNavigation.m_LastActivity == humanNavigation.m_TargetActivity)
                    {
                        return humanNavigation.m_TransformState != TransformState.Action;
                    }
                    return false;
                }
                return true;
            }

            private static bool ShouldFinishExitVehicle(HumanNavigation humanNavigation)
            {
                if (humanNavigation.m_TargetActivity == 11)
                {
                    if (humanNavigation.m_LastActivity == humanNavigation.m_TargetActivity)
                    {
                        return humanNavigation.m_TransformState != TransformState.Action;
                    }
                    return false;
                }
                return true;
            }

            private void TickGroupMemberWalking(
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity entity,
                PrefabRef prefabRef,
                HumanNavigation navigation,
                GroupMember groupMember,
                ref Game.Creatures.Resident resident,
                ref Creature creature,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                ref Divert divert
            )
            {
                if ((resident.m_Flags & ResidentFlags.Disembarking) != 0)
                {
                    resident.m_Flags &= ~ResidentFlags.Disembarking;
                }
                else if (divert.m_Purpose == Purpose.None && !m_EntityLookup.Exists(target.m_Target))
                {
                    if (
                        m_HealthProblemData.TryGetComponent(resident.m_Citizen, out var componentData)
                        && (componentData.m_Flags & HealthProblemFlags.RequireTransport) != 0
                        && (componentData.m_Flags & (HealthProblemFlags.Dead | HealthProblemFlags.Injured)) != 0
                    )
                    {
                        if ((componentData.m_Flags & HealthProblemFlags.Dead) != 0)
                        {
                            AddDeletedResident(DeletedResidentType.Dead);
                            m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                        }
                        else
                        {
                            SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.None, Entity.Null);
                            WaitHere(entity, ref currentLane, ref pathOwner);
                        }
                        return;
                    }
                    if (ReturnHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner))
                    {
                        return;
                    }
                }
                else
                {
                    if (CreatureUtils.IsStuck(pathOwner))
                    {
                        AddDeletedResident(DeletedResidentType.StuckLoop);
                        m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                        return;
                    }
                    if (CreatureUtils.ActionLocationReached(currentLane) && ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
                    {
                        return;
                    }
                }
                if ((resident.m_Flags & ResidentFlags.Arrived) == 0 && m_HumanData.TryGetComponent(groupMember.m_Leader, out var componentData2))
                {
                    human.m_Flags |= componentData2.m_Flags & (HumanFlags.Run | HumanFlags.Emergency);
                }
                UpdateMoodFlags(ref random, navigation, hasCurrentLane: true, ref resident, ref human, ref divert);
                if (!m_CurrentVehicleData.HasComponent(groupMember.m_Leader) || (currentLane.m_Flags & CreatureLaneFlags.EndReached) == 0)
                {
                    return;
                }
                CurrentVehicle currentVehicle = m_CurrentVehicleData[groupMember.m_Leader];
                Game.Objects.Transform transform = m_TransformData[entity];
                Entity vehicle = currentVehicle.m_Vehicle;
                if (m_ControllerData.HasComponent(currentVehicle.m_Vehicle))
                {
                    Controller controller = m_ControllerData[currentVehicle.m_Vehicle];
                    if (controller.m_Controller != Entity.Null)
                    {
                        vehicle = controller.m_Controller;
                    }
                }
                m_BoardingQueue.Enqueue(
                    ResidentAISystem.Boarding.TryEnterVehicle(
                        entity,
                        groupMember.m_Leader,
                        vehicle,
                        currentVehicle.m_Vehicle,
                        Entity.Null,
                        transform.m_Position,
                        (CreatureVehicleFlags)0u
                    )
                );
            }

            private void TickWalking(
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity entity,
                PrefabRef prefabRef,
                HumanNavigation navigation,
                bool isUnspawned,
                ref Game.Creatures.Resident resident,
                ref Creature creature,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                ref Divert divert,
                DynamicBuffer<GroupCreature> groupCreatures
            )
            {
                if (
                    CreatureUtils.ResetUpdatedPath(ref pathOwner)
                    && CheckPath(jobIndex, entity, prefabRef, ref random, ref creature, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident)
                )
                {
                    FindNewPath(entity, prefabRef, ref random, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
                    return;
                }
                if (
                    CreatureUtils.ResetUncheckedLane(ref currentLane)
                    && CheckLane(jobIndex, entity, prefabRef, ref random, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident)
                )
                {
                    FindNewPath(entity, prefabRef, ref random, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
                    return;
                }
                UpdateMoodFlags(ref random, navigation, hasCurrentLane: true, ref resident, ref human, ref divert);
                if ((resident.m_Flags & ResidentFlags.Disembarking) != 0)
                {
                    resident.m_Flags &= ~ResidentFlags.Disembarking;
                }
                else if (divert.m_Purpose == Purpose.None && !m_EntityLookup.Exists(target.m_Target))
                {
                    if (
                        HandleHealthProblem(jobIndex, entity, ref resident, ref currentLane, ref pathOwner)
                        || ReturnHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner)
                    )
                    {
                        return;
                    }
                }
                else if (CreatureUtils.PathfindFailed(pathOwner))
                {
                    if (CreatureUtils.IsStuck(pathOwner))
                    {
                        AddDeletedResident(DeletedResidentType.StuckLoop);
                        m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                        return;
                    }
                    if (divert.m_Purpose != Purpose.None)
                    {
                        SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.None, Entity.Null, 0, Resource.NoResource);
                    }
                    else if (ReturnHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner))
                    {
                        return;
                    }
                }
                else if (CreatureUtils.EndReached(currentLane))
                {
                    if (CreatureUtils.PathEndReached(currentLane))
                    {
                        if (PathEndReached(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner))
                        {
                            return;
                        }
                    }
                    else if (CreatureUtils.ParkingSpaceReached(currentLane))
                    {
                        if (ParkingSpaceReached(jobIndex, ref random, entity, ref resident, ref currentLane, ref pathOwner, ref target, groupCreatures))
                        {
                            return;
                        }
                    }
                    else if (CreatureUtils.TransportStopReached(currentLane))
                    {
                        if (TransportStopReached(jobIndex, ref random, entity, prefabRef, isUnspawned, ref resident, ref currentLane, ref pathOwner, ref target))
                        {
                            return;
                        }
                    }
                    else if (CreatureUtils.ActionLocationReached(currentLane) && ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
                    {
                        return;
                    }
                }
                else if (currentLane.m_QueueArea.radius > 0f)
                {
                    QueueReached(entity, ref resident, ref currentLane, ref pathOwner);
                }
                if (CreatureUtils.RequireNewPath(pathOwner))
                {
                    FindNewPath(entity, prefabRef, ref random, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
                }
            }

            private bool HandleHealthProblem(int jobIndex, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
            {
                if (!m_HealthProblemData.TryGetComponent(resident.m_Citizen, out var componentData))
                {
                    return false;
                }
                if ((componentData.m_Flags & HealthProblemFlags.RequireTransport) == 0)
                {
                    return false;
                }
                if ((componentData.m_Flags & HealthProblemFlags.Dead) != 0)
                {
                    AddDeletedResident(DeletedResidentType.Dead);
                    m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                    return true;
                }
                if ((componentData.m_Flags & HealthProblemFlags.Injured) != 0)
                {
                    WaitHere(entity, ref currentLane, ref pathOwner);
                    return true;
                }
                if (
                    (componentData.m_Flags & HealthProblemFlags.Sick) != 0
                    && m_CurrentBuildingData.TryGetComponent(resident.m_Citizen, out var componentData2)
                    && componentData2.m_CurrentBuilding != Entity.Null
                )
                {
                    WaitHere(entity, ref currentLane, ref pathOwner);
                    return true;
                }
                return false;
            }

            private void UpdateMoodFlags(
                ref Unity.Mathematics.Random random,
                HumanNavigation navigation,
                bool hasCurrentLane,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref Divert divert
            )
            {
                if (hasCurrentLane && (resident.m_Flags & ResidentFlags.Arrived) == 0 && navigation.m_MaxSpeed < 0.1f)
                {
                    if ((human.m_Flags & HumanFlags.Waiting) == 0 && random.NextInt(10) == 0)
                    {
                        human.m_Flags |= HumanFlags.Waiting;
                    }
                }
                else
                {
                    human.m_Flags &= ~HumanFlags.Waiting;
                }
                if (divert.m_Purpose == Purpose.Safety)
                {
                    human.m_Flags |= HumanFlags.Angry;
                }
                else if ((human.m_Flags & HumanFlags.Angry) != 0 && random.NextInt(10) == 0)
                {
                    human.m_Flags &= ~HumanFlags.Angry;
                }
                if (random.NextInt(100) == 0)
                {
                    int num = random.NextInt(20, 40);
                    int num2 = random.NextInt(60, 80);
                    int num3 = ((!m_CitizenData.TryGetComponent(resident.m_Citizen, out var componentData)) ? random.NextInt(101) : componentData.Happiness);
                    if (num3 < num)
                    {
                        human.m_Flags &= ~HumanFlags.Happy;
                        human.m_Flags |= HumanFlags.Sad;
                    }
                    else if (componentData.Happiness > num2)
                    {
                        human.m_Flags &= ~HumanFlags.Sad;
                        human.m_Flags |= HumanFlags.Happy;
                    }
                    else
                    {
                        human.m_Flags &= ~(HumanFlags.Sad | HumanFlags.Happy);
                    }
                }
            }

            private void SetEnterVehiclePath(
                Entity entity,
                Entity vehicle,
                GroupMember groupMember,
                ref Unity.Mathematics.Random random,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner
            )
            {
                currentLane.m_Flags &= ~(CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Transport | CreatureLaneFlags.Taxi);
                DynamicBuffer<PathElement> path = m_PathElements[entity];
                if (groupMember.m_Leader != Entity.Null)
                {
                    path.Clear();
                    path.Add(new PathElement(vehicle, 0f));
                    pathOwner.m_ElementIndex = 0;
                }
                else
                {
                    if (path.Length > pathOwner.m_ElementIndex && path[pathOwner.m_ElementIndex].m_Target == vehicle)
                    {
                        return;
                    }
                    if (pathOwner.m_ElementIndex > 0)
                    {
                        path[--pathOwner.m_ElementIndex] = new PathElement(vehicle, 0f);
                    }
                    else
                    {
                        path.Insert(pathOwner.m_ElementIndex, new PathElement(vehicle, 0f));
                    }
                }
                if (m_TransformData.HasComponent(vehicle) && m_LaneData.HasComponent(currentLane.m_Lane))
                {
                    float3 position = m_TransformData[vehicle].m_Position;
                    if (pathOwner.m_ElementIndex > 0)
                    {
                        path[--pathOwner.m_ElementIndex] = new PathElement(currentLane.m_Lane, currentLane.m_CurvePosition.y);
                    }
                    else
                    {
                        path.Insert(pathOwner.m_ElementIndex, new PathElement(currentLane.m_Lane, currentLane.m_CurvePosition.y));
                    }
                    CreatureUtils.FixEnterPath(
                        ref random,
                        position,
                        pathOwner.m_ElementIndex,
                        path,
                        ref m_OwnerData,
                        ref m_LaneData,
                        ref m_EdgeLaneData,
                        ref m_ConnectionLaneData,
                        ref m_CurveData,
                        ref m_SubLanes,
                        ref m_AreaNodes,
                        ref m_AreaTriangles
                    );
                }
            }

            private unsafe void AddDeletedResident(DeletedResidentType type)
            {
                Interlocked.Increment(ref UnsafeUtility.ArrayElementAsRef<int>(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(m_DeletedResidents), (int)type));
            }

            private void TickQueue(ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref Creature creature, ref HumanCurrentLane currentLane)
            {
                resident.m_Timer += random.NextInt(1, 3);
                if (currentLane.m_QueueArea.radius > 0f)
                {
                    if (
                        (creature.m_QueueArea.radius == 0f || currentLane.m_QueueEntity != creature.m_QueueEntity)
                        && (m_RouteConnectedData.HasComponent(currentLane.m_QueueEntity) || m_BoardingVehicleData.HasComponent(currentLane.m_QueueEntity))
                        && (resident.m_Flags & ResidentFlags.WaitingTransport) == 0
                    )
                    {
                        resident.m_Flags |= ResidentFlags.WaitingTransport;
                        resident.m_Timer = 0;
                    }
                    creature.m_QueueEntity = currentLane.m_QueueEntity;
                    creature.m_QueueArea = currentLane.m_QueueArea;
                }
                else
                {
                    creature.m_QueueEntity = Entity.Null;
                    creature.m_QueueArea = default;
                }
            }

            private void QueueReached(Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
            {
                if ((resident.m_Flags & ResidentFlags.WaitingTransport) != 0 && resident.m_Timer >= 5000)
                {
                    m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.WaitTimeExceeded(entity, currentLane.m_QueueEntity));
                    if (m_BoardingVehicleData.HasComponent(currentLane.m_QueueEntity))
                    {
                        resident.m_Flags |= ResidentFlags.IgnoreTaxi;
                    }
                    else
                    {
                        resident.m_Flags |= ResidentFlags.IgnoreTransport;
                    }
                    pathOwner.m_State &= ~PathFlags.Failed;
                    pathOwner.m_State |= PathFlags.Obsolete;
                }
            }

            private void WaitHere(Entity entity, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
            {
                currentLane.m_CurvePosition.y = currentLane.m_CurvePosition.x;
                pathOwner.m_ElementIndex = 0;
                m_PathElements[entity].Clear();
            }

            private void ExitVehicle(
                Entity entity,
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity controllerVehicle,
                PrefabRef prefabRef,
                CurrentVehicle currentVehicle,
                GroupMember groupMember,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                Entity household = GetHousehold(resident);
                int ticketPrice = 0;
                if ((currentVehicle.m_Flags & CreatureVehicleFlags.Leader) != 0 && m_TaxiData.HasComponent(controllerVehicle))
                {
                    Game.Vehicles.Taxi taxi = m_TaxiData[controllerVehicle];
                    ticketPrice = math.select(taxi.m_CurrentFee, -taxi.m_CurrentFee, (taxi.m_State & TaxiFlags.FromOutside) != 0);
                }
                if (m_TransformData.HasComponent(currentVehicle.m_Vehicle))
                {
                    Game.Objects.Transform transform = m_TransformData[currentVehicle.m_Vehicle];
                    Game.Objects.Transform transform2 = transform;
                    DynamicBuffer<PathElement> path = m_PathElements[entity];
                    HumanCurrentLane newCurrentLane = default;
                    float3 targetPosition = transform2.m_Position;
                    if (pathOwner.m_ElementIndex < path.Length && (pathOwner.m_State & PathFlags.Obsolete) == 0)
                    {
                        PathElement pathElement = path[pathOwner.m_ElementIndex];
                        Game.Objects.Transform componentData2;
                        if (m_CurveData.TryGetComponent(pathElement.m_Target, out var componentData))
                        {
                            targetPosition = MathUtils.Position(componentData.m_Bezier, pathElement.m_TargetDelta.x);
                        }
                        else if (m_TransformData.TryGetComponent(pathElement.m_Target, out componentData2))
                        {
                            targetPosition = componentData2.m_Position;
                        }
                    }
                    BufferLookup<SubMeshGroup> subMeshGroupBuffers = default;
                    BufferLookup<CharacterElement> characterElementBuffers = default;
                    BufferLookup<SubMesh> subMeshBuffers = default;
                    BufferLookup<AnimationClip> animationClipBuffers = default;
                    BufferLookup<AnimationMotion> animationMotionBuffers = default;
                    bool isDriver = (currentVehicle.m_Flags & CreatureVehicleFlags.Driver) != 0;
                    ActivityCondition conditions = CreatureUtils.GetConditions(human);
                    m_PseudoRandomSeedData.TryGetComponent(entity, out var componentData3);
                    transform2 = CreatureUtils.GetVehicleDoorPosition(
                        ref random,
                        ActivityType.Exit,
                        conditions,
                        transform,
                        componentData3,
                        targetPosition,
                        isDriver,
                        m_LefthandTraffic,
                        prefabRef.m_Prefab,
                        currentVehicle.m_Vehicle,
                        default,
                        ref m_PublicTransportData,
                        ref m_TrainData,
                        ref m_ControllerData,
                        ref m_PrefabRefData,
                        ref m_PrefabCarData,
                        ref m_PrefabActivityLocationElements,
                        ref subMeshGroupBuffers,
                        ref characterElementBuffers,
                        ref subMeshBuffers,
                        ref animationClipBuffers,
                        ref animationMotionBuffers,
                        out var activityMask,
                        out var _
                    );
                    if (pathOwner.m_ElementIndex < path.Length && (pathOwner.m_State & PathFlags.Obsolete) == 0)
                    {
                        CreatureUtils.FixPathStart(
                            ref random,
                            transform2.m_Position,
                            pathOwner.m_ElementIndex,
                            path,
                            ref m_OwnerData,
                            ref m_LaneData,
                            ref m_EdgeLaneData,
                            ref m_ConnectionLaneData,
                            ref m_CurveData,
                            ref m_SubLanes,
                            ref m_AreaNodes,
                            ref m_AreaTriangles
                        );
                        PathElement pathElement2 = path[pathOwner.m_ElementIndex];
                        CreatureLaneFlags creatureLaneFlags = (CreatureLaneFlags)0u;
                        if (pathElement2.m_TargetDelta.y < pathElement2.m_TargetDelta.x)
                        {
                            creatureLaneFlags |= CreatureLaneFlags.Backward;
                        }
                        if (m_PedestrianLaneData.HasComponent(pathElement2.m_Target))
                        {
                            newCurrentLane = new HumanCurrentLane(pathElement2, creatureLaneFlags);
                        }
                        else if (m_ConnectionLaneData.HasComponent(pathElement2.m_Target))
                        {
                            Game.Net.ConnectionLane connectionLane = m_ConnectionLaneData[pathElement2.m_Target];
                            if ((connectionLane.m_Flags & ConnectionLaneFlags.Area) != 0)
                            {
                                creatureLaneFlags |= CreatureLaneFlags.Area;
                                if (m_OwnerData.HasComponent(pathElement2.m_Target))
                                {
                                    Owner owner = m_OwnerData[pathElement2.m_Target];
                                    if (m_HangaroundLocationData.HasComponent(owner.m_Owner))
                                    {
                                        creatureLaneFlags |= CreatureLaneFlags.Hangaround;
                                    }
                                }
                            }
                            else
                            {
                                creatureLaneFlags |= CreatureLaneFlags.Connection;
                            }
                            if ((connectionLane.m_Flags & ConnectionLaneFlags.Parking) == 0)
                            {
                                newCurrentLane = new HumanCurrentLane(pathElement2, creatureLaneFlags);
                            }
                        }
                        else if (m_SpawnLocation.HasComponent(pathElement2.m_Target))
                        {
                            creatureLaneFlags |= CreatureLaneFlags.TransformTarget;
                            if (++pathOwner.m_ElementIndex >= path.Length)
                            {
                                creatureLaneFlags |= CreatureLaneFlags.EndOfPath;
                            }
                            newCurrentLane = new HumanCurrentLane(pathElement2, creatureLaneFlags);
                        }
                        else if (m_PrefabRefData.HasComponent(pathElement2.m_Target))
                        {
                            creatureLaneFlags |= CreatureLaneFlags.FindLane;
                            newCurrentLane = new HumanCurrentLane(creatureLaneFlags);
                        }
                    }
                    if (newCurrentLane.m_Lane == Entity.Null)
                    {
                        if (m_UnspawnedData.HasComponent(currentVehicle.m_Vehicle))
                        {
                            newCurrentLane.m_Flags |= CreatureLaneFlags.EmergeUnspawned;
                        }
                        if (m_PathOwnerData.TryGetComponent(controllerVehicle, out var componentData4) && VehicleUtils.PathfindFailed(componentData4))
                        {
                            newCurrentLane.m_Flags |= CreatureLaneFlags.EmergeUnspawned;
                            pathOwner.m_State |= PathFlags.Stuck;
                        }
                    }
                    ActivityMask activityMask2 = new ActivityMask(ActivityType.Driving);
                    activityMask2.m_Mask |= new ActivityMask(ActivityType.Biking).m_Mask;
                    if ((activityMask.m_Mask & activityMask2.m_Mask) != 0)
                    {
                        newCurrentLane.m_Flags |= CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached;
                    }
                    m_BoardingQueue.Enqueue(
                        ResidentAISystem.Boarding.ExitVehicle(
                            entity,
                            household,
                            groupMember.m_Leader,
                            currentVehicle.m_Vehicle,
                            newCurrentLane,
                            transform2.m_Position,
                            transform2.m_Rotation,
                            ticketPrice
                        )
                    );
                }
                else
                {
                    Game.Objects.Transform transform3 = m_TransformData[entity];
                    m_BoardingQueue.Enqueue(
                        ResidentAISystem.Boarding.ExitVehicle(
                            entity,
                            household,
                            groupMember.m_Leader,
                            currentVehicle.m_Vehicle,
                            default,
                            transform3.m_Position,
                            transform3.m_Rotation,
                            ticketPrice
                        )
                    );
                    pathOwner.m_State &= ~PathFlags.Failed;
                    pathOwner.m_State |= PathFlags.Obsolete;
                }
                currentVehicle.m_Flags |= CreatureVehicleFlags.Exiting;
                m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
                switch (divert.m_Purpose)
                {
                    case Purpose.None:
                    {
                        if (m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData5) && componentData5.m_Purpose == Purpose.EmergencyShelter)
                        {
                            human.m_Flags |= HumanFlags.Run | HumanFlags.Emergency;
                        }
                        break;
                    }
                    case Purpose.Safety:
                    case Purpose.Escape:
                        human.m_Flags |= HumanFlags.Run;
                        break;
                }
            }

            private bool HasEveryoneBoarded(DynamicBuffer<GroupCreature> group)
            {
                if (group.IsCreated)
                {
                    for (int i = 0; i < group.Length; i++)
                    {
                        Entity creature = group[i].m_Creature;
                        if (!m_CurrentVehicleData.HasComponent(creature))
                        {
                            return false;
                        }
                        if ((m_CurrentVehicleData[creature].m_Flags & CreatureVehicleFlags.Ready) == 0)
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            private bool CheckLane(
                int jobIndex,
                Entity entity,
                PrefabRef prefabRef,
                ref Unity.Mathematics.Random random,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner,
                ref Game.Creatures.Resident resident
            )
            {
                Entity entity2 = Entity.Null;
                if (m_OwnerData.HasComponent(currentLane.m_Lane))
                {
                    entity2 = m_OwnerData[currentLane.m_Lane].m_Owner;
                }
                DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                if (dynamicBuffer.Length > pathOwner.m_ElementIndex)
                {
                    PathElement pathElement = dynamicBuffer[pathOwner.m_ElementIndex];
                    if (m_OwnerData.HasComponent(pathElement.m_Target))
                    {
                        Entity owner = m_OwnerData[pathElement.m_Target].m_Owner;
                        if (owner != entity2)
                        {
                            return FindDivertTargets(
                                jobIndex,
                                entity,
                                prefabRef,
                                ref random,
                                ref human,
                                ref currentLane,
                                ref target,
                                ref divert,
                                ref pathOwner,
                                ref resident,
                                owner,
                                entity2
                            );
                        }
                    }
                }
                return false;
            }

            private bool GetDivertNeeds(
                PrefabRef prefabRef,
                ref Unity.Mathematics.Random random,
                ref Human human,
                ref Game.Creatures.Resident resident,
                ref Divert divert,
                out ActivityMask actionMask,
                out HouseholdNeed householdNeed
            )
            {
                CreatureData creatureData = m_PrefabCreatureData[prefabRef.m_Prefab];
                householdNeed = default;
                actionMask = default;
                if ((human.m_Flags & HumanFlags.Selfies) == 0)
                {
                    actionMask.m_Mask |= creatureData.m_SupportedActivities.m_Mask & new ActivityMask(ActivityType.Selfies).m_Mask;
                }
                if ((resident.m_Flags & ResidentFlags.ActivityDone) != 0)
                {
                    if (random.NextInt(3) != 0)
                    {
                        actionMask = default;
                    }
                    resident.m_Flags &= ~ResidentFlags.ActivityDone;
                }
                bool flag = actionMask.m_Mask != 0;
                if (divert.m_Purpose != Purpose.None)
                {
                    return flag;
                }
                if (m_AttendingMeetingData.HasComponent(resident.m_Citizen))
                {
                    AttendingMeeting attendingMeeting = m_AttendingMeetingData[resident.m_Citizen];
                    if (m_PrefabRefData.HasComponent(attendingMeeting.m_Meeting))
                    {
                        return flag;
                    }
                }
                if (m_CitizenData.HasComponent(resident.m_Citizen))
                {
                    CitizenAge age = m_CitizenData[resident.m_Citizen].GetAge();
                    if ((age == CitizenAge.Adult || age == CitizenAge.Elderly) && m_HouseholdMembers.HasComponent(resident.m_Citizen))
                    {
                        HouseholdMember householdMember = m_HouseholdMembers[resident.m_Citizen];
                        if (m_HouseholdNeedData.HasComponent(householdMember.m_Household))
                        {
                            householdNeed = m_HouseholdNeedData[householdMember.m_Household];
                            flag |= householdNeed.m_Resource != Resource.NoResource;
                        }
                    }
                }
                return flag;
            }

            private bool FindDivertTargets(
                int jobIndex,
                Entity entity,
                PrefabRef prefabRef,
                ref Unity.Mathematics.Random random,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner,
                ref Game.Creatures.Resident resident,
                Entity element,
                Entity ignoreElement
            )
            {
                if (!GetDivertNeeds(prefabRef, ref random, ref human, ref resident, ref divert, out var actionMask, out var householdNeed))
                {
                    return false;
                }
                if (m_ConnectedEdges.HasBuffer(element))
                {
                    DynamicBuffer<ConnectedEdge> dynamicBuffer = m_ConnectedEdges[element];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        Entity edge = dynamicBuffer[i].m_Edge;
                        if (
                            !(edge == ignoreElement)
                            && FindDivertTargets(
                                jobIndex,
                                entity,
                                ref random,
                                ref human,
                                ref currentLane,
                                ref target,
                                ref divert,
                                ref pathOwner,
                                ref resident,
                                edge,
                                ref actionMask,
                                householdNeed
                            )
                        )
                        {
                            return true;
                        }
                    }
                    return false;
                }
                if (m_ConnectedEdges.HasBuffer(ignoreElement))
                {
                    DynamicBuffer<ConnectedEdge> dynamicBuffer2 = m_ConnectedEdges[ignoreElement];
                    for (int j = 0; j < dynamicBuffer2.Length; j++)
                    {
                        if (dynamicBuffer2[j].m_Edge == element)
                        {
                            return false;
                        }
                    }
                }
                return FindDivertTargets(
                    jobIndex,
                    entity,
                    ref random,
                    ref human,
                    ref currentLane,
                    ref target,
                    ref divert,
                    ref pathOwner,
                    ref resident,
                    element,
                    ref actionMask,
                    householdNeed
                );
            }

            private HumanFlags SelectAttractionFlags(ref Unity.Mathematics.Random random, ActivityMask actionMask)
            {
                HumanFlags result = (HumanFlags)0u;
                int count = 0;
                CheckActionFlags(ref result, ref count, ref random, actionMask, ActivityType.Selfies, HumanFlags.Selfies);
                return result;
            }

            private void CheckActionFlags(
                ref HumanFlags result,
                ref int count,
                ref Unity.Mathematics.Random random,
                ActivityMask actionMask,
                ActivityType activityType,
                HumanFlags flags
            )
            {
                if ((actionMask.m_Mask & new ActivityMask(activityType).m_Mask) != 0 && random.NextInt(++count) == 0)
                {
                    result = flags;
                }
            }

            private bool FindDivertTargets(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner,
                ref Game.Creatures.Resident resident,
                Entity element,
                ref ActivityMask actionMask,
                HouseholdNeed householdNeed
            )
            {
                if (m_ConnectedBuildings.HasBuffer(element))
                {
                    DynamicBuffer<ConnectedBuilding> dynamicBuffer = m_ConnectedBuildings[element];
                    int num = random.NextInt(dynamicBuffer.Length);
                    bool flag = actionMask.m_Mask != 0;
                    bool flag2 = householdNeed.m_Resource != Resource.NoResource;
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        int num2 = num + i;
                        num2 = math.select(num2, num2 - dynamicBuffer.Length, num2 >= dynamicBuffer.Length);
                        Entity building = dynamicBuffer[num2].m_Building;
                        if (flag)
                        {
                            int num3 = 0;
                            if (m_AttractivenessProviderData.HasComponent(building))
                            {
                                num3 += m_AttractivenessProviderData[building].m_Attractiveness;
                            }
                            if (m_OnFireData.HasComponent(building))
                            {
                                num3 += Mathf.RoundToInt(m_OnFireData[building].m_Intensity);
                            }
                            if (random.NextInt(10) < num3 && AddPathAction(entity, ref random, ref currentLane, ref pathOwner, building))
                            {
                                human.m_Flags |= SelectAttractionFlags(ref random, actionMask);
                                actionMask = default;
                                flag = false;
                            }
                        }
                        if (building == target.m_Target || !flag2 || !m_Renters.HasBuffer(building))
                        {
                            continue;
                        }
                        DynamicBuffer<Renter> dynamicBuffer2 = m_Renters[building];
                        for (int j = 0; j < dynamicBuffer2.Length; j++)
                        {
                            Entity renter = dynamicBuffer2[j].m_Renter;
                            if (renter == target.m_Target || !m_ServiceAvailableData.HasComponent(renter))
                            {
                                continue;
                            }
                            PrefabRef prefabRef = m_PrefabRefData[renter];
                            if (
                                m_PrefabIndustrialProcessData.HasComponent(prefabRef.m_Prefab)
                                && (m_PrefabIndustrialProcessData[prefabRef.m_Prefab].m_Output.m_Resource & householdNeed.m_Resource) != Resource.NoResource
                            )
                            {
                                ServiceAvailable serviceAvailable = m_ServiceAvailableData[renter];
                                DynamicBuffer<Game.Economy.Resources> resources = m_Resources[renter];
                                if (math.min(EconomyUtils.GetResources(householdNeed.m_Resource, resources), serviceAvailable.m_ServiceAvailable) >= householdNeed.m_Amount)
                                {
                                    SetDivert(
                                        jobIndex,
                                        entity,
                                        ref resident,
                                        ref currentLane,
                                        ref divert,
                                        ref pathOwner,
                                        Purpose.Shopping,
                                        renter,
                                        householdNeed.m_Amount,
                                        householdNeed.m_Resource
                                    );
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }

            private bool AddPathAction(Entity entity, ref Unity.Mathematics.Random random, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, Entity actionTarget)
            {
                Game.Objects.Transform transform = m_TransformData[actionTarget];
                PrefabRef prefabRef = m_PrefabRefData[actionTarget];
                float3 position;
                if (m_PrefabObjectGeometryData.HasComponent(prefabRef.m_Prefab))
                {
                    ObjectGeometryData objectGeometryData = m_PrefabObjectGeometryData[prefabRef.m_Prefab];
                    position = ObjectUtils.LocalToWorld(transform, random.NextFloat3(objectGeometryData.m_Bounds.min, objectGeometryData.m_Bounds.max));
                }
                else
                {
                    position = transform.m_Position + random.NextFloat3(-10f, 10f);
                }
                float num = float.MaxValue;
                float t = 0f;
                int num2 = -2;
                if (currentLane.m_CurvePosition.y != currentLane.m_CurvePosition.x && m_PedestrianLaneData.HasComponent(currentLane.m_Lane))
                {
                    num = MathUtils.Distance(m_CurveData[currentLane.m_Lane].m_Bezier, position, currentLane.m_CurvePosition, out t);
                    num2 = -1;
                }
                DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                int num3 = math.min(dynamicBuffer.Length, pathOwner.m_ElementIndex + 8);
                for (int i = pathOwner.m_ElementIndex; i < num3; i++)
                {
                    PathElement pathElement = dynamicBuffer[i];
                    if (pathElement.m_TargetDelta.y != pathElement.m_TargetDelta.x && m_PedestrianLaneData.HasComponent(pathElement.m_Target))
                    {
                        float num4 = MathUtils.Distance(m_CurveData[pathElement.m_Target].m_Bezier, position, pathElement.m_TargetDelta, out var t2);
                        if (num4 < num)
                        {
                            num = num4;
                            t = t2;
                            num2 = i;
                        }
                    }
                }
                Entity target;
                float y;
                switch (num2)
                {
                    case -2:
                        return false;
                    case -1:
                        target = currentLane.m_Lane;
                        y = currentLane.m_CurvePosition.y;
                        currentLane.m_CurvePosition.y = t;
                        num2 = pathOwner.m_ElementIndex;
                        break;
                    default:
                    {
                        PathElement value = dynamicBuffer[num2];
                        target = value.m_Target;
                        y = value.m_TargetDelta.y;
                        value.m_TargetDelta.y = t;
                        dynamicBuffer[num2++] = value;
                        break;
                    }
                }
                dynamicBuffer.Insert(num2, new PathElement { m_Target = target, m_TargetDelta = new float2(t, y) });
                dynamicBuffer.Insert(num2, new PathElement { m_Target = actionTarget, m_Flags = PathElementFlags.Action });
                return true;
            }

            private bool CheckPath(
                int jobIndex,
                Entity entity,
                PrefabRef prefabRef,
                ref Unity.Mathematics.Random random,
                ref Creature creature,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner,
                ref Game.Creatures.Resident resident
            )
            {
                DynamicBuffer<PathElement> path = m_PathElements[entity];
                human.m_Flags &= ~(HumanFlags.Selfies | HumanFlags.Carried);
                resident.m_Flags &= ~(ResidentFlags.WaitingTransport | ResidentFlags.NoLateDeparture);
                resident.m_Timer = 0;
                creature.m_QueueEntity = Entity.Null;
                creature.m_QueueArea = default;
                Purpose purpose = divert.m_Purpose;
                switch (purpose)
                {
                    case Purpose.Shopping:
                        if (purpose == Purpose.Shopping && divert.m_Data != 0 && m_HouseholdMembers.HasComponent(resident.m_Citizen))
                        {
                            m_ActionQueue.Enqueue(
                                new ResidentAISystem.ResidentAction
                                {
                                    m_Type = ResidentAISystem.ResidentActionType.GoShopping,
                                    m_Citizen = resident.m_Citizen,
                                    m_Household = m_HouseholdMembers[resident.m_Citizen].m_Household,
                                    m_Resource = divert.m_Resource,
                                    m_Target = divert.m_Target,
                                    m_Amount = divert.m_Data,
                                    m_Distance = 100f,
                                }
                            );
                            divert.m_Data = 0;
                        }
                        break;
                    case Purpose.None:
                    {
                        if (m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData) && componentData.m_Purpose == Purpose.EmergencyShelter)
                        {
                            human.m_Flags |= HumanFlags.Run | HumanFlags.Emergency;
                        }
                        break;
                    }
                    case Purpose.Safety:
                    case Purpose.Escape:
                        human.m_Flags |= HumanFlags.Run;
                        break;
                    case Purpose.Disappear:
                        if (divert.m_Target == Entity.Null && path.Length >= 1)
                        {
                            divert.m_Target = path[path.Length - 1].m_Target;
                            path.RemoveAt(path.Length - 1);
                        }
                        break;
                }
                ParkedCar componentData2 = default;
                BicycleOwner component2;
                if (m_CarKeeperData.TryGetEnabledComponent(resident.m_Citizen, out var component))
                {
                    m_ParkedCarData.TryGetComponent(component.m_Car, out componentData2);
                }
                else if (m_BicycleOwnerData.TryGetEnabledComponent(resident.m_Citizen, out component2))
                {
                    m_ParkedCarData.TryGetComponent(component2.m_Bicycle, out componentData2);
                }
                int length = path.Length;
                for (int i = 0; i < path.Length; i++)
                {
                    PathElement pathElement = path[i];
                    if (pathElement.m_Target == componentData2.m_Lane)
                    {
                        VehicleUtils.SetParkingCurvePos(path, pathOwner, i, currentLane.m_Lane, componentData2.m_CurvePosition, ref m_CurveData);
                        length = i;
                        break;
                    }
                    if (m_ParkingLaneData.HasComponent(pathElement.m_Target))
                    {
                        float curvePos = random.NextFloat(0.05f, 0.95f);
                        VehicleUtils.SetParkingCurvePos(path, pathOwner, i, currentLane.m_Lane, curvePos, ref m_CurveData);
                        length = i;
                        break;
                    }
                    if (m_ConnectionLaneData.HasComponent(pathElement.m_Target))
                    {
                        Game.Net.ConnectionLane connectionLane = m_ConnectionLaneData[pathElement.m_Target];
                        if ((connectionLane.m_Flags & ConnectionLaneFlags.Parking) != 0)
                        {
                            float curvePos2 = random.NextFloat(0.05f, 0.95f);
                            VehicleUtils.SetParkingCurvePos(path, pathOwner, i, currentLane.m_Lane, curvePos2, ref m_CurveData);
                            length = i;
                            break;
                        }
                        if ((connectionLane.m_Flags & ConnectionLaneFlags.Area) != 0 && i == path.Length - 1)
                        {
                            CreatureUtils.SetRandomAreaTarget(
                                ref random,
                                i,
                                path,
                                m_OwnerData,
                                m_CurveData,
                                m_LaneData,
                                m_ConnectionLaneData,
                                m_SubLanes,
                                m_AreaNodes,
                                m_AreaTriangles
                            );
                            length = path.Length;
                            break;
                        }
                    }
                    else if (i == path.Length - 1 && m_SpawnLocation.HasComponent(pathElement.m_Target))
                    {
                        PrefabRef prefabRef2 = m_PrefabRefData[pathElement.m_Target];
                        if (i != 0 && m_PrefabSpawnLocationData.TryGetComponent(prefabRef2, out var componentData3) && componentData3.m_HangaroundOnLane)
                        {
                            ref PathElement reference = ref path.ElementAt(i - 1);
                            reference.m_TargetDelta.y = random.NextFloat();
                            reference.m_Flags |= PathElementFlags.Hangaround;
                            path.RemoveAt(i);
                            length = path.Length;
                            break;
                        }
                    }
                }
                TransportEstimateBuffer transportEstimateBuffer = new TransportEstimateBuffer { m_BoardingQueue = m_BoardingQueue };
                RouteUtils.StripTransportSegments(
                    ref random,
                    length,
                    path,
                    m_RouteConnectedData,
                    m_BoardingVehicleData,
                    m_OwnerData,
                    m_LaneData,
                    m_ConnectionLaneData,
                    m_CurveData,
                    m_PrefabRefData,
                    m_PrefabTransportStopData,
                    m_SubLanes,
                    m_AreaNodes,
                    m_AreaTriangles,
                    transportEstimateBuffer
                );
                if (m_OwnerData.HasComponent(currentLane.m_Lane))
                {
                    Entity owner = m_OwnerData[currentLane.m_Lane].m_Owner;
                    return FindDivertTargets(
                        jobIndex,
                        entity,
                        prefabRef,
                        ref random,
                        ref human,
                        ref currentLane,
                        ref target,
                        ref divert,
                        ref pathOwner,
                        ref resident,
                        owner,
                        Entity.Null
                    );
                }
                return false;
            }

            private void CurrentVehicleBoarding(
                int jobIndex,
                Entity entity,
                Entity controllerVehicle,
                ref Game.Creatures.Resident resident,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                Game.Vehicles.PublicTransport publicTransport = m_PublicTransportData[controllerVehicle];
                if ((publicTransport.m_State & (PublicTransportFlags.Evacuating | PublicTransportFlags.PrisonerTransport)) != 0)
                {
                    if ((publicTransport.m_State & PublicTransportFlags.Returning) == 0)
                    {
                        return;
                    }
                }
                else
                {
                    DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                    if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 2)
                    {
                        Entity target2 = dynamicBuffer[pathOwner.m_ElementIndex + 1].m_Target;
                        Entity nextLane = Entity.Null;
                        if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 3)
                        {
                            nextLane = dynamicBuffer[pathOwner.m_ElementIndex + 2].m_Target;
                        }
                        if (
                            !PatchedRouteUtils.ShouldExitVehicle(
                                nextLane,
                                target2,
                                controllerVehicle,
                                ref m_OwnerData,
                                ref m_RouteConnectedData,
                                ref m_BoardingVehicleData,
                                ref m_CurrentRouteData,
                                ref m_AccessLaneLaneData,
                                ref m_PublicTransportData,
                                ref m_ConnectedRoutes,
                                testing: false,
                                out var obsolete,
                                ref m_WaypointLookup,
                                ref m_CustomWaypointLookup,
                                ref m_RouteWaypointLookup
                            )
                        )
                        {
                            return;
                        }
                        pathOwner.m_ElementIndex += 2;
                        if (!obsolete)
                        {
                            resident.m_Flags |= ResidentFlags.Disembarking;
                            return;
                        }
                    }
                }
                pathOwner.m_State &= ~PathFlags.Failed;
                pathOwner.m_State |= PathFlags.Obsolete;
                resident.m_Flags |= ResidentFlags.Disembarking;
            }

            private void CurrentVehicleTesting(
                int jobIndex,
                Entity entity,
                Entity controllerVehicle,
                ref Game.Creatures.Resident resident,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                Game.Vehicles.PublicTransport publicTransport = m_PublicTransportData[controllerVehicle];
                if ((publicTransport.m_State & (PublicTransportFlags.Evacuating | PublicTransportFlags.PrisonerTransport)) != 0)
                {
                    if ((publicTransport.m_State & PublicTransportFlags.Returning) == 0)
                    {
                        return;
                    }
                }
                else
                {
                    DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                    if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 2)
                    {
                        Entity target2 = dynamicBuffer[pathOwner.m_ElementIndex + 1].m_Target;
                        Entity nextLane = Entity.Null;
                        if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 3)
                        {
                            nextLane = dynamicBuffer[pathOwner.m_ElementIndex + 2].m_Target;
                        }
                        if (
                            !PatchedRouteUtils.ShouldExitVehicle(
                                nextLane,
                                target2,
                                controllerVehicle,
                                ref m_OwnerData,
                                ref m_RouteConnectedData,
                                ref m_BoardingVehicleData,
                                ref m_CurrentRouteData,
                                ref m_AccessLaneLaneData,
                                ref m_PublicTransportData,
                                ref m_ConnectedRoutes,
                                testing: true,
                                out var _,
                                ref m_WaypointLookup,
                                ref m_CustomWaypointLookup,
                                ref m_RouteWaypointLookup
                            )
                        )
                        {
                            return;
                        }
                    }
                }
                m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.RequireStop(Entity.Null, controllerVehicle, default));
            }

            private void CurrentVehicleDisembarking(
                int jobIndex,
                Entity entity,
                Entity controllerVehicle,
                ref Game.Creatures.Resident resident,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                DynamicBuffer<PathElement> targetElements = m_PathElements[entity];
                DynamicBuffer<PathElement> sourceElements = m_PathElements[controllerVehicle];
                PathOwner sourceOwner = m_PathOwnerData[controllerVehicle];
                if (sourceElements.Length > sourceOwner.m_ElementIndex)
                {
                    PathUtils.CopyPath(sourceElements, sourceOwner, 0, targetElements);
                    pathOwner.m_ElementIndex = 0;
                    pathOwner.m_State |= PathFlags.Updated;
                }
                else
                {
                    pathOwner.m_State &= ~PathFlags.Failed;
                    pathOwner.m_State |= PathFlags.Obsolete;
                }
                resident.m_Flags |= ResidentFlags.Disembarking;
            }

            private void CurrentVehicleTransporting(Entity entity, Entity controllerVehicle, ref PathOwner pathOwner)
            {
                m_PathElements[entity].Clear();
                pathOwner.m_ElementIndex = 0;
            }

            private void GroupLeaderDisembarking(Entity entity, ref Game.Creatures.Resident resident, ref PathOwner pathOwner)
            {
                m_PathElements[entity].Clear();
                pathOwner.m_ElementIndex = 0;
                resident.m_Flags |= ResidentFlags.Disembarking;
            }

            private bool PathEndReached(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                return divert.m_Purpose switch
                {
                    Purpose.None => ReachTarget(
                        jobIndex,
                        entity,
                        ref random,
                        ref resident,
                        ref human,
                        ref currentLane,
                        ref target,
                        ref divert,
                        ref pathOwner,
                        new ResetTrip { m_Target = target.m_Target }
                    ),
                    Purpose.SendMail => ReachSendMail(jobIndex, entity, ref resident, ref currentLane, ref target, ref divert, ref pathOwner),
                    Purpose.Safety => ReachSafety(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner),
                    Purpose.Escape => ReachEscape(jobIndex, entity, ref resident, ref currentLane, ref target, ref divert, ref pathOwner),
                    Purpose.WaitingHome => ReachWaitingHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner),
                    Purpose.PathFailed => ReachPathFailed(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner),
                    _ => ReachDivert(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner),
                };
            }

            private bool ActionLocationReached(Entity entity, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
            {
                if ((currentLane.m_Flags & CreatureLaneFlags.ActivityDone) != 0)
                {
                    resident.m_Flags |= ResidentFlags.ActivityDone;
                    human.m_Flags &= ~HumanFlags.Selfies;
                    DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                    pathOwner.m_ElementIndex += math.select(0, 1, pathOwner.m_ElementIndex < dynamicBuffer.Length);
                    return false;
                }
                return true;
            }

            private bool ReachTarget(
                int jobIndex,
                Entity creatureEntity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner,
                ResetTrip resetTrip
            )
            {
                if (m_VehicleData.HasComponent(target.m_Target))
                {
                    return ReachVehicle(jobIndex, creatureEntity, ref resident, ref currentLane, ref target, ref pathOwner);
                }
                Entity entity = target.m_Target;
                if (m_PropertyRenters.HasComponent(entity))
                {
                    entity = m_PropertyRenters[entity].m_Property;
                }
                if (m_OnFireData.HasComponent(entity) || m_DestroyedData.HasComponent(entity))
                {
                    SetDivert(jobIndex, creatureEntity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Safety, Entity.Null, 0, Resource.NoResource);
                    return false;
                }
                if (CanHangAround(resident.m_Citizen))
                {
                    if ((currentLane.m_Flags & CreatureLaneFlags.Hangaround) != 0)
                    {
                        if (
                            (currentLane.m_Flags & (CreatureLaneFlags.TransformTarget | CreatureLaneFlags.ActivityDone))
                                == (CreatureLaneFlags.TransformTarget | CreatureLaneFlags.ActivityDone)
                            || random.NextInt(2500) == 0
                        )
                        {
                            ResidentFlags ignoreFlags = GetIgnoreFlags(entity, ref resident, ref currentLane);
                            if (ignoreFlags != ResidentFlags.None)
                            {
                                if ((ignoreFlags & ~resident.m_Flags) != 0)
                                {
                                    resident.m_Flags |= ignoreFlags;
                                    pathOwner.m_State &= ~PathFlags.Failed;
                                    pathOwner.m_State |= PathFlags.Obsolete;
                                    resident.m_Flags &= ~ResidentFlags.Hangaround;
                                    if ((resident.m_Flags & ResidentFlags.Arrived) != 0)
                                    {
                                        resetTrip.m_Source = target.m_Target;
                                        resetTrip.m_Target = target.m_Target;
                                        bool flag = false;
                                        if (
                                            m_HouseholdMembers.TryGetComponent(resident.m_Citizen, out var componentData)
                                            && m_HouseholdAnimals.TryGetBuffer(componentData.m_Household, out var bufferData)
                                        )
                                        {
                                            for (int i = 0; i < bufferData.Length; i++)
                                            {
                                                HouseholdAnimal householdAnimal = bufferData[i];
                                                if (
                                                    m_CurrentBuildingData.TryGetComponent(householdAnimal.m_HouseholdPet, out var componentData2)
                                                    && m_CurrentTransportData.TryGetComponent(householdAnimal.m_HouseholdPet, out var componentData3)
                                                    && componentData2.m_CurrentBuilding == entity
                                                )
                                                {
                                                    Entity e = m_CommandBuffer.CreateEntity(jobIndex, m_ResetTripArchetype);
                                                    resetTrip.m_Creature = componentData3.m_CurrentTransport;
                                                    m_CommandBuffer.SetComponent(jobIndex, e, resetTrip);
                                                    flag = true;
                                                }
                                            }
                                        }
                                        if (flag)
                                        {
                                            Entity e2 = m_CommandBuffer.CreateEntity(jobIndex, m_ResetTripArchetype);
                                            resetTrip.m_Creature = creatureEntity;
                                            m_CommandBuffer.SetComponent(jobIndex, e2, resetTrip);
                                        }
                                    }
                                }
                                return false;
                            }
                        }
                        resident.m_Flags |= ResidentFlags.Hangaround;
                    }
                }
                else
                {
                    resident.m_Flags &= ~ResidentFlags.Hangaround;
                }
                human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
                resident.m_Flags &= ~(ResidentFlags.IgnoreTaxi | ResidentFlags.IgnoreTransport);
                if ((resident.m_Flags & ResidentFlags.Arrived) == 0)
                {
                    resetTrip.m_Creature = creatureEntity;
                    resetTrip.m_Arrived = entity;
                    Entity e3 = m_CommandBuffer.CreateEntity(jobIndex, m_ResetTripArchetype);
                    m_CommandBuffer.SetComponent(jobIndex, e3, resetTrip);
                    resident.m_Flags |= ResidentFlags.Arrived;
                    if (
                        m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData4)
                        && componentData4.m_Purpose == Purpose.GoingHome
                        && m_BicycleOwnerData.TryGetComponent(resident.m_Citizen, out var componentData5)
                        && m_PersonalCarData.TryGetComponent(componentData5.m_Bicycle, out var componentData6)
                        && (componentData6.m_State & PersonalCarFlags.HomeTarget) == 0
                    )
                    {
                        m_VehicleLayouts.TryGetBuffer(componentData5.m_Bicycle, out var bufferData2);
                        VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, componentData5.m_Bicycle, bufferData2);
                    }
                    return false;
                }
                if ((resident.m_Flags & ResidentFlags.Hangaround) != 0)
                {
                    return false;
                }
                if (m_EntityLookup.Exists(resident.m_Citizen))
                {
                    m_CommandBuffer.RemoveComponent<CurrentTransport>(jobIndex, resident.m_Citizen);
                }
                m_CommandBuffer.AddComponent<Deleted>(jobIndex, creatureEntity, default);
                return true;
            }

            private bool CanHangAround(Entity citizenEntity)
            {
                if (m_TravelPurposeData.TryGetComponent(citizenEntity, out var componentData) && componentData.m_Purpose == Purpose.Sleeping)
                {
                    return false;
                }
                return true;
            }

            private ResidentFlags GetIgnoreFlags(Entity building, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane)
            {
                if ((resident.m_Flags & ResidentFlags.CannotIgnore) != 0)
                {
                    return ResidentFlags.None;
                }
                ResidentFlags residentFlags = ResidentFlags.None;
                Owner componentData3;
                PrefabRef componentData4;
                SpawnLocationData componentData5;
                if ((currentLane.m_Flags & CreatureLaneFlags.TransformTarget) != 0)
                {
                    if (
                        m_PrefabRefData.TryGetComponent(currentLane.m_Lane, out var componentData)
                        && m_PrefabSpawnLocationData.TryGetComponent(componentData.m_Prefab, out var componentData2)
                        && (
                            (componentData2.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.BenchSitting).m_Mask) != 0
                            || (componentData2.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PullUps).m_Mask) != 0
                            || (componentData2.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Reading).m_Mask) != 0
                        )
                    )
                    {
                        residentFlags |= ResidentFlags.IgnoreBenches;
                    }
                }
                else if (
                    (currentLane.m_Flags & CreatureLaneFlags.Area) != 0
                    && m_OwnerData.TryGetComponent(currentLane.m_Lane, out componentData3)
                    && m_PrefabRefData.TryGetComponent(componentData3.m_Owner, out componentData4)
                    && m_PrefabSpawnLocationData.TryGetComponent(componentData4.m_Prefab, out componentData5)
                    && (
                        (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Standing).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundLaying).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundSitting).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PushUps).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.SitUps).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingJacks).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingLunges).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Squats).m_Mask) != 0
                        || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Yoga).m_Mask) != 0
                    )
                )
                {
                    residentFlags |= ResidentFlags.IgnoreAreas;
                }
                if ((residentFlags & ~resident.m_Flags) != 0)
                {
                    ResidentFlags residentFlags2 = ~(resident.m_Flags | residentFlags);
                    bool flag = false;
                    if (m_SpawnLocationElements.TryGetBuffer(building, out var bufferData))
                    {
                        for (int i = 0; i < bufferData.Length; i++)
                        {
                            SpawnLocationElement spawnLocationElement = bufferData[i];
                            if (spawnLocationElement.m_Type == SpawnLocationType.SpawnLocation || spawnLocationElement.m_Type == SpawnLocationType.HangaroundLocation)
                            {
                                PrefabRef prefabRef = m_PrefabRefData[spawnLocationElement.m_SpawnLocation];
                                SpawnLocationData spawnLocationData = m_PrefabSpawnLocationData[prefabRef.m_Prefab];
                                ResidentFlags residentFlags3 = ResidentFlags.None;
                                if (
                                    (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.BenchSitting).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PullUps).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Reading).m_Mask) != 0
                                )
                                {
                                    residentFlags3 |= ResidentFlags.IgnoreBenches;
                                }
                                if (
                                    (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Standing).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundLaying).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundSitting).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PushUps).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.SitUps).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingJacks).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingLunges).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Squats).m_Mask) != 0
                                    || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Yoga).m_Mask) != 0
                                )
                                {
                                    residentFlags3 |= ResidentFlags.IgnoreAreas;
                                }
                                if (residentFlags3 == ResidentFlags.None || (residentFlags3 & residentFlags2) != 0)
                                {
                                    return residentFlags;
                                }
                                flag |= (residentFlags3 & ~residentFlags) != 0;
                            }
                        }
                    }
                    if (!flag)
                    {
                        resident.m_Flags |= ResidentFlags.CannotIgnore;
                        return ResidentFlags.None;
                    }
                    resident.m_Flags &= ~(ResidentFlags.IgnoreBenches | ResidentFlags.IgnoreAreas) | residentFlags;
                }
                return residentFlags;
            }

            private bool ReachVehicle(
                int jobIndex,
                Entity entity,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref PathOwner pathOwner
            )
            {
                Entity entity2 = target.m_Target;
                if (m_ControllerData.HasComponent(target.m_Target))
                {
                    Controller controller = m_ControllerData[target.m_Target];
                    if (controller.m_Controller != Entity.Null)
                    {
                        entity2 = controller.m_Controller;
                    }
                }
                if (m_PublicTransportData.HasComponent(entity2))
                {
                    if ((m_PublicTransportData[entity2].m_State & PublicTransportFlags.Boarding) != 0 && m_OwnerData.HasComponent(entity2))
                    {
                        Owner owner = m_OwnerData[entity2];
                        if (m_PrefabRefData.HasComponent(owner.m_Owner))
                        {
                            TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
                            target.m_Target = owner.m_Owner;
                            pathOwner.m_State &= ~PathFlags.Failed;
                            pathOwner.m_State |= PathFlags.Obsolete;
                            return true;
                        }
                    }
                }
                else if (m_PoliceCarData.HasComponent(entity2))
                {
                    if ((m_PoliceCarData[entity2].m_State & PoliceCarFlags.AtTarget) != 0 && m_OwnerData.HasComponent(entity2))
                    {
                        Owner owner2 = m_OwnerData[entity2];
                        if (m_PrefabRefData.HasComponent(owner2.m_Owner))
                        {
                            TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
                            target.m_Target = owner2.m_Owner;
                            pathOwner.m_State &= ~PathFlags.Failed;
                            pathOwner.m_State |= PathFlags.Obsolete;
                            return true;
                        }
                    }
                }
                else if (m_AmbulanceData.HasComponent(entity2))
                {
                    if ((m_AmbulanceData[entity2].m_State & AmbulanceFlags.AtTarget) != 0 && m_OwnerData.HasComponent(entity2))
                    {
                        Owner owner3 = m_OwnerData[entity2];
                        if (m_PrefabRefData.HasComponent(owner3.m_Owner))
                        {
                            TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
                            target.m_Target = owner3.m_Owner;
                            pathOwner.m_State &= ~PathFlags.Failed;
                            pathOwner.m_State |= PathFlags.Obsolete;
                            return true;
                        }
                    }
                }
                else if (m_HearseData.HasComponent(entity2) && (m_HearseData[entity2].m_State & HearseFlags.AtTarget) != 0 && m_OwnerData.HasComponent(entity2))
                {
                    Owner owner4 = m_OwnerData[entity2];
                    if (m_PrefabRefData.HasComponent(owner4.m_Owner))
                    {
                        TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
                        target.m_Target = owner4.m_Owner;
                        pathOwner.m_State &= ~PathFlags.Failed;
                        pathOwner.m_State |= PathFlags.Obsolete;
                        return true;
                    }
                }
                AddDeletedResident(DeletedResidentType.InvalidVehicleTarget);
                m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                return true;
            }

            private bool ReachDivert(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                ResetTrip resetTrip = new ResetTrip
                {
                    m_Target = divert.m_Target,
                    m_TravelPurpose = divert.m_Purpose,
                    m_TravelData = divert.m_Data,
                    m_TravelResource = divert.m_Resource,
                };
                if (m_TravelPurposeData.HasComponent(resident.m_Citizen) && m_PrefabRefData.HasComponent(target.m_Target))
                {
                    TravelPurpose travelPurpose = m_TravelPurposeData[resident.m_Citizen];
                    resetTrip.m_NextTarget = target.m_Target;
                    resetTrip.m_NextPurpose = travelPurpose.m_Purpose;
                    resetTrip.m_NextData = travelPurpose.m_Data;
                    resetTrip.m_NextResource = travelPurpose.m_Resource;
                }
                target.m_Target = divert.m_Target;
                divert = default;
                m_CommandBuffer.RemoveComponent<Divert>(jobIndex, entity);
                pathOwner.m_State &= ~PathFlags.CachedObsolete;
                return ReachTarget(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner, resetTrip);
            }

            private bool ReachSendMail(
                int jobIndex,
                Entity entity,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                m_ActionQueue.Enqueue(
                    new ResidentAISystem.ResidentAction
                    {
                        m_Type = ResidentAISystem.ResidentActionType.SendMail,
                        m_Citizen = resident.m_Citizen,
                        m_Target = currentLane.m_Lane,
                    }
                );
                SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.None, Entity.Null, 0, Resource.NoResource);
                return false;
            }

            private bool ReachSafety(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
                if (!m_PrefabRefData.HasComponent(target.m_Target) || m_DestroyedData.HasComponent(target.m_Target))
                {
                    Entity homeBuilding = GetHomeBuilding(ref resident, out var movingAway);
                    if (homeBuilding != Entity.Null && !m_DestroyedData.HasComponent(homeBuilding))
                    {
                        if (m_OnFireData.HasComponent(homeBuilding))
                        {
                            FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
                            return false;
                        }
                        SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
                        return false;
                    }
                    if (movingAway)
                    {
                        target.m_Target = Entity.Null;
                        SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Disappear, Entity.Null, 0, Resource.NoResource);
                        return false;
                    }
                    SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.WaitingHome, Entity.Null, 0, Resource.NoResource);
                    return false;
                }
                if (m_OnFireData.HasComponent(target.m_Target))
                {
                    FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
                    return false;
                }
                Purpose purpose = Purpose.None;
                if (m_TravelPurposeData.HasComponent(resident.m_Citizen))
                {
                    purpose = m_TravelPurposeData[resident.m_Citizen].m_Purpose;
                }
                SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, purpose, target.m_Target);
                return false;
            }

            private bool ReachEscape(
                int jobIndex,
                Entity entity,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                Entity homeBuilding = GetHomeBuilding(ref resident, out var _);
                if (homeBuilding == Entity.Null || m_OnFireData.HasComponent(homeBuilding) || m_DestroyedData.HasComponent(homeBuilding))
                {
                    SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Disappear, Entity.Null, 0, Resource.NoResource);
                    return false;
                }
                SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
                return false;
            }

            private bool ReachWaitingHome(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                Entity homeBuilding = GetHomeBuilding(ref resident, out var movingAway);
                if (homeBuilding == Entity.Null || m_DestroyedData.HasComponent(homeBuilding))
                {
                    if (movingAway)
                    {
                        SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Disappear, Entity.Null, 0, Resource.NoResource);
                        return false;
                    }
                    divert.m_Data += random.NextInt(1, 3);
                    bool flag = divert.m_Data <= 2500;
                    if (!flag)
                    {
                        flag =
                            (currentLane.m_Flags & CreatureLaneFlags.Connection) == 0
                            || !m_ConnectionLaneData.TryGetComponent(currentLane.m_Lane, out var componentData)
                            || (componentData.m_Flags & ConnectionLaneFlags.Outside) == 0;
                    }
                    if (flag)
                    {
                        FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
                        return false;
                    }
                    if (m_CitizenData.HasComponent(resident.m_Citizen))
                    {
                        m_CommandBuffer.AddComponent<Deleted>(jobIndex, resident.m_Citizen, default);
                    }
                    AddDeletedResident(DeletedResidentType.WaitingHome_AlreadyOutside);
                    m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                    return true;
                }
                SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
                return false;
            }

            private bool ReachPathFailed(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                divert.m_Data += random.NextInt(1, 3);
                if (divert.m_Data <= 250)
                {
                    FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
                    return false;
                }
                SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.None, Entity.Null, 0, Resource.NoResource);
                return false;
            }

            private bool ReturnHome(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                if (m_AttendingMeetingData.HasComponent(resident.m_Citizen))
                {
                    AttendingMeeting attendingMeeting = m_AttendingMeetingData[resident.m_Citizen];
                    if (m_CoordinatedMeetingData.HasComponent(attendingMeeting.m_Meeting) && m_CoordinatedMeetingData[attendingMeeting.m_Meeting].m_Target == target.m_Target)
                    {
                        m_CommandBuffer.RemoveComponent<AttendingMeeting>(jobIndex, resident.m_Citizen);
                    }
                }
                Entity homeBuilding = GetHomeBuilding(ref resident, out var movingAway);
                if (homeBuilding != Entity.Null && homeBuilding != target.m_Target)
                {
                    SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
                    return false;
                }
                if (homeBuilding == Entity.Null && currentLane.m_Lane != Entity.Null)
                {
                    if (movingAway)
                    {
                        AddDeletedResident(DeletedResidentType.NoPath_AlreadyMovingAway);
                        if (m_CitizenData.HasComponent(resident.m_Citizen))
                        {
                            m_CommandBuffer.AddComponent<Deleted>(jobIndex, resident.m_Citizen, default);
                        }
                        m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                        return true;
                    }
                    SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.WaitingHome, Entity.Null, 0, Resource.NoResource);
                    return false;
                }
                if (
                    (currentLane.m_Flags & CreatureLaneFlags.Connection) != 0
                    && m_ConnectionLaneData.TryGetComponent(currentLane.m_Lane, out var componentData)
                    && (componentData.m_Flags & ConnectionLaneFlags.Outside) != 0
                )
                {
                    if (m_CitizenData.HasComponent(resident.m_Citizen))
                    {
                        m_CommandBuffer.AddComponent<Deleted>(jobIndex, resident.m_Citizen, default);
                    }
                    AddDeletedResident(DeletedResidentType.NoPathToHome_AlreadyOutside);
                }
                else
                {
                    AddDeletedResident(DeletedResidentType.NoPathToHome);
                }
                m_CommandBuffer.AddComponent<Deleted>(jobIndex, entity, default);
                return true;
            }

            private void FindWaitingPosition(
                int jobIndex,
                Entity entity,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Target target,
                ref Divert divert,
                ref PathOwner pathOwner
            )
            {
                if (m_PedestrianLaneData.HasComponent(currentLane.m_Lane))
                {
                    if ((m_PedestrianLaneData[currentLane.m_Lane].m_Flags & PedestrianLaneFlags.Crosswalk) == 0 && !m_LaneSignalData.HasComponent(currentLane.m_Lane))
                    {
                        return;
                    }
                    pathOwner.m_ElementIndex = 0;
                    DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                    dynamicBuffer.Clear();
                    NativeParallelHashSet<Entity> ignoreLanes = new NativeParallelHashSet<Entity>(16, Allocator.Temp) { currentLane.m_Lane };
                    Entity lane = currentLane.m_Lane;
                    float2 yy = currentLane.m_CurvePosition.yy;
                    if (yy.y >= 0.5f)
                    {
                        if (yy.y != 1f)
                        {
                            yy.y = 1f;
                            dynamicBuffer.Add(new PathElement(currentLane.m_Lane, yy));
                        }
                    }
                    else if (yy.y != 0f)
                    {
                        yy.y = 0f;
                        dynamicBuffer.Add(new PathElement(currentLane.m_Lane, yy));
                    }
                    while (TryFindNextLane(ignoreLanes, ref lane, ref yy.y))
                    {
                        ignoreLanes.Add(lane);
                        yy.x = yy.y;
                        if ((m_PedestrianLaneData[lane].m_Flags & PedestrianLaneFlags.Crosswalk) == 0 && !m_LaneSignalData.HasComponent(lane))
                        {
                            yy.y = random.NextFloat(0f, 1f);
                            dynamicBuffer.Add(new PathElement(lane, yy));
                            break;
                        }
                        yy.y = math.select(0f, 1f, yy.x < 0.5f);
                        dynamicBuffer.Add(new PathElement(lane, yy));
                    }
                    ignoreLanes.Dispose();
                    if (dynamicBuffer.Length != 0)
                    {
                        currentLane.m_Flags &= ~(
                            CreatureLaneFlags.EndOfPath | CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Transport | CreatureLaneFlags.Taxi | CreatureLaneFlags.Action
                        );
                    }
                }
                else if (
                    m_ConnectionLaneData.HasComponent(currentLane.m_Lane)
                    && (currentLane.m_Flags & CreatureLaneFlags.WaitPosition) == 0
                    && (m_ConnectionLaneData[currentLane.m_Lane].m_Flags & ConnectionLaneFlags.Outside) != 0
                )
                {
                    currentLane.m_Flags &= ~CreatureLaneFlags.EndReached;
                    currentLane.m_Flags |= CreatureLaneFlags.WaitPosition;
                    currentLane.m_CurvePosition.y = random.NextFloat(0f, 1f);
                }
            }

            private bool TryFindNextLane(NativeParallelHashSet<Entity> ignoreLanes, ref Entity lane, ref float curveDelta)
            {
                if (!m_OwnerData.HasComponent(lane))
                {
                    return false;
                }
                Owner owner = m_OwnerData[lane];
                if (TryFindNextLane(ignoreLanes, owner.m_Owner, ref lane, ref curveDelta))
                {
                    return true;
                }
                if (m_ConnectedEdges.HasBuffer(owner.m_Owner))
                {
                    DynamicBuffer<ConnectedEdge> dynamicBuffer = m_ConnectedEdges[owner.m_Owner];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        if (TryFindNextLane(ignoreLanes, dynamicBuffer[i].m_Edge, ref lane, ref curveDelta))
                        {
                            return true;
                        }
                    }
                }
                else if (m_EdgeData.HasComponent(owner.m_Owner))
                {
                    Game.Net.Edge edge = m_EdgeData[owner.m_Owner];
                    if (TryFindNextLane(ignoreLanes, edge.m_Start, ref lane, ref curveDelta))
                    {
                        return true;
                    }
                    if (TryFindNextLane(ignoreLanes, edge.m_End, ref lane, ref curveDelta))
                    {
                        return true;
                    }
                }
                return false;
            }

            private bool TryFindNextLane(NativeParallelHashSet<Entity> ignoreLanes, Entity owner, ref Entity lane, ref float curveDelta)
            {
                if (!m_SubLanes.HasBuffer(owner))
                {
                    return false;
                }
                DynamicBuffer<Game.Net.SubLane> dynamicBuffer = m_SubLanes[owner];
                Lane lane2 = m_LaneData[lane];
                PathNode other;
                if (curveDelta == 0f)
                {
                    other = lane2.m_StartNode;
                }
                else
                {
                    other = ((curveDelta != 1f) ? lane2.m_MiddleNode : lane2.m_EndNode);
                }
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    Entity subLane = dynamicBuffer[i].m_SubLane;
                    if (!ignoreLanes.Contains(subLane) && m_PedestrianLaneData.HasComponent(subLane))
                    {
                        Lane lane3 = m_LaneData[subLane];
                        if (lane3.m_StartNode.EqualsIgnoreCurvePos(other))
                        {
                            lane = subLane;
                            curveDelta = 0f;
                            return true;
                        }
                        if (lane3.m_EndNode.EqualsIgnoreCurvePos(other))
                        {
                            lane = subLane;
                            curveDelta = 1f;
                            return true;
                        }
                        if (lane3.m_MiddleNode.EqualsIgnoreCurvePos(other))
                        {
                            lane = subLane;
                            curveDelta = lane3.m_MiddleNode.GetCurvePos();
                            return true;
                        }
                    }
                }
                return false;
            }

            private void SetDivert(
                int jobIndex,
                Entity entity,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Divert divert,
                ref PathOwner pathOwner,
                Purpose purpose,
                Entity targetEntity,
                int data = 0,
                Resource resource = Resource.NoResource
            )
            {
                if (purpose != Purpose.None)
                {
                    bool flag = divert.m_Purpose == Purpose.None;
                    divert = new Divert
                    {
                        m_Purpose = purpose,
                        m_Target = targetEntity,
                        m_Data = data,
                        m_Resource = resource,
                    };
                    if (flag)
                    {
                        m_CommandBuffer.AddComponent(jobIndex, entity, divert);
                    }
                    pathOwner.m_State |= PathFlags.DivertObsolete;
                }
                else if (divert.m_Purpose != Purpose.None)
                {
                    divert = default;
                    m_CommandBuffer.RemoveComponent<Divert>(jobIndex, entity);
                    if ((pathOwner.m_State & PathFlags.CachedObsolete) != 0)
                    {
                        pathOwner.m_State &= ~PathFlags.CachedObsolete;
                        pathOwner.m_State |= PathFlags.Obsolete;
                    }
                }
                if ((resident.m_Flags & ResidentFlags.Arrived) != 0 && m_PrefabRefData.HasComponent(resident.m_Citizen))
                {
                    m_CommandBuffer.RemoveComponent<CurrentBuilding>(jobIndex, resident.m_Citizen);
                }
                currentLane.m_Flags &= ~CreatureLaneFlags.EndOfPath;
                resident.m_Flags &= ~(ResidentFlags.Arrived | ResidentFlags.Hangaround | ResidentFlags.IgnoreBenches | ResidentFlags.IgnoreAreas | ResidentFlags.CannotIgnore);
                pathOwner.m_State &= ~PathFlags.Failed;
            }

            private void SetTarget(
                int jobIndex,
                Entity entity,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref Divert divert,
                ref PathOwner pathOwner,
                ref Target target,
                Purpose purpose,
                Entity targetEntity
            )
            {
                Entity source = Entity.Null;
                if ((resident.m_Flags & ResidentFlags.Arrived) != 0)
                {
                    if (m_PrefabRefData.HasComponent(resident.m_Citizen))
                    {
                        m_CommandBuffer.RemoveComponent<CurrentBuilding>(jobIndex, resident.m_Citizen);
                    }
                    source = target.m_Target;
                }
                Entity e = m_CommandBuffer.CreateEntity(jobIndex, m_ResetTripArchetype);
                m_CommandBuffer.SetComponent(
                    jobIndex,
                    e,
                    new ResetTrip
                    {
                        m_Creature = entity,
                        m_Source = source,
                        m_Target = targetEntity,
                        m_TravelPurpose = purpose,
                    }
                );
            }

            private bool ParkingSpaceReached(
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity entity,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                DynamicBuffer<GroupCreature> groupCreatures
            )
            {
                if ((currentLane.m_Flags & CreatureLaneFlags.Taxi) != 0)
                {
                    if (m_RideNeederData.TryGetComponent(entity, out var componentData))
                    {
                        if (m_Dispatched.TryGetComponent(componentData.m_RideRequest, out var componentData2))
                        {
                            if (
                                m_TaxiData.TryGetComponent(componentData2.m_Handler, out var componentData3)
                                && m_ServiceDispatches.TryGetBuffer(componentData2.m_Handler, out var bufferData)
                                && (componentData3.m_State & TaxiFlags.Dispatched) != 0
                                && bufferData.Length >= 1
                                && bufferData[0].m_Request == componentData.m_RideRequest
                            )
                            {
                                if (m_CarNavigationLanes.HasBuffer(componentData2.m_Handler))
                                {
                                    DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                                    DynamicBuffer<CarNavigationLane> dynamicBuffer2 = m_CarNavigationLanes[componentData2.m_Handler];
                                    if (dynamicBuffer2.Length > 0 && dynamicBuffer.Length > pathOwner.m_ElementIndex)
                                    {
                                        PathElement value = dynamicBuffer[pathOwner.m_ElementIndex];
                                        CarNavigationLane carNavigationLane = dynamicBuffer2[dynamicBuffer2.Length - 1];
                                        if (
                                            carNavigationLane.m_Lane == value.m_Target
                                            && carNavigationLane.m_CurvePosition.y != value.m_TargetDelta.y
                                            && m_CurveData.HasComponent(currentLane.m_Lane)
                                            && m_CurveData.HasComponent(value.m_Target)
                                        )
                                        {
                                            value.m_TargetDelta = carNavigationLane.m_CurvePosition.y;
                                            dynamicBuffer[pathOwner.m_ElementIndex] = value;
                                            float3 position = MathUtils.Position(m_CurveData[value.m_Target].m_Bezier, value.m_TargetDelta.y);
                                            MathUtils.Distance(m_CurveData[currentLane.m_Lane].m_Bezier, position, out var t);
                                            if (t != currentLane.m_CurvePosition.y)
                                            {
                                                currentLane.m_CurvePosition.y = t;
                                                currentLane.m_Flags &= ~CreatureLaneFlags.EndReached;
                                                return true;
                                            }
                                        }
                                    }
                                }
                                if ((componentData3.m_State & TaxiFlags.Boarding) != 0)
                                {
                                    Game.Objects.Transform transform = m_TransformData[entity];
                                    m_BoardingQueue.Enqueue(
                                        ResidentAISystem.Boarding.TryEnterVehicle(
                                            entity,
                                            Entity.Null,
                                            componentData2.m_Handler,
                                            Entity.Null,
                                            Entity.Null,
                                            transform.m_Position,
                                            CreatureVehicleFlags.Leader
                                        )
                                    );
                                }
                            }
                        }
                        else if (m_ServiceRequestData.HasComponent(componentData.m_RideRequest) && m_ServiceRequestData[componentData.m_RideRequest].m_FailCount >= 3)
                        {
                            resident.m_Flags |= ResidentFlags.IgnoreTaxi;
                            currentLane.m_Flags &= ~(CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Taxi);
                            pathOwner.m_State &= ~PathFlags.Failed;
                            pathOwner.m_State |= PathFlags.Obsolete;
                            m_CommandBuffer.RemoveComponent<RideNeeder>(jobIndex, entity);
                            return false;
                        }
                        return true;
                    }
                    m_CommandBuffer.AddComponent<RideNeeder>(jobIndex, entity, default);
                    return true;
                }
                BicycleOwner component2;
                if (m_CarKeeperData.TryGetEnabledComponent(resident.m_Citizen, out var component))
                {
                    if (m_ParkedCarData.HasComponent(component.m_Car))
                    {
                        ActivateParkedCar(jobIndex, ref random, entity, component.m_Car, ref resident, ref pathOwner, ref target, groupCreatures);
                        Game.Objects.Transform transform2 = m_TransformData[entity];
                        m_BoardingQueue.Enqueue(
                            ResidentAISystem.Boarding.TryEnterVehicle(
                                entity,
                                Entity.Null,
                                component.m_Car,
                                Entity.Null,
                                Entity.Null,
                                transform2.m_Position,
                                CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver
                            )
                        );
                        return true;
                    }
                }
                else if (m_BicycleOwnerData.TryGetEnabledComponent(resident.m_Citizen, out component2) && m_ParkedCarData.HasComponent(component2.m_Bicycle))
                {
                    ActivateParkedCar(jobIndex, ref random, entity, component2.m_Bicycle, ref resident, ref pathOwner, ref target, groupCreatures);
                    Game.Objects.Transform transform3 = m_TransformData[entity];
                    m_BoardingQueue.Enqueue(
                        ResidentAISystem.Boarding.TryEnterVehicle(
                            entity,
                            Entity.Null,
                            component2.m_Bicycle,
                            Entity.Null,
                            Entity.Null,
                            transform3.m_Position,
                            CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver
                        )
                    );
                    return true;
                }
                currentLane.m_Flags &= ~(CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Taxi);
                pathOwner.m_State &= ~PathFlags.Failed;
                pathOwner.m_State |= PathFlags.Obsolete;
                return false;
            }

            private bool TransportStopReached(
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity entity,
                PrefabRef prefabRef,
                bool isUnspawned,
                ref Game.Creatures.Resident resident,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target
            )
            {
                DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 2)
                {
                    Entity target2 = dynamicBuffer[pathOwner.m_ElementIndex].m_Target;
                    Entity target3 = dynamicBuffer[pathOwner.m_ElementIndex + 1].m_Target;
                    if ((resident.m_Flags & ResidentFlags.WaitingTransport) == 0)
                    {
                        resident.m_Flags |= ResidentFlags.NoLateDeparture;
                    }
                    uint minDeparture = math.select(0u, m_SimulationFrameIndex, (resident.m_Flags & ResidentFlags.NoLateDeparture) != 0);
                    if (
                        RouteUtils.GetBoardingVehicle(
                            currentLane.m_Lane,
                            target2,
                            target3,
                            minDeparture,
                            ref m_OwnerData,
                            ref m_TargetData,
                            ref m_RouteConnectedData,
                            ref m_BoardingVehicleData,
                            ref m_CurrentRouteData,
                            ref m_AccessLaneLaneData,
                            ref m_PublicTransportData,
                            ref m_TaxiData,
                            ref m_ConnectedRoutes,
                            ref m_RouteWaypointLookup,
                            out var vehicle,
                            out var testing,
                            out var obsolete
                        )
                    )
                    {
                        TryEnterVehicle(entity, vehicle, target2, ref resident, ref currentLane);
                        SetQueuePosition(entity, prefabRef, target2, ref currentLane);
                        return true;
                    }
                    if (!obsolete)
                    {
                        if ((resident.m_Flags & ResidentFlags.WaitingTransport) == 0 || resident.m_Timer < 5000)
                        {
                            if (testing)
                            {
                                Game.Objects.Transform transform = m_TransformData[entity];
                                m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.RequireStop(entity, vehicle, transform.m_Position));
                            }
                            if (
                                isUnspawned
                                && (currentLane.m_Flags & (CreatureLaneFlags.TransformTarget | CreatureLaneFlags.Connection)) != 0
                                && (currentLane.m_Flags & CreatureLaneFlags.WaitPosition) == 0
                            )
                            {
                                currentLane.m_Flags &= ~CreatureLaneFlags.EndReached;
                                currentLane.m_Flags |= CreatureLaneFlags.WaitPosition;
                                currentLane.m_CurvePosition.y = random.NextFloat(0f, 1f);
                            }
                            SetQueuePosition(entity, prefabRef, target2, ref currentLane);
                            return true;
                        }
                        m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.WaitTimeExceeded(entity, target2));
                        if (m_BoardingVehicleData.HasComponent(target2))
                        {
                            resident.m_Flags |= ResidentFlags.IgnoreTaxi;
                        }
                        else
                        {
                            resident.m_Flags |= ResidentFlags.IgnoreTransport;
                        }
                    }
                }
                currentLane.m_Flags &= ~CreatureLaneFlags.Transport;
                pathOwner.m_State &= ~PathFlags.Failed;
                pathOwner.m_State |= PathFlags.Obsolete;
                return false;
            }

            private void SetQueuePosition(Entity entity, PrefabRef prefabRef, Entity targetEntity, ref HumanCurrentLane currentLane)
            {
                Game.Objects.Transform transform = m_TransformData[entity];
                Sphere3 queueArea = CreatureUtils.GetQueueArea(m_PrefabObjectGeometryData[prefabRef.m_Prefab], transform.m_Position);
                CreatureUtils.SetQueue(ref currentLane.m_QueueEntity, ref currentLane.m_QueueArea, targetEntity, queueArea);
            }

            private Entity GetHomeBuilding(ref Game.Creatures.Resident resident, out bool movingAway)
            {
                movingAway = false;
                if (m_HouseholdMembers.TryGetComponent(resident.m_Citizen, out var componentData))
                {
                    if (m_MovingAwayData.HasComponent(componentData.m_Household))
                    {
                        movingAway = true;
                        return Entity.Null;
                    }
                    if (
                        m_PropertyRenters.TryGetComponent(componentData.m_Household, out var componentData2)
                        && m_EntityLookup.Exists(componentData2.m_Property)
                        && !m_DeletedData.HasComponent(componentData2.m_Property)
                    )
                    {
                        return componentData2.m_Property;
                    }
                    if (
                        m_TouristHouseholds.TryGetComponent(componentData.m_Household, out var componentData3)
                        && m_PropertyRenters.TryGetComponent(componentData3.m_Hotel, out componentData2)
                        && m_EntityLookup.Exists(componentData2.m_Property)
                        && !m_DeletedData.HasComponent(componentData2.m_Property)
                    )
                    {
                        return componentData2.m_Property;
                    }
                    if (
                        m_HomelessHouseholdData.TryGetComponent(componentData.m_Household, out var componentData4)
                        && m_EntityLookup.Exists(componentData4.m_TempHome)
                        && !m_DeletedData.HasComponent(componentData4.m_TempHome)
                    )
                    {
                        return componentData4.m_TempHome;
                    }
                    if (m_CitizenData.TryGetComponent(resident.m_Citizen, out var componentData5))
                    {
                        movingAway = (componentData5.m_State & CitizenFlags.Commuter) != 0 || !m_EntityLookup.Exists(componentData.m_Household);
                        return Entity.Null;
                    }
                }
                movingAway = true;
                return Entity.Null;
            }

            private void TryEnterVehicle(Entity entity, Entity vehicle, Entity waypoint, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane)
            {
                Game.Objects.Transform transform = m_TransformData[entity];
                m_BoardingQueue.Enqueue(
                    ResidentAISystem.Boarding.TryEnterVehicle(entity, Entity.Null, vehicle, Entity.Null, waypoint, transform.m_Position, CreatureVehicleFlags.Leader)
                );
            }

            private void FinishEnterVehicle(
                Entity entity,
                Entity vehicle,
                Entity controllerVehicle,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane
            )
            {
                Entity household = GetHousehold(resident);
                int ticketPrice = GetTicketPrice(vehicle);
                m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.FinishEnterVehicle(entity, household, vehicle, controllerVehicle, currentLane, ticketPrice));
                human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
            }

            private void FinishExitVehicle(Entity entity, Entity vehicle, ref HumanCurrentLane currentLane)
            {
                currentLane.m_Flags &= ~(CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached);
                m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.FinishExitVehicle(entity, vehicle));
            }

            private void CancelEnterVehicle(
                Entity entity,
                Entity vehicle,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner
            )
            {
                m_BoardingQueue.Enqueue(ResidentAISystem.Boarding.CancelEnterVehicle(entity, vehicle));
                human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
                DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
                for (int i = pathOwner.m_ElementIndex; i < dynamicBuffer.Length; i++)
                {
                    if (dynamicBuffer[i].m_Target == vehicle)
                    {
                        dynamicBuffer.RemoveRange(0, i + 1);
                        pathOwner.m_ElementIndex = 0;
                        return;
                    }
                }
                dynamicBuffer.Clear();
                pathOwner.m_ElementIndex = 0;
                pathOwner.m_State &= ~PathFlags.Failed;
                pathOwner.m_State |= PathFlags.Obsolete;
            }

            private Entity GetHousehold(Game.Creatures.Resident resident)
            {
                if (m_HouseholdMembers.HasComponent(resident.m_Citizen))
                {
                    return m_HouseholdMembers[resident.m_Citizen].m_Household;
                }
                return Entity.Null;
            }

            private int GetTicketPrice(Entity vehicle)
            {
                if (m_CurrentRouteData.HasComponent(vehicle))
                {
                    CurrentRoute currentRoute = m_CurrentRouteData[vehicle];
                    if (m_TransportLineData.HasComponent(currentRoute.m_Route))
                    {
                        return m_TransportLineData[currentRoute.m_Route].m_TicketPrice;
                    }
                }
                return 0;
            }

            private void FindNewPath(
                Entity entity,
                PrefabRef prefabRef,
                ref Unity.Mathematics.Random random,
                ref Game.Creatures.Resident resident,
                ref Human human,
                ref HumanCurrentLane currentLane,
                ref PathOwner pathOwner,
                ref Target target,
                ref Divert divert
            )
            {
                CreatureData creatureData = m_PrefabCreatureData[prefabRef.m_Prefab];
                HumanData humanData = m_PrefabHumanData[prefabRef.m_Prefab];
                pathOwner.m_State &= ~(PathFlags.AddDestination | PathFlags.Divert);
                PathfindParameters parameters = new PathfindParameters
                {
                    m_MaxSpeed = 277.77777f,
                    m_WalkSpeed = humanData.m_WalkSpeed,
                    m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                    m_Methods = (PathMethod.Pedestrian | RouteUtils.GetTaxiMethods(resident) | RouteUtils.GetPublicTransportMethods(resident, m_TimeOfDay)),
                    m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
                    m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost,
                };
                SetupQueueTarget origin = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Pedestrian,
                    m_RandomCost = 30f,
                };
                SetupQueueTarget destination = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = target.m_Target,
                    m_RandomCost = 30f,
                    m_ActivityMask = creatureData.m_SupportedActivities,
                };
                Entity entity2 = Entity.Null;
                Entity entity3 = target.m_Target;
                if (m_PropertyRenters.TryGetComponent(target.m_Target, out var componentData))
                {
                    entity3 = componentData.m_Property;
                }
                if (
                    m_HouseholdMembers.TryGetComponent(resident.m_Citizen, out var componentData2)
                    && m_PropertyRenters.TryGetComponent(componentData2.m_Household, out componentData)
                )
                {
                    entity2 = componentData.m_Property;
                    parameters.m_Authorization1 = componentData.m_Property;
                }
                if (m_CitizenData.TryGetComponent(resident.m_Citizen, out var componentData3))
                {
                    Household household = m_HouseholdData[componentData2.m_Household];
                    DynamicBuffer<HouseholdCitizen> dynamicBuffer = m_HouseholdCitizens[componentData2.m_Household];
                    parameters.m_Weights = CitizenUtils.GetPathfindWeights(componentData3, household, dynamicBuffer.Length);
                }
                if (m_WorkerData.HasComponent(resident.m_Citizen))
                {
                    Worker worker = m_WorkerData[resident.m_Citizen];
                    if (m_PropertyRenters.HasComponent(worker.m_Workplace))
                    {
                        parameters.m_Authorization2 = m_PropertyRenters[worker.m_Workplace].m_Property;
                    }
                    else
                    {
                        parameters.m_Authorization2 = worker.m_Workplace;
                    }
                }
                float value = 20f;
                if (m_CurrentDistrictData.TryGetComponent(entity2, out var componentData4) && m_DistrictModifiers.TryGetBuffer(componentData4.m_District, out var bufferData))
                {
                    AreaUtils.ApplyModifier(ref value, bufferData, DistrictModifierType.BikeProbability);
                }
                bool flag = random.NextFloat(100f) < value;
                BicycleOwner component2;
                if (m_CarKeeperData.TryGetEnabledComponent(resident.m_Citizen, out var component))
                {
                    if (m_ParkedCarData.TryGetComponent(component.m_Car, out var componentData5))
                    {
                        PrefabRef prefabRef2 = m_PrefabRefData[component.m_Car];
                        CarData carData = m_PrefabCarData[prefabRef2.m_Prefab];
                        parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
                        parameters.m_ParkingTarget = componentData5.m_Lane;
                        parameters.m_ParkingDelta = componentData5.m_CurvePosition;
                        parameters.m_ParkingSize = VehicleUtils.GetParkingSize(component.m_Car, ref m_PrefabRefData, ref m_PrefabObjectGeometryData);
                        parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
                        parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
                        if (m_PersonalCarData.TryGetComponent(component.m_Car, out var componentData6) && (componentData6.m_State & PersonalCarFlags.HomeTarget) == 0)
                        {
                            parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
                        }
                    }
                }
                else if (m_BicycleOwnerData.TryGetEnabledComponent(resident.m_Citizen, out component2))
                {
                    if (
                        (
                            !m_PrefabRefData.TryGetComponent(component2.m_Bicycle, out var componentData7)
                            && m_CurrentBuildingData.TryGetComponent(resident.m_Citizen, out var componentData8)
                            && componentData8.m_CurrentBuilding == entity2
                        ) & flag
                    )
                    {
                        Unity.Mathematics.Random random2 = componentData3.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
                        componentData7.m_Prefab = m_PersonalCarSelectData.SelectVehiclePrefab(
                            ref random2,
                            1,
                            0,
                            avoidTrailers: true,
                            noSlowVehicles: false,
                            bicycle: true,
                            out var _
                        );
                    }
                    if (
                        m_PrefabCarData.TryGetComponent(componentData7.m_Prefab, out var componentData9)
                        && m_PrefabObjectGeometryData.TryGetComponent(componentData7.m_Prefab, out var componentData10)
                    )
                    {
                        parameters.m_MaxSpeed.x = componentData9.m_MaxSpeed;
                        parameters.m_ParkingSize = VehicleUtils.GetParkingSize(componentData10, out var _);
                        parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
                        parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
                        if (m_ParkedCarData.TryGetComponent(component2.m_Bicycle, out var componentData11))
                        {
                            parameters.m_ParkingTarget = componentData11.m_Lane;
                            parameters.m_ParkingDelta = componentData11.m_CurvePosition;
                            if (m_PersonalCarData.TryGetComponent(component2.m_Bicycle, out var componentData12) && (componentData12.m_State & PersonalCarFlags.HomeTarget) == 0)
                            {
                                parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
                            }
                        }
                        if (entity3 == entity2 && divert.m_Purpose == Purpose.None)
                        {
                            destination.m_Methods |= PathMethod.Bicycle;
                            destination.m_RoadTypes |= RoadTypes.Bicycle;
                        }
                    }
                }
                bool flag2 = false;
                if (m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData13))
                {
                    switch (componentData13.m_Purpose)
                    {
                        case Purpose.EmergencyShelter:
                            parameters.m_Weights = new PathfindWeights(1f, 0.2f, 0f, 0.1f);
                            break;
                        case Purpose.Hospital:
                        case Purpose.Deathcare:
                        {
                            flag2 =
                                m_HealthProblemData.TryGetComponent(resident.m_Citizen, out var componentData14)
                                && (componentData14.m_Flags & HealthProblemFlags.RequireTransport) != 0;
                            break;
                        }
                    }
                }
                if ((resident.m_Flags & ResidentFlags.IgnoreBenches) != 0)
                {
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.BenchSitting).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.PullUps).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Reading).m_Mask;
                }
                if ((resident.m_Flags & ResidentFlags.IgnoreAreas) != 0)
                {
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Standing).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.GroundLaying).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.GroundSitting).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.PushUps).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.SitUps).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.JumpingJacks).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.JumpingLunges).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Squats).m_Mask;
                    destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Yoga).m_Mask;
                }
                if (flag2)
                {
                    human.m_Flags |= HumanFlags.Carried;
                    currentLane.m_CurvePosition.y = currentLane.m_CurvePosition.x;
                    pathOwner.m_ElementIndex = 0;
                    pathOwner.m_State &= ~(PathFlags.Failed | PathFlags.Obsolete | PathFlags.DivertObsolete | PathFlags.CachedObsolete);
                    m_PathElements[entity].Clear();
                }
                else if (CreatureUtils.DivertDestination(ref destination, ref pathOwner, divert))
                {
                    CreatureUtils.SetupPathfind(
                        item: new SetupQueueItem(entity, parameters, origin, destination),
                        currentLane: ref currentLane,
                        pathOwner: ref pathOwner,
                        queue: m_PathfindQueue
                    );
                }
                else
                {
                    currentLane.m_CurvePosition.y = currentLane.m_CurvePosition.x;
                    pathOwner.m_ElementIndex = 0;
                    pathOwner.m_State |= PathFlags.CachedObsolete;
                    pathOwner.m_State &= ~(PathFlags.Failed | PathFlags.Obsolete | PathFlags.DivertObsolete);
                    m_PathElements[entity].Clear();
                }
            }

            private void ActivateParkedCar(
                int jobIndex,
                ref Unity.Mathematics.Random random,
                Entity entity,
                Entity carEntity,
                ref Game.Creatures.Resident resident,
                ref PathOwner pathOwner,
                ref Target target,
                DynamicBuffer<GroupCreature> groupCreatures
            )
            {
                ParkedCar parkedCar = m_ParkedCarData[carEntity];
                Game.Vehicles.CarLaneFlags carLaneFlags = Game.Vehicles.CarLaneFlags.EndReached | Game.Vehicles.CarLaneFlags.ParkingSpace | Game.Vehicles.CarLaneFlags.FixedLane;
                DynamicBuffer<LayoutElement> dynamicBuffer = default;
                if (m_VehicleLayouts.HasBuffer(carEntity))
                {
                    dynamicBuffer = m_VehicleLayouts[carEntity];
                }
                if (parkedCar.m_Lane == Entity.Null)
                {
                    DynamicBuffer<PathElement> dynamicBuffer2 = m_PathElements[entity];
                    if (dynamicBuffer2.Length > pathOwner.m_ElementIndex)
                    {
                        PathElement pathElement = dynamicBuffer2[pathOwner.m_ElementIndex];
                        if (m_CurveData.HasComponent(pathElement.m_Target))
                        {
                            parkedCar.m_Lane = pathElement.m_Target;
                            Curve curve = m_CurveData[parkedCar.m_Lane];
                            Game.Objects.Transform transform = m_TransformData[entity];
                            MathUtils.Distance(curve.m_Bezier, transform.m_Position, out parkedCar.m_CurvePosition);
                            Game.Objects.Transform component = VehicleUtils.CalculateTransform(curve, parkedCar.m_CurvePosition);
                            bool flag = false;
                            if (m_ConnectionLaneData.HasComponent(parkedCar.m_Lane))
                            {
                                Game.Net.ConnectionLane connectionLane = m_ConnectionLaneData[parkedCar.m_Lane];
                                if ((connectionLane.m_Flags & ConnectionLaneFlags.Parking) != 0)
                                {
                                    parkedCar.m_CurvePosition = random.NextFloat(0f, 1f);
                                    component.m_Position = VehicleUtils.GetConnectionParkingPosition(connectionLane, curve.m_Bezier, parkedCar.m_CurvePosition);
                                }
                                flag = true;
                            }
                            m_CommandBuffer.SetComponent(jobIndex, carEntity, component);
                            if (flag)
                            {
                                m_CommandBuffer.AddComponent<Unspawned>(jobIndex, carEntity, default);
                            }
                            if (dynamicBuffer.IsCreated)
                            {
                                for (int i = 1; i < dynamicBuffer.Length; i++)
                                {
                                    Entity vehicle = dynamicBuffer[i].m_Vehicle;
                                    m_CommandBuffer.SetComponent(jobIndex, vehicle, component);
                                    if (flag)
                                    {
                                        m_CommandBuffer.AddComponent<Unspawned>(jobIndex, vehicle, default);
                                    }
                                }
                            }
                        }
                    }
                }
                if (m_ConnectionLaneData.HasComponent(parkedCar.m_Lane))
                {
                    carLaneFlags = (
                        ((m_ConnectionLaneData[parkedCar.m_Lane].m_Flags & ConnectionLaneFlags.Area) == 0)
                            ? (carLaneFlags | Game.Vehicles.CarLaneFlags.Connection)
                            : (carLaneFlags | Game.Vehicles.CarLaneFlags.Area)
                    );
                }
                Game.Vehicles.PersonalCar component2 = m_PersonalCarData[carEntity];
                component2.m_State |= PersonalCarFlags.Boarding;
                m_CommandBuffer.RemoveComponent(jobIndex, carEntity, in m_ParkedToMovingCarRemoveTypes);
                m_CommandBuffer.AddComponent(jobIndex, carEntity, in m_ParkedToMovingCarAddTypes);
                m_CommandBuffer.SetComponent(jobIndex, carEntity, component2);
                m_CommandBuffer.SetComponent(jobIndex, carEntity, new CarCurrentLane(parkedCar, carLaneFlags));
                if (m_ParkingLaneData.HasComponent(parkedCar.m_Lane) || m_GarageLaneData.HasComponent(parkedCar.m_Lane))
                {
                    m_CommandBuffer.AddComponent<PathfindUpdated>(jobIndex, parkedCar.m_Lane);
                }
                if (dynamicBuffer.IsCreated)
                {
                    for (int j = 1; j < dynamicBuffer.Length; j++)
                    {
                        Entity vehicle2 = dynamicBuffer[j].m_Vehicle;
                        ParkedCar parkedCar2 = m_ParkedCarData[vehicle2];
                        if (parkedCar2.m_Lane == Entity.Null)
                        {
                            parkedCar2.m_Lane = parkedCar.m_Lane;
                            parkedCar2.m_CurvePosition = parkedCar.m_CurvePosition;
                        }
                        m_CommandBuffer.RemoveComponent(jobIndex, vehicle2, in m_ParkedToMovingCarRemoveTypes);
                        m_CommandBuffer.AddComponent(jobIndex, vehicle2, in m_ParkedToMovingTrailerAddTypes);
                        m_CommandBuffer.SetComponent(jobIndex, vehicle2, new CarTrailerLane(parkedCar2));
                        if ((m_ParkingLaneData.HasComponent(parkedCar2.m_Lane) || m_GarageLaneData.HasComponent(parkedCar2.m_Lane)) && parkedCar2.m_Lane != parkedCar.m_Lane)
                        {
                            m_CommandBuffer.AddComponent<PathfindUpdated>(jobIndex, parkedCar2.m_Lane);
                        }
                    }
                }
                if (dynamicBuffer.IsCreated && dynamicBuffer.Length > 1)
                {
                    return;
                }
                int num = 1;
                int num2 = 0;
                if (groupCreatures.IsCreated)
                {
                    for (int k = 0; k < groupCreatures.Length; k++)
                    {
                        if (m_AnimalData.HasComponent(groupCreatures[k].m_Creature))
                        {
                            num2++;
                        }
                        else
                        {
                            num++;
                        }
                    }
                }
                int num3 = num;
                int num4 = 1 + num2;
                if (m_TravelPurposeData.HasComponent(resident.m_Citizen))
                {
                    switch (m_TravelPurposeData[resident.m_Citizen].m_Purpose)
                    {
                        case Purpose.MovingAway:
                            if (random.NextInt(20) == 0)
                            {
                                num3 += 5;
                                num4 += 5;
                            }
                            else if (random.NextInt(10) == 0)
                            {
                                num4 += 5;
                                if (random.NextInt(10) == 0)
                                {
                                    num4 += 5;
                                }
                            }
                            break;
                        case Purpose.Leisure:
                            if (random.NextInt(20) == 0)
                            {
                                num3 += 5;
                                num4 += 5;
                            }
                            break;
                        case Purpose.Shopping:
                            if (random.NextInt(10) == 0)
                            {
                                num4 += 5;
                                if (random.NextInt(10) == 0)
                                {
                                    num4 += 5;
                                }
                            }
                            break;
                    }
                }
                Game.Objects.Transform tractorTransform = m_TransformData[carEntity];
                PrefabRef prefabRef = m_PrefabRefData[carEntity];
                Entity entity2 = m_PersonalCarSelectData.CreateTrailer(
                    m_CommandBuffer,
                    jobIndex,
                    ref random,
                    num3,
                    num4,
                    noSlowVehicles: false,
                    prefabRef.m_Prefab,
                    tractorTransform,
                    (PersonalCarFlags)0u,
                    stopped: false
                );
                if (entity2 != Entity.Null)
                {
                    DynamicBuffer<LayoutElement> dynamicBuffer3 = (
                        (!dynamicBuffer.IsCreated) ? m_CommandBuffer.AddBuffer<LayoutElement>(jobIndex, carEntity) : m_CommandBuffer.SetBuffer<LayoutElement>(jobIndex, carEntity)
                    );
                    dynamicBuffer3.Add(new LayoutElement(carEntity));
                    dynamicBuffer3.Add(new LayoutElement(entity2));
                    m_CommandBuffer.SetComponent(jobIndex, entity2, new Controller(carEntity));
                    m_CommandBuffer.SetComponent(jobIndex, entity2, new CarTrailerLane(parkedCar));
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        private EndFrameBarrier m_EndFrameBarrier;

        private SimulationSystem m_SimulationSystem;

        private PathfindSetupSystem m_PathfindSetupSystem;

        private TimeSystem m_TimeSystem;

        private CityConfigurationSystem m_CityConfigurationSystem;

        private ResidentAISystem.Actions m_Actions;

        private PersonalCarSelectData m_PersonalCarSelectData;

        private EntityQuery m_CreatureQuery;

        private EntityQuery m_GroupCreatureQuery;

        private EntityQuery m_CarPrefabQuery;

        private EntityArchetype m_ResetTripArchetype;

        private ComponentTypeSet m_ParkedToMovingCarRemoveTypes;

        private ComponentTypeSet m_ParkedToMovingCarAddTypes;

        private ComponentTypeSet m_ParkedToMovingTrailerAddTypes;

        [EnumArray(typeof(DeletedResidentType))]
        [DebugWatchValue]
        private NativeArray<int> m_DeletedResidents;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();
            m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            m_Actions = World.GetOrCreateSystemManaged<ResidentAISystem.Actions>();
            m_PersonalCarSelectData = new PersonalCarSelectData(this);
            m_CreatureQuery = GetEntityQuery(
                ComponentType.ReadWrite<Game.Creatures.Resident>(),
                ComponentType.ReadOnly<UpdateFrame>(),
                ComponentType.Exclude<GroupMember>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Stumbling>()
            );
            m_GroupCreatureQuery = GetEntityQuery(
                ComponentType.ReadWrite<Game.Creatures.Resident>(),
                ComponentType.ReadOnly<GroupMember>(),
                ComponentType.ReadOnly<UpdateFrame>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Stumbling>()
            );
            m_CarPrefabQuery = GetEntityQuery(PersonalCarSelectData.GetEntityQueryDesc());
            m_ResetTripArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<Game.Common.Event>(), ComponentType.ReadWrite<ResetTrip>());
            m_ParkedToMovingCarRemoveTypes = new ComponentTypeSet(ComponentType.ReadWrite<ParkedCar>(), ComponentType.ReadWrite<Stopped>());
            m_ParkedToMovingCarAddTypes = new ComponentTypeSet(
                new ComponentType[12]
                {
                    ComponentType.ReadWrite<Moving>(),
                    ComponentType.ReadWrite<TransformFrame>(),
                    ComponentType.ReadWrite<InterpolatedTransform>(),
                    ComponentType.ReadWrite<CarNavigation>(),
                    ComponentType.ReadWrite<CarNavigationLane>(),
                    ComponentType.ReadWrite<CarCurrentLane>(),
                    ComponentType.ReadWrite<PathOwner>(),
                    ComponentType.ReadWrite<Target>(),
                    ComponentType.ReadWrite<Blocker>(),
                    ComponentType.ReadWrite<PathElement>(),
                    ComponentType.ReadWrite<Swaying>(),
                    ComponentType.ReadWrite<Updated>(),
                }
            );
            m_ParkedToMovingTrailerAddTypes = new ComponentTypeSet(
                new ComponentType[6]
                {
                    ComponentType.ReadWrite<Moving>(),
                    ComponentType.ReadWrite<TransformFrame>(),
                    ComponentType.ReadWrite<InterpolatedTransform>(),
                    ComponentType.ReadWrite<CarTrailerLane>(),
                    ComponentType.ReadWrite<Swaying>(),
                    ComponentType.ReadWrite<Updated>(),
                }
            );
            m_DeletedResidents = new NativeArray<int>(7, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_DeletedResidents.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            uint index = m_SimulationSystem.frameIndex % 16;
            m_CreatureQuery.SetSharedComponentFilter(new UpdateFrame(index));
            m_GroupCreatureQuery.SetSharedComponentFilter(new UpdateFrame(index));
            m_Actions.m_BoardingQueue = new NativeQueue<ResidentAISystem.Boarding>(Allocator.TempJob);
            m_Actions.m_ActionQueue = new NativeQueue<ResidentAISystem.ResidentAction>(Allocator.TempJob);
            m_PersonalCarSelectData.PreUpdate(this, m_CityConfigurationSystem, m_CarPrefabQuery, Allocator.TempJob, out var jobHandle);
            ResidentTickJob jobData = new ResidentTickJob
            {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_CurrentVehicleType = SystemAPI.GetComponentTypeHandle<CurrentVehicle>(true),
                m_GroupMemberType = SystemAPI.GetComponentTypeHandle<GroupMember>(true),
                m_UnspawnedType = SystemAPI.GetComponentTypeHandle<Unspawned>(true),
                m_HumanNavigationType = SystemAPI.GetComponentTypeHandle<HumanNavigation>(true),
                m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                m_GroupCreatureType = SystemAPI.GetBufferTypeHandle<GroupCreature>(true),
                m_CreatureType = SystemAPI.GetComponentTypeHandle<Creature>(false),
                m_HumanType = SystemAPI.GetComponentTypeHandle<Human>(false),
                m_ResidentType = SystemAPI.GetComponentTypeHandle<Game.Creatures.Resident>(false),
                m_CurrentLaneType = SystemAPI.GetComponentTypeHandle<HumanCurrentLane>(false),
                m_TargetType = SystemAPI.GetComponentTypeHandle<Target>(false),
                m_DivertType = SystemAPI.GetComponentTypeHandle<Divert>(false),
                m_EntityLookup = SystemAPI.GetEntityStorageInfoLookup(),
                m_HumanData = SystemAPI.GetComponentLookup<Human>(false),
                m_TransformData = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                m_OwnerData = SystemAPI.GetComponentLookup<Owner>(true),
                m_TargetData = SystemAPI.GetComponentLookup<Target>(true),
                m_PseudoRandomSeedData = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                m_CurrentVehicleData = SystemAPI.GetComponentLookup<CurrentVehicle>(true),
                m_DestroyedData = SystemAPI.GetComponentLookup<Destroyed>(true),
                m_DeletedData = SystemAPI.GetComponentLookup<Deleted>(true),
                m_UnspawnedData = SystemAPI.GetComponentLookup<Unspawned>(true),
                m_RideNeederData = SystemAPI.GetComponentLookup<RideNeeder>(true),
                m_MovingData = SystemAPI.GetComponentLookup<Moving>(true),
                m_SpawnLocation = SystemAPI.GetComponentLookup<Game.Objects.SpawnLocation>(true),
                m_AnimalData = SystemAPI.GetComponentLookup<Animal>(true),
                m_Dispatched = SystemAPI.GetComponentLookup<Dispatched>(true),
                m_ServiceRequestData = SystemAPI.GetComponentLookup<ServiceRequest>(true),
                m_OnFireData = SystemAPI.GetComponentLookup<OnFire>(true),
                m_EdgeData = SystemAPI.GetComponentLookup<Game.Net.Edge>(true),
                m_CurveData = SystemAPI.GetComponentLookup<Curve>(true),
                m_LaneData = SystemAPI.GetComponentLookup<Lane>(true),
                m_EdgeLaneData = SystemAPI.GetComponentLookup<EdgeLane>(true),
                m_ParkingLaneData = SystemAPI.GetComponentLookup<Game.Net.ParkingLane>(true),
                m_GarageLaneData = SystemAPI.GetComponentLookup<GarageLane>(true),
                m_PedestrianLaneData = SystemAPI.GetComponentLookup<Game.Net.PedestrianLane>(true),
                m_ConnectionLaneData = SystemAPI.GetComponentLookup<Game.Net.ConnectionLane>(true),
                m_LaneSignalData = SystemAPI.GetComponentLookup<LaneSignal>(true),
                m_HangaroundLocationData = SystemAPI.GetComponentLookup<HangaroundLocation>(true),
                m_CitizenData = SystemAPI.GetComponentLookup<Citizen>(true),
                m_HouseholdMembers = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                m_HouseholdData = SystemAPI.GetComponentLookup<Household>(true),
                m_CurrentBuildingData = SystemAPI.GetComponentLookup<CurrentBuilding>(true),
                m_CurrentTransportData = SystemAPI.GetComponentLookup<CurrentTransport>(true),
                m_WorkerData = SystemAPI.GetComponentLookup<Worker>(true),
                m_CarKeeperData = SystemAPI.GetComponentLookup<CarKeeper>(true),
                m_BicycleOwnerData = SystemAPI.GetComponentLookup<BicycleOwner>(true),
                m_HealthProblemData = SystemAPI.GetComponentLookup<HealthProblem>(true),
                m_TravelPurposeData = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                m_HomelessHouseholdData = SystemAPI.GetComponentLookup<HomelessHousehold>(true),
                m_TouristHouseholds = SystemAPI.GetComponentLookup<TouristHousehold>(true),
                m_HouseholdNeedData = SystemAPI.GetComponentLookup<HouseholdNeed>(true),
                m_AttendingMeetingData = SystemAPI.GetComponentLookup<AttendingMeeting>(true),
                m_CoordinatedMeetingData = SystemAPI.GetComponentLookup<CoordinatedMeeting>(true),
                m_MovingAwayData = SystemAPI.GetComponentLookup<MovingAway>(true),
                m_ServiceAvailableData = SystemAPI.GetComponentLookup<ServiceAvailable>(true),
                m_ParkedCarData = SystemAPI.GetComponentLookup<ParkedCar>(true),
                m_PersonalCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(true),
                m_TaxiData = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(true),
                m_PublicTransportData = SystemAPI.GetComponentLookup<Game.Vehicles.PublicTransport>(true),
                m_PoliceCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                m_AmbulanceData = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                m_HearseData = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                m_ControllerData = SystemAPI.GetComponentLookup<Controller>(true),
                m_VehicleData = SystemAPI.GetComponentLookup<Vehicle>(true),
                m_TrainData = SystemAPI.GetComponentLookup<Train>(true),
                m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                m_AttractivenessProviderData = SystemAPI.GetComponentLookup<AttractivenessProvider>(true),
                m_RouteConnectedData = SystemAPI.GetComponentLookup<Connected>(true),
                m_BoardingVehicleData = SystemAPI.GetComponentLookup<BoardingVehicle>(true),
                m_CurrentRouteData = SystemAPI.GetComponentLookup<CurrentRoute>(true),
                m_TransportLineData = SystemAPI.GetComponentLookup<TransportLine>(true),
                m_AccessLaneLaneData = SystemAPI.GetComponentLookup<AccessLane>(true),
                m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_PrefabCreatureData = SystemAPI.GetComponentLookup<CreatureData>(true),
                m_PrefabHumanData = SystemAPI.GetComponentLookup<HumanData>(true),
                m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(true),
                m_PrefabCarData = SystemAPI.GetComponentLookup<CarData>(true),
                m_PrefabIndustrialProcessData = SystemAPI.GetComponentLookup<IndustrialProcessData>(true),
                m_PrefabTransportStopData = SystemAPI.GetComponentLookup<TransportStopData>(true),
                m_PrefabSpawnLocationData = SystemAPI.GetComponentLookup<SpawnLocationData>(true),
                m_HouseholdAnimals = SystemAPI.GetBufferLookup<HouseholdAnimal>(true),
                m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(true),
                m_ConnectedRoutes = SystemAPI.GetBufferLookup<ConnectedRoute>(true),
                m_VehicleLayouts = SystemAPI.GetBufferLookup<LayoutElement>(true),
                m_CarNavigationLanes = SystemAPI.GetBufferLookup<CarNavigationLane>(true),
                m_ConnectedEdges = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                m_SubLanes = SystemAPI.GetBufferLookup<Game.Net.SubLane>(true),
                m_AreaNodes = SystemAPI.GetBufferLookup<Game.Areas.Node>(true),
                m_AreaTriangles = SystemAPI.GetBufferLookup<Triangle>(true),
                m_ConnectedBuildings = SystemAPI.GetBufferLookup<ConnectedBuilding>(true),
                m_Renters = SystemAPI.GetBufferLookup<Renter>(true),
                m_SpawnLocationElements = SystemAPI.GetBufferLookup<SpawnLocationElement>(true),
                m_Resources = SystemAPI.GetBufferLookup<Game.Economy.Resources>(true),
                m_ServiceDispatches = SystemAPI.GetBufferLookup<ServiceDispatch>(true),
                m_PrefabActivityLocationElements = SystemAPI.GetBufferLookup<ActivityLocationElement>(true),
                m_CurrentDistrictData = SystemAPI.GetComponentLookup<CurrentDistrict>(true),
                m_DistrictModifiers = SystemAPI.GetBufferLookup<DistrictModifier>(true),
                m_PathOwnerData = SystemAPI.GetComponentLookup<PathOwner>(true),
                m_PathElements = SystemAPI.GetBufferLookup<PathElement>(true),
                m_RandomSeed = RandomSeed.Next(),
                m_SimulationFrameIndex = m_SimulationSystem.frameIndex,
                m_LefthandTraffic = m_CityConfigurationSystem.leftHandTraffic,
                m_GroupMember = false,
                m_PersonalCarSelectData = m_PersonalCarSelectData,
                m_ResetTripArchetype = m_ResetTripArchetype,
                m_ParkedToMovingCarRemoveTypes = m_ParkedToMovingCarRemoveTypes,
                m_ParkedToMovingCarAddTypes = m_ParkedToMovingCarAddTypes,
                m_ParkedToMovingTrailerAddTypes = m_ParkedToMovingTrailerAddTypes,
                m_DeletedResidents = m_DeletedResidents,
                m_TimeOfDay = m_TimeSystem.normalizedTime,
                m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
                m_BoardingQueue = m_Actions.m_BoardingQueue.AsParallelWriter(),
                m_ActionQueue = m_Actions.m_ActionQueue.AsParallelWriter(),
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                m_CustomWaypointLookup = SystemAPI.GetComponentLookup<CustomWaypoint>(true),
                m_WaypointLookup = SystemAPI.GetComponentLookup<Waypoint>(true),
                m_RouteWaypointLookup = SystemAPI.GetBufferLookup<RouteWaypoint>(true),
            };
            JobHandle dependsOn = JobChunkExtensions.ScheduleParallel(jobData, m_CreatureQuery, JobHandle.CombineDependencies(Dependency, jobHandle));
            jobData.m_GroupMember = true;
            JobHandle jobHandle2 = JobChunkExtensions.ScheduleParallel(jobData, m_GroupCreatureQuery, dependsOn);
            m_PersonalCarSelectData.PostUpdate(jobHandle2);
            m_PathfindSetupSystem.AddQueueWriter(jobHandle2);
            m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
            m_Actions.m_Dependency = jobHandle2;
            Dependency = jobHandle2;
        }

        public PatchedResidentAISystem() { }
    }
}
