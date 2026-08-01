using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EventHorizon.Unity;

public sealed class EventHorizonGame : MonoBehaviour
{
    const float HalfWidth = 10.6f, HalfHeight = 6f;
    public bool Paused { get; private set; }
    public int Wave { get; private set; } = 1, Lives { get; private set; } = 3, Score { get; private set; };
    public SpaceEntity Player { get; private set; }
    public float PendingCash { get; private set; }
    readonly List<SpaceEntity> entities = new();
    readonly Dictionary<Powerup, float> powers = new();
    Camera cam; float fireCooldown, shield, nextEvent, intro, bannerUntil; string banner = "PRESS ENTER";
    bool playing, bonusTrial, bossWave, ricochet; int multiplier = 1;

    void Awake()
    {
        Application.targetFrameRate = 120;
        cam = Camera.main;
        if (cam == null) { var c = new GameObject("Event Horizon Camera"); cam = c.AddComponent<Camera>(); c.tag = "MainCamera"; }
        cam.orthographic = true; cam.orthographicSize = HalfHeight; cam.backgroundColor = new Color(.008f, .015f, .045f);
        MakeStars(); SpawnPlayer();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P)) Paused = !Paused;
        if (!playing) { if (Input.GetKeyDown(KeyCode.Return)) StartRun(); return; }
        if (Paused) return;
        UpdatePlayer(); UpdateThreats(); UpdatePowers();
        if (Time.time >= nextEvent && !bonusTrial && !bossWave) { SpawnRandomEvent(); nextEvent = Time.time + Random.Range(5f, 10f); }
        if (!entities.Any(e => e && e.Kind is EntityKind.Asteroid or EntityKind.Fighter or EntityKind.Mine or EntityKind.BlackHole or EntityKind.Supernova or EntityKind.Boss)) StartCoroutine(EndWave());
    }

    void StartRun()
    {
        foreach (var e in entities.Where(e => e).ToArray()) Destroy(e.gameObject); entities.Clear(); powers.Clear();
        Wave = 1; Lives = 3; Score = 0; PendingCash = 0; playing = true; SpawnWave();
    }

    void SpawnWave()
    {
        bonusTrial = Wave % 5 == 0; bossWave = Wave > 1 && Wave % 5 == 1;
        multiplier = 1; shield = Mathf.Max(shield, 0); nextEvent = Time.time + Random.Range(4f, 7f);
        if (bonusTrial) { banner = "DODGE TRIAL — WEAPONS OFF"; for (int i = 0; i < 13 + Wave * 2; i++) SpawnAsteroid(true); }
        else if (bossWave) { banner = "WARNING — ALIEN BOSS"; Spawn(EntityKind.Boss, RandomEdge(), Vector2.zero, 1.25f, 18 + Wave * 3, new Color(.6f, 1, .3f)); }
        else { banner = $"WAVE {Wave}"; for (int i = 0; i < 3 + Wave; i++) SpawnAsteroid(false); if (Wave >= 2) SpawnFighter(); }
        intro = Time.time + 2.4f; bannerUntil = intro;
    }

    IEnumerator EndWave()
    {
        if (!playing) yield break;
        playing = false; banner = PendingCash > 10000 ? "CASH BONUS!" : "WAVE CLEARED"; bannerUntil = Time.time + 3;
        Score += Mathf.RoundToInt(PendingCash); PendingCash = 0; yield return new WaitForSeconds(3); Wave++; playing = true; SpawnWave();
    }

    void UpdatePlayer()
    {
        if (Time.time < intro) return;
        float turn = Input.GetAxisRaw("Horizontal") * 220 * Time.deltaTime;
        Player.transform.Rotate(0, 0, -turn);
        if (Input.GetKey(KeyCode.Space)) Player.Velocity += (Vector2)Player.transform.up * 12 * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow) && shield > 0 && !bonusTrial) shield = Mathf.Max(0, shield - 24 * Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.H)) { Player.transform.position = RandomInside(); Player.Velocity *= .2f; }
        Player.Velocity *= powers.ContainsKey(Powerup.AirBrakes) && !Input.GetKey(KeyCode.Space) ? .82f : .997f;
        Player.Velocity = Vector2.ClampMagnitude(Player.Velocity, 8.5f);
        if (Input.GetKey(KeyCode.UpArrow) && fireCooldown <= 0 && !bonusTrial) Fire(false);
        fireCooldown -= Time.deltaTime;
    }

    void UpdateThreats()
    {
        foreach (var e in entities.Where(x => x).ToArray())
        {
            Vector2 d = (Vector2)Player.transform.position - (Vector2)e.transform.position;
            if (e.Kind == EntityKind.BlackHole) { Player.Velocity -= d.normalized * (15f / Mathf.Max(1.4f, d.sqrMagnitude)) * Time.deltaTime; if (d.magnitude < e.Radius + Player.Radius) DamagePlayer(); }
            if (e.Kind == EntityKind.Mine) e.Velocity += d.normalized * 4.8f * Time.deltaTime;
            if (e.Kind == EntityKind.Fighter) { e.Velocity = Vector2.Lerp(e.Velocity, d.normalized * 3, Time.deltaTime); if (Random.value < Time.deltaTime * .35f) FireEnemy(e); }
            if (e.Kind == EntityKind.Boss) { e.Velocity = Vector2.Lerp(e.Velocity, d.normalized * 2.3f, Time.deltaTime); if (Random.value < Time.deltaTime * .8f) FireEnemy(e); }
            if (e.Kind == EntityKind.Supernova && e.Age > 4) { Explode(e.transform.position, 28, Color.white); DamagePlayerNear(e.transform.position, 2.9f); Destroy(e.gameObject); }
            if (e.Kind is EntityKind.Fighter or EntityKind.Mine or EntityKind.Boss && d.magnitude < e.Radius + Player.Radius) DamagePlayer();
        }
    }

    void UpdatePowers() { foreach (var p in powers.Keys.ToArray()) { powers[p] -= Time.deltaTime; if (powers[p] <= 0) powers.Remove(p); } }
    void Fire(bool enemy)
    {
        if (!enemy) { fireCooldown = powers.ContainsKey(Powerup.RapidFire) ? .08f : .22f; Vector2 dir = Player.transform.up; Spawn(EntityKind.PlayerShot, Player.transform.position + (Vector3)dir * .45f, dir * 15, .08f, 1, Color.cyan).Life = powers.ContainsKey(Powerup.LongRange) ? 2.5f : 1.2f; if (powers.ContainsKey(Powerup.TripleFire)) { FireAngle(-14); FireAngle(14); } }
    }
    void FireAngle(float a) { Vector2 d = Quaternion.Euler(0, 0, a) * Player.transform.up; Spawn(EntityKind.PlayerShot, Player.transform.position, d * 15, .08f, 1, Color.cyan); }
    void FireEnemy(SpaceEntity enemy) { Vector2 d = ((Vector2)Player.transform.position - (Vector2)enemy.transform.position).normalized; Spawn(EntityKind.EnemyShot, enemy.transform.position, d * 8, .1f, 1, new Color(1, .25f, .35f)); }

    void SpawnRandomEvent()
    {
        int roll = Random.Range(0, 100);
        if (roll < 24) { banner = "ENEMY ASSAULT"; bannerUntil = Time.time + 2; for (int i = 0; i < 3; i++) SpawnFighter(); }
        else if (roll < 38 && Wave >= 4) { banner = "BLACK HOLE ASSAULT"; bannerUntil = Time.time + 2; for (int i = 0; i < 3; i++) Spawn(EntityKind.BlackHole, RandomInside(), Vector2.zero, .65f, 1, new Color(.55f, .2f, 1)).Life = 16; }
        else if (roll < 52 && Wave >= 4) { banner = "SUPERNOVA ASSAULT"; bannerUntil = Time.time + 2; for (int i = 0; i < 3; i++) Spawn(EntityKind.Supernova, RandomInside(), Vector2.zero, .55f, 1, new Color(1, .35f, .08f)).Life = 5; }
        else SpawnPickup(Random.value < .12f ? EntityKind.Rescue : EntityKind.Pickup);
    }

    void SpawnAsteroid(bool fast) { Vector2 d = ((Vector2)RandomInside()).normalized; Spawn(EntityKind.Asteroid, RandomEdge(), -d * Random.Range(fast ? 3.5f : 1.1f, fast ? 6.2f : 3.2f), Random.Range(.25f, .75f), fast ? 1 : 3, new Color(.64f, .46f, .28f)).ExitsArena = fast; }
    void SpawnFighter() => Spawn(EntityKind.Fighter, RandomEdge(), Random.insideUnitCircle * 2, .34f, 2 + Wave / 5, new Color(1, .2f, .42f));
    void SpawnPickup(EntityKind kind) { var p = Spawn(kind, RandomEdge(), Random.insideUnitCircle * 1.2f, kind == EntityKind.Rescue ? .34f : .22f, 1, kind == EntityKind.Rescue ? Color.green : Color.yellow); p.Powerup = (Powerup)Random.Range(0, 11); p.Life = kind == EntityKind.Rescue ? 18 : 14; }

    SpaceEntity Spawn(EntityKind kind, Vector2 position, Vector2 velocity, float radius, int health, Color color)
    {
        var g = new GameObject(kind.ToString()); g.transform.position = position; g.AddComponent<CircleCollider2D>().isTrigger = true;
        var e = g.AddComponent<SpaceEntity>(); e.Game = this; e.Kind = kind; e.Velocity = velocity; e.Radius = radius; e.Health = health;
        var v = g.AddComponent<ProceduralVisual>(); v.Radius = radius; v.Sides = kind == EntityKind.Asteroid ? Random.Range(7, 11) : 20; v.Tint = color;
        entities.Add(e); return e;
    }

    public void Hit(SpaceEntity target, int damage)
    {
        if (!target || target.Kind == EntityKind.PlayerShot) return;
        if (target.Kind is EntityKind.Pickup or EntityKind.Rescue) { Collect(target); return; }
        target.Health -= damage; if (target.Health > 0) return;
        if (target.Kind == EntityKind.Asteroid) { Explode(target.transform.position, target.Size * 8, new Color(1, .48f, .16f)); PendingCash += target.Size == 3 ? 20 : target.Size == 2 ? 50 : 100; if (target.Size > 1) for (int i = 0; i < 2; i++) { var c = Spawn(EntityKind.Asteroid, target.transform.position, Random.insideUnitCircle * 4, target.Radius * .63f, target.Size - 1, new Color(.64f, .46f, .28f)); c.Life = 30; } }
        else { Explode(target.transform.position, 18, Color.white); PendingCash += target.Kind == EntityKind.Boss ? 5000 + Wave * 1500 : 250; }
        Destroy(target.gameObject);
    }

    void Collect(SpaceEntity pickup) { if (pickup.Kind == EntityKind.Rescue) { Lives++; banner = "RESCUE +1 SHIP"; } else { powers[pickup.Powerup] = pickup.Powerup == Powerup.Shield ? 0 : 12; if (pickup.Powerup == Powerup.Shield) shield = 100; if (pickup.Powerup == Powerup.SmartBomb) foreach (var e in entities.Where(e => e && e.Kind == EntityKind.Asteroid).ToArray()) Hit(e, 99); if (pickup.Powerup == Powerup.RicochetArena) ricochet = true; banner = pickup.Powerup.ToString().ToUpper(); } bannerUntil = Time.time + 2; Destroy(pickup.gameObject); }
    void DamagePlayerNear(Vector2 point, float radius) { if (Vector2.Distance(Player.transform.position, point) < radius) DamagePlayer(); }
    void DamagePlayer() { if (Time.time < intro || Input.GetKey(KeyCode.DownArrow) && shield > 0) return; Lives--; Explode(Player.transform.position, 35, Color.cyan); if (Lives <= 0) { playing = false; banner = "GAME OVER — PRESS ENTER"; } else { Player.transform.position = Vector3.zero; Player.Velocity = Vector2.zero; intro = Time.time + 2; } }
    public void WrapOrBounce(SpaceEntity e) { Vector3 p = e.transform.position; if (e.Kind is EntityKind.PlayerShot or EntityKind.EnemyShot && !ricochet && (Mathf.Abs(p.x) > HalfWidth || Mathf.Abs(p.y) > HalfHeight)) Destroy(e.gameObject); if (e.ExitsArena && (Mathf.Abs(p.x) > HalfWidth + 1 || Mathf.Abs(p.y) > HalfHeight + 1)) Destroy(e.gameObject); if (!ricochet) { if (p.x > HalfWidth) p.x = -HalfWidth; if (p.x < -HalfWidth) p.x = HalfWidth; if (p.y > HalfHeight) p.y = -HalfHeight; if (p.y < -HalfHeight) p.y = HalfHeight; } else { if (Mathf.Abs(p.x) > HalfWidth) { e.Velocity.x *= -1; p.x = Mathf.Sign(p.x) * HalfWidth; } if (Mathf.Abs(p.y) > HalfHeight) { e.Velocity.y *= -1; p.y = Mathf.Sign(p.y) * HalfHeight; } } e.transform.position = p; }
    void Explode(Vector2 at, int count, Color color) { for (int i = 0; i < count; i++) { var p = Spawn(EntityKind.PlayerShot, at, Random.insideUnitCircle * Random.Range(3, 8), .025f, 1, color); p.Life = Random.Range(.12f, .4f); } }
    Vector2 RandomInside() => new(Random.Range(-HalfWidth + 1, HalfWidth - 1), Random.Range(-HalfHeight + 1, HalfHeight - 1));
    Vector2 RandomEdge() { Vector2 p = RandomInside(); return Random.value < .5f ? new Vector2(Mathf.Sign(p.x) * HalfWidth, p.y) : new Vector2(p.x, Mathf.Sign(p.y) * HalfHeight); }
    void SpawnPlayer() { Player = Spawn(EntityKind.PlayerShot, Vector2.zero, Vector2.zero, .28f, 99, Color.cyan); Player.name = "Player"; Player.GetComponent<CircleCollider2D>().radius = Player.Radius; }
    void MakeStars() { for (int i = 0; i < 160; i++) { var s = Spawn(EntityKind.PlayerShot, RandomInside(), Vector2.zero, .012f, 1, new Color(.5f, .8f, 1, Random.Range(.15f, .8f))); s.name = "Star"; s.Life = 999999; } }
    void OnGUI() { GUI.color = Color.white; GUI.Label(new Rect(18, 14, 520, 30), $"$ {Score:N0}    SHIPS {Lives}    WAVE {Wave}    SHIELD {shield:0}%", new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold }); if (Time.time < bannerUntil || !playing) { GUIStyle style = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold }; GUI.Label(new Rect(0, Screen.height * .43f, Screen.width, 50), banner, style); } }
}
