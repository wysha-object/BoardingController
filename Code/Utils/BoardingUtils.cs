using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Vehicles;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace BoardingController.Utils
{
    public static class BoardingUtils
    {
        public static bool BoardingSpaceReached(
            PathOwner pathOwner,
            CarCurrentLane currentLane,
            DynamicBuffer<PathElement> pathElementBuffer,
            DynamicBuffer<CarNavigationLane> carNavigationLaneBuffer,
            ref ComponentLookup<Owner> ownerLookup,
            ref ComponentLookup<SlaveLane> slaveLaneLookup
        )
        {
            if (pathElementBuffer.Length != 0 && pathOwner.m_ElementIndex != pathElementBuffer.Length)
            {
                return false;
            }
            if (currentLane.m_ChangeLane != Entity.Null)
            {
                return false;
            }
            if (carNavigationLaneBuffer.Length != 0)
            {
                var lastLane = carNavigationLaneBuffer[carNavigationLaneBuffer.Length - 1].m_Lane;
                if (ownerLookup.TryGetComponent(currentLane.m_Lane, out var currentOwner) && ownerLookup.TryGetComponent(lastLane, out var lastOwner))
                {
                    if (currentOwner.m_Owner != lastOwner.m_Owner)
                    {
                        return false;
                    }
                }
                if (slaveLaneLookup.TryGetComponent(currentLane.m_Lane, out var currentSlave) && slaveLaneLookup.TryGetComponent(lastLane, out var lastSlave))
                {
                    if (currentSlave.m_Group != lastSlave.m_Group)
                    {
                        return false;
                    }
                }
                else if (currentLane.m_Lane != lastLane)
                {
                    return false;
                }
            }
            return true;
        }

        public static float CalculateDistance(
            PathOwner pathOwner,
            CarCurrentLane currentLane,
            DynamicBuffer<PathElement> pathElementBuffer,
            DynamicBuffer<CarNavigationLane> carNavigationLaneBuffer,
            ref ComponentLookup<Curve> curveLookup
        )
        {
            float distance = 0;
            if (curveLookup.TryGetComponent(currentLane.m_Lane, out var curve))
            {
                distance = curve.m_Length * math.abs(currentLane.m_CurvePosition.z - currentLane.m_CurvePosition.x);
            }
            foreach (var item in carNavigationLaneBuffer)
            {
                if (curveLookup.TryGetComponent(item.m_Lane, out curve))
                {
                    distance += curve.m_Length * math.abs(item.m_CurvePosition.y - item.m_CurvePosition.x);
                }
            }
            for (int i = pathOwner.m_ElementIndex; i < pathElementBuffer.Length; i++)
            {
                var item = pathElementBuffer[i];
                if (curveLookup.TryGetComponent(item.m_Target, out curve))
                {
                    distance += curve.m_Length * math.abs(item.m_TargetDelta.y - item.m_TargetDelta.x);
                }
            }
            return distance;
        }
    }
}
