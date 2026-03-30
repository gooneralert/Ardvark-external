using FoulzExternal.SDK;
using FoulzExternal.SDK.caches;
using FoulzExternal.SDK.gamedetector;
using FoulzExternal.storage;
using Offsets;
using Options;
using System;
using SDKInstance = FoulzExternal.SDK.Instance;

namespace FoulzExternal.features.games.universal.checks.teamcheck
{
    public static class TeamCheck
    {
        public static bool isteammate(RobloxPlayer target)
        {
            var me = Storage.LocalPlayerInstance;
            if (!me.IsValid || target.address == 0) return false;

            if (me.Address == target.address) return true;

            var game = finder.whatgame();

            if (game == GameType.rivals)
            {
                return rivals_teamcheck(target);
            }

            if (game == GameType.pf)
            {
                if (!Settings.Checks.PFTeamCheck) return false;
                return pf_teamcheck(target);
            }

            if (!Settings.Checks.TeamCheck) return false;

            long myTeam = SDKInstance.Mem.ReadPtr(me.Address + Player.Team);
            long theirTeam = SDKInstance.Mem.ReadPtr(target.address + Player.Team);

            return myTeam != 0 && theirTeam != 0 && myTeam == theirTeam;
        }

        private static bool pf_teamcheck(RobloxPlayer target)
        {
            // Filtering is handled at cache level (CachePFPlayers skips the own team folder).
            // If a player reached isteammate() they are already confirmed as an enemy.
            // Always return false (= not a teammate) so they are rendered.
            return false;
        }

        private static bool rivals_teamcheck(RobloxPlayer p)
        {
            return p.TeammateLabel.IsValid;
        }
    }
}