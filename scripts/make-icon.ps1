param(
    [string]$Source = "$env:USERPROFILE\Desktop\Guven-ON.png",
    [string]$AppAssets = "$PSScriptRoot\..\src\GuvenOn.App\Assets",
    [string]$SiteAssets = "$PSScriptRoot\..\docs\assets"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Source)) {
    throw "Source icon not found: $Source"
}

New-Item -ItemType Directory -Force $AppAssets | Out-Null
New-Item -ItemType Directory -Force $SiteAssets | Out-Null

Add-Type -AssemblyName System.Drawing

if (-not ("GuvenOnIconMaker" -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

public static class GuvenOnIconMaker
{
    private static bool IsBackground(byte b, byte g, byte r, byte a)
    {
        if (a < 16)
        {
            return true;
        }

        // The supplied artwork uses a pure black background; the earlier
        // artwork used a near-white one. Support both so the same script can
        // regenerate every asset from either source.
        bool white = r > 236 && g > 236 && b > 236;
        bool black = r < 22 && g < 22 && b < 22;
        return white || black;
    }

    public static void Clean(string input, string outputPng, string sitePng, int siteSize, string socialPng, int appSize = 1024)
    {
        using (Bitmap source = new Bitmap(input))
        using (Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
            }

            int width = bitmap.Width;
            int height = bitmap.Height;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            int bytes = Math.Abs(stride) * height;
            byte[] pixels = new byte[bytes];
            Marshal.Copy(data.Scan0, pixels, 0, bytes);

            bool[] visited = new bool[width * height];
            Queue<int> queue = new Queue<int>();

            Action<int, int> enqueue = (x, y) =>
            {
                int index = y * width + x;
                if (visited[index])
                {
                    return;
                }

                visited[index] = true;
                int offset = y * stride + x * 4;
                if (IsBackground(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]))
                {
                    queue.Enqueue(index);
                }
            };

            for (int x = 0; x < width; x++)
            {
                enqueue(x, 0);
                enqueue(x, height - 1);
            }

            for (int y = 0; y < height; y++)
            {
                enqueue(0, y);
                enqueue(width - 1, y);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;
                int offset = y * stride + x * 4;
                pixels[offset] = 0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = 0;
                pixels[offset + 3] = 0;

                if (x > 0) enqueue(x - 1, y);
                if (x < width - 1) enqueue(x + 1, y);
                if (y > 0) enqueue(x, y - 1);
                if (y < height - 1) enqueue(x, y + 1);
            }

            Marshal.Copy(pixels, 0, data.Scan0, bytes);
            bitmap.UnlockBits(data);

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int alpha = pixels[y * stride + x * 4 + 3];
                    if (alpha > 8)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0)
            {
                throw new InvalidOperationException("The source image is completely transparent.");
            }

            int contentWidth = maxX - minX + 1;
            int contentHeight = maxY - minY + 1;
            int side = (int)Math.Ceiling(Math.Max(contentWidth, contentHeight) * 1.06);
            int offsetX = (side - contentWidth) / 2;
            int offsetY = (side - contentHeight) / 2;

            using (Bitmap square = new Bitmap(side, side, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(square))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.DrawImage(
                        bitmap,
                        new Rectangle(offsetX, offsetY, contentWidth, contentHeight),
                        new Rectangle(minX, minY, contentWidth, contentHeight),
                        GraphicsUnit.Pixel);
                }

                using (Bitmap app = new Bitmap(appSize, appSize, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(app))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.DrawImage(square, 0, 0, appSize, appSize);
                    }

                    app.Save(outputPng, ImageFormat.Png);
                }

                using (Bitmap site = new Bitmap(siteSize, siteSize, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(site))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.DrawImage(square, 0, 0, siteSize, siteSize);
                    }

                    site.Save(sitePng, ImageFormat.Png);
                }

                MakeSocial(square, socialPng);
            }
        }
    }

    private static void MakeSocial(Bitmap icon, string output)
    {
        using (Bitmap canvas = new Bitmap(1200, 630, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        {
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color cream = ColorTranslator.FromHtml("#F6F3EA");
            Color navy = ColorTranslator.FromHtml("#0E175A");
            Color muted = ColorTranslator.FromHtml("#5D5A52");
            Color line = ColorTranslator.FromHtml("#DCD5C6");

            graphics.Clear(cream);

            using (SolidBrush halo = new SolidBrush(Color.FromArgb(16, navy)))
            {
                graphics.FillEllipse(halo, 708, 38, 452, 452);
            }

            using (Pen ring = new Pen(Color.FromArgb(38, navy), 1))
            {
                graphics.DrawEllipse(ring, 708, 38, 452, 452);
            }

            int iconSize = 320;
            graphics.DrawImage(icon, 790, 145, iconSize, iconSize);

            using (SolidBrush navyBrush = new SolidBrush(navy))
            using (SolidBrush mutedBrush = new SolidBrush(muted))
            using (Font labelFont = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font titleFont = new Font("Segoe UI", 68, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font subtitleFont = new Font("Segoe UI", 31, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font microFont = new Font("Segoe UI", 19, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.FillRectangle(navyBrush, 84, 142, 52, 6);
                graphics.DrawString("WINDOWS 11  \u00B7  PREVIEW", labelFont, navyBrush, 84, 166);
                graphics.DrawString("Guven-ON", titleFont, navyBrush, 80, 206);
                graphics.DrawString("Privacy that", subtitleFont, navyBrush, 84, 318);
                graphics.DrawString("just works.", subtitleFont, navyBrush, 84, 356);
                graphics.DrawString("DNS leak protection for untrusted Wi-Fi.", microFont, mutedBrush, 84, 470);
            }

            using (Pen divider = new Pen(line, 1))
            {
                graphics.DrawLine(divider, 84, 528, 1116, 528);
            }

            using (Font footerFont = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush mutedBrush = new SolidBrush(muted))
            {
                graphics.DrawString("Independent  \u00B7  English  \u00B7  Deutsch  \u00B7  T\u00FCrk\u00E7e", footerFont, mutedBrush, 84, 552);
            }

            canvas.Save(output, ImageFormat.Png);
        }
    }

    public static void MakeIco(string pngPath, string icoPath)
    {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();

        using (Bitmap source = new Bitmap(pngPath))
        {
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.DrawImage(source, 0, 0, size, size);
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        bitmap.Save(stream, ImageFormat.Png);
                        images.Add(stream.ToArray());
                    }
                }
            }
        }

        using (FileStream file = new FileStream(icoPath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(file))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)images.Count);

            int offset = 6 + 16 * images.Count;
            for (int i = 0; i < images.Count; i++)
            {
                int size = sizes[i];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)images[i].Length);
                writer.Write((uint)offset);
                offset += images[i].Length;
            }

            for (int i = 0; i < images.Count; i++)
            {
                writer.Write(images[i]);
            }
        }
    }
}
'@
}

$cleanPng = Join-Path $AppAssets "Guven-ON.png"
$sitePng = Join-Path $SiteAssets "guvenon.png"
$socialPng = Join-Path $SiteAssets "og.png"
$icoPath = Join-Path $AppAssets "guvenon.ico"

$tempDir = Join-Path $env:TEMP "guvenon-icon"
New-Item -ItemType Directory -Force $tempDir | Out-Null
$tempPng = Join-Path $tempDir "Guven-ON.png"

[GuvenOnIconMaker]::Clean($Source, $tempPng, $sitePng, 256, $socialPng)
Copy-Item $tempPng $cleanPng -Force
[GuvenOnIconMaker]::MakeIco($cleanPng, $icoPath)

Write-Host "Wrote $cleanPng"
Write-Host "Wrote $sitePng"
Write-Host "Wrote $socialPng"
Write-Host "Wrote $icoPath"
