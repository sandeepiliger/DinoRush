namespace DinoRush.Core
{
    // The centralized state set from CLAUDE.md section 30. Kept in Core (not as a Unity enum
    // next to a MonoBehaviour) so transition rules are unit-testable without the editor, and
    // so "no random script directly controls global game state" is enforceable rather than
    // aspirational.
    public enum GameState
    {
        Boot,
        Menu,
        Tutorial,
        Ready,
        Playing,
        Paused,
        Revive,
        GameOver,
        Shop,
        Collection,
        Missions,
        Settings,
    }
}
