using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace mmc
{
    [CustomEditor(typeof(TerrainMeshGen))]
    public class TerrainMeshGenEditor : Editor
    {
        struct Float3Pair
        {
            public float3 P0;
            public float3 P1;
        }

        static class JobHelper
        {
            private static bool F3Equals(float3 a, float3 b, float radius = math.EPSILON)
            {
                var diff = math.abs(a - b);
                return diff.x < radius
                    && diff.y < radius
                    && diff.z < radius;
            }

            public struct JobCombinePair : IJobParallelFor
            {
                [ReadOnly]
                public NativeArray<UnsafeList<Float3Pair>> InCutlines;

                [WriteOnly]
                public NativeQueue<UnsafeList<float3>>.ParallelWriter OutCutlines;

                public void Execute(int index)
                {
                    using var headUnique = new NativeHashSet<int>(4, Allocator.Temp);

                    var cutlines = InCutlines[index];
                    for (var i = 0; i != cutlines.Length; ++i)
                    {
                        var headIndex = Find(ref cutlines, i);
                        if (headIndex == -1)
                        {
                            var list = new UnsafeList<float3>(1, Allocator.TempJob);
                            Link(ref cutlines,  i, ref list);
                            OutCutlines.Enqueue(list); break;
                        }
                        else if (!headUnique.Contains(headIndex))
                        {
                            var list = new UnsafeList<float3>(1, Allocator.TempJob);
                            Link(ref cutlines, headIndex, ref list);
                            OutCutlines.Enqueue(list);
                            headUnique.Add(headIndex);
                        }
                    }
                }

                private int Find(ref UnsafeList<Float3Pair> list, int origin)
                {
                    var exit = false;
                    var ring = false;
                    var curr = origin;
                    var ringEnd = list[origin].P1;
                    while (!exit)
                    {
                        exit = true;
                        for (var i = 0; i != list.Length; ++i)
                        {
                            if (F3Equals(list[curr].P0, list[i].P1))
                            {
                                ring = F3Equals(list[i].P0, ringEnd);
                                if (!ring) { curr = i; exit = false; }
                                break;
                            }
                        }
                    }
                    return ring ? -1 : curr;
                }

                private void Link(ref UnsafeList<Float3Pair> list, int origin, ref UnsafeList<float3> result)
                {
                    var curr = origin;
                    result.Add(list[origin].P0);
                    result.Add(list[origin].P1);
                    for (var exit = false; !exit;)
                    {
                        exit = true;
                        for (var i = 0; i != list.Length; ++i)
                        {
                            if (i == curr) { continue; }

                            var pt = list[i].P1;
                            if (F3Equals(list[curr].P1, list[i].P0))
                            {
                                exit = F3Equals(pt,list[origin].P0);
                                result.Add(pt);
                                curr = i;break;
                            }
                        }
                    }
                }
            }

            private static bool FindCutline1(ref NativeArray<UnsafeList<float3>> list, int origin, out int result)
            {
                var exit = false;
                var ring = false;
                var curr = origin;
                var ringEnd = list[origin][^1];
                while (!exit)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; ++i)
                    {
                        if (F3Equals(list[curr][0], list[i][^1]))
                        {
                            ring = F3Equals(list[i][0], ringEnd);
                            if (!ring) { curr = i; exit = false; }
                            break;
                        }
                    }
                }
                result = curr;
                return !ring;
            }

            private static void LinkCutline1(ref NativeArray<UnsafeList<float3>> list, int origin, ref UnsafeList<float3> result)
            {
                var curr = origin;
                result.AddRange(list[origin]);
                for (var exit = false; !exit;)
                {
                    exit = true;
                    for (var i = 0; i != list.Length; ++i)
                    {
                        if (i == curr) { continue; }

                        var ipt = list[i][^1];
                        if (F3Equals(list[curr][^1], list[i][0]))
                        {
                            exit = F3Equals(ipt, list[origin][0]);
                            for (var j = 1; j != list[i].Length; ++j)
                            {
                                result.Add(list[i][j]);
                            }
                            curr = i; break;
                        }
                    }
                }
            }

            //  生成Mesh边缘
            public struct JobCombineCutline1 : IJobParallelFor
            {
                private static readonly object sLockMutex = new();

                [ReadOnly]
                public NativeArray<UnsafeList<float3>> InCutlines;
                [WriteOnly]
                public NativeHashSet<int> OutHeadIndexs;

                public void Execute(int index)
                {
                    FindCutline1(ref InCutlines, index, out int result);

                    lock (sLockMutex) { OutHeadIndexs.Add(result); }
                }
            }

            public struct JobCutlineJoin1 : IJobParallelFor
            {
                [ReadOnly]
                public NativeArray<UnsafeList<float3>> InCutlines;
                [ReadOnly]
                public NativeArray<int> InHeadIndexs;
                [WriteOnly]
                public NativeArray<UnsafeList<float3>> OutCutlines;

                public void Execute(int index)
                {
                    var list = new UnsafeList<float3>(1, Allocator.TempJob);
                    LinkCutline1(ref InCutlines, InHeadIndexs[index], ref list);
                    OutCutlines[index] = list;
                }
            }
        }

        private TerrainMeshGen Target => target as TerrainMeshGen;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("生成"))
            {
                GenMeshEdge();
            }
        }

        private NativeArray<UnsafeList<Float3Pair>> CombineCutline0()
        {
            var meshFilters = CollectMeshFilters(Target.InParam.RootModels);
            using var result = new NativeQueue<UnsafeList<Float3Pair>>(Allocator.TempJob);
            for (var i = 0; i != meshFilters.Length; ++i)
            {
                var meshFilter = meshFilters[i];
                var vertColors = meshFilter.sharedMesh.colors32;
                var vertPoints = meshFilter.sharedMesh.vertices;
                var triangles = meshFilter.sharedMesh.triangles;

                var pairs = new UnsafeList<Float3Pair>(1, Allocator.TempJob);
                for (var j = 0; j != triangles.Length; j += 3)
                {
                    var aIndex = triangles[j];
                    var bIndex = triangles[(j + 1) % triangles.Length];
                    var cIndex = triangles[(j + 2) % triangles.Length];
                    if (Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor))
                    {
                        pairs.Add(new Float3Pair
                        {
                            P0 = meshFilter.transform.TransformPoint(vertPoints[aIndex]),
                            P1 = meshFilter.transform.TransformPoint(vertPoints[bIndex]),
                        });
                    }
                    if (Mathm.Color32Equal(vertColors[bIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor))
                    {
                        pairs.Add(new Float3Pair
                        {
                            P0 = meshFilter.transform.TransformPoint(vertPoints[bIndex]),
                            P1 = meshFilter.transform.TransformPoint(vertPoints[cIndex]),
                        });
                    }
                    if (Mathm.Color32Equal(vertColors[cIndex], Target.InParam.EdgeColor) && Mathm.Color32Equal(vertColors[aIndex], Target.InParam.EdgeColor))
                    {
                        pairs.Add(new Float3Pair
                        {
                            P0 = meshFilter.transform.TransformPoint(vertPoints[cIndex]),
                            P1 = meshFilter.transform.TransformPoint(vertPoints[aIndex]),
                        });
                    }
                }
                if (!pairs.IsEmpty) { result.Enqueue(pairs); }
            }
            return result.ToArray(Allocator.TempJob);
        }

        private NativeArray<UnsafeList<float3>> CombineCutline1(ref NativeArray<UnsafeList<Float3Pair>> cutlines)
        {
            var queue = new NativeQueue<UnsafeList<float3>>(Allocator.TempJob);
            new JobHelper.JobCombinePair()
            {
                InCutlines  = cutlines,
                OutCutlines = queue.AsParallelWriter(),
            }.Schedule(cutlines.Length, 64).Complete();
            var ret = queue.ToArray(Allocator.TempJob);
            queue.Dispose();
            return ret;
        }

        private void GenMeshEdge()
        {
            var cutlines0 = CombineCutline0();
            var cutlines1 = CombineCutline1(ref cutlines0);
            //var cutlines2 = CombineCutline2(ref cutlines1);

            //  copy
            Target.OutParam.MeshEdges = new();
            for (var i = 0; i != cutlines1.Length; ++i)
            {
                var genEdge = new float3[cutlines1[i].Length];
                for (var j = 0; j != cutlines1[i].Length; ++j)
                {
                    genEdge[j] = cutlines1[i][j];
                }
                Target.OutParam.MeshEdges.Add(genEdge);
            }

            //  Dispose
            for (var i = 0; i != cutlines0.Length; ++i)
            {
                cutlines0[i].Dispose();
            }
            cutlines0.Dispose();

            for (var i = 0; i != cutlines1.Length; ++i)
            {
                cutlines1[i].Dispose();
            }
            cutlines1.Dispose();
        }

        private MeshFilter[] CollectMeshFilters(Transform root)
        {
            var list = new List<MeshFilter>();
            for (var i = 0; i != root.childCount; ++i)
            {
                list.Add(root.GetChild(i).GetComponent<MeshFilter>());
            }
            return list.ToArray();
        }
    }
}