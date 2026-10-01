using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoardingController.Components;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Common;
using Game.Prefabs;
using Game.Routes;
using Newtonsoft.Json;
using Unity.Entities;

namespace BoardingController.Systems.UI
{
    public partial class UISystem
    {
        private void AddUIBindings()
        {
            AddBinding(
                new CallBinding<string, string>(
                    "BoardingController",
                    "GetWaypoints",
                    (inputJsonString) =>
                    {
                        var inputValue = JsonConvert.DeserializeAnonymousType(inputJsonString, new { line = Entity.Null });
                        var rs = new List<UITypes.Waypoint>();
                        if (EntityManager.TryGetBuffer(inputValue.line, true, out DynamicBuffer<RouteWaypoint> routeWaypointBuffer))
                        {
                            for (int i = 0; i < routeWaypointBuffer.Length; i++)
                            {
                                var routeWaypoint = routeWaypointBuffer[i];
                                if (EntityManager.TryGetComponent(routeWaypoint.m_Waypoint, out Connected connected))
                                {
                                    var stop = connected.m_Connected;
                                    var isSelected = false;
                                    while (true)
                                    {
                                        if (Equals(stop, m_SelectedInfoUISystem.selectedEntity))
                                        {
                                            isSelected = true;
                                            break;
                                        }
                                        if (!EntityManager.TryGetComponent(stop, out Owner owner))
                                        {
                                            break;
                                        }
                                        if (EntityManager.HasComponent<TransportStation>(stop) && !EntityManager.HasComponent<TransportStation>(owner.m_Owner))
                                        {
                                            break;
                                        }
                                        stop = owner.m_Owner;
                                    }
                                    if (isSelected)
                                    {
                                        if (!EntityManager.TryGetComponent(routeWaypoint.m_Waypoint, out CustomWaypoint customWaypoint))
                                        {
                                            customWaypoint = new CustomWaypoint();
                                        }

                                        rs.Add(
                                            new UITypes.Waypoint
                                            {
                                                entity = new UITypes.Entity { index = routeWaypoint.m_Waypoint.Index, version = routeWaypoint.m_Waypoint.Version },
                                                isLinked = (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0,
                                            }
                                        );
                                    }
                                }
                            }
                        }
                        return JsonConvert.SerializeObject(rs);
                    }
                )
            );
            AddBinding(
                new CallBinding<string, string>(
                    "BoardingController",
                    "SetWaypoint",
                    (inputJsonString) =>
                    {
                        var inputValue = JsonConvert.DeserializeAnonymousType(inputJsonString, new UITypes.Waypoint());
                        var entity = new Entity { Index = inputValue.entity.index, Version = inputValue.entity.version };
                        if (!EntityManager.TryGetComponent(entity, out CustomWaypoint customWaypoint))
                        {
                            customWaypoint = new CustomWaypoint();
                        }

                        if (inputValue.isLinked)
                        {
                            customWaypoint.m_Options |= CustomWaypoint.Options.Linked;
                        }
                        else
                        {
                            customWaypoint.m_Options &= ~CustomWaypoint.Options.Linked;
                        }

                        EntityManager.AddComponentData(entity, customWaypoint);
                        if (
                            EntityManager.TryGetComponent<Game.Routes.Waypoint>(entity, out var waypoint)
                            && EntityManager.TryGetComponent<Owner>(entity, out var owner)
                            && EntityManager.TryGetBuffer<RouteWaypoint>(owner.m_Owner, true, out var routeWaypointBuffer)
                            && EntityManager.TryGetBuffer<RouteSegment>(owner.m_Owner, true, out var routeSegmentBuffer)
                        )
                        {
                            EntityManager.AddComponentData(m_SelectedInfoUISystem.selectedEntity, new Updated());
                            EntityManager.AddComponentData(owner.m_Owner, new Updated());
                            for (int i = 0; i < routeWaypointBuffer.Length; i++)
                            {
                                EntityManager.AddComponentData(routeWaypointBuffer[i].m_Waypoint, new Updated());
                                EntityManager.AddComponentData(routeSegmentBuffer[i].m_Segment, new Updated());
                            }
                        }
                        return "";
                    }
                )
            );
        }
    }
}
