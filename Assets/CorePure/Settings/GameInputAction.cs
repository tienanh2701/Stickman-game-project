namespace StickmanBeatEmUp.CorePure.Settings
{
    /// <summary>
    /// All rebindable/fixed player input actions mapped 1-to-1 with InputHandler.
    /// </summary>
    public enum GameInputAction
    {
        MoveLeft = 0,
        MoveRight = 1,
        Jump = 2,
        AltJump = 3,
        Attack = 4,
        Skill1 = 5,
        Skill2 = 6,
        Transform = 7,
        Block = 8,
        ItemQ = 9,
        ItemE = 10,
        Pause = 11
    }

    public enum ControlPresetId
    {
        DefaultWasd = 0,
        ArrowKeysZx = 1,
        Custom = 2
    }

    public enum KeyBindConflictPolicy
    {
        Reject = 0,
        Swap = 1
    }

    public enum KeyBindChangeStatus
    {
        Applied = 0,
        Unchanged = 1,
        Swapped = 2,
        ConflictRejected = 3,
        ReservedActionLocked = 4,
        ReservedKeyRejected = 5,
        UnsupportedKeyRejected = 6
    }

    public readonly struct KeyBindChangeResult
    {
        public KeyBindChangeStatus Status { get; }
        public GameInputAction TargetAction { get; }
        public int RequestedKeyCode { get; }
        public int PreviousKeyCode { get; }
        public GameInputAction? ConflictAction { get; }

        public bool IsSuccess =>
            Status == KeyBindChangeStatus.Applied ||
            Status == KeyBindChangeStatus.Unchanged ||
            Status == KeyBindChangeStatus.Swapped;

        public KeyBindChangeResult(
            KeyBindChangeStatus status,
            GameInputAction targetAction,
            int requestedKeyCode,
            int previousKeyCode,
            GameInputAction? conflictAction = null)
        {
            Status = status;
            TargetAction = targetAction;
            RequestedKeyCode = requestedKeyCode;
            PreviousKeyCode = previousKeyCode;
            ConflictAction = conflictAction;
        }
    }
}
