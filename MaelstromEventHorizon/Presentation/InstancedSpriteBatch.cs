using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D9;

namespace MaelstromEventHorizon.Presentation;

internal sealed class InstancedSpriteBatch : IDisposable
{
    private const int MaximumInstances = 2_048;
    private readonly IDirect3DVertexBuffer9 quadBuffer;
    private readonly IDirect3DVertexBuffer9 instanceBuffer;
    private readonly IDirect3DIndexBuffer9 indexBuffer;
    private readonly IDirect3DVertexDeclaration9 declaration;
    private readonly IDirect3DVertexShader9 vertexShader;
    private readonly IDirect3DPixelShader9 pixelShader;
    private readonly SpriteInstance[] instances = new SpriteInstance[MaximumInstances];
    private int count;

    public InstancedSpriteBatch(IDirect3DDevice9 device)
    {
        QuadVertex[] quad = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        ushort[] indices = [0, 1, 2, 0, 2, 3];

        quadBuffer = device.CreateVertexBuffer((uint)(quad.Length * Marshal.SizeOf<QuadVertex>()), Usage.WriteOnly,
            VertexFormat.None, Pool.Default);

        indexBuffer = device.CreateIndexBuffer((uint)(indices.Length * sizeof(ushort)), Usage.WriteOnly,
            true, Pool.Default);

        instanceBuffer = device.CreateVertexBuffer((uint)(MaximumInstances * Marshal.SizeOf<SpriteInstance>()),
            Usage.Dynamic | Usage.WriteOnly, VertexFormat.None, Pool.Default);

        Upload(quadBuffer, quad, LockFlags.None);
        Upload(indexBuffer, indices, LockFlags.None);

        declaration = device.CreateVertexDeclaration(
        [
            new VertexElement(
                0, 0, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate),
            new VertexElement(
                1, 0, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 1),
            new VertexElement(
                1, 16, DeclarationType.Color, DeclarationMethod.Default, DeclarationUsage.Color),
            VertexElement.VertexDeclarationEnd
        ]);

        ReadOnlyMemory<byte> vertexCode = Compile(VertexShaderSource, "vs_3_0");
        ReadOnlyMemory<byte> pixelCode = Compile(PixelShaderSource, "ps_3_0");
        vertexShader = device.CreateVertexShader(MemoryMarshal.Cast<byte, uint>(vertexCode.Span));
        pixelShader = device.CreatePixelShader(MemoryMarshal.Cast<byte, uint>(pixelCode.Span));
    }

    public void Clear() => count = 0;

    public void Add(double x, double y, double radius, uint color, double alpha)
    {
        if (count >= MaximumInstances)
        {
            return;
        }

        instances[count++] = new SpriteInstance((float)x, (float)y, (float)radius, (float)alpha, color);
    }

    public void Draw(IDirect3DDevice9 device)
    {
        if (count == 0)
        {
            return;
        }

        Upload(instanceBuffer, instances.AsSpan(0, count), LockFlags.Discard);
        device.VertexDeclaration = declaration;
        device.VertexShader = vertexShader;
        device.PixelShader = pixelShader;
        device.SetStreamSourceFrequency(0, (uint)count, StreamSource.IndexedData);
        device.SetStreamSourceFrequency(1, 1, StreamSource.InstanceData);
        device.SetStreamSource(0, quadBuffer, 0, (uint)Marshal.SizeOf<QuadVertex>());
        device.SetStreamSource(1, instanceBuffer, 0, (uint)Marshal.SizeOf<SpriteInstance>());
        device.Indices = indexBuffer;
        device.DrawIndexedPrimitive(PrimitiveType.TriangleList, 0, 0, 4, 0, 2);
        device.ResetStreamSourceFrequency(0);
        device.ResetStreamSourceFrequency(1);
        device.VertexShader = null;
        device.PixelShader = null;
        device.VertexDeclaration = null;
    }

    public void Dispose()
    {
        pixelShader.Dispose();
        vertexShader.Dispose();
        declaration.Dispose();
        instanceBuffer.Dispose();
        indexBuffer.Dispose();
        quadBuffer.Dispose();
    }

    private static ReadOnlyMemory<byte> Compile(string source, string target)
    {
        return Compiler.Compile(source, "main", "InstancedSpriteBatch", target, ShaderFlags.OptimizationLevel3);
    }

    private static unsafe void Upload<T>(IDirect3DVertexBuffer9 buffer, ReadOnlySpan<T> data, LockFlags flags)
        where T : unmanaged
    {
        IntPtr target = buffer.LockToPointer(0, (uint)(data.Length * sizeof(T)), flags);

        fixed (T* source = data)
        {
            Buffer.MemoryCopy(source, target.ToPointer(), data.Length * sizeof(T), data.Length * sizeof(T));
        }

        buffer.Unlock();
    }

    private static unsafe void Upload(IDirect3DIndexBuffer9 buffer, ReadOnlySpan<ushort> data, LockFlags flags)
    {
        IntPtr target = buffer.LockToPointer(0, (uint)(data.Length * sizeof(ushort)), flags);

        fixed (ushort* source = data)
        {
            Buffer.MemoryCopy(source, target.ToPointer(), data.Length * sizeof(ushort), data.Length * sizeof(ushort));
        }

        buffer.Unlock();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct QuadVertex(float x, float y)
    {
        public readonly float X = x;
        public readonly float Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SpriteInstance(float x, float y, float radius, float alpha, uint color)
    {
        public readonly float X = x;
        public readonly float Y = y;
        public readonly float Radius = radius;
        public readonly float Alpha = alpha;
        public readonly uint Color = color;
    }

    private const string VertexShaderSource = "struct I{float2 c:TEXCOORD0;float4 p:TEXCOORD1;float4 color:COLOR0;};struct O{float4 p:POSITION0;float2 c:TEXCOORD0;float4 color:COLOR0;};O main(I i){O o;float2 q=i.p.xy+i.c*i.p.z;o.p=float4(q.x/640-1,1-q.y/360,0,1);o.c=i.c;o.color=i.color;o.color.a*=i.p.w;return o;}";
    private const string PixelShaderSource = "struct I{float2 c:TEXCOORD0;float4 color:COLOR0;};float4 main(I i):COLOR0{clip(1-dot(i.c,i.c));return float4(i.color.rgb,i.color.a);}";
}
