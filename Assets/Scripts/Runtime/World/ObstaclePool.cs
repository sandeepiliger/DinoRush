using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // CLAUDE.md section 34: obstacles are pooled, never instantiated and destroyed per spawn.
    // At M4 scale the difference is invisible, but the allocation pattern is the thing being
    // established — section 35 forbids per-frame allocations and avoidable GC pressure, and
    // retrofitting pooling after the spawner exists is far more disruptive than starting with it.
    public sealed class ObstaclePool
    {
        private readonly Transform _parent;
        private readonly PlayerMotorConfig _config;
        private readonly Stack<GameObject> _idle = new Stack<GameObject>();
        private readonly List<GameObject> _all = new List<GameObject>();

        public ObstaclePool(Transform parent, PlayerMotorConfig config, int prewarmCount)
        {
            _parent = parent;
            _config = config;

            for (int i = 0; i < prewarmCount; i++)
                _idle.Push(CreateInstance());
        }

        public int TotalCreated => _all.Count;

        // The palette is applied at spawn time rather than re-tinting live objects every frame.
        // That's both cheaper (a hundred material writes per frame is real cost on mobile) and
        // better looking: obstacles spawn ~60m ahead, so during a biome blend the world in the
        // distance turns volcanic first and the change sweeps toward the player.
        public GameObject Rent(ObstacleSpawn spawn, float worldX, BiomePalette palette)
        {
            var instance = _idle.Count > 0 ? _idle.Pop() : CreateInstance();

            bool isGroundObstacle = spawn.RequiredAction == PlayerAction.Jump;
            float height = isGroundObstacle
                ? _config.JumpObstacleHeightMeters
                : _config.DuckObstacleTopMeters - _config.DuckObstacleBottomMeters;
            float centreY = isGroundObstacle
                ? height * 0.5f
                : _config.DuckObstacleBottomMeters + height * 0.5f;

            instance.transform.localScale = new Vector3(spawn.WidthMeters, height, 1.5f);
            instance.transform.position = new Vector3(worldX + spawn.WidthMeters * 0.5f, centreY, 0f);

            // Ground obstacles read as solid hazards, overhead ones as things to slip under.
            var renderer = instance.GetComponent<Renderer>();
            renderer.material.color = isGroundObstacle
                ? palette.GroundObstacle.ToColor()
                : palette.OverheadObstacle.ToColor();

            instance.SetActive(true);
            return instance;
        }

        public void Return(GameObject instance)
        {
            instance.SetActive(false);
            _idle.Push(instance);
        }

        private GameObject CreateInstance()
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = "Obstacle";
            instance.transform.SetParent(_parent, worldPositionStays: false);
            // Collision is resolved analytically in Core (CollisionResolver), so the primitive's
            // collider is dead weight — and leaving it in would let Unity physics produce a
            // second, disagreeing answer.
            Object.Destroy(instance.GetComponent<Collider>());
            instance.SetActive(false);
            _all.Add(instance);
            return instance;
        }
    }
}
