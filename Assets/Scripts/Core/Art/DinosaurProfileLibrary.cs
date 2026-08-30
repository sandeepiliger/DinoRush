using System.Collections.Generic;

namespace DinoRush.Core
{
    // Maps a catalogue id to the shape that gets built for it.
    //
    // This is where section 50's promise gets tested for real: the catalogue already describes
    // six dinosaurs as data, and adding the *look* of one should be a table of numbers rather
    // than new geometry code. For the theropods it is exactly that.
    //
    // The quadrupeds — Triceratops, Stegosaurus, Ankylosaurus — are not here yet, and fall back
    // to the starter. That is a real gap and it is deliberate rather than overlooked: the rig
    // and the gait are both built around two weight-bearing legs, and a four-legged animal needs
    // the forelimbs brought into the IK and a second gait with a different footfall pattern.
    // Shipping a Triceratops that is secretly a Velociraptor would be worse than saying so.
    public static class DinosaurProfileLibrary
    {
        private static readonly Dictionary<string, DinosaurProfile> Profiles =
            new Dictionary<string, DinosaurProfile>
            {
                ["velociraptor"] = DinosaurProfile.Velociraptor(),
                ["spinosaurus"] = DinosaurProfile.Spinosaurus(),
                ["trex"] = DinosaurProfile.Tyrannosaurus(),
            };

        public static bool HasProfileFor(string dinosaurId) =>
            dinosaurId != null && Profiles.ContainsKey(dinosaurId);

        public static DinosaurProfile For(string dinosaurId) =>
            dinosaurId != null && Profiles.TryGetValue(dinosaurId, out var profile)
                ? profile
                : DinosaurProfile.Velociraptor();
    }
}
