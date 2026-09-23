using System;
using System.Collections.Generic;
using Tekla.Structures.Drawing;
using Tekla.Structures.Solid;
using TSM = Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

namespace RoAxisDimensionRemover
{
    // Jednorazowy, jawnie ograniczony pilot. Nie jest jeszcze regułą
    // produkcyjną: brama bezpieczeństwa dla tworzenia wymiaru trwa.
    internal static class NotchPilot
    {
        private const string PilotDrawingMark = "[35021]";
        private const double NumericalZero = 0.000001;

        public static bool InsertWidthTest(Drawing drawing, Action<string> log)
        {
            return InsertTest(drawing, log, longest: false, label: "szerokości");
        }

        public static bool InsertLengthTest(Drawing drawing, Action<string> log)
        {
            return InsertTest(drawing, log, longest: true, label: "długości");
        }

        private static bool InsertTest(Drawing drawing, Action<string> log, bool longest, string label)
        {
            if (!string.Equals(drawing.Mark, PilotDrawingMark, StringComparison.OrdinalIgnoreCase))
            {
                log("TEST WSTRZYMANY: pilot jest ograniczony do rysunku " + PilotDrawingMark + ".");
                return false;
            }

            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                log("TEST WSTRZYMANY: brak połączenia z Teklą (Model).");
                return false;
            }

            var candidates = new List<Candidate>();
            var top = drawing.GetSheet().GetAllObjects();
            while (top.MoveNext())
            {
                if (!(top.Current is View view)) continue;
                var parts = view.GetAllObjects(new[] { typeof(Part) });
                while (parts.MoveNext())
                {
                    if (!(parts.Current is Part drawingPart)) continue;
                    if (!(model.SelectModelObject(drawingPart.ModelIdentifier) is TSM.Part modelPart)) continue;
                    if (TryFindChord(view, modelPart, longest, out Candidate candidate)
                        // To jedynie tolerancja błędu numerycznego projekcji,
                        // nie próg geometrii: zmierzone punkty pilota mają Z=0.
                        && Math.Abs(candidate.Start.Z) <= NumericalZero
                        && Math.Abs(candidate.End.Z) <= NumericalZero)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count != 1)
            {
                log($"TEST WSTRZYMANY: znaleziono {candidates.Count} płaskich kandydatów {label}; wymagany jest dokładnie jeden.");
                return false;
            }

            var width = candidates[0];
            if (HasSameDimension(width.View, width.Start, width.End))
            {
                log("TEST WSTRZYMANY: taki wymiar już istnieje w widoku.");
                return false;
            }

            var reference = FindReferenceDimension(width.View, width.Start, width.End);
            var referenceSet = reference?.GetDimensionSet() as StraightDimensionSet;
            if (referenceSet?.Attributes == null)
            {
                log("TEST WSTRZYMANY: nie znaleziono istniejącego wymiaru jako wzorca stylu.");
                return false;
            }

            // Przejmujemy faktyczny styl i odsunięcie z rysunku. Obrót
            // kierunku (góra -> lewo) kładzie pionowy wymiar z boku rury.
            var up = reference.UpDirection;
            var side = new TSG.Vector(-up.Y, up.X, 0);
            var dimension = new StraightDimension(
                width.View, width.Start, width.End, side, reference.Distance, referenceSet.Attributes);
            if (!dimension.Insert())
            {
                log("TEST: StraightDimension.Insert() zwrócił false.");
                return false;
            }
            if (!drawing.CommitChanges("Test wymiaru szerokości wcięcia"))
            {
                log("TEST: Insert() się powiódł, ale CommitChanges() zwrócił false.");
                return false;
            }

            log($"TEST: wstawiono {label} wcięcia {Distance(width.Start, width.End):F2} mm. Obejrzyj rysunek w Tekli; Ctrl+Z cofa.");
            return true;
        }

        private static bool TryFindChord(View view, TSM.Part part, bool longest, out Candidate candidate)
        {
            candidate = null;
            TSG.Vector axis = null;
            if (part is TSM.Beam beam)
            {
                var delta = beam.EndPoint - beam.StartPoint;
                axis = new TSG.Vector(delta.X, delta.Y, delta.Z).GetNormal();
            }

            List<TSG.Point> outerLoop = null;
            var faces = part.GetSolid().GetFaceEnumerator();
            while (faces.MoveNext())
            {
                if (!(faces.Current is Face face)) continue;
                var loops = new List<List<TSG.Point>>();
                var enumerator = face.GetLoopEnumerator();
                while (enumerator.MoveNext())
                {
                    if (!(enumerator.Current is Loop loop)) continue;
                    var points = new List<TSG.Point>();
                    var vertices = loop.GetVertexEnumerator();
                    while (vertices.MoveNext()) if (vertices.Current is TSG.Point p) points.Add(p);
                    if (points.Count > 4) loops.Add(points);
                }
                if (loops.Count < 2) continue;
                if (axis != null && Math.Abs(new TSG.Vector(face.Normal).GetNormal().Dot(axis)) > 0.999) continue;
                foreach (var loop in loops)
                    if (outerLoop == null || LoopSpan(loop) > LoopSpan(outerLoop)) outerLoop = loop;
            }
            if (outerLoop == null) return false;

            var pair = FindChord(outerLoop, Centroid(outerLoop), longest);
            var cs = view.DisplayCoordinateSystem;
            candidate = new Candidate { View = view, Start = ToViewSpace(pair.A, cs), End = ToViewSpace(pair.B, cs) };
            return true;
        }

        private static bool HasSameDimension(View view, TSG.Point start, TSG.Point end)
        {
            var objects = view.GetAllObjects(new[] { typeof(StraightDimension) });
            while (objects.MoveNext())
            {
                if (!(objects.Current is StraightDimension dimension)) continue;
                bool sameOrder = Distance(dimension.StartPoint, start) <= NumericalZero && Distance(dimension.EndPoint, end) <= NumericalZero;
                bool reverseOrder = Distance(dimension.StartPoint, end) <= NumericalZero && Distance(dimension.EndPoint, start) <= NumericalZero;
                if (sameOrder || reverseOrder) return true;
            }
            return false;
        }

        private static StraightDimension FindReferenceDimension(View view, TSG.Point start, TSG.Point end)
        {
            var objects = view.GetAllObjects(new[] { typeof(StraightDimension) });
            while (objects.MoveNext())
            {
                if (!(objects.Current is StraightDimension dimension)) continue;
                bool sameOrder = Distance(dimension.StartPoint, start) <= NumericalZero && Distance(dimension.EndPoint, end) <= NumericalZero;
                bool reverseOrder = Distance(dimension.StartPoint, end) <= NumericalZero && Distance(dimension.EndPoint, start) <= NumericalZero;
                if (!sameOrder && !reverseOrder) return dimension;
            }
            return null;
        }

        private sealed class Candidate { public View View; public TSG.Point Start; public TSG.Point End; }

        private static double LoopSpan(List<TSG.Point> loop)
        {
            double max = 0;
            for (int i = 0; i < loop.Count; i++) for (int j = i + 1; j < loop.Count; j++) max = Math.Max(max, Distance(loop[i], loop[j]));
            return max;
        }

        private static TSG.Point Centroid(List<TSG.Point> loop)
        {
            double x = 0, y = 0, z = 0;
            foreach (var point in loop) { x += point.X; y += point.Y; z += point.Z; }
            return new TSG.Point(x / loop.Count, y / loop.Count, z / loop.Count);
        }

        private static (TSG.Point A, TSG.Point B) FindChord(List<TSG.Point> loop, TSG.Point centroid, bool longest)
        {
            double minimumOffset = double.MaxValue;
            var pairs = new List<(TSG.Point A, TSG.Point B, double Offset)>();
            for (int i = 0; i < loop.Count; i++) for (int j = i + 1; j < loop.Count; j++)
            {
                var middle = new TSG.Point((loop[i].X + loop[j].X) / 2, (loop[i].Y + loop[j].Y) / 2, (loop[i].Z + loop[j].Z) / 2);
                double offset = Distance(middle, centroid);
                pairs.Add((loop[i], loop[j], offset));
                minimumOffset = Math.Min(minimumOffset, offset);
            }
            double tolerance = Math.Max(0.5, LoopSpan(loop) * 0.05);
            var throughCenter = pairs.FindAll(pair => pair.Offset <= minimumOffset + tolerance);
            throughCenter.Sort((a, b) => Distance(a.A, a.B).CompareTo(Distance(b.A, b.B)));
            var pick = longest ? throughCenter[throughCenter.Count - 1] : throughCenter[0];
            return (pick.A, pick.B);
        }

        private static double Distance(TSG.Point a, TSG.Point b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static TSG.Point ToViewSpace(TSG.Point point, TSG.CoordinateSystem cs)
        {
            var relative = point - cs.Origin;
            var x = new TSG.Vector(cs.AxisX); x.Normalize();
            var y = new TSG.Vector(cs.AxisY); y.Normalize();
            var z = TSG.Vector.Cross(x, y);
            var vector = new TSG.Vector(relative.X, relative.Y, relative.Z);
            return new TSG.Point(vector.Dot(x), vector.Dot(y), vector.Dot(z));
        }
    }
}
