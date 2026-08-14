using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Application.Services.Combat;

internal sealed partial class CollisionService
{
    private static Asteroid? FindBonusImpact(GameEngine game)
    {
        if (game is not { PlayerRespawning: false, IsBonusStage: true, BonusStageFailed: false })
        {
            return null;
        }

        foreach (Asteroid asteroid in game.Asteroids)
        {
            if (asteroid is { Alive: true, ExitsArena: true } && game.Touching(game.Player, asteroid))
            {
                return asteroid;
            }
        }

        return null;
    }

    private static Asteroid? FindHitAsteroid(GameEngine game, Shot shot, CollisionSpatialHash hash)
    {
        foreach (Body body in hash.Nearby(shot))
        {
            if (body is Asteroid { Alive: true } asteroid &&
                asteroid != shot.LastPiercedAsteroid &&
                game.Touching(shot, asteroid))
            {
                return asteroid;
            }
        }

        return null;
    }

    private static Body? FindPlayerShotTarget(GameEngine game, Shot shot, CollisionSpatialHash hash)
    {
        Asteroid? asteroid = null;
        Fighter? fighter = null;
        AlienBoss? boss = null;
        HomingMine? mine = null;
        GravityVortex? vortex = null;
        Nova? nova = null;
        Comet? comet = null;
        Pickup? prize = null;


        foreach (Body body in hash.Nearby(shot))
        {
            switch (body)
            {
                case Asteroid candidate when asteroid is null &&
                                             candidate.Alive &&
                                             candidate != shot.LastPiercedAsteroid &&
                                             game.Touching(shot, candidate):
                    asteroid = candidate;

                    break;

                case Fighter candidate when fighter is null && candidate.Alive && game.Touching(shot, candidate):
                    fighter = candidate;
                    break;

                case AlienBoss candidate when boss is null && candidate.Alive && game.Touching(shot, candidate):
                    boss = candidate;
                    break;

                case HomingMine candidate when mine is null && candidate.Alive && game.Touching(shot, candidate):
                    mine = candidate;
                    break;

                case GravityVortex candidate when vortex is null && candidate.Alive && game.Touching(shot, candidate):
                    vortex = candidate;
                    break;

                case Nova candidate when nova is null &&
                                         candidate is { Alive: true, Detonated: false } &&
                                         game.Touching(shot, candidate):
                    nova = candidate;

                    break;

                case Comet candidate when comet is null && candidate.Alive && game.TouchingComet(shot, candidate):
                    comet = candidate;
                    break;

                case Pickup candidate when prize is null &&
                                           candidate is { Alive: true, Kind: PickupKind.Multiplier or PickupKind.Bonus } &&
                                           game.Touching(shot, candidate):
                    prize = candidate;

                    break;
            }
        }

        if (asteroid is not null)
        {
            return asteroid;
        }

        if (fighter is not null)
        {
            return fighter;
        }

        if (boss is not null)
        {
            return boss;
        }

        if (mine is not null)
        {
            return mine;
        }

        if (vortex is not null)
        {
            return vortex;
        }

        if (nova is not null)
        {
            return nova;
        }

        if (comet is not null)
        {
            return comet;
        }

        return prize;
    }

    private static Body? FindPlayerDanger(GameEngine game)
    {
        foreach (Asteroid asteroid in game.Asteroids)
        {
            if (asteroid.Alive && game.Touching(game.Player, asteroid))
            {
                return asteroid;
            }
        }

        foreach (Fighter fighter in game.Fighters)
        {
            if (fighter.Alive && game.Touching(game.Player, fighter))
            {
                return fighter;
            }
        }

        foreach (AlienBoss boss in game.Bosses)
        {
            if (boss.Alive && game.Touching(game.Player, boss))
            {
                return boss;
            }
        }

        foreach (HomingMine mine in game.Mines)
        {
            if (mine.Alive && game.Touching(game.Player, mine))
            {
                return mine;
            }
        }

        foreach (GravityVortex vortex in game.Vortices)
        {
            if (vortex.Alive && game.Touching(game.Player, vortex))
            {
                return vortex;
            }
        }

        return null;
    }
}
