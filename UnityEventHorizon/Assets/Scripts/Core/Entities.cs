using UnityEngine;

namespace EventHorizon.Unity;

public enum EntityKind { Asteroid, Fighter, Mine, BlackHole, Supernova, Boss, Pickup, Rescue, PlayerShot, EnemyShot }
public enum Powerup { RapidFire, AirBrakes, Luck, TripleFire, RiftVolley, LongRange, Shield, Freeze, SmartBomb, RicochetArena, GiantShip }

public sealed class SpaceEntity : MonoBehaviour
{
    public EntityKind Kind;
    public Vector2 Velocity;
    public float Radius = .4f, Life = 20, Age;
    public int Size = 1, Health = 1;
    public bool Enemy, Steel, ExitsArena;
    public Powerup Powerup;
    public EventHorizonGame Game;

    void Update()
    {
        if (Game == null || Game.Paused) return;
        Age += Time.deltaTime;
        if (Age >= Life) { Destroy(gameObject); return; }
        transform.position += (Vector3)(Velocity * Time.deltaTime);
        transform.Rotate(0, 0, (Kind == EntityKind.Asteroid ? 80 : 25) * Time.deltaTime);
        Game.WrapOrBounce(this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var hit = other.GetComponent<SpaceEntity>();
        if (hit == null || hit == this) return;
        if (Kind == EntityKind.PlayerShot && hit.Kind is not EntityKind.PlayerShot and not EntityKind.Pickup and not EntityKind.Rescue) { Game.Hit(hit, 1); Destroy(gameObject); }
        if (Kind == EntityKind.EnemyShot && hit.Kind == EntityKind.PlayerShot) Destroy(gameObject);
        if (hit.Kind == EntityKind.PlayerShot && Kind is not EntityKind.PlayerShot and not EntityKind.Pickup and not EntityKind.Rescue) { Game.Hit(this, 1); Destroy(hit.gameObject); }
    }
}
