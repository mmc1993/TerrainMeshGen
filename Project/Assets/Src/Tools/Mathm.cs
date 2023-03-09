using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace mmc
{
    public static class Mathm
    {
        public struct Quad
        {
            public float X;
            public float Y;
            public float W;
            public float H;

            public static Quad New(float x, float y, float w, float h)
            {
                Quad q; q.X = x; q.Y = y; q.W = w; q.H = h; return q;
            }

            public Quad(float x, float y, float w, float h)
            {
                X = x; Y = y; W = w; H = h;
            }

            public Vector2 Center()
            {
                Vector2 c;
                c.x = X + W * 0.5f;
                c.y = Y + H * 0.5f;
                return c;
            }

            public bool IsCross(Quad q)
            {
                return !(X > q.X + q.W
                    || Y > q.Y + q.H
                    || q.X > X + W
                    || q.Y > Y + H);
            }

            public bool IsCross(Circular c)
            {
                Vector2 h;
                h.x = W * 0.5f;
                h.y = H * 0.5f;

                Vector2 v = c.Origin - Center();
                v.x = Mathf.Abs(v.x);
                v.y = Mathf.Abs(v.y);

                var u = v - h;
                u.x = Mathf.Max(0, u.x);
                u.y = Mathf.Max(0, u.y);

                return u.SqrMagnitude() <= c.Radius * c.Radius;
            }

            public bool IsCross(Triangle t)
            {
                Vector2 p0; p0.x = X; p0.y = Y;
                Vector2 p1; p1.x = X + W; p1.y = Y;
                Vector2 p2; p2.x = X + W; p2.y = Y + H;
                Vector2 p3; p3.x = X; p3.y = Y + H;

                Line2D qe0; qe0.P0 = p0; qe0.P1 = p1;
                Line2D qe1; qe1.P0 = p1; qe1.P1 = p2;
                Line2D qe2; qe2.P0 = p2; qe2.P1 = p3;
                Line2D qe3; qe3.P0 = p3; qe3.P1 = p0;

                Line2D te0; te0.P0 = t.P0; te0.P1 = t.P1;
                Line2D te1; te1.P0 = t.P1; te1.P1 = t.P2;
                Line2D te2; te2.P0 = t.P2; te2.P1 = t.P0;

                return qe0.IsCross(te0) || qe0.IsCross(te1) || qe0.IsCross(te2)
                    || qe1.IsCross(te0) || qe1.IsCross(te1) || qe1.IsCross(te2)
                    || qe2.IsCross(te0) || qe2.IsCross(te1) || qe2.IsCross(te2)
                    || qe3.IsCross(te0) || qe3.IsCross(te1) || qe3.IsCross(te2);
            }

            public bool IsContains(Quad q)
            {
                return X <= q.X && X + W >= q.X + q.W
                    && Y <= q.Y && Y + H >= q.Y + q.H;
            }

            //  矩形 圆
            public bool IsContains(Circular c)
            {
                Quad q;
                q.X = c.Origin.x - c.Radius;
                q.Y = c.Origin.y - c.Radius;
                q.W = c.Radius * 2;
                q.H = c.Radius * 2;
                return IsContains(q);
            }

            //  矩形 点
            public bool IsContains(Vector2 p)
            {
                return p.x >= X && p.x <= X + W
                    && p.y >= Y && p.y <= Y + H;
            }

            //  矩形 三角形
            public bool IsContains(Triangle t)
            {
                return IsContains(t.P0) && IsContains(t.P1) && IsContains(t.P2);
            }

            //  矩形 线段
            public bool IsContains(Line2D s)
            {
                return IsContains(s.P0) && IsContains(s.P1);
            }
        }

        public struct Line2D
        {
            public Vector2 P0;
            public Vector2 P1;

            public Line2D(Vector2 p0, Vector2 p1)
            {
                P0 = p0; P1 = p1;
            }

            public override bool Equals(object obj)
            {
                var other = (Line2D)obj;
                return P0 == other.P0 && P1 == other.P1
                    || P0 == other.P1 && P1 == other.P0;
            }

            public bool IsOn(Vector2 p)
            {
                var ap = p - P0;
                var bp = p - P1;
                return Vector2.Dot(ap, bp) < 0
                    && V2Cross(ap, bp) == 0.0f;
            }

            public bool IsCross(Line2D s)
            {
                return IsCross(s, out var _0, out var _1);
            }

            public bool IsCross(Line2D b, out float u, out float v)
            {
                if (IsCrossLine(b, out u, out v))
                {
                    return u >= 0.0f && u <= 1.0f
                        && v >= 0.0f && v <= 1.0f;
                }
                return false;
            }

            public bool IsCrossLine(Line2D s, out float u, out float v)
            {
                var cross = V2Cross(P1 - P0, s.P1 - s.P0);
                if (cross != 0.0f)
                {
                    u = V2Cross(s.P1 - s.P0, P0 - s.P0) / cross;
                    v = V2Cross(P1 - P0, P0 - s.P0) / cross;
                    return true;
                }
                u = 0; v = 0;
                return false;
            }

            public bool IsCross(Quad q)
            {
                Vector2 p0; p0.x = q.X; p0.y = q.Y;
                Vector2 p1; p1.x = q.X + q.W; p1.y = q.Y;
                Vector2 p2; p2.x = q.X + q.W; p2.y = q.Y + q.H;
                Vector2 p3; p3.x = q.X; p3.y = q.Y + q.H;

                Line2D e0; e0.P0 = p0; e0.P1 = p1;
                Line2D e1; e1.P0 = p1; e1.P1 = p2;
                Line2D e2; e2.P0 = p2; e2.P1 = p3;
                Line2D e3; e3.P0 = p3; e3.P1 = p0;
                return IsCross(e0) || IsCross(e1) || IsCross(e2) || IsCross(e3);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        public struct Line3D
        {
            public Vector3 P0;
            public Vector3 P1;

            public Line3D(Vector3 p0, Vector3 p1)
            {
                P0 = p0;
                P1 = p1;
            }

            public override bool Equals(object obj)
            {
                var other = (Line3D)obj;
                return P0 == other.P0 && P1 == other.P1
                    || P0 == other.P1 && P1 == other.P0;
            }

            public override int GetHashCode()
            {
                return System.HashCode.Combine(P0, P1);
            }

            public float GetDistance01(Vector3 pt, out Vector3 cross)
            {
                var e0e1 = P1 - P0;
                var e0pt = pt - P0;
                var e1pt = pt - P1;
                if (Vector3.Dot(e0e1, e0pt) <= 0)
                {
                    cross = P0; return Vector3.Distance(P0, pt);
                }
                else if (Vector3.Dot(e0e1, e1pt) >= 0)
                {
                    cross = P1; return Vector3.Distance(P1, pt);
                }
                else
                {
                    e0e1.Normalize();
                    var scale = Vector3.Dot(e0pt, e0e1);
                    cross = e0e1 * scale + P0;
                    return Vector3.Magnitude(pt - cross);
                }
            }
        }

        public struct Triangle
        {
            public Vector2 P0;
            public Vector2 P1;
            public Vector2 P2;

            public Triangle(Vector2 p0, Vector2 p1, Vector2 p2)
            {
                P0 = p0; P1 = p1; P2 = p2;
            }

            public override bool Equals(object obj)
            {
                var other = (Triangle)obj;
                return (P0 == other.P0 || P0 == other.P1 || P0 == other.P2)
                    && (P1 == other.P0 || P1 == other.P1 || P1 == other.P2)
                    && (P2 == other.P0 || P2 == other.P1 || P2 == other.P2);
            }

            //  查询共边
            public bool IsCommonEdge(Triangle other, out Line2D edge)
            {
                Line2D selfS0, selfS1, selfS2;
                selfS0.P0 = P0; selfS0.P1 = P1;
                selfS1.P0 = P1; selfS1.P1 = P2;
                selfS2.P0 = P2; selfS2.P1 = P0;

                Line2D otherS0, otherS1, otherS2;
                otherS0.P0 = other.P0; otherS0.P1 = other.P1;
                otherS1.P0 = other.P1; otherS1.P1 = other.P2;
                otherS2.P0 = other.P2; otherS2.P1 = other.P0;

                if (selfS0.Equals(otherS0) || selfS0.Equals(otherS1) || selfS0.Equals(otherS2)) { edge = selfS0; return true; }
                if (selfS1.Equals(otherS0) || selfS1.Equals(otherS1) || selfS1.Equals(otherS2)) { edge = selfS1; return true; }
                if (selfS2.Equals(otherS0) || selfS2.Equals(otherS1) || selfS2.Equals(otherS2)) { edge = selfS2; return true; }

                edge.P0 = Vector2.zero;
                edge.P1 = Vector2.zero;
                return false;
            }

            public bool IsContains(Line2D segment)
            {
                Line2D selfS0, selfS1, selfS2;
                selfS0.P0 = P0; selfS0.P1 = P1;
                selfS1.P0 = P1; selfS1.P1 = P2;
                selfS2.P0 = P2; selfS2.P1 = P0;
                return selfS0.Equals(segment) || selfS1.Equals(segment) || selfS2.Equals(segment);
            }

            public bool IsContains(Vector2 p)
            {
                var p0p1 = P1 - P0;
                var p1p2 = P2 - P1;
                var p2p0 = P0 - P2;
                var c0 = V2Cross(p0p1, p1p2);
                if (c0 * V2Cross(p0p1, p - P0) < 0) { return false; }
                var c1 = V2Cross(p1p2, p2p0);
                if (c1 * V2Cross(p1p2, p - P1) < 0) { return false; }
                var c2 = V2Cross(p2p0, p0p1);
                if (c2 * V2Cross(p2p0, p - P2) < 0) { return false; }
                return true;
            }

            //  三角形 矩形
            public bool IsContains(Quad q)
            {
                Vector2 p0; p0.x = q.X; p0.y = q.Y;
                Vector2 p1; p1.x = q.X + q.W; p1.y = q.Y;
                Vector2 p2; p2.x = q.X + q.W; p2.y = q.Y + q.H;
                Vector2 p3; p3.x = q.X; p3.y = q.Y + q.H;
                return IsContains(p0) && IsContains(p1)
                    && IsContains(p2) && IsContains(p3);
            }

            //  外接圆心
            public Vector2 OutsideCircleCenter()
            {
                Line2D s0, s1;

                var p0p1 = P1 - P0;
                var p1p2 = P2 - P1;
                Tools.Swap(ref p0p1.x, ref p0p1.y); p0p1.y = -p0p1.y;
                Tools.Swap(ref p1p2.x, ref p1p2.y); p1p2.y = -p1p2.y;

                s0.P0 = Vector2.Lerp(P0, P1, 0.5f);
                s1.P0 = Vector2.Lerp(P1, P2, 0.5f);
                s0.P1 = s0.P0 + p0p1; s1.P1 = s1.P0 + p1p2;

                s0.IsCrossLine(s1, out var u, out var _);
                return Vector2.LerpUnclamped(s0.P0, s0.P1, u);
            }

            //  内接圆心
            public Vector2 InsideCircleCenter()
            {
                var l0 = Vector2.Distance(P0, P1);
                var l1 = Vector2.Distance(P1, P2);
                var l2 = Vector2.Distance(P2, P0);
                var p = l0 + l1 + l2;
                return l1 / p * P0 + l2 / p * P1 + l0 / p * P2;
            }

            public float InsideCircleRadius()
            {
                var l0 = Vector2.Distance(P0, P1);
                var l1 = Vector2.Distance(P1, P2);
                var l2 = Vector2.Distance(P2, P0);
                var p = l0 + l1 + l2;

                var p0p1 = P1 - P0;
                var p1p2 = P2 - P1;
                var s = (p0p1.x * p1p2.y - p0p1.y * p1p2.x);

                return s / p;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        public struct Circular
        {
            public Vector2 Origin;
            public float Radius;

            public Circular(Vector2 origin, float radius)
            {
                Origin = origin;
                Radius = radius;
            }

            public bool IsCross(Circular c)
            {
                var dist = (Radius + c.Radius) * (Radius + c.Radius);
                return dist >= (Origin - c.Origin).SqrMagnitude();
            }
        }

        //
        //  2D向量计算
        //
        public static float V2Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        //  计算三点贝塞尔曲线
        public static Vector3 Beizer(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            var ab = Vector3.LerpUnclamped(a, b, t);
            var bc = Vector3.LerpUnclamped(b, c, t);
            return Vector3.LerpUnclamped(ab, bc, t);
        }

        public static Color Beizer(Color a, Color b, Color c, float t)
        {
            var ab = Color.LerpUnclamped(a, b, t);
            var bc = Color.LerpUnclamped(b, c, t);
            return Color.LerpUnclamped(ab, bc, t);
        }

        public static int Index(int index, int length)
        {
            index %= length;
            return index >= 0 ? index : (length + index) % length;
        }

        public static bool Float3Equal(float3 a, float3 b)
        {
            var diff = math.abs(a - b);
            return diff.x < math.EPSILON
                && diff.y < math.EPSILON
                && diff.z < math.EPSILON;
        }

        public static bool Color32Equal(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }
    }
}