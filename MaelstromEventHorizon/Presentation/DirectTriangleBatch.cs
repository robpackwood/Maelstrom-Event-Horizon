using System.Runtime.InteropServices;
using Vortice.Direct3D9;

namespace MaelstromEventHorizon.Presentation;

/// <summary>Maintains the small custom-geometry batch used beside the instanced sprite pass.</summary>
internal sealed class DirectTriangleBatch : IDisposable
{
    private const int FloatsPerVertex = 6;
    private D3DVertex[] vertices = new D3DVertex[16_384 / FloatsPerVertex];
    private GCHandle pinnedVertices;

    public void Upload(float[] source, int floatCount)
    {
        int count = floatCount / FloatsPerVertex;
        EnsureCapacity(count);

        for (int sourceIndex = 0, destination = 0; sourceIndex < floatCount; sourceIndex += FloatsPerVertex, destination++)
        {
            vertices[destination] = new D3DVertex(source[sourceIndex], source[sourceIndex + 1], PackColor(source, sourceIndex));
        }
    }

    public void Draw(IDirect3DDevice9 device, int totalVertices, int firstVertex, int maximumVertices)
    {
        int vertexCount = Math.Min(totalVertices - firstVertex, maximumVertices);
        if (vertexCount < 3)
        {
            return;
        }

        device.VertexFormat = VertexFormat.PositionRhw | VertexFormat.Diffuse;
        device.Indices = null;
        IntPtr start = IntPtr.Add(pinnedVertices.AddrOfPinnedObject(), firstVertex * Marshal.SizeOf<D3DVertex>());
        device.DrawPrimitiveUP(PrimitiveType.TriangleList, (uint)(vertexCount / 3), start,
            (uint)Marshal.SizeOf<D3DVertex>());
    }

    public void Dispose()
    {
        if (pinnedVertices.IsAllocated)
        {
            pinnedVertices.Free();
        }
    }

    private void EnsureCapacity(int count)
    {
        if (count > vertices.Length)
        {
            if (pinnedVertices.IsAllocated)
            {
                pinnedVertices.Free();
            }

            Array.Resize(ref vertices, Math.Max(count, vertices.Length * 2));
        }

        if (!pinnedVertices.IsAllocated)
        {
            pinnedVertices = GCHandle.Alloc(vertices, GCHandleType.Pinned);
        }
    }

    private static uint PackColor(float[] source, int index)
    {
        uint red = (uint)Math.Clamp(source[index + 2] * 255, 0, 255);
        uint green = (uint)Math.Clamp(source[index + 3] * 255, 0, 255);
        uint blue = (uint)Math.Clamp(source[index + 4] * 255, 0, 255);
        uint alpha = (uint)Math.Clamp(source[index + 5] * 255, 0, 255);
        return alpha << 24 | red << 16 | green << 8 | blue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct D3DVertex(float x, float y, uint color)
    {
        public readonly float X = x;
        public readonly float Y = y;
        public readonly float Z = 0;
        public readonly float Rhw = 1;
        public readonly uint Color = color;
    }
}
