using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoardingController.Systems.UI;
using Game.Common;
using Game.Net;
using Game.Notifications;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BoardingController.Systems.Tool
{
    public partial class ToolSystem : ToolBaseSystem
    {
        public void Enable()
        {
            m_ToolSystem.activeTool = this;
        }

        public void Disable()
        {
            if (m_ToolSystem.activeTool == this)
            {
                m_ToolSystem.activeTool = m_DefaultToolSystem;
            }
            EntityManager.RemoveComponent<Highlighted>(m_LastEntity);
            EntityManager.AddComponent<BatchesUpdated>(m_LastEntity);
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            if (m_Underground)
            {
                m_ToolRaycastSystem.collisionMask = CollisionMask.Underground;
            }
            else
            {
                m_ToolRaycastSystem.collisionMask = (CollisionMask.OnGround | CollisionMask.Overground);
            }
            m_ToolRaycastSystem.typeMask = TypeMask.All;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.SubElements | RaycastFlags.Cargo | RaycastFlags.Passenger | RaycastFlags.EditorContainers;
            m_ToolRaycastSystem.netLayerMask = Layer.All;
        }

        private bool m_Underground;

        public override bool allowUnderground => true;

        public override void SetUnderground(bool isUnderground)
        {
            m_Underground = isUnderground;
        }

        public override void ElevationUp()
        {
            m_Underground = false;
        }

        public override void ElevationDown()
        {
            m_Underground = true;
        }

        public override void ElevationScroll()
        {
            m_Underground = !m_Underground;
        }

        public override string toolID => $"{nameof(BoardingController)} Tool";

        public override PrefabBase GetPrefab()
        {
            return null;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            return false;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
        }

        private Entity m_LastEntity;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            requireUnderground = m_Underground;

            base.applyAction.shouldBeEnabled = true;
            base.secondaryApplyAction.shouldBeEnabled = true;
            GetRaycastResult(out Entity entity, out RaycastHit hit);
            if (applyAction.WasReleasedThisFrame())
            {
                int i = 0;
            }
            if (secondaryApplyAction.WasReleasedThisFrame()) { }

            EntityManager.RemoveComponent<Highlighted>(m_LastEntity);
            EntityManager.AddComponent<BatchesUpdated>(m_LastEntity);
            if (true)
            {
                EntityManager.AddComponent<Highlighted>(entity);
                EntityManager.AddComponent<BatchesUpdated>(entity);
            }
            m_LastEntity = entity;

            return inputDeps;
        }
    }
}
