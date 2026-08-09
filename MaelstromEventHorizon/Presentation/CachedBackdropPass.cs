using System.Numerics;
using System.Runtime.InteropServices;
using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Domain.Effects;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D9;

namespace MaelstromEventHorizon.Presentation;

/// <summary>Draws the wave tint and a prebuilt star texture without rebuilding background geometry.</summary>
internal sealed class CachedBackdropPass : IDisposable
{
    private static readonly int TextureWidth = (int)GameEngine.Width;
    private static readonly int TextureHeight = (int)GameEngine.Height;
    private readonly IDirect3DVertexBuffer9 quadBuffer;
    private readonly IDirect3DVertexDeclaration9 declaration;
    private readonly IDirect3DTexture9 starTexture;
    private readonly IDirect3DVertexShader9 vertexShader;
    private readonly IDirect3DPixelShader9 pixelShader;

    public CachedBackdropPass(IDirect3DDevice9 device, GameEngine game)
    {
        QuadVertex[] quad = [new(-1, 1, 0, 0), new(1, 1, 1, 0), new(-1, -1, 0, 1), new(1, -1, 1, 1)];
        quadBuffer = device.CreateVertexBuffer((uint)(quad.Length * Marshal.SizeOf<QuadVertex>()), Usage.WriteOnly,
            VertexFormat.None, Pool.Default);
        Upload(quadBuffer, quad);
        declaration = device.CreateVertexDeclaration(
        [
            new VertexElement(0, 0, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.Position, 0),
            new VertexElement(0, 8, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
            VertexElement.VertexDeclarationEnd
        ]);
        starTexture = device.CreateTexture((uint)TextureWidth, (uint)TextureHeight, 1, Usage.None, Format.A8R8G8B8, Pool.Default);
        BuildStars(game.Stars);
        vertexShader = device.CreateVertexShader<uint>(MemoryMarshal.Cast<byte, uint>(Compile(VertexSource, "vs_3_0").Span));
        pixelShader = device.CreatePixelShader<uint>(MemoryMarshal.Cast<byte, uint>(Compile(PixelSource, "ps_3_0").Span));
        device.SetSamplerState(0, SamplerState.MinFilter, (int)TextureFilter.Point);
        device.SetSamplerState(0, SamplerState.MagFilter, (int)TextureFilter.Point);
        device.SetSamplerState(0, SamplerState.MipFilter, (int)TextureFilter.None);
        device.SetSamplerState(0, SamplerState.AddressU, (int)TextureAddress.Clamp);
        device.SetSamplerState(0, SamplerState.AddressV, (int)TextureAddress.Clamp);
    }

    public void Draw(IDirect3DDevice9 device, uint color)
    {
        device.SetRenderState(RenderState.AlphaBlendEnable, false);
        device.Indices = null;
        device.VertexDeclaration = declaration;
        device.VertexShader = vertexShader;
        device.PixelShader = pixelShader;
        device.SetPixelShaderConstant(0,
        [
            new Vector4(((color >> 16) & 0xff) / 255f, ((color >> 8) & 0xff) / 255f, (color & 0xff) / 255f, 1)
        ]);
        device.SetTexture(0, starTexture);
        device.SetStreamSource(0, quadBuffer, 0, (uint)Marshal.SizeOf<QuadVertex>());
        device.DrawPrimitive(PrimitiveType.TriangleStrip, 0, 2);
        device.VertexShader = null;
        device.PixelShader = null;
        device.VertexDeclaration = null;
        device.SetRenderState(RenderState.AlphaBlendEnable, true);
    }

    public void Dispose()
    {
        pixelShader.Dispose();
        vertexShader.Dispose();
        starTexture.Dispose();
        declaration.Dispose();
        quadBuffer.Dispose();
    }

    private void BuildStars(IEnumerable<Star> stars)
    {
        int stride = TextureWidth * 4;
        byte[] pixels = new byte[stride * TextureHeight];

        foreach (Star star in stars)
        {
            int radius = star.Depth > .82 ? 2 : 1;
            int red = (int)(180 + star.Depth * 70);
            int green = (int)(205 + star.Depth * 45);
            int alpha = (int)(125 + star.Depth * 105);
            DrawStar(pixels, (int)Math.Round(star.Position.X), (int)Math.Round(star.Position.Y), radius, red, green, alpha);
        }

        LockedRectangle target = starTexture.LockRect(0, LockFlags.None);

        for (int y = 0; y < TextureHeight; y++)
        {
            Marshal.Copy(pixels, y * stride, IntPtr.Add(target.DataPointer, y * target.Pitch), stride);
        }

        starTexture.UnlockRect(0);
    }

    private static void DrawStar(byte[] pixels, int centerX, int centerY, int radius, int red, int green, int alpha)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int px = centerX + x;
                int py = centerY + y;

                if (px < 0 || px >= TextureWidth || py < 0 || py >= TextureHeight || x * x + y * y > radius * radius)
                {
                    continue;
                }

                int index = (py * TextureWidth + px) * 4;
                int strength = (int)(alpha * (1 - Math.Sqrt(x * x + y * y) / (radius + 1.0)));
                pixels[index] = Math.Max(pixels[index], (byte)255);
                pixels[index + 1] = Math.Max(pixels[index + 1], (byte)green);
                pixels[index + 2] = Math.Max(pixels[index + 2], (byte)red);
                pixels[index + 3] = Math.Max(pixels[index + 3], (byte)strength);
            }
        }
    }

    private static ReadOnlyMemory<byte> Compile(string source, string target)
    {
        return Compiler.Compile(source, "main", "CachedBackdropPass", target, ShaderFlags.OptimizationLevel3, EffectFlags.None);
    }

    private static unsafe void Upload(IDirect3DVertexBuffer9 buffer, ReadOnlySpan<QuadVertex> source)
    {
        IntPtr target = buffer.LockToPointer(0, (uint)(source.Length * sizeof(QuadVertex)), LockFlags.None);
        fixed (QuadVertex* data = source)
        {
            Buffer.MemoryCopy(data, target.ToPointer(), source.Length * sizeof(QuadVertex), source.Length * sizeof(QuadVertex));
        }

        buffer.Unlock();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct QuadVertex(float x, float y, float u, float v)
    {
        public readonly float X = x;
        public readonly float Y = y;
        public readonly float U = u;
        public readonly float V = v;
    }

    private const string VertexSource = "struct I{float2 p:POSITION0;float2 uv:TEXCOORD0;};struct O{float4 p:POSITION0;float2 uv:TEXCOORD0;};O main(I i){O o;o.p=float4(i.p,0,1);o.uv=i.uv;return o;}";
    private const string PixelSource = "sampler stars:register(s0);float4 tint:register(c0);struct I{float2 uv:TEXCOORD0;};float4 main(I i):COLOR0{float4 star=tex2D(stars,i.uv);return float4(lerp(tint.rgb,star.rgb,star.a),1);}";
}
