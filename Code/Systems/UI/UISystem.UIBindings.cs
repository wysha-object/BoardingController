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
        private ValueBinding<int> m_GetToolStateBinding;

        private void AddUIBindings()
        {
            AddBinding(m_GetToolStateBinding = new ValueBinding<int>("BoardingController", "GetToolState", (int)ToolState.Disabled));

            AddBinding(
                new CallBinding<int, string>(
                    "BoardingController",
                    "SetToolState",
                    (inputValue) =>
                    {
                        SetToolState((ToolState)inputValue);
                        return "";
                    }
                )
            );
            AddBinding(
                new CallBinding<string, string>(
                    "BoardingController",
                    "GetWaypoints",
                    (inputJsonString) =>
                    {
                        var inputValue = JsonConvert.DeserializeAnonymousType(inputJsonString, new { line = Entity.Null });
                        var rs = new List<Waypoint>();
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

                                        rs.Add(new Waypoint() { entity = routeWaypoint.m_Waypoint, isLinked = (customWaypoint.m_Options & CustomWaypoint.Options.Linked) != 0 });
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
                        var inputValue = JsonConvert.DeserializeAnonymousType(inputJsonString, new Waypoint());
                        if (!EntityManager.TryGetComponent(inputValue.entity, out CustomWaypoint customWaypoint))
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

                        EntityManager.AddComponentData(inputValue.entity, customWaypoint);
                        return "";
                    }
                )
            );
        }
    }
}
