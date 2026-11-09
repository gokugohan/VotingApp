using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace VotingApp.Web.Ui
{
    public sealed record MapShape(string Key, string Name, string Path);

    /// <summary>
    /// Municipality outlines of Timor-Leste as SVG paths, decoded once from the TopoJSON file
    /// (wwwroot/data/tl-all.topo.json). The map is drawn on the server, so the browser downloads
    /// a few kilobytes of SVG instead of a mapping library.
    /// </summary>
    public sealed class TimorMap
    {
        public const double Width = 1000;

        public TimorMap(IWebHostEnvironment environment)
        {
            var file = Path.Combine(environment.WebRootPath, "data", "tl-all.topo.json");
            using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
            (Shapes, Height) = Decode(doc.RootElement);
            ViewBox = string.Create(CultureInfo.InvariantCulture, $"0 0 {Width:0} {Height:0}");
        }

        public IReadOnlyList<MapShape> Shapes { get; }
        public double Height { get; }
        public string ViewBox { get; }

        private static (IReadOnlyList<MapShape>, double) Decode(JsonElement root)
        {
            var transform = root.GetProperty("transform");
            double sx = transform.GetProperty("scale")[0].GetDouble(), sy = transform.GetProperty("scale")[1].GetDouble();
            double tx = transform.GetProperty("translate")[0].GetDouble(), ty = transform.GetProperty("translate")[1].GetDouble();

            // Arcs are quantized and delta-encoded.
            var arcs = new List<(double Lon, double Lat)[]>();
            foreach (var arc in root.GetProperty("arcs").EnumerateArray())
            {
                long x = 0, y = 0;
                var points = new List<(double, double)>();
                foreach (var p in arc.EnumerateArray())
                {
                    x += p[0].GetInt64();
                    y += p[1].GetInt64();
                    points.Add((x * sx + tx, y * sy + ty));
                }
                arcs.Add(points.ToArray());
            }

            var geometries = root.GetProperty("objects").EnumerateObject().First().Value.GetProperty("geometries");
            var raw = new List<(string Key, string Name, List<List<(double Lon, double Lat)>> Rings)>();
            foreach (var g in geometries.EnumerateArray())
            {
                var props = g.GetProperty("properties");
                var rings = new List<List<(double, double)>>();
                var type = g.GetProperty("type").GetString();
                IEnumerable<JsonElement> polygons = type == "MultiPolygon"
                    ? g.GetProperty("arcs").EnumerateArray()
                    : new[] { g.GetProperty("arcs") };
                foreach (var polygon in polygons)
                {
                    foreach (var ring in polygon.EnumerateArray())
                    {
                        var points = new List<(double, double)>();
                        foreach (var index in ring.EnumerateArray().Select(i => i.GetInt32()))
                        {
                            var arc = index >= 0 ? arcs[index] : Enumerable.Reverse(arcs[~index]).ToArray();
                            points.AddRange(points.Count == 0 ? arc : arc.Skip(1));
                        }
                        rings.Add(points);
                    }
                }
                raw.Add((props.GetProperty("hc-key").GetString(), props.GetProperty("name").GetString(), rings));
            }

            // Equirectangular projection corrected for latitude, fitted to Width.
            var all = raw.SelectMany(r => r.Rings).SelectMany(r => r).ToList();
            var cosLat = Math.Cos(all.Average(p => p.Lat) * Math.PI / 180);
            double minX = all.Min(p => p.Lon * cosLat), maxX = all.Max(p => p.Lon * cosLat);
            double minY = all.Min(p => -p.Lat), maxY = all.Max(p => -p.Lat);
            const double pad = 8;
            var k = (Width - 2 * pad) / (maxX - minX);
            var height = (maxY - minY) * k + 2 * pad;

            var shapes = raw.Select(r =>
            {
                var sb = new StringBuilder();
                foreach (var ring in r.Rings)
                {
                    for (var i = 0; i < ring.Count; i++)
                    {
                        var x = (ring[i].Lon * cosLat - minX) * k + pad;
                        var y = (-ring[i].Lat - minY) * k + pad;
                        sb.Append(i == 0 ? 'M' : 'L').Append(x.ToString("0.#", CultureInfo.InvariantCulture))
                          .Append(',').Append(y.ToString("0.#", CultureInfo.InvariantCulture));
                    }
                    sb.Append('Z');
                }
                return new MapShape(r.Key, r.Name, sb.ToString());
            }).ToList();
            return (shapes, height);
        }
    }

    /// <summary>
    /// Categorical colour slots (CSS classes s-1..s-8 / f-1..f-8, validated palette) assigned to
    /// contestants in fixed ballot order, never by rank. Contestants beyond the cap share "other".
    /// </summary>
    public static class SeriesPalette
    {
        /// <summary>Maps (all-pairs visible, e.g. a map) stay colourblind-safe with at most 3 colours.</summary>
        public const int AllPairsCap = 3;
        /// <summary>Adjacent-only forms (seat dots grouped by party) validate up to 8.</summary>
        public const int AdjacentCap = 8;

        public static IReadOnlyDictionary<Guid, string> Assign(IEnumerable<(Guid Id, int BallotPosition)> contestants, int cap)
        {
            var map = new Dictionary<Guid, string>();
            var slot = 0;
            foreach (var c in contestants.OrderBy(c => c.BallotPosition))
            {
                map[c.Id] = slot < cap ? (++slot).ToString(CultureInfo.InvariantCulture) : "other";
            }
            return map;
        }
    }
}
