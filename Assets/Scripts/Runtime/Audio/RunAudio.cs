using UnityEngine;

namespace DinoRush.Runtime
{
    // Placeholder audio, synthesised at runtime rather than loaded from files.
    //
    // Section 37 lists the real sound set and section 70 says that when an asset isn't
    // available yet, build the abstraction and a temporary stand-in rather than inventing
    // something that later breaks. There are no audio assets in this repo (and none may be
    // committed without a commercial-use licence recorded in LICENSES/THIRD_PARTY_ASSETS.md,
    // per section 57), so these are decaying sine blips generated into AudioClips at startup.
    //
    // They cost no repo size, carry no licence risk, and prove the trigger points are wired to
    // the right gameplay events. AudioManager (section 33) with real clips, mixer groups and
    // the music/SFX volume settings from section 37 replaces this wholesale.
    public sealed class RunAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _source;
        private AudioClip _jump;
        private AudioClip _coin;
        private AudioClip _hit;
        private AudioClip _footstep;
        private AudioClip _land;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;

            _jump = CreateBlip("sfx_jump", startHz: 330f, endHz: 620f, seconds: 0.12f);
            _coin = CreateBlip("sfx_coin", startHz: 880f, endHz: 1320f, seconds: 0.09f);
            _hit = CreateBlip("sfx_hit", startHz: 220f, endHz: 70f, seconds: 0.32f);
            _footstep = CreateBlip("sfx_step", startHz: 150f, endHz: 58f, seconds: 0.07f);
            _land = CreateBlip("sfx_land", startHz: 190f, endHz: 48f, seconds: 0.16f);
        }

        public void PlayJump() => _source.PlayOneShot(_jump, 0.35f);
        public void PlayCoin() => _source.PlayOneShot(_coin, 0.30f);
        public void PlayHit() => _source.PlayOneShot(_hit, 0.55f);
        public void PlayLand() => _source.PlayOneShot(_land, 0.30f);

        // Quiet, and quieter still at a jittered pitch. Footsteps fire several times a second,
        // so an identical sample at a fixed level turns into a machine-gun the ear locks onto
        // within about two strides.
        public void PlayFootstep()
        {
            _source.pitch = Random.Range(0.88f, 1.14f);
            _source.PlayOneShot(_footstep, 0.16f);
            _source.pitch = 1f;
        }

        // A sine sweep with an exponential decay envelope — enough shape to read as a distinct
        // event without sounding like a click.
        private static AudioClip CreateBlip(string name, float startHz, float endHz, float seconds)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * seconds);
            var samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float frequency = Mathf.Lerp(startHz, endHz, t);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                samples[i] = Mathf.Sin(phase) * Mathf.Exp(-4f * t);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
