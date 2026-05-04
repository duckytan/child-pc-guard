namespace ChildPCGuard.Shared.State;

/// <summary>锁屏触发原因</summary>
public enum LockReason
{
    None,
    DailyLimitReached,
    OutsideAllowedWindow,
    TimeTampered,
    ManualLock,
    AutoShutdown,
    ContinuousLimitReached
}
