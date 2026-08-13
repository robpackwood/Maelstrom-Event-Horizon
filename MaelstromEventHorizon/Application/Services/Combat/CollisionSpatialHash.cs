using MaelstromEventHorizon.Domain.Entities;

namespace MaelstromEventHorizon.Application.Services.Combat;

internal sealed class CollisionSpatialHash
{
    private const int CellSize = 128;
    private const int Columns = (int)GameEngine.Width / CellSize;
    private const int Rows = (int)GameEngine.Height / CellSize;
    private readonly List<Body>[] cells = [.. Enumerable.Range(0, Columns * Rows).Select(_ => new List<Body>(8))];
    private readonly List<Body> nearby = new(32);

    internal void Build(GameEngine game)
    {
        foreach (List<Body> cell in cells)
        {
            cell.Clear();
        }

        Add(game.Asteroids);
        Add(game.Fighters);
        Add(game.Bosses);
        Add(game.Mines);
        Add(game.Vortices);
        Add(game.Novas);
        Add(game.Comets);
        Add(game.Pickups);
    }

    internal List<Body> Nearby(Body body)
    {
        nearby.Clear();
        int x = Math.Clamp((int)(body.Position.X / CellSize), 0, Columns - 1);
        int y = Math.Clamp((int)(body.Position.Y / CellSize), 0, Rows - 1);

        for (int oy = -1; oy <= 1; oy++)
        for (int ox = -1; ox <= 1; ox++)
        {
            int cx = (x + ox + Columns) % Columns;
            int cy = (y + oy + Rows) % Rows;
            nearby.AddRange(cells[cy * Columns + cx]);
        }

        return nearby;
    }

    private void Add<T>(List<T> bodies) where T : Body
    {
        for (int i = 0; i < bodies.Count; i++)
        {
            T body = bodies[i];

            if (!body.Alive)
            {
                continue;
            }

            int x = Math.Clamp((int)(body.Position.X / CellSize), 0, Columns - 1);
            int y = Math.Clamp((int)(body.Position.Y / CellSize), 0, Rows - 1);
            cells[y * Columns + x].Add(body);
        }
    }
}
