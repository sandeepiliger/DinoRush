using System;
using System.IO;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // The Unity side of Core's ISaveStore. Everything about *when* to write which slot, and how
    // to recover, lives in SaveManager and is unit-tested; this only knows where files go and
    // how to put bytes there safely.
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string _directory;

        public FileSaveStore(string directory = null)
        {
            // persistentDataPath is the only location that survives app updates on Android and
            // is included in auto-backup; Application.dataPath is read-only on device.
            _directory = directory ?? Application.persistentDataPath;
        }

        public bool Exists(SaveSlot slot) => File.Exists(PathFor(slot));

        public string Read(SaveSlot slot) => File.ReadAllText(PathFor(slot));

        public void Write(SaveSlot slot, string contents)
        {
            string target = PathFor(slot);
            string temporary = target + ".tmp";

            // Write-then-move: File.Move over an existing path is atomic on the same volume, so
            // a crash or a kill (Android will kill a backgrounded process without warning)
            // leaves either the old file or the new one, never a half-written file. Writing
            // directly to `target` is what actually produces the corrupt saves SaveManager's
            // recovery path exists to handle — better not to create them in the first place.
            File.WriteAllText(temporary, contents);
            if (File.Exists(target)) File.Delete(target);
            File.Move(temporary, target);
        }

        public void Delete(SaveSlot slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        private string PathFor(SaveSlot slot) =>
            Path.Combine(_directory, slot == SaveSlot.Primary ? "dinorush.sav" : "dinorush.bak");
    }

    // Owns the loaded save for the session and keeps writes off the hot path.
    public sealed class SaveService
    {
        private readonly SaveManager _manager;

        public SaveDataV1 Data { get; private set; }
        public SaveLoadOutcome LastOutcome { get; private set; }

        public SaveService(ISaveStore store = null)
        {
            _manager = new SaveManager(store ?? new FileSaveStore());

            // Section 55: a failure here must never stop the game starting. SaveManager already
            // handles corrupt and unreadable files; this catches anything more exotic (a
            // permissions failure constructing the store, say) and starts fresh rather than
            // taking down the boot sequence.
            try
            {
                var result = _manager.Load();
                Data = result.Data;
                LastOutcome = result.Outcome;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DinoRush] Save load failed, starting fresh: {e.Message}");
                Data = SaveMigrator.CreateDefault();
                LastOutcome = SaveLoadOutcome.StartedFresh;
            }

            if (LastOutcome == SaveLoadOutcome.RecoveredFromBackup)
            {
                // The design's "PROGRESS RECOVERED" dialog belongs here once real UI exists (M7).
                Debug.Log("[DinoRush] Recovered progress from the backup save.");
            }
        }

        public void Save()
        {
            try
            {
                _manager.Save(Data);
            }
            catch (Exception e)
            {
                // Losing one write is survivable; crashing the run over it is not.
                Debug.LogWarning($"[DinoRush] Save write failed: {e.Message}");
            }
        }
    }
}
