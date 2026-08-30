using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // An engine-free triangle mesh under construction.
    //
    // Why this lives in Core rather than being built straight into a UnityEngine.Mesh: the
    // dinosaur is generated, not imported (CLAUDE.md section 10 wants an automated pipeline,
    // and there is no Blender on the build machine — docs/DECISIONS.md D13). A generated asset
    // is only trustworthy if its geometry can be asserted on, and assertions need to run in
    // `dotnet test` like every other rule in this project (D9). So the mesh is data here, and
    // Runtime uploads it at the boundary.
    //
    // Skinning is stored as two influences per vertex rather than Unity's four. Two is enough
    // for a chain-of-rings body — a ring only ever sits between two consecutive bones — and it
    // halves the per-vertex skinning cost on mobile (section 35).
    public sealed class MeshBuffer
    {
        private readonly List<Vec3> _positions = new List<Vec3>();
        private readonly List<Vec3> _normals = new List<Vec3>();
        private readonly List<Vec2> _uvs = new List<Vec2>();
        private readonly List<int> _boneA = new List<int>();
        private readonly List<int> _boneB = new List<int>();
        private readonly List<float> _weightA = new List<float>();
        private readonly List<int> _triangles = new List<int>();

        public IReadOnlyList<Vec3> Positions => _positions;
        public IReadOnlyList<Vec3> Normals => _normals;
        public IReadOnlyList<Vec2> Uvs => _uvs;
        public IReadOnlyList<int> BoneA => _boneA;
        public IReadOnlyList<int> BoneB => _boneB;
        public IReadOnlyList<float> WeightA => _weightA;
        public IReadOnlyList<int> Triangles => _triangles;

        public int VertexCount => _positions.Count;
        public int TriangleCount => _triangles.Count / 3;

        public int AddVertex(Vec3 position, Vec2 uv, int boneA, int boneB, float weightA)
        {
            if (boneA < 0) throw new ArgumentOutOfRangeException(nameof(boneA));
            if (boneB < 0) throw new ArgumentOutOfRangeException(nameof(boneB));

            _positions.Add(position);
            _normals.Add(new Vec3(0f, 1f, 0f));
            _uvs.Add(uv);
            _boneA.Add(boneA);
            _boneB.Add(boneB);
            _weightA.Add(weightA < 0f ? 0f : weightA > 1f ? 1f : weightA);
            return _positions.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            if (a == b || b == c || a == c) return; // shares a vertex, e.g. at a cap's apex

            // Distinct indices are not enough: a loft station whose radii have tapered to zero
            // — the tip of a claw, the point of a tail — puts every vertex of its ring at the
            // same place, so the band and cap around it are built from different vertices that
            // happen to coincide. Those triangles cost a vertex fetch each and contribute
            // nothing but noise to the area-weighted normals of everything they touch.
            var ab = _positions[b] - _positions[a];
            var ac = _positions[c] - _positions[a];
            if (Vec3.Cross(ab, ac).Magnitude < 1e-10f) return;

            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        // Wound so that (a, b, c, d) traced in order has its front face towards the viewer.
        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        // Area-weighted vertex normals. Weighting by the cross product's un-normalised length
        // rather than averaging unit face normals matters here: the loft rings vary hugely in
        // size along the body, and unweighted averaging makes the tiny triangles at the snout
        // tip drag the shading of the much larger skull triangles next to them.
        public void RecalculateNormals()
        {
            for (int i = 0; i < _normals.Count; i++) _normals[i] = new Vec3(0f, 0f, 0f);

            for (int t = 0; t < _triangles.Count; t += 3)
            {
                int ia = _triangles[t], ib = _triangles[t + 1], ic = _triangles[t + 2];
                var face = Vec3.Cross(_positions[ib] - _positions[ia], _positions[ic] - _positions[ia]);
                _normals[ia] += face;
                _normals[ib] += face;
                _normals[ic] += face;
            }

            for (int i = 0; i < _normals.Count; i++) _normals[i] = _normals[i].Normalised;
        }

        public void GetBounds(out Vec3 min, out Vec3 max)
        {
            if (_positions.Count == 0)
            {
                min = max = new Vec3(0f, 0f, 0f);
                return;
            }

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var p in _positions)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            min = new Vec3(minX, minY, minZ);
            max = new Vec3(maxX, maxY, maxZ);
        }

        // Mirrors every vertex across the XY plane and flips winding, appending the result.
        // Used to build one side of a limb pair and get the other for free — the alternative,
        // authoring both, is exactly the repeated manual operation section 71 rules out.
        public void AppendMirroredZ(int fromVertex, int fromTriangle, IReadOnlyList<int> boneMirror)
        {
            int offset = _positions.Count - fromVertex;

            int vertexEnd = _positions.Count;
            for (int i = fromVertex; i < vertexEnd; i++)
            {
                var p = _positions[i];
                int a = _boneA[i], b = _boneB[i];
                _positions.Add(new Vec3(p.X, p.Y, -p.Z));
                _normals.Add(new Vec3(0f, 1f, 0f));
                _uvs.Add(_uvs[i]);
                _boneA.Add(a < boneMirror.Count ? boneMirror[a] : a);
                _boneB.Add(b < boneMirror.Count ? boneMirror[b] : b);
                _weightA.Add(_weightA[i]);
            }

            int triangleEnd = _triangles.Count;
            for (int t = fromTriangle; t < triangleEnd; t += 3)
            {
                // Reversed: mirroring negates the handedness, so the original winding would
                // leave the mirrored half inside-out and lit from behind.
                _triangles.Add(_triangles[t] + offset);
                _triangles.Add(_triangles[t + 2] + offset);
                _triangles.Add(_triangles[t + 1] + offset);
            }
        }
    }
}
