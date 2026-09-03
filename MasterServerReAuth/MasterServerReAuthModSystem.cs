using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace MasterServerReAuth;

public class MasterServerReAuthModSystem : ModSystem
{
    private const int RetryIntervalMs = 60000;

    private ICoreServerAPI sapi = null!;
    private ServerSystemHeartbeat? heartbeatSystem;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        heartbeatSystem = ResolveHeartbeatSystem(api);

        if (heartbeatSystem == null)
        {
            api.Logger.Warning("[MasterServerReAuth] Could not locate the master server heartbeat system via reflection (game version may have changed internals). Mod is inactive.");
            return;
        }

        api.Event.RegisterGameTickListener(OnCheckTick, RetryIntervalMs, RetryIntervalMs);
        api.Logger.Notification("[MasterServerReAuth] Active. Will retry master server registration every {0}s if a previous attempt failed.", RetryIntervalMs / 1000);
    }

    private void OnCheckTick(float dt)
    {
        try
        {
            // TryRegister() already no-ops if advertising is off, the server is not
            // dedicated, Upnp is in use, or a registration token is already held -
            // so it is safe to call unconditionally on every tick.
            heartbeatSystem!.TryRegister();
        }
        catch (Exception e)
        {
            sapi.Logger.Error("[MasterServerReAuth] Error while attempting to re-register with the master server: {0}", e);
        }
    }

    private static ServerSystemHeartbeat? ResolveHeartbeatSystem(ICoreServerAPI api)
    {
        try
        {
            FieldInfo? serverField = api.GetType().GetField("server", BindingFlags.NonPublic | BindingFlags.Instance);
            object? serverMain = serverField?.GetValue(api);
            if (serverMain == null) return null;

            FieldInfo? heartbeatField = serverMain.GetType().GetField("HeartbeatSystem", BindingFlags.NonPublic | BindingFlags.Instance);
            return heartbeatField?.GetValue(serverMain) as ServerSystemHeartbeat;
        }
        catch (Exception e)
        {
            api.Logger.Error("[MasterServerReAuth] Reflection failed while resolving ServerSystemHeartbeat: {0}", e);
            return null;
        }
    }
}
