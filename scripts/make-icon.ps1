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
using System.IO;

public static class GuvenOnIconMaker
{
    private static bool IsBackground(Color color)
    {
        return color.A < 250 && color.R > 230 && color.G > 230 && color.B > 230;
    }

    private static void Enqueue(Bitmap bitmap, bool[] visited, Queue<int> queue, int x, int y)
    {
        int width = bitmap.Width;
        int index = y * width + x;
        if (visited[index])
        {
            return;
        }

        visited[index] = true;
        if (IsBackground(bitmap.GetPixel(x, y)))
        {
            queue.Enqueue(index);
        }
    }

    public static void Clean(string input, string outputPng, string sitePng, int siteSize)
    {
        using (Bitmap source = new Bitmap(input))
        using (Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            int width = bitmap.Width;
            int height = bitmap.Height;
            bool[] visited = new bool[width * height];
            Queue<int> queue = new Queue<int>();

            for (int x = 0; x < width; x++)
            {
                Enqueue(bitmap, visited, queue, x, 0);
                Enqueue(bitmap, visited, queue, x, height - 1);
            }

            for (int y = 0; y < height; y++)
            {
                Enqueue(bitmap, visited, queue, 0, y);
                Enqueue(bitmap, visited, queue, width - 1, y);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;
                bitmap.SetPixel(x, y, Color.Transparent);

                if (x > 0) Enqueue(bitmap, visited, queue, x - 1, y);
                if (x < width - 1) Enqueue(bitmap, visited, queue, x + 1, y);
                if (y > 0) Enqueue(bitmap, visited, queue, x, y - 1);
                if (y < height - 1) Enqueue(bitmap, visited, queue, x, y + 1);
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (bitmap.GetPixel(x, y).A > 8)
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
            int side = (int)Math.Ceiling(Math.Max(contentWidth, contentHeight) * 1.08);
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

                square.Save(outputPng, ImageFormat.Png);

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
            }
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
$icoPath = Join-Path $AppAssets "guvenon.ico"

$tempDir = Join-Path $env:TEMP "guvenon-icon"
New-Item -ItemType Directory -Force $tempDir | Out-Null
$tempPng = Join-Path $tempDir "Guven-ON.png"

[GuvenOnIconMaker]::Clean($Source, $tempPng, $sitePng, 256)
Copy-Item $tempPng $cleanPng -Force
[GuvenOnIconMaker]::MakeIco($cleanPng, $icoPath)

Write-Host "Wrote $cleanPng"
Write-Host "Wrote $sitePng"
Write-Host "Wrote $icoPath"
