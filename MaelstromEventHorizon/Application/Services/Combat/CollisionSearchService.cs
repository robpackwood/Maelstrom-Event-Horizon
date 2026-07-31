using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Application.Services.Combat;

internal sealed partial class CollisionService
{
    private static Asteroid? FindBonusImpact(GameEngine game)
    {
        if (game is not { PlayerRespawning: false, IsBonusStage: true, BonusStageFailed: false }) return null;
        foreach (Asteroid asteroid in game.Asteroids)
            if (asteroid is { Alive: true, ExitsArena: true } && game.Touching(game.Player, asteroid)) return asteroid;
        return null;
    }

    private static Asteroid? FindHitAsteroid(GameEngine game, Shot shot)
    {
        foreach (Asteroid asteroid in game.Asteroids)
            if (asteroid.Alive && game.Touching(shot, asteroid)) return asteroid;
        return null;
    }

    private static Fighter? FindHitFighter(GameEngine game, Shot shot)
    {
        foreach (Fighter fighter in game.Fighters)
            if (fighter.Alive && game.Touching(shot, fighter)) return fighter;
        return null;
    }

    private static AlienBoss? FindHitBoss(GameEngine game, Shot shot)
    {
        foreach (AlienBoss boss in game.Bosses)
            if (boss.Alive && game.Touching(shot, boss)) return boss;
        return null;
    }

    private static HomingMine? FindHitMine(GameEngine game, Shot shot)
    {
        foreach (HomingMine mine in game.Mines)
            if (mine.Alive && game.Touching(shot, mine)) return mine;
        return null;
    }

    private static GravityVortex? FindHitVortex(GameEngine game, Shot shot)
    {
        foreach (GravityVortex vortex in game.Vortices)
            if (vortex.Alive && game.Touching(shot, vortex)) return vortex;
        return null;
    }

    private static Nova? FindHitNova(GameEngine game, Shot shot)
    {
        foreach (Nova nova in game.Novas)
            if (nova is { Alive: true, Detonated: false } && game.Touching(shot, nova)) return nova;
        return null;
    }

    private static Comet? FindHitComet(GameEngine game, Shot shot)
    {
        foreach (Comet comet in game.Comets)
            if (comet.Alive && game.TouchingComet(shot, comet)) return comet;
        return null;
    }

    private static Pickup? FindHitPrize(GameEngine game, Shot shot)
    {
        foreach (Pickup pickup in game.Pickups)
            if (pickup is { Alive: true, Kind: PickupKind.Multiplier or PickupKind.Bonus } && game.Touching(shot, pickup)) return pickup;
        return null;
    }

    private static Body? FindPlayerDanger(GameEngine game)
    {
        foreach (Asteroid asteroid in game.Asteroids)
            if (asteroid.Alive && game.Touching(game.Player, asteroid)) return asteroid;
        foreach (Fighter fighter in game.Fighters)
            if (fighter.Alive && game.Touching(game.Player, fighter)) return fighter;
        foreach (AlienBoss boss in game.Bosses)
            if (boss.Alive && game.Touching(game.Player, boss)) return boss;
        foreach (HomingMine mine in game.Mines)
            if (mine.Alive && game.Touching(game.Player, mine)) return mine;
        foreach (GravityVortex vortex in game.Vortices)
            if (vortex.Alive && game.Touching(game.Player, vortex)) return vortex;
        return null;
    }
}
