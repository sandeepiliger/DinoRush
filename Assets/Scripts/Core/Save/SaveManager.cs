using System;

namespace DinoRush.Core
{
    public enum SaveSlot
    {
        Primary,
        Backup,
    }

    // File access, abstracted so save logic stays testable without touching a disk
    // (docs/DECISIONS.md D9). The Unity implementation writes under persistentDataPath.
    public interface ISaveStore
    {
        bool Exists(SaveSlot slot);
        string Read(SaveSlot slot);
        void Write(SaveSlot slot, string contents);
        void Delete(SaveSlot slot);
    }

    public enum SaveLoadOutcome
    {
        LoadedPrimary,
        RecoveredFromBackup,
        StartedFresh,
    }

    public sealed class SaveLoadResult
    {
        public SaveDataV1 Data { get; }
        public SaveLoadOutcome Outcome { get; }

        public SaveLoadResult(SaveDataV1 data, SaveLoadOutcome outcome)
        {
            Data = data;
            Outcome = outcome;
        }
    }

    // Two-slot save with recovery, per CLAUDE.md section 29 ("handle corrupted save data
    // gracefully") and the "PROGRESS RECOVERED — restored your last good save" state in the UI
    // design.
    //
    // The ordering below is the whole point: the current primary is copied to backup *before*
    // the new primary is written. A crash or kill mid-write can therefore corrupt at most the
    // primary, leaving the previous good save intact — which is exactly the case the recovery
    // dialog exists to report.
    public sealed class SaveManager
    {
        private readonly ISaveStore _store;

        public SaveManager(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public SaveLoadResult Load()
        {
            if (TryLoadSlot(SaveSlot.Primary, out var primary))
                return new SaveLoadResult(primary, SaveLoadOutcome.LoadedPrimary);

            if (TryLoadSlot(SaveSlot.Backup, out var backup))
                return new SaveLoadResult(backup, SaveLoadOutcome.RecoveredFromBackup);

            return new SaveLoadResult(SaveMigrator.CreateDefault(), SaveLoadOutcome.StartedFresh);
        }

        public void Save(SaveDataV1 data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            string serialised = SaveSerializer.Serialize(data);

            // Promote the existing primary to backup first — see the class comment.
            if (_store.Exists(SaveSlot.Primary))
            {
                string existing = _store.Read(SaveSlot.Primary);
                // Only keep a backup that is itself loadable; copying a corrupt primary over a
                // good backup would destroy the only recoverable copy.
                if (SaveSerializer.TryDeserialize(existing, out _))
                    _store.Write(SaveSlot.Backup, existing);
            }

            _store.Write(SaveSlot.Primary, serialised);
        }

        private bool TryLoadSlot(SaveSlot slot, out SaveDataV1 data)
        {
            data = null;
            if (!_store.Exists(slot)) return false;

            string contents;
            try
            {
                contents = _store.Read(slot);
            }
            catch (Exception)
            {
                // An unreadable file is indistinguishable from a missing one as far as recovery
                // is concerned, and must never take the game down — section 55.
                return false;
            }

            if (!SaveSerializer.TryDeserialize(contents, out var parsed)) return false;

            data = SaveMigrator.Validate(parsed);
            return true;
        }
    }
}
