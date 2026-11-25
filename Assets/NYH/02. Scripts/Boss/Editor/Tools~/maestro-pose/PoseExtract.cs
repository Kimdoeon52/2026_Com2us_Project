using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

public static class PoseExtract
{
    // cfg line: name|sheet|x,y,w,h|chestX,bottomY|poly;poly   (poly = "x y,x y,...", sheet coords)
    public static string Run(string cfgPath, string outDir, int thr)
    {
        var log = new System.Text.StringBuilder();
        var cache = new Dictionary<string, Bitmap>();
        foreach (var raw in File.ReadAllLines(cfgPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var f = line.Split('|');
            string name = f[0];
            if (!cache.ContainsKey(f[1])) cache[f[1]] = new Bitmap(f[1]);
            var img = cache[f[1]];
            var r = Nums(f[2]); int rx = (int)r[0], ry = (int)r[1], rw = (int)r[2], rh = (int)r[3];
            var polys = new List<PointF[]>();
            float pox = 0f, poy = 0f, ps_ = 1f; // polygon coords transform: sheet = o + p / s
            if (f.Length > 5 && f[5].Trim().Length > 0) { var t = Nums(f[5]); pox = t[0]; poy = t[1]; ps_ = t[2]; }
            if (f.Length > 4 && f[4].Trim().Length > 0)
                foreach (var ps in f[4].Split(';'))
                {
                    var pts = new List<PointF>();
                    foreach (var pp in ps.Split(',')) { var xy = pp.Trim().Split(' '); pts.Add(new PointF(pox + F(xy[0]) / ps_, poy + F(xy[1]) / ps_)); }
                    polys.Add(pts.ToArray());
                }

            if (f.Length > 6 && f[6].Trim().Length > 0)
                foreach (var ps in f[6].Split(';'))
                {
                    var pts = new List<PointF>();
                    foreach (var pp in ps.Split(',')) { var xy = pp.Trim().Split(' '); pts.Add(new PointF(F(xy[0]), F(xy[1]))); }
                    polys.Add(pts.ToArray());
                }
            // background = median-ish of frame corners
            int bgR = 14, bgG = 13, bgB = 24;
            var mask = new bool[rw, rh];
            var col = new Color[rw, rh];
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                {
                    var c = img.GetPixel(rx + x, ry + y);
                    col[x, y] = c;
                    int d = Math.Max(c.R - bgR, Math.Max(c.G - bgG, c.B - bgB));
                    if (d < thr) continue;
                    bool ex = false;
                    foreach (var p in polys) if (Inside(p, rx + x + 0.5f, ry + y + 0.5f)) { ex = true; break; }
                    if (!ex) mask[x, y] = true;
                }

            // closing (dilate ??erode) then fill holes: outlines are bright, body interior is dark but enclosed
            mask = Close(mask, rw, rh);
            mask = FillHoles(mask, rw, rh);
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                    if (mask[x, y]) foreach (var p in polys) if (Inside(p, rx + x + 0.5f, ry + y + 0.5f)) { mask[x, y] = false; break; }

            // connected components (8-neigh); keep the largest + components near it
            var lab = new int[rw, rh];
            var sizes = new List<int> { 0 };
            var boxes = new List<Rectangle> { Rectangle.Empty };
            var st = new Stack<Point>();
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                {
                    if (!mask[x, y] || lab[x, y] != 0) continue;
                    int id = sizes.Count; int n = 0; int x0 = x, x1 = x, y0 = y, y1 = y;
                    st.Push(new Point(x, y)); lab[x, y] = id;
                    while (st.Count > 0)
                    {
                        var q = st.Pop(); n++;
                        x0 = Math.Min(x0, q.X); x1 = Math.Max(x1, q.X); y0 = Math.Min(y0, q.Y); y1 = Math.Max(y1, q.Y);
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = q.X + dx, ny = q.Y + dy;
                                if (nx < 0 || ny < 0 || nx >= rw || ny >= rh || !mask[nx, ny] || lab[nx, ny] != 0) continue;
                                lab[nx, ny] = id; st.Push(new Point(nx, ny));
                            }
                    }
                    sizes.Add(n); boxes.Add(Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1));
                }
            int main = 1;
            for (int i = 1; i < sizes.Count; i++) if (sizes[i] > sizes[main]) main = i;
            var mb = boxes[main];
            var keepRect = Rectangle.Inflate(mb, 14, 14);
            var keep = new bool[sizes.Count];
            for (int i = 1; i < sizes.Count; i++) keep[i] = i == main || keepRect.IntersectsWith(boxes[i]);

            var a = Nums(f[3]); float chestX = a[0] - rx, bottomY = a[1] - ry;
            // bottom = lowest kept pixel if bottomY <= 0
            int count = 0, minX = rw, maxX = 0, minY = rh, maxY = 0;
            for (int y = 0; y < rh; y++) for (int x = 0; x < rw; x++)
                {
                    if (lab[x, y] == 0 || !keep[lab[x, y]]) continue;
                    count++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            if (bottomY <= 0) bottomY = maxY + 1;

            // output: transparent PNG, crop to kept bbox; write anchor in name file
            int ow = maxX - minX + 1, oh = maxY - minY + 1;
            var outBmp = new Bitmap(ow, oh, PixelFormat.Format32bppArgb);
            var prev = new Bitmap(rw * 3, rh * 3, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(prev)) g.Clear(Color.FromArgb(255, 40, 10, 10));
            for (int y = 0; y < rh; y++) for (int x = 0; x < rw; x++)
                {
                    bool k = lab[x, y] != 0 && keep[lab[x, y]];
                    var c = col[x, y];
                    var pc = k ? c : Color.FromArgb(255, c.R / 4, c.G / 4 + 20, c.B / 4);
                    for (int sy = 0; sy < 3; sy++) for (int sx = 0; sx < 3; sx++) prev.SetPixel(x * 3 + sx, y * 3 + sy, pc);
                    if (k) outBmp.SetPixel(x - minX, y - minY, Color.FromArgb(255, c.R, c.G, c.B));
                }
            // anchor marks in preview
            for (int i = -6; i <= 6; i++)
            {
                int px = (int)(chestX * 3) + i, py = (int)(bottomY * 3) - 1;
                if (px >= 0 && px < prev.Width && py >= 0 && py < prev.Height) prev.SetPixel(px, py, Color.Lime);
                int qy = (int)(bottomY * 3) - 1 + i; int qx = (int)(chestX * 3);
                if (qx >= 0 && qx < prev.Width && qy >= 0 && qy < prev.Height) prev.SetPixel(qx, qy, Color.Lime);
            }
            outBmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
            prev.Save(Path.Combine(outDir, "prev_" + name + ".png"), ImageFormat.Png);
            // anchor relative to output image (px, top = 0)
            log.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}|{1}x{2}|chest {3:0.#}|bottom {4:0.#}|px {5}|origin {6},{7}", name, ow, oh, chestX - minX, bottomY - minY, count, rx + minX, ry + minY));
            outBmp.Dispose(); prev.Dispose();
        }
        foreach (var b in cache.Values) b.Dispose();
        return log.ToString();
    }

    static float F(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }
    static float[] Nums(string s) { var p = s.Split(','); var r = new float[p.Length]; for (int i = 0; i < p.Length; i++) r[i] = F(p[i]); return r; }

    static bool[,] Close(bool[,] m, int w, int h)
    {
        var d = new bool[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                bool any = false;
                for (int dy = -1; dy <= 1 && !any; dy++) for (int dx = -1; dx <= 1 && !any; dx++)
                    { int nx = x + dx, ny = y + dy; if (nx >= 0 && ny >= 0 && nx < w && ny < h && m[nx, ny]) any = true; }
                d[x, y] = any;
            }
        var e = new bool[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                bool all = true;
                for (int dy = -1; dy <= 1 && all; dy++) for (int dx = -1; dx <= 1 && all; dx++)
                    { int nx = x + dx, ny = y + dy; if (nx >= 0 && ny >= 0 && nx < w && ny < h && !d[nx, ny]) all = false; }
                e[x, y] = all || m[x, y];
            }
        return e;
    }

    public static int MaxHole = 500;
    static bool[,] FillHoles(bool[,] m, int w, int h)
    {
        var outside = new bool[w, h];
        var st = new Stack<Point>();
        for (int x = 0; x < w; x++) { st.Push(new Point(x, 0)); st.Push(new Point(x, h - 1)); }
        for (int y = 0; y < h; y++) { st.Push(new Point(0, y)); st.Push(new Point(w - 1, y)); }
        while (st.Count > 0)
        {
            var p = st.Pop();
            if (p.X < 0 || p.Y < 0 || p.X >= w || p.Y >= h || outside[p.X, p.Y] || m[p.X, p.Y]) continue;
            outside[p.X, p.Y] = true;
            st.Push(new Point(p.X + 1, p.Y)); st.Push(new Point(p.X - 1, p.Y)); st.Push(new Point(p.X, p.Y + 1)); st.Push(new Point(p.X, p.Y - 1));
        }
        // fill only small enclosed holes (body interior) ??big enclosed areas are gaps between effect and body
        var r = new bool[w, h];
        var seen = new bool[w, h];
        var list = new List<Point>();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                r[x, y] = m[x, y];
                if (m[x, y] || outside[x, y] || seen[x, y]) continue;
                list.Clear(); st.Push(new Point(x, y)); seen[x, y] = true;
                while (st.Count > 0)
                {
                    var p = st.Pop(); list.Add(p);
                    foreach (var q in new[] { new Point(p.X + 1, p.Y), new Point(p.X - 1, p.Y), new Point(p.X, p.Y + 1), new Point(p.X, p.Y - 1) })
                    {
                        if (q.X < 0 || q.Y < 0 || q.X >= w || q.Y >= h || seen[q.X, q.Y] || m[q.X, q.Y] || outside[q.X, q.Y]) continue;
                        seen[q.X, q.Y] = true; st.Push(q);
                    }
                }
                if (list.Count <= MaxHole) foreach (var p in list) r[p.X, p.Y] = true;
            }
        return r;
    }

    static bool Inside(PointF[] poly, float x, float y)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (((poly[i].Y > y) != (poly[j].Y > y)) && (x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)) c = !c;
        return c;
    }
}


